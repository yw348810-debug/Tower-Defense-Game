using System;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.EditorTools
{
    /// <summary>Portable audio import defaults; streamed loops avoid resident PCM buffers.</summary>
    public sealed class GameAudioImport : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Game/Resources/Audio/", StringComparison.Ordinal)) return;
            Configure((AudioImporter)assetImporter, assetPath);
        }

        static void Configure(AudioImporter importer, string path)
        {
            bool music = System.IO.Path.GetFileName(path).StartsWith("Music_", StringComparison.Ordinal);
            importer.forceToMono = !music;
            importer.loadInBackground = music;
            importer.ambisonic = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            settings.quality = .85f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = !music;
            importer.defaultSampleSettings = settings;
        }

        [MenuItem("Tools/Tower Defense/Configure Audio Imports")]
        public static void Build()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Game/Resources/Audio" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (!importer) continue;
                Configure(importer, path);
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
        }
    }
}
