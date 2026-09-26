using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Glasscore.EditorTools
{
    /// <summary>
    /// One-click / unattended builds. Used by scripts/build_and_shutdown.ps1:
    ///   Unity.exe -batchmode -quit -projectPath Glasscore -executeMethod Glasscore.EditorTools.GlasscoreBuild.BuildWindowsCli
    /// Creates the single bootstrap scene, configures player settings and builds Build/Windows/GLASSCORE.exe.
    /// </summary>
    public static class GlasscoreBuild
    {
        private const string ScenePath = "Assets/Scenes/Glasscore.unity";

        [MenuItem("GLASSCORE/Setup Project")]
        public static void Setup()
        {
            EnsureScene();
            ConfigurePlayer();
            Debug.Log("[GLASSCORE] Project configured. Press Play in Assets/Scenes/Glasscore.unity.");
        }

        [MenuItem("GLASSCORE/Build Windows (x64)")]
        public static void BuildWindowsMenu() => BuildWindows(DefaultOutput());

        /// <summary>Batch-mode entry point: exits with 0 on success, 1 on failure.</summary>
        public static void BuildWindowsCli()
        {
            string output = DefaultOutput();
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-glasscoreOutput") output = args[i + 1];

            bool ok;
            try { ok = BuildWindows(output); }
            catch (Exception ex)
            {
                Debug.LogError("[GLASSCORE] Build crashed: " + ex);
                ok = false;
            }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static string DefaultOutput()
        {
            string root = Directory.GetParent(Application.dataPath).Parent.FullName;
            return Path.Combine(root, "Build", "Windows", "GLASSCORE.exe");
        }

        public static bool BuildWindows(string outputExe)
        {
            EnsureScene();
            ConfigurePlayer();
            Directory.CreateDirectory(Path.GetDirectoryName(outputExe));

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputExe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"[GLASSCORE] Build {summary.result}: {summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors, {summary.totalTime}");
            return summary.result == BuildResult.Succeeded;
        }

        private static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                // Empty scene: GameBootstrap builds camera, lights, UI and audio at runtime.
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Glasscore";
            PlayerSettings.productName = "GLASSCORE";
            PlayerSettings.runInBackground = true;              // multiplayer: keep simulating when unfocused
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.forceSingleInstance = false;         // allow two clients on one PC for testing
            PlayerSettings.colorSpace = ColorSpace.Linear;
            QualitySettings.shadows = ShadowQuality.Disable;
            EnsureLegacyInputEnabled();
        }

        /// <summary>
        /// GLASSCORE reads input through the classic Input Manager (no package dependency). New Unity 6
        /// projects may default to "Input System Package (New)" only, which disables it — switch to "Both".
        /// </summary>
        private static void EnsureLegacyInputEnabled()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            SerializedProperty handler = so.FindProperty("activeInputHandler");
            if (handler == null || handler.intValue != 1) return; // 0 = old, 1 = new only, 2 = both
            handler.intValue = 2;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("[GLASSCORE] Active Input Handling set to 'Both' (restart the editor if you use Play mode).");
        }
    }
}
