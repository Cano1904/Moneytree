using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RePlanet.EditorTools
{
    /// <summary>
    /// Build-Menü: Windows-Build (64 Bit) per Menü oder Kommandozeile, Spielstandordner öffnen.
    /// Kommandozeile (Windows, Pfade anpassen):
    /// <code>
    /// "C:\Program Files\Unity\Hub\Editor\&lt;Version&gt;\Editor\Unity.exe" -batchmode -quit -projectPath RePlanet
    ///     -executeMethod RePlanet.EditorTools.RePlanetBuild.BuildWindowsCI -logFile build.log [-rpOutput Builds/Windows/RePlanet.exe]
    /// </code>
    /// </summary>
    public static class RePlanetBuild
    {
        /// <summary>Zielpfad relativ zum Unity-Projektordner (RePlanet/).</summary>
        public const string DefaultOutput = "Builds/Windows/RePlanet.exe";
        const string Tag = "[RE:PLANET] ";

        public class Result
        {
            public bool Success;
            public string ExePath;
            public long SizeBytes;
            public int Errors, Warnings;
            public TimeSpan Duration;
            public string Message;
        }

        [MenuItem("RE:PLANET/Windows-Build erstellen (64 Bit)", false, 20)]
        static void BuildWindowsMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("RE:PLANET", "Bitte zuerst den Play-Modus beenden.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Result r;
            try { r = BuildWindows(DefaultOutput); }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("RE:PLANET – Build fehlgeschlagen", "Unerwarteter Fehler: " + e.Message, "OK");
                return;
            }
            if (r.Success)
            {
                bool open = EditorUtility.DisplayDialog("RE:PLANET – Build erfolgreich",
                    "Windows-Build erstellt:\n" + r.ExePath + "\n\nGröße: " + FormatSize(r.SizeBytes) + "\nDauer: " + FormatDuration(r.Duration) +
                    (r.Warnings > 0 ? "\nWarnungen: " + r.Warnings : "") +
                    "\n\nZum Weitergeben den kompletten Ordner „" + Path.GetFileName(Path.GetDirectoryName(r.ExePath)) + "“ verteilen (z. B. als ZIP) – nicht nur die .exe.",
                    "Ordner öffnen", "Schließen");
                if (open) EditorUtility.RevealInFinder(r.ExePath);
            }
            else
            {
                EditorUtility.DisplayDialog("RE:PLANET – Build fehlgeschlagen", r.Message ?? "Unbekannter Fehler (siehe Konsole).", "OK");
            }
        }

        /// <summary>Einstiegspunkt für die Kommandozeile (-batchmode -quit -executeMethod …). Beendet Unity bei Fehlschlag mit Code 1.</summary>
        public static void BuildWindowsCI()
        {
            string output = ArgValue("-rpOutput") ?? DefaultOutput;
            Result r;
            try { r = BuildWindows(output); }
            catch (Exception e)
            {
                Debug.LogError(Tag + "Build abgebrochen: " + e);
                EditorApplication.Exit(1);
                return;
            }
            if (!r.Success)
            {
                Debug.LogError(Tag + "Build fehlgeschlagen: " + r.Message);
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log(Tag + "Build erfolgreich: " + r.ExePath + " (" + FormatSize(r.SizeBytes) + ", " + FormatDuration(r.Duration) + ")");
        }

        /// <summary>Führt das Projekt-Setup aus und baut den Windows-Player (64 Bit).</summary>
        public static Result BuildWindows(string output)
        {
            var r = new Result();
            if (!RePlanetSetup.Run(false)) Debug.LogWarning(Tag + "Setup meldete Probleme – Build wird trotzdem versucht (Details oben im Log).");

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            {
                r.Message = "Windows-Build wird von dieser Unity-Installation nicht unterstützt.\n\nIm Unity Hub unter Installs › (Version) › Add modules das Modul „Windows Build Support (Mono)“ hinzufügen.";
                return r;
            }

            var scenes = new List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled && !string.IsNullOrEmpty(s.path)) scenes.Add(s.path);
            if (scenes.Count == 0)
            {
                r.Message = "Keine Szene in der Build-Liste. Bitte „RE:PLANET/Projekt einrichten“ ausführen.";
                return r;
            }

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string exe = Path.GetFullPath(Path.IsPathRooted(output) ? output : Path.Combine(projectRoot, output));
            if (!exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) exe = Path.Combine(exe, "RePlanet.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(exe));
            r.ExePath = exe;

            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            Debug.Log(Tag + "Starte Windows-Build (64 Bit) nach " + exe + " …");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            r.Success = summary.result == BuildResult.Succeeded;
            r.Errors = summary.totalErrors;
            r.Warnings = summary.totalWarnings;
            r.Duration = summary.totalTime;
            r.SizeBytes = r.Success ? FolderSize(Path.GetDirectoryName(exe)) : 0;
            if (r.SizeBytes == 0 && r.Success) r.SizeBytes = (long)summary.totalSize;

            if (!r.Success)
            {
                var sb = new StringBuilder();
                sb.Append("Ergebnis: ").Append(summary.result).Append(", Fehler: ").Append(summary.totalErrors).Append('\n');
                int shown = 0;
                foreach (var step in report.steps)
                    foreach (var msg in step.messages)
                    {
                        if (msg.type != LogType.Error && msg.type != LogType.Exception) continue;
                        if (shown++ >= 8) break;
                        sb.Append("• ").Append(Shorten(msg.content, 300)).Append('\n');
                    }
                if (shown == 0) sb.Append("Details stehen in der Konsole bzw. im Editor-Log.");
                r.Message = sb.ToString();
            }
            Debug.Log(Tag + "Build " + (r.Success ? "erfolgreich" : "fehlgeschlagen") + ": " + exe + " – Größe " + FormatSize(r.SizeBytes) +
                ", Dauer " + FormatDuration(r.Duration) + ", Fehler " + r.Errors + ", Warnungen " + r.Warnings + (r.Success ? "" : "\n" + r.Message));
            return r;
        }

        [MenuItem("RE:PLANET/Spielstandordner öffnen", false, 40)]
        static void OpenSaveFolder()
        {
            string dir = Application.persistentDataPath;
            try { Directory.CreateDirectory(dir); } catch (Exception) { }
            // Eine vorhandene Datei im Ordner anzeigen, damit sich der Ordner selbst öffnet
            string target = dir;
            foreach (var child in new[] { "saves", "settings.json", "profile.json" })
            {
                string p = Path.Combine(dir, child);
                if (Directory.Exists(p) || File.Exists(p)) { target = p; break; }
            }
            Debug.Log(Tag + "Spielstandordner (Editor): " + dir);
            EditorUtility.RevealInFinder(target);
        }

        // ------------------------------------------------------------------ Hilfen
        static string ArgValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        static long FolderSize(string dir)
        {
            long total = 0;
            try
            {
                foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    // Debug-Symbole, die Unity außerhalb des Spielordners ablegt, zählen nicht
                    if (f.Contains("_BurstDebugInformation_DoNotShip") || f.Contains("_BackUpThisFolder_ButDontShipItWithYourGame")) continue;
                    total += new FileInfo(f).Length;
                }
            }
            catch (Exception) { }
            return total;
        }

        static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "unbekannt";
            double mb = bytes / (1024.0 * 1024.0);
            return mb >= 1024 ? (mb / 1024.0).ToString("0.00") + " GB" : mb.ToString("0.0") + " MB";
        }

        static string FormatDuration(TimeSpan t)
        {
            return t.TotalMinutes >= 1 ? (int)t.TotalMinutes + " min " + t.Seconds + " s" : t.Seconds + " s";
        }

        static string Shorten(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max) + " …";
        }
    }
}
