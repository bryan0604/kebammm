using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// Central Sound / Music mute point, shared by the Pause Menu and the Settings popup.
    /// The two flags live in the save file (KebammSaveSystem settings, default ON).
    /// There is no game audio yet: future AudioSources call RegisterSound / RegisterMusic
    /// (or read SoundOn / MusicOn) so they follow the toggles automatically.
    /// </summary>
    public static class KebammAudio
    {
        const string LogTag = "[KebammAudio]";

        /// <summary>Raised after either flag changes (or after a save reset) so open UIs can refresh.</summary>
        public static event Action Changed;

        static readonly List<AudioSource> _sound = new List<AudioSource>();
        static readonly List<AudioSource> _music = new List<AudioSource>();

        public static bool SoundOn => KebammSaveSystem.Data.settings.soundOn;
        public static bool MusicOn => KebammSaveSystem.Data.settings.musicOn;

        public static void RegisterSound(AudioSource source) { Register(_sound, source); }
        public static void RegisterMusic(AudioSource source) { Register(_music, source); }

        public static void Unregister(AudioSource source)
        {
            _sound.Remove(source);
            _music.Remove(source);
        }

        static void Register(List<AudioSource> list, AudioSource source)
        {
            if (source == null)
                return;
            if (!list.Contains(source))
                list.Add(source);
            Apply();
        }

        public static void SetSound(bool on, string from)
        {
            if (SoundOn == on)
                return;
            KebammSaveSystem.Data.settings.soundOn = on;
            KebammSaveSystem.Save();
            Debug.Log($"{LogTag} Sound {(on ? "ON" : "OFF")} ({from}), saved.");
            Refresh();
        }

        public static void SetMusic(bool on, string from)
        {
            if (MusicOn == on)
                return;
            KebammSaveSystem.Data.settings.musicOn = on;
            KebammSaveSystem.Save();
            Debug.Log($"{LogTag} Music {(on ? "ON" : "OFF")} ({from}), saved.");
            Refresh();
        }

        /// <summary>Re-applies the saved flags to registered sources and notifies UIs (also after a save reset or load).</summary>
        public static void Refresh()
        {
            Apply();
            Changed?.Invoke();
        }

        static void Apply()
        {
            _sound.RemoveAll(s => s == null);
            _music.RemoveAll(s => s == null);
            bool soundMute = !SoundOn, musicMute = !MusicOn;
            foreach (var s in _sound) s.mute = soundMute;
            foreach (var s in _music) s.mute = musicMute;
        }
    }
}
