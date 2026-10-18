using System;
using System.IO;
using System.Linq;
using TowerDefense.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TowerDefense.EditorTools
{
    public static class BattlePresentationSetup
    {
        [MenuItem("Tools/Tower Defense/Polish Camera Presentation")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scene edits first.");
            var original = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (var name in new[] { "MainMenu", "Level01_ForestOutpost", "Level02_CanyonConfluence", "Level03_CoreFortress" })
                {
                    var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/" + name + ".unity", OpenSceneMode.Single);
                    Configure(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(original); }
        }

        public static void Configure(Scene scene)
        {
            var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).First(c => c.CompareTag("MainCamera"));
            var rig = camera.GetComponent<MapCameraRig>();
            if (rig)
            {
                // Reserve the real HUD footprint so the spell column cannot cover the core or pads.
                float width = scene.name.StartsWith("Level03", StringComparison.Ordinal) ? .625f : .735f;
                rig.gameplayViewport = new Rect(.025f, .195f, width, .565f);
                rig.alignToViewportCenter = true;
                rig.ApplyFraming();
            }
            var data = camera.GetUniversalAdditionalCameraData();
            camera.allowHDR = true;
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            string folder = "Assets/Game/Presentation";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            string path = folder + "/" + scene.name + "_Color.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (!profile) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }
            if (!profile.TryGet<Bloom>(out var bloom)) bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1.05f); bloom.intensity.Override(.22f); bloom.scatter.Override(.55f);
            if (!profile.TryGet<ColorAdjustments>(out var color)) color = profile.Add<ColorAdjustments>();
            color.contrast.Override(8f); color.saturation.Override(-4f); color.postExposure.Override(0f);
            if (!profile.TryGet<Vignette>(out var vignette)) vignette = profile.Add<Vignette>();
            vignette.intensity.Override(.10f); vignette.smoothness.Override(.72f);
            foreach (var component in profile.components)
            {
                if (!AssetDatabase.Contains(component)) AssetDatabase.AddObjectToAsset(component, profile);
                EditorUtility.SetDirty(component);
            }
            EditorUtility.SetDirty(profile);
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Presentation Volume");
            if (!root) { root = new GameObject("Presentation Volume"); SceneManager.MoveGameObjectToScene(root, scene); }
            var volume = root.GetComponent<Volume>(); if (!volume) volume = root.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 20; volume.sharedProfile = profile;
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
