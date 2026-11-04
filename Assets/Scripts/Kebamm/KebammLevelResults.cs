using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Kebamm
{
    [Serializable]
    public class KebammLevelResult
    {
        public string localId;
        public int level;
        public int stars;
        public int score;
        public int mergeScore;
        public int monsterScore;
        public int monstersDestroyed;
        public int dataVersion;
        public string dataSource;
        public string appVersion;
        public string playerId;
        public string username;
        public int attempt;
        public int durationSec;
        public string timeUtc;
        public int[] ballsByTier; // index 0 = GDD tier 1 (launcher drops + merges, any colour)
    }

    [Serializable]
    class KebammLevelResultQueue
    {
        public List<KebammLevelResult> items = new List<KebammLevelResult>();
    }

    /// <summary>
    /// Analytics (Notion: Analytics - Level Results to Notion). Every score screen adds one level_result to an
    /// offline queue file (persistentDataPath/kebamm_results_queue.json) first; queued rows are then sent one by one
    /// to the Notion "Level Results" database (POST /v1/pages). A row leaves the queue only after Notion accepts it.
    /// 429 / 5xx / network errors: retry with backoff (Retry-After respected). Nothing is dropped.
    /// Token: Resources/Kebamm/Secrets/notion_token.txt (gitignored, internal builds only). No token = queue only.
    /// INTERNAL TESTING ONLY: the token in the APK can edit the Level Results database. Remove or replace
    /// (relay / analytics service with consent) before any public release.
    /// </summary>
    public static class KebammLevelResults
    {
        const string LogTag = "[Kebamm][Results]";
        const string QueueFile = "kebamm_results_queue.json";
        const string TokenResource = "Kebamm/Secrets/notion_token";
        const string DatabaseId = "2970402e3b774fb1afca6021e4c8a90b"; // Level Results (GDD > Internal Data Performance)
        const string NotionVersion = "2022-06-28";
        const string PagesUrl = "https://api.notion.com/v1/pages";
        const float MaxBackoff = 300f;

        static KebammLevelResultQueue _queue;
        static string _token;
        static bool _tokenLoaded;
        static bool _sending;
        static bool _blocked; // auth/permission problem: stop sending this session, keep queueing
        static float _backoff;
        static float _nextTryAt;
        static Runner _runner;

        static string QueuePath => Path.Combine(Application.persistentDataPath, QueueFile);
        static string TmpPath => QueuePath + ".tmp";

        public static int PendingCount => Queue.items.Count;

        static KebammLevelResultQueue Queue
        {
            get
            {
                if (_queue == null)
                    LoadQueue();
                return _queue;
            }
        }

        static string Token
        {
            get
            {
                if (!_tokenLoaded)
                {
                    _tokenLoaded = true;
                    var asset = Resources.Load<TextAsset>(TokenResource);
                    _token = asset != null ? asset.text.Trim() : "";
                    if (asset != null)
                        Resources.UnloadAsset(asset);
                    Debug.Log(string.IsNullOrEmpty(_token)
                        ? $"{LogTag} No Notion token in this build - results are queued only, not sent."
                        : $"{LogTag} Notion token found - results will be sent to Level Results.");
                }
                return _token;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            EnsureRunner();
            Debug.Log($"{LogTag} Startup: {PendingCount} result(s) waiting in the queue.");
            Kick("app start");
        }

        /// <summary>Score screen: queue one row for this attempt, then try to send.</summary>
        public static void Record(int level, int stars)
        {
            try
            {
                var save = KebammSaveSystem.Data;
                var r = new KebammLevelResult
                {
                    localId = Guid.NewGuid().ToString("N"),
                    level = level,
                    stars = stars,
                    score = KebammScore.FinalScore,
                    mergeScore = KebammScore.MergeScore,
                    monsterScore = KebammScore.MonsterScore,
                    monstersDestroyed = KebammScore.MonstersDestroyed,
                    dataVersion = KebammRemoteData.DataVersion,
                    dataSource = SourceName(KebammRemoteData.Source),
                    appVersion = Application.version,
                    playerId = save.player.playerId ?? "",
                    username = save.player.username ?? "",
                    attempt = KebammSaveSystem.NextAttempt(level),
                    durationSec = Mathf.RoundToInt(KebammScore.PlaySeconds),
                    ballsByTier = BallCounts(),
                    timeUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
                };
                Queue.items.Add(r);
                SaveQueue();
                Debug.Log($"{LogTag} Queued L{r.level} stars {r.stars} score {r.score} (merge {r.mergeScore}, monster {r.monsterScore}, destroyed {r.monstersDestroyed}) attempt {r.attempt} {r.durationSec}s balls [{string.Join(",", r.ballsByTier)}] data v{r.dataVersion} {r.dataSource} - {PendingCount} waiting.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogTag} Could not queue result: {e.Message}");
            }
            _backoff = 0f;
            _nextTryAt = 0f;
            Kick("new result");
        }

        static int[] BallCounts()
        {
            var a = new int[KebammScore.BallTierCount];
            for (int t = 1; t <= a.Length; t++)
                a[t - 1] = KebammScore.BallsCreated(t);
            return a;
        }

        static string SourceName(KebammRemoteData.DataSource s)
        {
            switch (s)
            {
                case KebammRemoteData.DataSource.Online: return "online";
                case KebammRemoteData.DataSource.Cached: return "cached";
                default: return "built-in";
            }
        }

        static void Kick(string reason)
        {
            if (_sending || _blocked || PendingCount == 0)
                return;
            if (string.IsNullOrEmpty(Token))
                return;
            EnsureRunner();
            _runner.StartCoroutine(SendAll(reason));
        }

        static IEnumerator SendAll(string reason)
        {
            _sending = true;
            Debug.Log($"{LogTag} Sending {PendingCount} result(s) ({reason}).");
            while (PendingCount > 0 && !_blocked)
            {
                while (Time.realtimeSinceStartup < _nextTryAt)
                    yield return null;
                if (Application.internetReachability == NetworkReachability.NotReachable)
                {
                    Debug.Log($"{LogTag} Offline - {PendingCount} result(s) stay queued.");
                    break;
                }

                var r = Queue.items[0];
                byte[] body = Encoding.UTF8.GetBytes(BuildJson(r));
                using (var req = new UnityWebRequest(PagesUrl, UnityWebRequest.kHttpVerbPOST))
                {
                    req.uploadHandler = new UploadHandlerRaw(body);
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.timeout = 20;
                    req.SetRequestHeader("Authorization", "Bearer " + Token);
                    req.SetRequestHeader("Notion-Version", NotionVersion);
                    req.SetRequestHeader("Content-Type", "application/json");
                    yield return req.SendWebRequest();

                    long code = req.responseCode;
                    if (req.result == UnityWebRequest.Result.Success && code >= 200 && code < 300)
                    {
                        Queue.items.RemoveAt(0);
                        SaveQueue();
                        _backoff = 0f;
                        Debug.Log($"{LogTag} Sent L{r.level} attempt {r.attempt} to Notion - {PendingCount} left.");
                        continue;
                    }

                    if (code == 401 || code == 403 || code == 404)
                    {
                        _blocked = true;
                        Debug.LogWarning($"{LogTag} Notion refused ({code}): token invalid or database not shared with the integration. Sending stopped this session; {PendingCount} result(s) kept.");
                        break;
                    }

                    float wait = NextBackoff();
                    string retryAfter = req.GetResponseHeader("Retry-After");
                    if (!string.IsNullOrEmpty(retryAfter) && float.TryParse(retryAfter, NumberStyles.Float, CultureInfo.InvariantCulture, out float ra))
                        wait = Mathf.Max(wait, ra);
                    _nextTryAt = Time.realtimeSinceStartup + wait;
                    string what = code == 0 ? req.error : $"HTTP {code}";
                    if (code == 400)
                        Debug.LogWarning($"{LogTag} Notion rejected the row ({what}): {Short(req.downloadHandler?.text)} - kept in queue, retry in {wait:0}s.");
                    else
                        Debug.Log($"{LogTag} Send failed ({what}) - retry in {wait:0}s, {PendingCount} result(s) kept.");
                }
            }
            _sending = false;
        }

        static float NextBackoff()
        {
            _backoff = _backoff <= 0f ? 2f : Mathf.Min(_backoff * 2f, MaxBackoff);
            return _backoff * UnityEngine.Random.Range(0.8f, 1.2f);
        }

        static string Short(string s) => string.IsNullOrEmpty(s) ? "" : (s.Length > 200 ? s.Substring(0, 200) : s);

        static string BuildJson(KebammLevelResult r)
        {
            string shortTime = r.timeUtc != null && r.timeUtc.Length >= 16 ? r.timeUtc.Substring(0, 16).Replace('T', ' ') + "Z" : r.timeUtc;
            string title = $"L{r.level} \u00b7 {(string.IsNullOrEmpty(r.username) ? "Player" : r.username)} \u00b7 {shortTime}";
            var sb = new StringBuilder(1024);
            sb.Append("{\"parent\":{\"database_id\":\"").Append(DatabaseId).Append("\"},\"properties\":{");
            sb.Append("\"Result\":{\"title\":[{\"text\":{\"content\":").Append(Str(title)).Append("}}]}");
            Num(sb, "level", r.level);
            Num(sb, "stars", r.stars);
            Num(sb, "score", r.score);
            Num(sb, "merge_score", r.mergeScore);
            Num(sb, "monster_score", r.monsterScore);
            Num(sb, "monsters_destroyed", r.monstersDestroyed);
            Num(sb, "data_version", r.dataVersion);
            sb.Append(",\"data_source\":{\"select\":{\"name\":").Append(Str(r.dataSource)).Append("}}");
            Text(sb, "app_version", r.appVersion);
            Text(sb, "player_id", r.playerId);
            Text(sb, "username", r.username);
            Num(sb, "attempt", r.attempt);
            Num(sb, "duration_sec", r.durationSec);
            sb.Append(",\"time_utc\":{\"date\":{\"start\":").Append(Str(r.timeUtc)).Append("}}");
            // Rows queued by an older build have no ball counts: those columns stay empty.
            if (r.ballsByTier != null)
                for (int t = 1; t <= r.ballsByTier.Length; t++)
                    Num(sb, $"tier_{t}_balls", r.ballsByTier[t - 1]);
            sb.Append("}}");
            return sb.ToString();
        }

        static void Num(StringBuilder sb, string name, int v) =>
            sb.Append(",\"").Append(name).Append("\":{\"number\":").Append(v.ToString(CultureInfo.InvariantCulture)).Append('}');

        static void Text(StringBuilder sb, string name, string v) =>
            sb.Append(",\"").Append(name).Append("\":{\"rich_text\":[{\"text\":{\"content\":").Append(Str(v)).Append("}}]}");

        static string Str(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        static void LoadQueue()
        {
            _queue = new KebammLevelResultQueue();
            try
            {
                if (File.Exists(QueuePath))
                {
                    var q = JsonUtility.FromJson<KebammLevelResultQueue>(File.ReadAllText(QueuePath));
                    if (q != null && q.items != null)
                        _queue = q;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogTag} Queue file unreadable ({e.Message}); keeping it as .bad and starting empty.");
                try { File.Copy(QueuePath, QueuePath + ".bad", true); } catch { }
            }
        }

        static void SaveQueue()
        {
            if (_queue == null)
                return; // never loaded: nothing changed, don't overwrite the file
            try
            {
                File.WriteAllText(TmpPath, JsonUtility.ToJson(_queue));
                if (File.Exists(QueuePath))
                    File.Delete(QueuePath);
                File.Move(TmpPath, QueuePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogTag} Could not write queue file: {e.Message}");
            }
        }

        static void EnsureRunner()
        {
            if (_runner != null)
                return;
            var go = new GameObject("KebammLevelResults");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            _runner = go.AddComponent<Runner>();
        }

        class Runner : MonoBehaviour
        {
            float _nextPoll;

            void OnApplicationPause(bool paused)
            {
                if (paused)
                    SaveQueue();
                else
                    Kick("app resumed");
            }

            void Update()
            {
                // Retry later (e.g. phone came back online) without needing a new result.
                if (Time.realtimeSinceStartup < _nextPoll)
                    return;
                _nextPoll = Time.realtimeSinceStartup + 30f;
                if (Time.realtimeSinceStartup >= _nextTryAt)
                    Kick("periodic retry");
            }
        }
    }
}
