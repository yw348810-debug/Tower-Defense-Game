using System;
using System.Collections.Generic;
using TowerDefense.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense.Audio
{
    /// <summary>
    /// One persistent music crossfade and a bounded sound-effect pool. No scene references are required.
    /// All voices are 2D; world positions provide gentle stereo placement without distance attenuation.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class GameAudio : MonoBehaviour
    {
        public const int VoiceCapacity = 24;
        public const float MusicFadeSeconds = 1.25f;
        const float MusicMixVolume = .20f;
        const string SourceRootName = "Audio Sources";

        sealed class Voice
        {
            internal AudioSource Source;
            internal SoundCue Cue;
            internal int Priority;
            internal bool Combat;
            internal bool Active;
            internal bool Paused;
            internal double Started;
        }

        static GameAudio instance;
        static bool quitting;
        readonly Dictionary<SoundCue, AudioClip> clips = new Dictionary<SoundCue, AudioClip>();
        readonly HashSet<string> missingWarnings = new HashSet<string>();
        Voice[] voices;
        AudioSource[] music;
        double[] nextAllowed;
        int[] cueCounts;
        readonly float[] fadeStart = new float[2];
        readonly float[] fadeTarget = new float[2];
        float fadeElapsed;
        bool fading;
        Camera sceneCamera;
        string musicKey;
        AudioClip musicClip;

        public static GameAudio Instance => EnsureInitialized();
        public int MusicSourceCount => music == null ? 0 : music.Length;
        public string CurrentMusicKey => musicKey;
        public AudioClip CurrentMusicClip => musicClip;
        public bool PausedBattleAudio => Time.timeScale <= .0001f;
        public SoundCue LastCue { get; private set; }
        public int PlayedCueCount { get; private set; }
        public int ThrottledCueCount { get; private set; }
        public int MissingClipCount => missingWarnings.Count;
        public int ActiveVoiceCount => CountVoices(false, false);
        public int ActiveCombatVoiceCount => CountVoices(true, false);
        public int PausedCombatVoiceCount => CountVoices(true, true);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            quitting = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install() => EnsureInitialized();

        /// <summary>Lazy access also repairs bootstrap after a script reload; it never creates editor objects.</summary>
        public static GameAudio EnsureInitialized()
        {
            if (!Application.isPlaying || quitting) return null;
            if (instance) return instance;
            var existing = FindObjectsByType<GameAudio>(FindObjectsInactive.Include);
            if (existing.Length > 0)
            {
                instance = existing[0];
                for (int i = 1; i < existing.Length; i++)
                {
                    existing[i].StopAllVoices();
                    Destroy(existing[i].gameObject);
                }
                instance.gameObject.SetActive(true);
                instance.enabled = true;
                instance.ReinitializeForPlaySession();
                return instance;
            }
            var root = new GameObject("Game Audio");
            return root.AddComponent<GameAudio>();
        }

        void Awake()
        {
            if (!Application.isPlaying) return;
            if (instance && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSources();
            _ = GameSettings.MasterVolume;
        }

        void OnEnable()
        {
            if (!Application.isPlaying || (instance && instance != this)) return;
            instance = this;
            InitializeSources();
            _ = GameSettings.MasterVolume;
            Subscribe();
            ChangeMusic(SceneManager.GetActiveScene().name);
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            StopAllVoices();
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (instance == this) instance = null;
        }

        void OnApplicationQuit() => quitting = true;

        void ReinitializeForPlaySession()
        {
            DontDestroyOnLoad(gameObject);
            InitializeSources();
            StopAllVoices();
            Array.Clear(cueCounts, 0, cueCounts.Length);
            ResetCooldowns();
            PlayedCueCount = 0;
            ThrottledCueCount = 0;
            LastCue = default;
            musicKey = null;
            musicClip = null;
            fading = false;
            _ = GameSettings.MasterVolume;
            Subscribe();
            ChangeMusic(SceneManager.GetActiveScene().name);
        }

        void Subscribe()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void InitializeSources()
        {
            if (voices != null && voices.Length == VoiceCapacity && voices[0].Source && music != null && music[0])
                return;
            // Nonserialized fields are rebuilt after an editor script reload. Stop the old children first.
            var oldRoot = transform.Find(SourceRootName);
            if (oldRoot)
            {
                foreach (var source in oldRoot.GetComponentsInChildren<AudioSource>(true))
                {
                    source.Stop();
                    source.enabled = false;
                }
                Destroy(oldRoot.gameObject);
            }
            var sourceRoot = new GameObject(SourceRootName).transform;
            sourceRoot.SetParent(transform, false);
            voices = new Voice[VoiceCapacity];
            for (int i = 0; i < voices.Length; i++)
                voices[i] = new Voice { Source = CreateSource(sourceRoot, "SFX " + i, false) };
            music = new[] { CreateSource(sourceRoot, "Music A", true), CreateSource(sourceRoot, "Music B", true) };
            int count = Enum.GetValues(typeof(SoundCue)).Length;
            nextAllowed = new double[count];
            cueCounts = new int[count];
            ResetCooldowns();
            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue))) GetClip(cue);
        }

        static AudioSource CreateSource(Transform parent, string name, bool loop)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.ignoreListenerPause = true;
            source.priority = loop ? 80 : 200;
            source.volume = 0f;
            return source;
        }

        void ResetCooldowns()
        {
            if (nextAllowed == null) return;
            for (int i = 0; i < nextAllowed.Length; i++) nextAllowed[i] = double.NegativeInfinity;
        }

        public AudioClip GetClip(SoundCue cue)
        {
            if (!Enum.IsDefined(typeof(SoundCue), cue)) return null;
            AudioClip clip;
            if (clips.TryGetValue(cue, out clip) && clip) return clip;
            string path = "Audio/SFX_" + cue;
            clip = Resources.Load<AudioClip>(path);
            if (clip) clips[cue] = clip;
            else WarnMissing(path);
            return clip;
        }

        void WarnMissing(string path)
        {
            if (missingWarnings.Add(path)) Debug.LogWarning("Game audio clip is missing: Resources/" + path, this);
        }

        public int GetPlayCount(SoundCue cue)
        {
            int index = (int)cue;
            return cueCounts != null && index >= 0 && index < cueCounts.Length ? cueCounts[index] : 0;
        }

        public static bool Play(SoundCue cue)
        {
            var audio = EnsureInitialized();
            return audio && audio.PlayInternal(cue, null);
        }

        public static bool Play(SoundCue cue, Vector3 worldPosition)
        {
            var audio = EnsureInitialized();
            return audio && audio.PlayInternal(cue, worldPosition);
        }

        bool PlayInternal(SoundCue cue, Vector3? worldPosition)
        {
            int index = (int)cue;
            if (index < 0 || index >= nextAllowed.Length) return false;
            var mix = SoundMix.For(cue);
            if (mix.Combat && PausedBattleAudio) return false;
            double now = Time.unscaledTimeAsDouble;
            if (now < nextAllowed[index])
            {
                ThrottledCueCount++;
                return false;
            }
            var clip = GetClip(cue);
            if (!clip) return false;
            SynchronizeVoices();
            // Button clicks happen immediately before a successful purchase/selection. Keep its clearer cue.
            if (!mix.Combat && cue != SoundCue.UIClick && mix.Priority < 72)
            {
                for (int i = 0; i < voices.Length; i++)
                    if (voices[i].Active && voices[i].Cue == SoundCue.UIClick && now - voices[i].Started < .10)
                        StopVoice(voices[i]);
                nextAllowed[(int)SoundCue.UIClick] = now + .10;
            }
            Voice voice = FindAvailableVoice(mix.Priority);
            if (voice == null)
            {
                ThrottledCueCount++;
                return false;
            }
            StopVoice(voice);
            voice.Cue = cue;
            voice.Priority = mix.Priority;
            voice.Combat = mix.Combat;
            voice.Active = true;
            voice.Paused = false;
            voice.Started = now;
            var source = voice.Source;
            source.clip = clip;
            source.volume = mix.Volume;
            source.priority = mix.Priority;
            // A local deterministic variation never consumes gameplay or card-offer random numbers.
            int variation = (cueCounts[index] * 17 + index * 7) % 11;
            source.pitch = mix.VaryPitch ? .95f + variation * .01f : 1f;
            source.panStereo = 0f;
            if (worldPosition.HasValue)
            {
                if (!sceneCamera) sceneCamera = Camera.main;
                if (sceneCamera)
                {
                    var viewport = sceneCamera.WorldToViewportPoint(worldPosition.Value);
                    if (viewport.z > 0f) source.panStereo = Mathf.Clamp((viewport.x - .5f) * .5f, -.25f, .25f);
                }
            }
            source.Play();
            nextAllowed[index] = now + mix.Cooldown;
            cueCounts[index]++;
            PlayedCueCount++;
            LastCue = cue;
            return true;
        }

        Voice FindAvailableVoice(int incomingPriority)
        {
            Voice victim = null;
            for (int i = 0; i < voices.Length; i++)
            {
                Voice voice = voices[i];
                if (!voice.Active) return voice;
                if (voice.Priority < incomingPriority) continue;
                // Ordinary effects cannot repeatedly cut each other or a key event at the same priority.
                if (voice.Priority == incomingPriority) continue;
                if (victim == null || voice.Priority > victim.Priority ||
                    (voice.Priority == victim.Priority && voice.Started < victim.Started)) victim = voice;
            }
            return victim;
        }

        void Update()
        {
            SynchronizeVoices();
            if (!fading || music == null) return;
            fadeElapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fadeElapsed / MusicFadeSeconds));
            for (int i = 0; i < music.Length; i++) music[i].volume = Mathf.Lerp(fadeStart[i], fadeTarget[i], t);
            if (fadeElapsed < MusicFadeSeconds) return;
            fading = false;
            for (int i = 0; i < music.Length; i++)
                if (fadeTarget[i] <= 0f) { music[i].Stop(); music[i].clip = null; }
        }

        void SynchronizeVoices()
        {
            if (voices == null) return;
            bool paused = PausedBattleAudio;
            for (int i = 0; i < voices.Length; i++)
            {
                Voice voice = voices[i];
                if (!voice.Active) continue;
                if (voice.Combat && paused)
                {
                    if (!voice.Paused) { voice.Source.Pause(); voice.Paused = true; }
                    continue;
                }
                if (voice.Paused) { voice.Source.UnPause(); voice.Paused = false; }
                if (!voice.Source.isPlaying) StopVoice(voice);
            }
        }

        static void StopVoice(Voice voice)
        {
            if (voice.Source)
            {
                voice.Source.Stop();
                voice.Source.clip = null;
            }
            voice.Active = false;
            voice.Paused = false;
        }

        public static void StopCombatSounds()
        {
            if (!instance || instance.voices == null) return;
            for (int i = 0; i < instance.voices.Length; i++)
                if (instance.voices[i].Combat || IsResult(instance.voices[i].Cue)) StopVoice(instance.voices[i]);
            // A restart should not inherit the previous run's warning or shooting cooldowns.
            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
                if (SoundMix.For(cue).Combat || IsResult(cue)) instance.nextAllowed[(int)cue] = double.NegativeInfinity;
        }

        static bool IsResult(SoundCue cue) => cue == SoundCue.Victory || cue == SoundCue.Defeat;

        void StopAllVoices()
        {
            if (voices != null) for (int i = 0; i < voices.Length; i++) StopVoice(voices[i]);
            if (music != null) for (int i = 0; i < music.Length; i++) if (music[i]) music[i].Stop();
        }

        int CountVoices(bool combatOnly, bool pausedOnly)
        {
            if (voices == null) return 0;
            int count = 0;
            for (int i = 0; i < voices.Length; i++)
                if (voices[i].Active && (!combatOnly || voices[i].Combat) && (!pausedOnly || voices[i].Paused)) count++;
            return count;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive && scene != SceneManager.GetActiveScene()) return;
            StopCombatSounds();
            sceneCamera = null;
            ChangeMusic(scene.name);
        }

        public static string MusicKeyForScene(string sceneName)
        {
            switch (sceneName)
            {
                case "MainMenu": return "Audio/Music_Menu";
                case "Level01_ForestOutpost": return "Audio/Music_Forest";
                case "Level02_CanyonConfluence": return "Audio/Music_Canyon";
                case "Level03_CoreFortress": return "Audio/Music_Snow";
                default: return null;
            }
        }

        void ChangeMusic(string sceneName)
        {
            string key = MusicKeyForScene(sceneName);
            if (key == musicKey && musicClip &&
                ((music[0].clip == musicClip && music[0].isPlaying) ||
                 (music[1].clip == musicClip && music[1].isPlaying))) return;
            var clip = key == null ? null : Resources.Load<AudioClip>(key);
            if (key != null && !clip) WarnMissing(key);
            musicKey = key;
            musicClip = clip;
            int incoming = -1;
            if (clip)
                for (int i = 0; i < music.Length; i++)
                    if (music[i].clip == clip && music[i].isPlaying) { incoming = i; break; }
            // Reversing a transition keeps the old track's playback position, rather
            // than layering a second copy of that same track with a different phase.
            bool reuse = incoming >= 0;
            if (!reuse) incoming = music[0].volume >= music[1].volume ? 1 : 0;
            if (clip && !reuse)
            {
                music[incoming].Stop();
                music[incoming].clip = clip;
                music[incoming].volume = 0f;
                music[incoming].pitch = 1f;
                music[incoming].Play();
            }
            for (int i = 0; i < music.Length; i++)
            {
                fadeStart[i] = music[i].volume;
                fadeTarget[i] = clip && i == incoming ? MusicMixVolume : 0f;
            }
            fadeElapsed = 0f;
            fading = true;
        }
    }
}
