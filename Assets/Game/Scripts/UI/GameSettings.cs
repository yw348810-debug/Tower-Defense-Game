using System;
using TowerDefense.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense.UI
{
    public enum GameLanguage { English, Chinese }
    public enum GameDifficulty { Easy, Hard, Cheat }

    /// <summary>One persisted source of truth, shared by the home and in-battle settings.</summary>
    public static class GameSettings
    {
        const string VolumeKey = "TowerDefense.MasterVolume";
        const string LanguageKey = "TowerDefense.Language";
        const string DifficultyKey = "TowerDefense.Difficulty";
        static bool loaded;
        static float volume = .8f;
        static GameLanguage language = GameLanguage.Chinese;
        static GameDifficulty difficulty = GameDifficulty.Easy;
        public static event Action Changed;
        public static float MasterVolume { get { EnsureLoaded(); return volume; } }
        public static GameLanguage Language { get { EnsureLoaded(); return language; } }
        public static GameDifficulty Difficulty { get { EnsureLoaded(); return difficulty; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { loaded = false; Changed = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            EnsureLoaded();
            SceneManager.sceneLoaded -= AttachBattleMenu;
            SceneManager.sceneLoaded += AttachBattleMenu;
        }

        static void EnsureLoaded()
        {
            if (loaded) return;
            float saved = PlayerPrefs.GetFloat(VolumeKey, .8f);
            volume = float.IsNaN(saved) || float.IsInfinity(saved) ? .8f : Mathf.Clamp01(saved);
            language = PlayerPrefs.GetInt(LanguageKey, (int)GameLanguage.Chinese) == (int)GameLanguage.English
                ? GameLanguage.English : GameLanguage.Chinese;
            int savedDifficulty = PlayerPrefs.GetInt(DifficultyKey, (int)GameDifficulty.Easy);
            difficulty = Enum.IsDefined(typeof(GameDifficulty), savedDifficulty)
                ? (GameDifficulty)savedDifficulty : GameDifficulty.Easy;
            loaded = true;
            AudioListener.volume = volume;
        }

        public static void SetVolume(float value)
        {
            EnsureLoaded();
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            value = Mathf.Clamp01(value);
            bool changed = !Mathf.Approximately(volume, value);
            volume = value; AudioListener.volume = volume;
            PlayerPrefs.SetFloat(VolumeKey, volume); PlayerPrefs.Save();
            if (changed) Changed?.Invoke();
        }

        public static void SetLanguage(GameLanguage value)
        {
            EnsureLoaded();
            if (value != GameLanguage.Chinese && value != GameLanguage.English) return;
            bool changed = language != value;
            language = value;
            PlayerPrefs.SetInt(LanguageKey, (int)value); PlayerPrefs.Save();
            if (changed) Changed?.Invoke();
        }

        public static void SetDifficulty(GameDifficulty value)
        {
            EnsureLoaded();
            if (!Enum.IsDefined(typeof(GameDifficulty), value) || difficulty == value) return;
            difficulty = value;
            PlayerPrefs.SetInt(DifficultyKey, (int)value); PlayerPrefs.Save();
            Changed?.Invoke();
        }

        static void AttachBattleMenu(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var session in root.GetComponentsInChildren<LevelSession>(true))
                {
                    var menu = session.GetComponent<PauseMenu>();
                    if (!menu) menu = session.gameObject.AddComponent<PauseMenu>();
                    menu.session = session;
                }
        }
    }
}
