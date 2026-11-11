using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Kebamm
{
    [Serializable]
    public class KebammSavePlayer
    {
        public string playerId = "";
        public string username = "";
        public string createdAtUtc = "";
        public string createdTimeSource = ""; // "server" or "device"
    }

    [Serializable]
    public class KebammSaveProgress
    {
        public int highestLevelUnlocked = 1;
        public int lastLevelPlayed = 1;
        public int coins = 0; // total coins earned (Sprint 4 coin reward); no spending yet
    }

    [Serializable]
    public class KebammSaveSettings
    {
        // Shared by the Pause Menu and the Settings popup (KebammAudio). Saves without this block load as ON.
        public bool soundOn = true;
        public bool musicOn = true;
    }

    [Serializable]
    public class KebammSaveLevel
    {
        public int level;
        public int bestStars;
        public int bestScore;
        public int attempts; // score screens reached on this level (analytics attempt counter)
    }

    [Serializable]
    public class KebammSaveData
    {
        public int saveVersion = KebammSaveSystem.CurrentVersion;
        public KebammSavePlayer player = new KebammSavePlayer();
        public KebammSaveProgress progress = new KebammSaveProgress();
        public List<KebammSaveLevel> levels = new List<KebammSaveLevel>();
        public KebammSaveSettings settings = new KebammSaveSettings();
        public string savedAt = "";
    }

    /// <summary>
    /// Local game-progression save (Notion: Save System). One JSON file in persistentDataPath:
    /// write kebamm_save.tmp, then replace kebamm_save.json, keeping the previous file as kebamm_save.bak.
    /// Best-only: stars and score per level only go up; highestLevelUnlocked only goes up.
    /// Username: "Player" + MMddHH (UTC) + 2 random digits, from the server Date header of the remote data
    /// check; if that fails on the first launch, the device UTC clock is used once.
    /// </summary>
    public static class KebammSaveSystem
    {
        public const int CurrentVersion = 1;
        const string LogTag = "[KebammSave]";
        const string FileName = "kebamm_save.json";

        static KebammSaveData _data;

        /// <summary>Set by the debug console win command: the next score screen does not write the save.</summary>
        public static bool SkipNextResult { get; set; }

        static string Dir => Application.persistentDataPath;
        static string MainPath => Path.Combine(Dir, FileName);
        static string TmpPath => Path.Combine(Dir, "kebamm_save.tmp");
        static string BakPath => Path.Combine(Dir, "kebamm_save.bak");

        public static KebammSaveData Data
        {
            get
            {
                if (_data == null)
                    Load();
                return _data;
            }
        }

        public static bool HasUsername => !string.IsNullOrEmpty(Data.player.username);

        public static void Load()
        {
            if (TryRead(MainPath, out _data))
            {
                Debug.Log($"{LogTag} Loaded {MainPath}: {Summary()}");
                return;
            }
            if (TryRead(BakPath, out _data))
            {
                Debug.LogWarning($"{LogTag} Main save missing or corrupt; using backup: {Summary()}");
                Save();
                return;
            }
            bool existed = File.Exists(MainPath) || File.Exists(BakPath);
            _data = NewData();
            if (existed)
                Debug.LogWarning($"{LogTag} Save and backup unreadable; starting a fresh save.");
            else
                Debug.Log($"{LogTag} No save found; starting a fresh save (first launch).");
            Save();
        }

        static KebammSaveData NewData()
        {
            var d = new KebammSaveData();
            d.player.playerId = Guid.NewGuid().ToString();
            return d;
        }

        static bool TryRead(string path, out KebammSaveData data)
        {
            data = null;
            try
            {
                if (!File.Exists(path))
                    return false;
                string json = File.ReadAllText(path);
                data = JsonUtility.FromJson<KebammSaveData>(json);
                if (data == null || data.player == null || data.progress == null)
                    return false;
                if (data.levels == null)
                    data.levels = new List<KebammSaveLevel>();
                // Saves written before the Sound / Music toggles have no "settings" block: both default ON.
                if (data.settings == null || !json.Contains("\"settings\""))
                    data.settings = new KebammSaveSettings();
                if (string.IsNullOrEmpty(data.player.playerId))
                    data.player.playerId = Guid.NewGuid().ToString();
                if (data.saveVersion > CurrentVersion)
                    Debug.LogWarning($"{LogTag} Save version {data.saveVersion} is newer than this build ({CurrentVersion}); reading what it can.");
                // Older versions would be migrated here (none yet).
                data.saveVersion = CurrentVersion;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogTag} Could not read {path}: {e.Message}");
                data = null;
                return false;
            }
        }

        public static void Save()
        {
            if (_data == null)
                return;
            try
            {
                _data.savedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                File.WriteAllText(TmpPath, JsonUtility.ToJson(_data, true));
                if (File.Exists(MainPath))
                    File.Copy(MainPath, BakPath, true);
                if (File.Exists(MainPath))
                    File.Delete(MainPath);
                File.Move(TmpPath, MainPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogTag} Save failed: {e.Message}");
            }
        }

        /// <summary>Called once the remote data check returns (dateHeader null = no server time; device clock is used).</summary>
        public static void ResolveUsername(string dateHeader)
        {
            if (HasUsername)
                return;
            DateTime utc;
            string source;
            if (!string.IsNullOrEmpty(dateHeader) &&
                DateTime.TryParse(dateHeader, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc))
            {
                source = "server";
            }
            else
            {
                utc = DateTime.UtcNow;
                source = "device";
            }
            var p = Data.player;
            p.username = "Player" + utc.ToString("MMddHH", CultureInfo.InvariantCulture) +
                         UnityEngine.Random.Range(0, 100).ToString("00", CultureInfo.InvariantCulture);
            p.createdAtUtc = utc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            p.createdTimeSource = source;
            Debug.Log($"{LogTag} Username created: {p.username} (time {p.createdAtUtc}, source {source})");
            Save();
        }

        static KebammSaveLevel GetLevel(int level, bool create)
        {
            foreach (var l in Data.levels)
                if (l.level == level)
                    return l;
            if (!create)
                return null;
            var n = new KebammSaveLevel { level = level };
            Data.levels.Add(n);
            Data.levels.Sort((a, b) => a.level.CompareTo(b.level));
            return n;
        }

        /// <summary>Score screen result. Best-only; unlocks nextLevel.</summary>
        public static void RecordResult(int level, int stars, int score, int nextLevel, int coinsEarned = 0)
        {
            if (SkipNextResult)
            {
                SkipNextResult = false;
                Debug.Log($"{LogTag} Debug win on level {level}: save not changed.");
                return;
            }
            var l = GetLevel(level, true);
            int oldStars = l.bestStars, oldScore = l.bestScore;
            l.bestStars = Mathf.Max(l.bestStars, Mathf.Clamp(stars, 0, 3));
            l.bestScore = Mathf.Max(l.bestScore, score);
            var pr = Data.progress;
            pr.highestLevelUnlocked = Mathf.Max(pr.highestLevelUnlocked, Mathf.Max(level, nextLevel));
            // Written when the score screen is shown: quitting there resumes at the next level.
            pr.lastLevelPlayed = Mathf.Max(level, nextLevel);
            int oldCoins = pr.coins;
            pr.coins = Mathf.Max(0, pr.coins) + Mathf.Max(0, coinsEarned);
            Debug.Log($"{LogTag} Coins: +{Mathf.Max(0, coinsEarned)} ({oldCoins} -> {pr.coins})");
            Debug.Log($"{LogTag} Level {level} result: stars {stars} (best {oldStars} -> {l.bestStars}), score {score} (best {oldScore} -> {l.bestScore}), highest unlocked {pr.highestLevelUnlocked}, last played {pr.lastLevelPlayed}");
            Save();
            Debug.Log($"{LogTag} Saved to disk at score screen (level {level}).");
        }

        /// <summary>Analytics: counts one more attempt on this level (score screen reached) and returns it (1 = first).</summary>
        public static int NextAttempt(int level)
        {
            var l = GetLevel(level, true);
            l.attempts++;
            Save();
            return l.attempts;
        }

        public static void SetLastPlayed(int level)
        {
            if (Data.progress.lastLevelPlayed == level)
                return;
            Data.progress.lastLevelPlayed = level;
            Save();
        }

        /// <summary>Deletes kebamm_save.json and .bak (remote data cache untouched) and starts a fresh save.</summary>
        public static void DeleteAndReset()
        {
            try
            {
                if (File.Exists(MainPath)) File.Delete(MainPath);
                if (File.Exists(BakPath)) File.Delete(BakPath);
                if (File.Exists(TmpPath)) File.Delete(TmpPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogTag} Delete failed: {e.Message}");
            }
            SkipNextResult = false;
            _data = NewData();
            Debug.Log($"{LogTag} Save deleted; fresh save started.");
            Save();
        }

        /// <summary>
        /// Debug: resets this player's progression to a fresh state (same player id / username / sound + music settings).
        /// Clears every saved level (best stars, best score, attempts), unlocks back to level 1, last played 1, coins 0.
        /// </summary>
        public static void ResetProgress()
        {
            var d = Data;
            int levelsCleared = d.levels.Count;
            int oldCoins = d.progress.coins;
            d.levels.Clear();
            d.progress = new KebammSaveProgress(); // highestLevelUnlocked 1, lastLevelPlayed 1, coins 0
            SkipNextResult = false;
            Debug.Log($"{LogTag} Progression reset: {levelsCleared} level result(s) cleared, coins {oldCoins} -> 0, unlocked back to level 1 (player id, username and settings kept).");
            Save();
        }

        public static void UnlockAll(int maxLevel)
        {
            Data.progress.highestLevelUnlocked = Mathf.Max(Data.progress.highestLevelUnlocked, maxLevel);
            Debug.Log($"{LogTag} Unlock all: highest unlocked {Data.progress.highestLevelUnlocked}");
            Save();
        }

        /// <summary>Debug: sets a level's stars exactly (can lower them).</summary>
        public static void SetStars(int level, int stars)
        {
            GetLevel(level, true).bestStars = Mathf.Clamp(stars, 0, 3);
            Debug.Log($"{LogTag} Set stars: level {level} = {Mathf.Clamp(stars, 0, 3)}");
            Save();
        }

        public static int Coins => Data.progress.coins;

        public static int BestStars(int level) => GetLevel(level, false)?.bestStars ?? 0;

        /// <summary>Sum of best stars across all saved levels (main menu player panel).</summary>
        public static int TotalStars()
        {
            int total = 0;
            foreach (var l in Data.levels)
                total += l.bestStars;
            return total;
        }

        public static string Summary()
        {
            var d = _data;
            if (d == null)
                return "(not loaded)";
            string name = string.IsNullOrEmpty(d.player.username) ? "(pending)" : d.player.username;
            return $"user {name}, highest unlocked {d.progress.highestLevelUnlocked}, coins {d.progress.coins}, last played {d.progress.lastLevelPlayed}, levels saved {d.levels.Count}, sound {(d.settings.soundOn ? "on" : "off")}, music {(d.settings.musicOn ? "on" : "off")}";
        }

        /// <summary>Per-level lines for the debug console.</summary>
        public static List<string> LevelLines()
        {
            var lines = new List<string>();
            foreach (var l in Data.levels)
                lines.Add($"L{l.level}: {l.bestStars}* {l.bestScore}");
            return lines;
        }
    }
}
