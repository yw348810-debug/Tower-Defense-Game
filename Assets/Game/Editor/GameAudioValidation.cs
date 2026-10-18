using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TowerDefense.Audio;
using TowerDefense.Gameplay;
using TowerDefense.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense.EditorTools
{
    /// <summary>Synchronous Play Mode checks; no scene reload or gameplay/economy mutations.</summary>
    public static class GameAudioValidation
    {
        const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
        const string VolumeKey = "TowerDefense.MasterVolume";

        [Serializable] public sealed class Check { public string name; public bool passed; }
        [Serializable] public sealed class Report
        {
            public bool passed;
            public int failures, level;
            public string error;
            public Check[] checks;
        }

        static object Call(GameAudio audio, string name, params object[] args)
            => typeof(GameAudio).GetMethods(PrivateInstance)
                .First(method => method.Name == name && method.GetParameters().Length == args.Length).Invoke(audio, args);
        static T Field<T>(GameAudio audio, string name)
            => (T)typeof(GameAudio).GetField(name, PrivateInstance).GetValue(audio);

        static void ClearEffects(GameAudio audio)
        {
            var stop = typeof(GameAudio).GetMethod("StopVoice", PrivateStatic);
            foreach (object voice in Field<Array>(audio, "voices")) stop.Invoke(null, new[] { voice });
            Call(audio, "ResetCooldowns");
        }

        static void ResetCooldown(GameAudio audio, SoundCue cue)
            => Field<double[]>(audio, "nextAllowed")[(int)cue] = double.NegativeInfinity;

        static void CompleteFade(GameAudio audio)
        {
            typeof(GameAudio).GetField("fadeElapsed", PrivateInstance).SetValue(audio, GameAudio.MusicFadeSeconds);
            Call(audio, "Update");
        }

        static AudioSource[] Sources(GameAudio audio) => audio.GetComponentsInChildren<AudioSource>(true);
        static AudioSource[] MusicSources(GameAudio audio) => Field<AudioSource[]>(audio, "music");
        static int ClipVoices(GameAudio audio, SoundCue cue)
            => Sources(audio).Count(source => !source.loop && source.clip == audio.GetClip(cue));

        public static Report Run()
        {
            var session = UnityEngine.Object.FindAnyObjectByType<LevelSession>();
            var report = new Report { level = session ? session.levelIndex : 0 };
            var checks = new List<Check>();
            Action<string, bool> check = (name, pass) => checks.Add(new Check { name = name, passed = pass });
            if (!EditorApplication.isPlaying)
            {
                report.error = "Requires Play Mode in MainMenu or a running level.";
                return Save(report, checks);
            }

            float originalTimeScale = Time.timeScale;
            float originalVolume = GameSettings.MasterVolume;
            bool hadVolumePref = PlayerPrefs.HasKey(VolumeKey);
            float originalPref = PlayerPrefs.GetFloat(VolumeKey);
            string originalScene = SceneManager.GetActiveScene().name;
            GameAudio audio = null;
            try
            {
                audio = GameAudio.EnsureInitialized();
                check("Automatic audio singleton exists in Play Mode", audio != null);
                if (!audio) throw new InvalidOperationException("Audio bootstrap failed.");
                ClearEffects(audio);
                Time.timeScale = 1f;
                GameSettings.SetVolume(0f);

                string[] scenes = { "MainMenu", "Level01_ForestOutpost", "Level02_CanyonConfluence", "Level03_CoreFortress" };
                string[] musicKeys = { "Audio/Music_Menu", "Audio/Music_Forest", "Audio/Music_Canyon", "Audio/Music_Snow" };
                int resources = 0;
                for (int i = 0; i < scenes.Length; i++)
                {
                    check(scenes[i] + " maps to its own music", GameAudio.MusicKeyForScene(scenes[i]) == musicKeys[i]);
                    var clip = Resources.Load<AudioClip>(musicKeys[i]);
                    resources += clip ? 1 : 0;
                    var importer = clip ? AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) as AudioImporter : null;
                    check(musicKeys[i] + " is a readable stereo music asset", clip && clip.frequency == 44100
                        && clip.channels == 2 && clip.samples > 0 && clip.length >= 30f);
                    check(musicKeys[i] + " streams compressed music without preloading", importer
                        && importer.defaultSampleSettings.loadType == AudioClipLoadType.Streaming
                        && importer.defaultSampleSettings.compressionFormat == AudioCompressionFormat.Vorbis
                        && !importer.defaultSampleSettings.preloadAudioData && importer.loadInBackground);
                }
                check("Unknown scenes have no unintended music", GameAudio.MusicKeyForScene("Audio test fixture") == null);
                foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
                {
                    var clip = audio.GetClip(cue);
                    resources += clip ? 1 : 0;
                    var importer = clip ? AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) as AudioImporter : null;
                    check(cue + " is a readable mono effect asset", clip && clip.frequency == 44100
                        && clip.channels == 1 && clip.samples > 0 && clip.length > 0f && clip.length < 8f);
                    check(cue + " is preloaded PCM for immediate playback", importer
                        && importer.defaultSampleSettings.loadType == AudioClipLoadType.DecompressOnLoad
                        && importer.defaultSampleSettings.compressionFormat == AudioCompressionFormat.PCM
                        && importer.defaultSampleSettings.preloadAudioData && !importer.loadInBackground);
                    bool audibleData = false;
                    if (clip)
                    {
                        var samples = new float[Mathf.Min(clip.samples, 4096) * clip.channels];
                        audibleData = clip.GetData(samples, 0) && samples.Any(sample => Mathf.Abs(sample) > .00001f);
                    }
                    check(cue + " contains actual decoded sound samples", audibleData);
                }
                check("All 29 music and effect resources are available", resources == 29 && audio.MissingClipCount == 0);
                check("Exactly one audio owner exists", UnityEngine.Object.FindObjectsByType<GameAudio>(FindObjectsInactive.Include).Length == 1);
                check("The pool contains exactly 24 effects and two music sources", Sources(audio).Length == 26
                    && Sources(audio).Count(source => !source.loop) == GameAudio.VoiceCapacity
                    && Sources(audio).Count(source => source.loop) == 2 && audio.MusicSourceCount == 2);
                check("Every source remains audible independent of map-camera distance", Sources(audio).All(source =>
                    source.spatialBlend == 0f && source.dopplerLevel == 0f && !source.playOnAwake));

                // Simulate the SubsystemRegistration reset used with Domain Reload disabled.
                var owner = audio;
                typeof(GameAudio).GetMethod("ResetStatics", PrivateStatic).Invoke(null, null);
                audio = GameAudio.EnsureInitialized();
                check("Static reset reuses the existing owner and bounded source pool", ReferenceEquals(owner, audio)
                    && Sources(audio).Length == 26 && UnityEngine.Object.FindObjectsByType<GameAudio>(FindObjectsInactive.Include).Length == 1);
                Call(audio, "ChangeMusic", originalScene);
                CompleteFade(audio);

                // Local mix gains must not change when the one shared listener gain changes.
                ClearEffects(audio);
                int shotsBefore = audio.GetPlayCount(SoundCue.GunShot);
                check("A muted effect still schedules its real clip", GameAudio.Play(SoundCue.GunShot)
                    && audio.GetPlayCount(SoundCue.GunShot) == shotsBefore + 1);
                var shotSource = Sources(audio).First(source => source.clip == audio.GetClip(SoundCue.GunShot));
                float localShotGain = shotSource.volume;
                var activeMusic = MusicSources(audio).First(source => source.clip == audio.CurrentMusicClip);
                float localMusicGain = activeMusic.volume;
                check("Zero volume mutes the common listener", GameSettings.MasterVolume == 0f && AudioListener.volume == 0f);
                GameSettings.SetVolume(.37f);
                check("Intermediate volume affects music and effects through the listener once", Mathf.Approximately(AudioListener.volume, .37f)
                    && Mathf.Approximately(GameSettings.MasterVolume, .37f) && Mathf.Approximately(shotSource.volume, localShotGain)
                    && Mathf.Approximately(activeMusic.volume, localMusicGain));
                GameSettings.SetVolume(1f);
                check("Restoring volume resumes the same sources without remixing", Mathf.Approximately(AudioListener.volume, 1f)
                    && shotSource.isPlaying && activeMusic.isPlaying && Mathf.Approximately(shotSource.volume, localShotGain)
                    && Mathf.Approximately(activeMusic.volume, localMusicGain));
                GameSettings.SetVolume(0f);
                ClearEffects(audio);

                int totalBefore = audio.PlayedCueCount;
                int clicksBefore = audio.GetPlayCount(SoundCue.UIClick);
                check("A click increments both actual playback counters", GameAudio.Play(SoundCue.UIClick)
                    && audio.PlayedCueCount == totalBefore + 1 && audio.GetPlayCount(SoundCue.UIClick) == clicksBefore + 1
                    && audio.LastCue == SoundCue.UIClick && ClipVoices(audio, SoundCue.UIClick) == 1);
                int throttlesBefore = audio.ThrottledCueCount;
                check("A same-frame repeated click is throttled", !GameAudio.Play(SoundCue.UIClick)
                    && audio.PlayedCueCount == totalBefore + 1 && audio.ThrottledCueCount == throttlesBefore + 1);
                check("A successful operation replaces its ordinary click", GameAudio.Play(SoundCue.Build)
                    && ClipVoices(audio, SoundCue.UIClick) == 0 && ClipVoices(audio, SoundCue.Build) == 1
                    && !GameAudio.Play(SoundCue.UIClick));
                ClearEffects(audio);

                bool fullPoolScheduled = true;
                for (int i = 0; i < GameAudio.VoiceCapacity; i++)
                {
                    ResetCooldown(audio, SoundCue.ProjectileHit);
                    fullPoolScheduled &= GameAudio.Play(SoundCue.ProjectileHit);
                }
                check("Many simultaneous effects fill the fixed pool without allocating sources", fullPoolScheduled
                    && audio.ActiveVoiceCount == GameAudio.VoiceCapacity && Sources(audio).Length == 26);
                ResetCooldown(audio, SoundCue.ProjectileHit);
                check("An ordinary hit cannot grow or churn the full pool", !GameAudio.Play(SoundCue.ProjectileHit)
                    && audio.ActiveVoiceCount == GameAudio.VoiceCapacity);
                check("Victory preempts an ordinary hit in the full pool", GameAudio.Play(SoundCue.Victory)
                    && audio.LastCue == SoundCue.Victory && ClipVoices(audio, SoundCue.Victory) == 1
                    && ClipVoices(audio, SoundCue.ProjectileHit) == GameAudio.VoiceCapacity - 1
                    && audio.ActiveVoiceCount == GameAudio.VoiceCapacity);
                check("Boss warnings also preempt ordinary hits without interrupting victory", GameAudio.Play(SoundCue.BossArrival)
                    && ClipVoices(audio, SoundCue.BossArrival) == 1 && ClipVoices(audio, SoundCue.Victory) == 1
                    && Sources(audio).Length == 26);
                GameAudio.StopCombatSounds();
                check("Combat cleanup also removes previous result sounds", audio.ActiveVoiceCount == 0
                    && ClipVoices(audio, SoundCue.Victory) == 0 && ClipVoices(audio, SoundCue.BossArrival) == 0);

                // Apply the same private synchronization used in the real Update, without waiting a frame.
                ClearEffects(audio);
                check("Combat effect starts before pause", GameAudio.Play(SoundCue.Explosion) && audio.ActiveCombatVoiceCount == 1);
                var battleSource = Sources(audio).First(source => source.clip == audio.GetClip(SoundCue.Explosion));
                Time.timeScale = 0f;
                Call(audio, "SynchronizeVoices");
                check("Battle pause preserves but pauses existing effects", audio.PausedBattleAudio
                    && audio.ActiveCombatVoiceCount == 1 && audio.PausedCombatVoiceCount == 1 && !battleSource.isPlaying);
                check("Battle pause rejects newly requested attacks", !GameAudio.Play(SoundCue.GunShot)
                    && audio.ActiveCombatVoiceCount == 1);
                check("Menus and purchases remain audible during battle pause", GameAudio.Play(SoundCue.UICancel)
                    && GameAudio.Play(SoundCue.Upgrade));
                check("Background music keeps playing while battle is paused", MusicSources(audio).Any(source => source.isPlaying)
                    && MusicSources(audio).All(source => source.ignoreListenerPause));
                Time.timeScale = 1f;
                Call(audio, "SynchronizeVoices");
                check("Resume continues the paused combat voice", !audio.PausedBattleAudio
                    && audio.PausedCombatVoiceCount == 0 && battleSource.isPlaying);
                ClearEffects(audio);
                Time.timeScale = 0f;
                check("Results are allowed while gameplay time is stopped", GameAudio.Play(SoundCue.Victory) && GameAudio.Play(SoundCue.Defeat));
                ResetCooldown(audio, SoundCue.UIClick);
                GameAudio.Play(SoundCue.UIClick);
                GameAudio.StopCombatSounds();
                check("Restart clears both result cues while preserving the menu click", ClipVoices(audio, SoundCue.Victory) == 0
                    && ClipVoices(audio, SoundCue.Defeat) == 0 && ClipVoices(audio, SoundCue.UIClick) == 1);
                ClearEffects(audio);

                string sceneA = GameAudio.MusicKeyForScene(originalScene) != null ? originalScene : "MainMenu";
                string sceneB = sceneA == "Level01_ForestOutpost" ? "Level02_CanyonConfluence" : "Level01_ForestOutpost";
                Call(audio, "ChangeMusic", sceneA);
                CompleteFade(audio);
                var clipA = audio.CurrentMusicClip;
                var sourceA = MusicSources(audio).Single(source => source.clip == clipA);
                sourceA.time = Mathf.Min(3f, clipA.length * .25f);
                int originalMusicPosition = sourceA.timeSamples;
                Call(audio, "ChangeMusic", sceneB);
                check("A scene transition plays at most two distinct music clips", MusicSources(audio).Count(source => source.clip) == 2
                    && MusicSources(audio).Select(source => source.clip).Distinct().Count() == 2);
                Call(audio, "ChangeMusic", sceneA);
                check("A rapid return reuses a single copy of the original track", audio.CurrentMusicKey == GameAudio.MusicKeyForScene(sceneA)
                    && MusicSources(audio).Count(source => source.clip == clipA) == 1 && sourceA.clip == clipA && sourceA.isPlaying);
                check("A rapid return keeps the original music playback position", originalMusicPosition > 0
                    && sourceA.timeSamples >= originalMusicPosition && sourceA.timeSamples - originalMusicPosition < clipA.frequency);
                CompleteFade(audio);
                check("A paused scene transition completes and releases the outgoing music", MusicSources(audio).Count(source => source.clip) == 1
                    && MusicSources(audio).Single(source => source.clip).clip == clipA && MusicSources(audio).Single(source => source.clip).isPlaying);
                check("All stress and transition checks retain the original source count", Sources(audio).Length == 26
                    && audio.MusicSourceCount == 2 && audio.ActiveVoiceCount == 0);
            }
            catch (Exception ex) { report.error = ex.ToString(); }
            finally
            {
                try
                {
                    if (audio)
                    {
                        ClearEffects(audio);
                        Call(audio, "ChangeMusic", originalScene);
                        CompleteFade(audio);
                    }
                    Time.timeScale = originalTimeScale;
                    GameSettings.SetVolume(originalVolume);
                    if (hadVolumePref) PlayerPrefs.SetFloat(VolumeKey, originalPref);
                    else PlayerPrefs.DeleteKey(VolumeKey);
                    PlayerPrefs.Save();
                    check("Validation restores time scale, volume and the original saved preference", Mathf.Approximately(Time.timeScale, originalTimeScale)
                        && Mathf.Approximately(GameSettings.MasterVolume, originalVolume) && Mathf.Approximately(AudioListener.volume, originalVolume)
                        && PlayerPrefs.HasKey(VolumeKey) == hadVolumePref
                        && (!hadVolumePref || PlayerPrefs.GetFloat(VolumeKey).Equals(originalPref)));
                    check("Validation leaves the correct scene music and no combat sounds", audio
                        && audio.CurrentMusicKey == GameAudio.MusicKeyForScene(originalScene) && audio.ActiveCombatVoiceCount == 0);
                }
                catch (Exception ex)
                {
                    report.error = (report.error ?? "") + "\nCleanup failed: " + ex;
                    // Restore user settings even if the audio fixture itself was damaged.
                    Time.timeScale = originalTimeScale;
                    GameSettings.SetVolume(originalVolume);
                    if (hadVolumePref) PlayerPrefs.SetFloat(VolumeKey, originalPref);
                    else PlayerPrefs.DeleteKey(VolumeKey);
                    PlayerPrefs.Save();
                }
            }
            return Save(report, checks);
        }

        static Report Save(Report report, List<Check> checks)
        {
            report.checks = checks.ToArray();
            report.failures = checks.Count(item => !item.passed);
            report.passed = report.failures == 0 && string.IsNullOrEmpty(report.error);
            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Captures", "Audio");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "runtime-level" + report.level + ".json"), JsonUtility.ToJson(report, true));
            return report;
        }
    }
}
