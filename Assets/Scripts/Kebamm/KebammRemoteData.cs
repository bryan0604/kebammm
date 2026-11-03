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
    /// <summary>
    /// Remote data overrides (Notion stays the master; exported to kebamm_data.json and hosted at
    /// GameManager.remoteDataUrl). The cached copy in persistentDataPath (data the player accepted earlier)
    /// applies at launch without asking; otherwise built-in (the ScriptableObject assets as shipped). The Gist is
    /// checked at launch and on every return to the main menu; newer data is only applied after the player
    /// says Yes on the main-menu prompt (No = not again for that version this session). Overrides are applied to in-memory copies
    /// of the assets (the assets themselves are never changed) and only at launch or when the main menu
    /// is shown, never mid-level. A field missing from the JSON keeps the asset value.
    /// JSON: { "dataVersion": int, "updatedAt": ISO string,
    ///         "levelData":    { "level_01": { "Monster qty": 6, ... Notion column names ... } },
    ///         "ballTierData": { "ball_tier_01": { "Ball Size": 0.5, "damage": 50, ... } },
    ///         "gameManager":  { "GameManager": { "pillar_height": 5.8, ... } } }
    /// </summary>
    public static class KebammRemoteData
    {
        public enum DataSource { BuiltIn, Cached, Online }

        const string CacheFile = "kebamm_data.json";
        const string LogTag = "[Kebamm][RemoteData]";

        public static DataSource Source { get; private set; } = DataSource.BuiltIn;
        public static int DataVersion { get; private set; }
        public static string UpdatedAt { get; private set; } = "";

        /// <summary>Raised after a new data set has been applied (main menu refreshes its label).</summary>
        public static event Action Applied;

        static Dictionary<string, object> _applied;          // active data (null = built-in)
        static Dictionary<string, object> _pending;          // waiting for the next safe point
        static DataSource _pendingSource;
        static readonly Dictionary<UnityEngine.Object, UnityEngine.Object> _copies = new Dictionary<UnityEngine.Object, UnityEngine.Object>();

        static string CachePath => Path.Combine(Application.persistentDataPath, CacheFile);

        public static bool HasPending => _pending != null;

        public static string StatusText
        {
            get
            {
                string src = Source == DataSource.Online ? "online" : Source == DataSource.Cached ? "cached" : "built-in";
                if (_applied == null)
                    return $"Data: {src}";
                return $"Data v{DataVersion} | {FormatTime(UpdatedAt)} | {src}";
            }
        }

        static string _url = "";
        static float _timeout = 5f;
        static bool _fetching;
        static Dictionary<string, object> _offer;            // newer online data waiting for the player's Yes / No
        static string _offerText;
        static readonly HashSet<string> _declined = new HashSet<string>(); // "version|updatedAt" declined this session

        /// <summary>Newer online data is waiting for the player's answer.</summary>
        public static bool HasOffer => _offer != null;
        /// <summary>"Data vN | time" for the update prompt.</summary>
        public static string OfferText => _offer == null ? "" : $"Data v{VersionOf(_offer)} | {FormatTime(UpdatedOf(_offer))}";

        /// <summary>Launch: queue the cached copy (data the player already accepted) so it applies without asking.</summary>
        public static void Begin(MonoBehaviour host)
        {
            var gm = Resources.Load<KebammGameManagerData>("Kebamm/GameManager");
            _url = gm != null ? (gm.remoteDataUrl ?? "").Trim() : "";
            _timeout = gm != null ? gm.remoteDataTimeoutSeconds : 5f;

            if (TryLoadCache(out var cached, out string cachedText))
            {
                _pending = cached;
                _pendingSource = DataSource.Cached;
            }
            if (string.IsNullOrEmpty(_url))
                Debug.Log($"{LogTag} No remote data URL set in GameManager; using {(_pending != null ? "cached" : "built-in")} data.");
        }

        /// <summary>Checks the Gist for newer data (launch and every return to the main menu). Never applies by itself.</summary>
        public static void CheckOnline(MonoBehaviour host)
        {
            if (string.IsNullOrEmpty(_url) || _fetching || host == null)
                return;
            host.StartCoroutine(FetchOnline(_url, _timeout));
        }

        static string KeyOf(Dictionary<string, object> d) => VersionOf(d) + "|" + UpdatedOf(d);
        static string UpdatedOf(Dictionary<string, object> d) => d.TryGetValue("updatedAt", out object u) && u is string s ? s : "";

        static IEnumerator FetchOnline(string url, float timeout)
        {
            _fetching = true;
            // Cache-busting query: Gist raw links can be served from a CDN cache for a few minutes.
            string requestUrl = url + (url.Contains("?") ? "&" : "?") + "t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            using (var req = UnityWebRequest.Get(requestUrl))
            {
                req.timeout = Mathf.Max(1, Mathf.CeilToInt(timeout));
                yield return req.SendWebRequest();
                _fetching = false;

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"{LogTag} Online check failed ({req.error}); keeping {StatusText}.");
                    yield break;
                }

                string text = req.downloadHandler.text;
                if (!TryParse(text, out var data, out string why))
                {
                    Debug.LogWarning($"{LogTag} Online data rejected: {why}");
                    yield break;
                }

                var current = _pending ?? _applied;
                bool newer = current == null || VersionOf(data) > VersionOf(current) || UpdatedOf(data) != UpdatedOf(current);
                if (!newer)
                {
                    if (_pending == null && Source == DataSource.Cached)
                    {
                        Source = DataSource.Online; // cached copy confirmed current
                        Applied?.Invoke();
                    }
                    Debug.Log($"{LogTag} Online data v{VersionOf(data)} is the same as the applied data.");
                    yield break;
                }
                if (_declined.Contains(KeyOf(data)))
                {
                    Debug.Log($"{LogTag} Online data v{VersionOf(data)} was declined this session; not asking again.");
                    yield break;
                }

                _offer = data;
                _offerText = text;
                Debug.Log($"{LogTag} Newer online data available: {OfferText}; asking the player on the main menu.");
                OfferReady?.Invoke();
            }
        }

        /// <summary>Raised when newer online data is waiting for the player's answer.</summary>
        public static event Action OfferReady;

        /// <summary>Player said Yes: cache the offered data and queue it (the caller applies it on the main menu).</summary>
        public static void AcceptOffer()
        {
            if (_offer == null)
                return;
            try { File.WriteAllText(CachePath, _offerText, new UTF8Encoding(false)); }
            catch (Exception e) { Debug.LogWarning($"{LogTag} Could not write cache: {e.Message}"); }
            Debug.Log($"{LogTag} Player accepted {OfferText}.");
            _pending = _offer;
            _pendingSource = DataSource.Online;
            _offer = null;
            _offerText = null;
        }

        /// <summary>Player said No: keep current data, don't cache, don't ask about this version again this session.</summary>
        public static void DeclineOffer()
        {
            if (_offer == null)
                return;
            Debug.Log($"{LogTag} Player declined {OfferText}; keeping {StatusText}.");
            _declined.Add(KeyOf(_offer));
            _offer = null;
            _offerText = null;
        }

        static bool TryLoadCache(out Dictionary<string, object> data, out string text)
        {
            data = null;
            text = null;
            try
            {
                if (!File.Exists(CachePath))
                    return false;
                text = File.ReadAllText(CachePath, Encoding.UTF8);
                if (TryParse(text, out data, out string why))
                    return true;
                Debug.LogWarning($"{LogTag} Cached data rejected: {why}");
            }
            catch (Exception e) { Debug.LogWarning($"{LogTag} Could not read cache: {e.Message}"); }
            data = null;
            return false;
        }

        static bool TryParse(string text, out Dictionary<string, object> data, out string why)
        {
            data = null;
            why = null;
            object root;
            try { root = MiniJson.Parse(text); }
            catch (Exception e) { why = "invalid JSON: " + e.Message; return false; }
            data = root as Dictionary<string, object>;
            if (data == null) { why = "root is not an object"; return false; }
            if (!(data.TryGetValue("dataVersion", out object v) && v is double)) { why = "missing dataVersion"; data = null; return false; }
            return true;
        }

        static int VersionOf(Dictionary<string, object> d) => d.TryGetValue("dataVersion", out object v) && v is double n ? (int)n : 0;

        /// <summary>Swaps in the pending data set. Call only at launch or on the main menu. Returns true if applied.</summary>
        public static bool CommitPending()
        {
            if (_pending == null)
                return false;
            _applied = _pending;
            Source = _pendingSource;
            _pending = null;
            DataVersion = VersionOf(_applied);
            UpdatedAt = _applied.TryGetValue("updatedAt", out object u) && u is string s ? s : "";
            foreach (var c in _copies.Values)
                if (c != null) UnityEngine.Object.Destroy(c);
            _copies.Clear();
            KebammBallTierData.ClearCache();
            Debug.Log($"{LogTag} Applying data v{DataVersion} ({StatusText}).");
            Applied?.Invoke();
            return true;
        }

        // ---- per-asset copies -------------------------------------------------------------------

        static Dictionary<string, object> Row(string table, string key)
        {
            if (_applied == null || !_applied.TryGetValue(table, out object t) || !(t is Dictionary<string, object> rows))
                return null;
            return rows.TryGetValue(key, out object r) ? r as Dictionary<string, object> : null;
        }

        static T CopyOf<T>(T asset) where T : ScriptableObject
        {
            if (_copies.TryGetValue(asset, out var c) && c != null)
                return (T)c;
            T copy = UnityEngine.Object.Instantiate(asset);
            copy.name = asset.name;
            copy.hideFlags = HideFlags.DontSave;
            _copies[asset] = copy;
            return copy;
        }

        public static KebammLevel ForLevel(KebammLevel asset)
        {
            if (asset == null) return null;
            var row = Row("levelData", asset.name);
            if (row == null) return asset;
            if (_copies.TryGetValue(asset, out var done) && done != null) return (KebammLevel)done;
            var l = CopyOf(asset);
            string n = asset.name;
            SetInt(row, "Monster qty", n, "monsterQty", ref l.monsterQty);
            SetEnumList(row, "Ball colours", n, "prepopulateBallColours", l.prepopulateBallColours);
            if (TryPair(row, "Monster tier range", n, out Vector2 tr))
            {
                SetLog(n, "prepopulateMonsterTierMin", l.prepopulateMonsterTierMin, (int)tr.x); l.prepopulateMonsterTierMin = (int)tr.x;
                SetLog(n, "prepopulateMonsterTierMax", l.prepopulateMonsterTierMax, (int)tr.y); l.prepopulateMonsterTierMax = (int)tr.y;
            }
            SetFloat(row, "Monster stack chance", n, "monsterStackChance", ref l.monsterStackChance);
            SetInt(row, "Ball tier min", n, "ballTierMin", ref l.ballTierMin);
            SetInt(row, "Ball tier max", n, "ballTierMax", ref l.ballTierMax);
            SetFloat(row, "Ball tier 1 weight", n, "ballTier1Weight", ref l.ballTier1Weight);
            SetFloat(row, "Ball tier 2 weight", n, "ballTier2Weight", ref l.ballTier2Weight);
            SetFloat(row, "Ball tier 3 weight", n, "ballTier3Weight", ref l.ballTier3Weight);
            SetFloat(row, "Ball tier 4 weight", n, "ballTier4Weight", ref l.ballTier4Weight);
            SetFloat(row, "Ball tier 5 weight", n, "ballTier5Weight", ref l.ballTier5Weight);
            SetBool(row, "Obstacle populate", n, "obstaclePopulate", ref l.obstaclePopulate);
            SetFloat(row, "Obstacle stack chance", n, "obstacleStackChance", ref l.obstacleStackChance);
            SetEnumList(row, "Allowed obstacle shapes", n, "allowedObstacleShapes", l.allowedObstacleShapes);
            SetInt(row, "Obstacle qty", n, "prepopulateObstacleQty", ref l.prepopulateObstacleQty);
            if (TryPair(row, "Obstacle X", n, out Vector2 ox))
            {
                SetLog(n, "prepopulateObstaclePositionRangeX", l.prepopulateObstaclePositionRangeX, ox);
                l.prepopulateObstaclePositionRangeX = ox;
            }
            return l;
        }

        public static KebammBallTierData ForBallTier(KebammBallTierData asset)
        {
            if (asset == null) return null;
            var row = Row("ballTierData", asset.name);
            if (row == null) return asset;
            if (_copies.TryGetValue(asset, out var done) && done != null) return (KebammBallTierData)done;
            var b = CopyOf(asset);
            string n = asset.name;
            SetFloat(row, "Ball Size", n, "ballSize", ref b.ballSize);
            SetFloat(row, "damage", n, "damage", ref b.damage);
            SetFloat(row, "hp", n, "hp", ref b.hp);
            SetInt(row, "merge_pts", n, "mergePts", ref b.mergePts);
            SetFloat(row, "impact_strength", n, "impactStrength", ref b.impactStrength);
            return b;
        }

        public static KebammGameManagerData ForGameManager(KebammGameManagerData asset)
        {
            if (asset == null) return null;
            var row = Row("gameManager", "GameManager");
            if (row == null) return asset;
            if (_copies.TryGetValue(asset, out var done) && done != null) return (KebammGameManagerData)done;
            var g = CopyOf(asset);
            SetFloat(row, "pillar_height", "GameManager", "pillarHeight", ref g.pillarHeight);
            SetFloat(row, "pillar_gap_width", "GameManager", "pillarGapWidth", ref g.pillarGapWidth);
            SetFloat(row, "caps_line_height", "GameManager", "capsLineHeight", ref g.capsLineHeight);
            SetFloat(row, "jar_to_platform_distance", "GameManager", "jarToPlatformDistance", ref g.jarToPlatformDistance);
            SetFloat(row, "title_breath_duration", "GameManager", "titleBreathDuration", ref g.titleBreathDuration);
            SetFloat(row, "title_letter_height", "GameManager", "titleLetterHeight", ref g.titleLetterHeight);
            return g;
        }

        // ---- field setters (log every change) ---------------------------------------------------

        static void SetLog(string asset, string field, object from, object to)
        {
            string a = Convert.ToString(from, CultureInfo.InvariantCulture);
            string b = Convert.ToString(to, CultureInfo.InvariantCulture);
            if (a != b)
                Debug.Log($"{LogTag} {asset}.{field}: {a} -> {b}");
        }

        static void SetFloat(Dictionary<string, object> row, string key, string asset, string field, ref float target)
        {
            if (!row.TryGetValue(key, out object v)) return;
            if (!(v is double d)) { Debug.LogWarning($"{LogTag} {asset}.{key} is not a number; kept {target}."); return; }
            SetLog(asset, field, target, (float)d);
            target = (float)d;
        }

        static void SetInt(Dictionary<string, object> row, string key, string asset, string field, ref int target)
        {
            if (!row.TryGetValue(key, out object v)) return;
            if (!(v is double d) || Math.Abs(d - Math.Round(d)) > 1e-6) { Debug.LogWarning($"{LogTag} {asset}.{key} is not a whole number; kept {target}."); return; }
            SetLog(asset, field, target, (int)Math.Round(d));
            target = (int)Math.Round(d);
        }

        static void SetBool(Dictionary<string, object> row, string key, string asset, string field, ref bool target)
        {
            if (!row.TryGetValue(key, out object v)) return;
            if (!(v is bool b)) { Debug.LogWarning($"{LogTag} {asset}.{key} is not true/false; kept {target}."); return; }
            SetLog(asset, field, target, b);
            target = b;
        }

        static bool TryPair(Dictionary<string, object> row, string key, string asset, out Vector2 pair)
        {
            pair = default;
            if (!row.TryGetValue(key, out object v)) return false;
            if (v is List<object> l && l.Count == 2 && l[0] is double a && l[1] is double b)
            {
                pair = new Vector2((float)a, (float)b);
                return true;
            }
            Debug.LogWarning($"{LogTag} {asset}.{key} is not a [min, max] pair; kept the asset value.");
            return false;
        }

        static void SetEnumList<TEnum>(Dictionary<string, object> row, string key, string asset, string field, List<TEnum> target) where TEnum : struct
        {
            if (!row.TryGetValue(key, out object v) || target == null) return;
            var parsed = new List<TEnum>();
            if (v is List<object> l)
            {
                foreach (object o in l)
                {
                    if (o is string s && Enum.TryParse(s, false, out TEnum e)) { parsed.Add(e); continue; }
                    Debug.LogWarning($"{LogTag} {asset}.{key}: unknown value '{o}'; kept the asset value.");
                    return;
                }
            }
            else { Debug.LogWarning($"{LogTag} {asset}.{key} is not a list; kept the asset value."); return; }
            SetLog(asset, field, string.Join(",", target), string.Join(",", parsed));
            target.Clear();
            target.AddRange(parsed);
        }

        static string FormatTime(string iso)
        {
            if (DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
                return t.ToString("d MMM yyyy HH:mm zzz", CultureInfo.InvariantCulture);
            return iso;
        }

        // ---- minimal JSON reader (objects, arrays, strings, numbers, bools, null) ---------------

        static class MiniJson
        {
            public static object Parse(string s)
            {
                int i = 0;
                object v = Value(s, ref i);
                Ws(s, ref i);
                if (i != s.Length) throw new FormatException("trailing characters at " + i);
                return v;
            }

            static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

            static object Value(string s, ref int i)
            {
                Ws(s, ref i);
                if (i >= s.Length) throw new FormatException("unexpected end");
                char c = s[i];
                if (c == '{') return Obj(s, ref i);
                if (c == '[') return Arr(s, ref i);
                if (c == '"') return Str(s, ref i);
                if (Lit(s, ref i, "true")) return true;
                if (Lit(s, ref i, "false")) return false;
                if (Lit(s, ref i, "null")) return null;
                return Num(s, ref i);
            }

            static bool Lit(string s, ref int i, string w)
            {
                if (string.CompareOrdinal(s, i, w, 0, w.Length) != 0) return false;
                i += w.Length;
                return true;
            }

            static Dictionary<string, object> Obj(string s, ref int i)
            {
                var d = new Dictionary<string, object>();
                i++;
                Ws(s, ref i);
                if (i < s.Length && s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(s, ref i);
                    string k = Str(s, ref i);
                    Ws(s, ref i);
                    if (i >= s.Length || s[i] != ':') throw new FormatException("':' expected at " + i);
                    i++;
                    d[k] = Value(s, ref i);
                    Ws(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == '}') { i++; return d; }
                    throw new FormatException("',' or '}' expected at " + i);
                }
            }

            static List<object> Arr(string s, ref int i)
            {
                var l = new List<object>();
                i++;
                Ws(s, ref i);
                if (i < s.Length && s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Value(s, ref i));
                    Ws(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == ']') { i++; return l; }
                    throw new FormatException("',' or ']' expected at " + i);
                }
            }

            static string Str(string s, ref int i)
            {
                if (i >= s.Length || s[i] != '"') throw new FormatException("string expected at " + i);
                i++;
                var sb = new StringBuilder();
                while (i < s.Length)
                {
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (i >= s.Length) break;
                    char e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                            i += 4;
                            break;
                        default: sb.Append(e); break;
                    }
                }
                throw new FormatException("unterminated string");
            }

            static double Num(string s, ref int i)
            {
                int start = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                if (start == i) throw new FormatException("value expected at " + start);
                return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }
    }
}
