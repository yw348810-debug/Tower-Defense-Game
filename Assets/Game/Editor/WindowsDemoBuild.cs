using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TowerDefense.EditorTools
{
    /// <summary>Builds the four playable scenes as a Windows x64 demo.</summary>
    public static class WindowsDemoBuild
    {
        static readonly string[] Scenes =
        {
            "Assets/Game/Scenes/MainMenu.unity",
            "Assets/Game/Scenes/Level01_ForestOutpost.unity",
            "Assets/Game/Scenes/Level02_CanyonConfluence.unity",
            "Assets/Game/Scenes/Level03_CoreFortress.unity"
        };

        [Serializable]
        sealed class BuildSummary
        {
            public string result;
            public string unityVersion;
            public string outputPath;
            public string[] scenes;
            public string totalBytes;
            public double seconds;
            public int errors;
            public int warnings;
            public bool development;
        }

        [MenuItem("Tools/Tower Defense/Build Windows Demo")]
        public static void Build()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string buildsRoot = Path.GetFullPath(Path.Combine(projectRoot, "Builds"));
            string destination = Path.Combine(buildsRoot, "WindowsDemo");
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < arguments.Length; i++)
                if (arguments[i] == "-demoBuildOutput") destination = arguments[i + 1];
            destination = Path.GetFullPath(destination);
            if (!destination.StartsWith(buildsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("The demo output must be a folder inside this project's Builds directory.");
            foreach (string scene in Scenes)
                if (!File.Exists(Path.Combine(projectRoot, scene)))
                    throw new BuildFailedException("Missing playable scene: " + scene);
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new BuildFailedException("Windows x64 build support is not installed for this Unity Editor.");

            Directory.CreateDirectory(destination);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = Path.Combine(destination, "TowerDefense.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CompressWithLz4HC | BuildOptions.StrictMode
            });
            var summary = new BuildSummary
            {
                result = report.summary.result.ToString(),
                unityVersion = Application.unityVersion,
                outputPath = report.summary.outputPath,
                scenes = Scenes,
                totalBytes = report.summary.totalSize.ToString(),
                seconds = report.summary.totalTime.TotalSeconds,
                errors = report.summary.totalErrors,
                warnings = report.summary.totalWarnings,
                development = false
            };
            string reportsDirectory = Path.Combine(projectRoot, "Logs", "WindowsDemo");
            Directory.CreateDirectory(reportsDirectory);
            File.WriteAllText(Path.Combine(reportsDirectory, "build-summary.json"), JsonUtility.ToJson(summary, true));
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Windows demo build failed. See Logs/WindowsDemo/build-summary.json.");
            Debug.Log("WINDOWS_DEMO_BUILD_SUCCEEDED: " + report.summary.outputPath);
        }
    }
}
