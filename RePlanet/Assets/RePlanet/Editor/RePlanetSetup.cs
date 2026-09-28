using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RePlanet.EditorTools
{
    /// <summary>
    /// Richtet das Unity-Projekt für RE:PLANET ein. Idempotent – mehrfaches Ausführen schadet nicht.
    /// <list type="bullet">
    /// <item>Material-Vorlagen in Assets/RePlanet/Resources/ (damit die benötigten Shader-Varianten im Build landen;
    /// die Runtime lädt sie per Resources.Load und klont sie, siehe Runtime/Render/Mats.cs)</item>
    /// <item>Szene Assets/RePlanet/Scenes/Main.unity (leer – die Runtime erzeugt alles selbst) als Startszene im Build</item>
    /// <item>Grafik-Einstellungen (immer eingebundene Shader, Nebel-Varianten)</item>
    /// <item>Player-Einstellungen (Name, Linear-Farbraum, Fenster, Eingabesystem)</item>
    /// </list>
    /// Läuft automatisch einmal pro Editorstart und jederzeit über das Menü „RE:PLANET/Projekt einrichten“.
    /// Hinweis: Dieses Skript darf keine Typen aus RePlanet.Core/RePlanet.Runtime verwenden (separate Kompilierprüfung).
    /// </summary>
    [InitializeOnLoad]
    public static class RePlanetSetup
    {
        static string root;
        /// <summary>
        /// Projektordner von RE:PLANET (normalerweise „Assets/RePlanet“). Wird über die Lage dieser Editor-Assembly
        /// ermittelt, damit das Setup auch funktioniert, wenn der Ordner an eine andere Stelle kopiert wurde.
        /// </summary>
        public static string Root
        {
            get
            {
                if (root != null) return root;
                root = "Assets/RePlanet";
                try
                {
                    foreach (var guid in AssetDatabase.FindAssets("RePlanet.Editor t:AssemblyDefinitionAsset"))
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                        if (!path.EndsWith("/Editor/RePlanet.Editor.asmdef")) continue;
                        root = path.Substring(0, path.Length - "/Editor/RePlanet.Editor.asmdef".Length);
                        break;
                    }
                }
                catch (Exception) { }
                return root;
            }
        }
        public static string ResourcesDir { get { return Root + "/Resources"; } }
        public static string ScenesDir { get { return Root + "/Scenes"; } }
        public static string ScenePath { get { return ScenesDir + "/Main.unity"; } }
        public static string WaterNormalPath { get { return ResourcesDir + "/RP_WaterNormal.png"; } }
        public const string CompanyName = "RE PLANET Projekt";
        public const string ProductName = "RE PLANET";
        const string SessionKey = "RePlanet.SetupDone";
        const string Tag = "[RE:PLANET] ";

        /// <summary>
        /// Shader, die immer vollständig in den Build kommen. Bewusst NICHT enthalten: „Standard“ und
        /// „Particles/Standard Unlit“ – bei „Always Included“ würde Unity sämtliche Keyword-Kombinationen dieser
        /// Shader kompilieren (zehntausende Varianten, sehr lange Buildzeit). Ihre benötigten Varianten kommen über
        /// die Material-Vorlagen in Resources/ in den Build; Shader.Find findet sie dadurch ebenfalls.
        /// </summary>
        public static readonly string[] AlwaysIncludedShaders =
        {
            "Skybox/Procedural", "Unlit/Color", "Unlit/Texture", "Unlit/Transparent", "Sprites/Default", "RePlanet/Sky",
            // Eigene Darstellung (liegen ohnehin in Resources/; der Eintrag meldet im Setup-Protokoll, falls einer nicht kompiliert)
            "RePlanet/Water", "RePlanet/Terrain", "Hidden/RePlanet/PostFX", "RePlanet/Surface", "RePlanet/Window",
        };

        static double waitStart;

        static RePlanetSetup()
        {
            // Im Batchmodus (CI-Build) ruft RePlanetBuild.BuildWindowsCI das Setup selbst auf.
            if (Application.isBatchMode) return;
            if (SessionState.GetBool(SessionKey, false)) return;
            EditorApplication.delayCall += () =>
            {
                waitStart = EditorApplication.timeSinceStartup;
                EditorApplication.update -= WaitAndRun;
                EditorApplication.update += WaitAndRun;
            };
        }

        /// <summary>Wartet, bis Kompilieren/Import fertig und der Play-Modus aus ist, und führt dann das Setup aus.</summary>
        static void WaitAndRun()
        {
            bool busy = EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode;
            if (busy && EditorApplication.timeSinceStartup - waitStart < 300) return;
            EditorApplication.update -= WaitAndRun;
            if (busy) return; // nach 5 Minuten aufgeben; beim nächsten Start (oder per Menü) erneut
            SessionState.SetBool(SessionKey, true);
            try { Run(false); }
            catch (Exception e) { Debug.LogError(Tag + "Automatisches Setup fehlgeschlagen: " + e); }
        }

        [MenuItem("RE:PLANET/Projekt einrichten", false, 0)]
        static void RunFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("RE:PLANET", "Bitte zuerst den Play-Modus beenden.", "OK");
                return;
            }
            Run(true);
        }

        /// <summary>
        /// Führt alle Einrichtungsschritte aus. interactive = true: Rückfragen/Abschlussdialog erlaubt.
        /// Gibt false zurück, wenn ein Schritt fehlgeschlagen ist (Details im Log).
        /// </summary>
        public static bool Run(bool interactive)
        {
            var log = new List<string>();
            int errors = 0;
            Step("Render-Pipeline", () => EnsureBuiltinPipeline(log), log, ref errors);
            Step("Material-Vorlagen", () => EnsureMaterials(log), log, ref errors);
            Step("Szene und Build-Liste", () => EnsureScene(log, interactive), log, ref errors);
            Step("Grafik-Einstellungen", () => EnsureGraphicsSettings(log), log, ref errors);
            Step("Player-Einstellungen", () => EnsurePlayerSettings(log), log, ref errors);
            try { AssetDatabase.SaveAssets(); } catch (Exception e) { log.Add("FEHLER beim Speichern der Assets: " + e.Message); errors++; }

            string summary = log.Count == 0 ? "Alles war bereits eingerichtet." : string.Join("\n", log);
            if (errors > 0) Debug.LogWarning(Tag + "Projekt-Setup mit " + errors + " Problem(en):\n" + summary);
            else Debug.Log(Tag + "Projekt-Setup abgeschlossen.\n" + summary);
            if (interactive)
                EditorUtility.DisplayDialog("RE:PLANET – Projekt einrichten",
                    (errors > 0 ? "Setup mit Problemen abgeschlossen (Details in der Konsole):\n\n" : "Setup abgeschlossen:\n\n") + Shorten(summary, 1800), "OK");
            return errors == 0;
        }

        /// <summary>
        /// RE:PLANET nutzt die Built-in Render Pipeline (Standard-Shader, eigener Himmels-Shader). Wurde das Projekt aus einer
        /// URP-/HDRP-Vorlage erstellt, bleibt sonst alles pink. Die Pipeline-Zuweisung wird deshalb entfernt
        /// (Grafik-Einstellungen und jede Qualitätsstufe); das URP-Paket selbst bleibt installiert und stört nicht.
        /// </summary>
        static bool EnsureBuiltinPipeline(List<string> log)
        {
            int changed = 0;
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
                log.Add("Render-Pipeline: „" + GraphicsSettings.defaultRenderPipeline.name + "“ entfernt → Built-in Render Pipeline (für RE:PLANET nötig).");
                GraphicsSettings.defaultRenderPipeline = null;
                changed++;
            }
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                if (QualitySettings.GetRenderPipelineAssetAt(i) == null) continue;
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = null;
                changed++;
            }
            QualitySettings.SetQualityLevel(current, false);
            if (changed > 0)
            {
                log.Add("Render-Pipeline: Built-in aktiv (" + changed + " Zuweisung(en) entfernt). Falls Materialien noch pink sind, den Editor einmal neu starten.");
                AssetDatabase.SaveAssets();
            }
            return true;
        }

        static void Step(string name, Func<bool> action, List<string> log, ref int errors)
        {
            try
            {
                if (!action()) errors++;
            }
            catch (Exception e)
            {
                errors++;
                log.Add("FEHLER (" + name + "): " + e.Message);
                Debug.LogException(e);
            }
        }

        static string Shorten(string s, int max) { return s.Length <= max ? s : s.Substring(0, max) + " …"; }

        // ================================================================== Ordner
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static string ProjectPath(string assetPath)
        {
            return Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath);
        }

        // ================================================================== Materialien
        class Changes { public int Count; }

        class MatSpec
        {
            public string Name, Shader;
            public Action<Material, Changes> Apply;
            public MatSpec(string name, string shader, Action<Material, Changes> apply) { Name = name; Shader = shader; Apply = apply; }
        }

        static MatSpec[] Specs(Texture2D waterNormal)
        {
            return new[]
            {
                new MatSpec("RP_Opaque", "Standard", (m, c) => { F(m, "_Glossiness", 0.2f, c); F(m, "_Metallic", 0f, c); }),
                new MatSpec("RP_Metal", "Standard", (m, c) => { F(m, "_Glossiness", 0.55f, c); F(m, "_Metallic", 0.6f, c); }),
                new MatSpec("RP_Emissive", "Standard", (m, c) =>
                {
                    Kw(m, "_EMISSION", true, c);
                    Col(m, "_EmissionColor", Color.white, c);
                    GI(m, MaterialGlobalIlluminationFlags.RealtimeEmissive, c);
                }),
                new MatSpec("RP_Fade", "Standard", (m, c) => StandardTransparent(m, 2, c)),
                new MatSpec("RP_Water", "Standard", (m, c) =>
                {
                    StandardTransparent(m, 3, c);
                    F(m, "_Glossiness", 0.95f, c);
                    F(m, "_Metallic", 0f, c);
                    Col(m, "_Color", new Color(0.2f, 0.6f, 0.7f, 0.6f), c);
                    // Die Runtime (WorldView) setzt eine animierte Wellen-Normalmap und aktiviert _NORMALMAP.
                    // Damit diese Variante nicht weggestrippt wird, nutzt schon die Vorlage eine Normalmap.
                    Tex(m, "_BumpMap", waterNormal, c);
                    F(m, "_BumpScale", 0.5f, c);
                    Kw(m, "_NORMALMAP", true, c);
                }),
                new MatSpec("RP_Particle", "Particles/Standard Unlit", (m, c) => ParticleBlend(m, false, c)),
                new MatSpec("RP_ParticleAdd", "Particles/Standard Unlit", (m, c) => ParticleBlend(m, true, c)),
                new MatSpec("RP_Sky", "Skybox/Procedural", (m, c) => { }),
                new MatSpec("RP_Unlit", "Unlit/Color", (m, c) => { }),
                new MatSpec("RP_UnlitTex", "Unlit/Texture", (m, c) => { }),
                new MatSpec("RP_UnlitTransparent", "Unlit/Transparent", (m, c) => { }),
                new MatSpec("RP_Line", "Sprites/Default", (m, c) => { }),
            };
        }

        /// <summary>Standard-Shader im Modus Fade (2) oder Transparent (3) – wie StandardShaderGUI bzw. Mats.SetupTransparent.</summary>
        static void StandardTransparent(Material m, int mode, Changes c)
        {
            F(m, "_Mode", mode, c);
            TagOverride(m, "RenderType", "Transparent", c);
            F(m, "_SrcBlend", mode == 3 ? (float)BlendMode.One : (float)BlendMode.SrcAlpha, c);
            F(m, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha, c);
            F(m, "_ZWrite", 0f, c);
            Kw(m, "_ALPHATEST_ON", false, c);
            Kw(m, "_ALPHABLEND_ON", mode == 2, c);
            Kw(m, "_ALPHAPREMULTIPLY_ON", mode == 3, c);
            Queue(m, (int)RenderQueue.Transparent, c);
        }

        /// <summary>Particles/Standard Unlit: Fade (2) oder Additiv (4).</summary>
        static void ParticleBlend(Material m, bool additive, Changes c)
        {
            F(m, "_Mode", additive ? 4f : 2f, c);
            TagOverride(m, "RenderType", "Transparent", c);
            F(m, "_SrcBlend", (float)BlendMode.SrcAlpha, c);
            F(m, "_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha, c);
            F(m, "_ZWrite", 0f, c);
            Kw(m, "_ALPHATEST_ON", false, c);
            Kw(m, "_ALPHABLEND_ON", true, c);
            Kw(m, "_ALPHAPREMULTIPLY_ON", false, c);
            Kw(m, "_ALPHAMODULATE_ON", false, c);
            Queue(m, (int)RenderQueue.Transparent, c);
        }

        static void F(Material m, string prop, float v, Changes c)
        {
            if (!m.HasProperty(prop)) return;
            if (Mathf.Abs(m.GetFloat(prop) - v) < 1e-5f) return;
            m.SetFloat(prop, v);
            c.Count++;
        }

        static void Col(Material m, string prop, Color v, Changes c)
        {
            if (!m.HasProperty(prop)) return;
            var cur = m.GetColor(prop);
            if (Mathf.Abs(cur.r - v.r) < 1e-4f && Mathf.Abs(cur.g - v.g) < 1e-4f && Mathf.Abs(cur.b - v.b) < 1e-4f && Mathf.Abs(cur.a - v.a) < 1e-4f) return;
            m.SetColor(prop, v);
            c.Count++;
        }

        static void Tex(Material m, string prop, Texture t, Changes c)
        {
            if (t == null || !m.HasProperty(prop) || m.GetTexture(prop) == t) return;
            m.SetTexture(prop, t);
            c.Count++;
        }

        static void Kw(Material m, string keyword, bool on, Changes c)
        {
            if (m.IsKeywordEnabled(keyword) == on) return;
            if (on) m.EnableKeyword(keyword); else m.DisableKeyword(keyword);
            c.Count++;
        }

        static void TagOverride(Material m, string tag, string value, Changes c)
        {
            if (m.GetTag(tag, false, "") == value) return;
            m.SetOverrideTag(tag, value);
            c.Count++;
        }

        static void Queue(Material m, int queue, Changes c)
        {
            if (m.renderQueue == queue) return;
            m.renderQueue = queue;
            c.Count++;
        }

        static void GI(Material m, MaterialGlobalIlluminationFlags flags, Changes c)
        {
            if (m.globalIlluminationFlags == flags) return;
            m.globalIlluminationFlags = flags;
            c.Count++;
        }

        static bool EnsureMaterials(List<string> log)
        {
            EnsureFolder(ResourcesDir);
            bool ok = true;
            Texture2D waterNormal = null;
            try { waterNormal = EnsureWaterNormal(log); }
            catch (Exception e) { log.Add("FEHLER Wasser-Normalmap: " + e.Message); ok = false; }

            int created = 0, updated = 0;
            foreach (var spec in Specs(waterNormal))
            {
                try
                {
                    string path = ResourcesDir + "/" + spec.Name + ".mat";
                    var shader = Shader.Find(spec.Shader);
                    if (shader == null)
                    {
                        log.Add("FEHLER: Shader „" + spec.Shader + "“ nicht gefunden – " + spec.Name + " wurde nicht angelegt.");
                        ok = false;
                        continue;
                    }
                    var c = new Changes();
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    bool isNew = mat == null;
                    if (isNew) mat = new Material(shader) { name = spec.Name };
                    else if (mat.shader != shader) { mat.shader = shader; c.Count++; }
                    spec.Apply(mat, c);
                    // Instancing auf allen Vorlagen: bei „Strip Unused“ bleiben Instancing-Varianten nur erhalten,
                    // wenn ein Material im Build Instancing mit derselben Keyword-Kombination nutzt
                    // (TrashRenderer zeichnet u. a. Ölteppiche mit dem Wasser-Material per DrawMeshInstanced).
                    if (!mat.enableInstancing) { mat.enableInstancing = true; c.Count++; }
                    if (isNew)
                    {
                        AssetDatabase.CreateAsset(mat, path);
                        created++;
                        log.Add("Material angelegt: " + path);
                    }
                    else if (c.Count > 0)
                    {
                        EditorUtility.SetDirty(mat);
                        updated++;
                        log.Add("Material aktualisiert (" + c.Count + " Werte): " + path);
                    }
                }
                catch (Exception e)
                {
                    log.Add("FEHLER bei " + spec.Name + ": " + e.Message);
                    ok = false;
                }
            }
            if (created > 0 || updated > 0) AssetDatabase.SaveAssets();
            return ok;
        }

        /// <summary>Kleine, kachelbare Normalmap (64×64) für die Wasser-Vorlage.</summary>
        static Texture2D EnsureWaterNormal(List<string> log)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(WaterNormalPath);
            if (tex == null)
            {
                const int N = 64;
                var h = new float[N * N];
                // Überlagerte Sinuswellen mit ganzzahligen Frequenzen → nahtlos kachelbar
                var waves = new[] { new Vector4(1, 2, 0.0f, 1f), new Vector4(3, -1, 1.3f, 0.6f), new Vector4(-2, 5, 2.1f, 0.4f), new Vector4(6, 3, 0.7f, 0.25f), new Vector4(-5, -7, 3.3f, 0.15f) };
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float v = 0;
                        foreach (var w in waves) v += w.w * Mathf.Sin(2f * Mathf.PI * (w.x * x + w.y * y) / N + w.z);
                        h[y * N + x] = v;
                    }
                var px = new Color32[N * N];
                const float strength = 0.35f;
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float dx = h[y * N + (x + 1) % N] - h[y * N + (x + N - 1) % N];
                        float dy = h[((y + 1) % N) * N + x] - h[((y + N - 1) % N) * N + x];
                        var n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                        px[y * N + x] = new Color32((byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f), (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f), (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 255);
                    }
                var t = new Texture2D(N, N, TextureFormat.RGBA32, false, true);
                t.SetPixels32(px);
                t.Apply();
                File.WriteAllBytes(ProjectPath(WaterNormalPath), t.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(WaterNormalPath, ImportAssetOptions.ForceUpdate);
                log.Add("Normalmap angelegt: " + WaterNormalPath);
            }
            var imp = AssetImporter.GetAtPath(WaterNormalPath) as TextureImporter;
            if (imp != null && (imp.textureType != TextureImporterType.NormalMap || imp.wrapMode != TextureWrapMode.Repeat || imp.mipmapEnabled != true))
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.mipmapEnabled = true;
                imp.SaveAndReimport();
                log.Add("Normalmap als „Normal map“ importiert.");
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(WaterNormalPath);
        }

        // ================================================================== Szene + Build-Liste
        static bool EnsureScene(List<string> log, bool interactive)
        {
            EnsureFolder(ScenesDir);
            bool ok = true;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) ok = CreateMainScene(log, interactive);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return false;

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int idx = scenes.FindIndex(s => s.path == ScenePath);
            bool changed = false;
            if (idx < 0)
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                changed = true;
                log.Add(scenes.Count == 1 ? "Main.unity als Startszene in die Build-Liste eingetragen." : "Main.unity als erste Szene in die Build-Liste eingetragen (weitere Szenen bleiben erhalten).");
            }
            else
            {
                var s = scenes[idx];
                if (!s.enabled) { s.enabled = true; changed = true; log.Add("Main.unity in der Build-Liste aktiviert."); }
                if (idx != 0) { scenes.RemoveAt(idx); scenes.Insert(0, s); changed = true; log.Add("Main.unity an die erste Stelle der Build-Liste verschoben."); }
            }
            if (changed) EditorBuildSettings.scenes = scenes.ToArray();
            return ok;
        }

        static bool CreateMainScene(List<string> log, bool interactive)
        {
            // Offene Szenen mit ungespeicherten Änderungen niemals ungefragt schließen
            bool dirty = false;
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) dirty = true;
            if (dirty)
            {
                if (!interactive)
                {
                    log.Add("Hinweis: Main.unity wurde noch nicht angelegt, weil die offene Szene ungespeicherte Änderungen hat. Bitte speichern und „RE:PLANET/Projekt einrichten“ ausführen.");
                    return false;
                }
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    log.Add("Hinweis: Anlegen von Main.unity abgebrochen.");
                    return false;
                }
            }
            var previous = EditorSceneManager.GetSceneManagerSetup();
            bool restore = previous.Length > 0;
            foreach (var p in previous) if (string.IsNullOrEmpty(p.path)) restore = false;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Nebel (Exp²) wie zur Laufzeit – so behält auch die automatische Nebel-Variantenauswahl die Nebel-Shader.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.01f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                log.Add("FEHLER: Szene konnte nicht gespeichert werden: " + ScenePath);
                return false;
            }
            log.Add("Leere Startszene angelegt: " + ScenePath + " (Kamera, Licht, Welt und Oberfläche erzeugt das Spiel zur Laufzeit).");
            if (restore)
            {
                try { EditorSceneManager.RestoreSceneManagerSetup(previous); }
                catch (Exception e) { log.Add("Hinweis: Vorherige Szene konnte nicht wieder geöffnet werden: " + e.Message); }
            }
            return true;
        }

        // ================================================================== Grafik
        static bool EnsureGraphicsSettings(List<string> log)
        {
            UnityEngine.Object gs = GraphicsSettings.GetGraphicsSettings();
            if (gs == null)
            {
                var all = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
                if (all != null && all.Length > 0) gs = all[0];
            }
            if (gs == null) { log.Add("FEHLER: GraphicsSettings nicht gefunden."); return false; }

            bool ok = true;
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null || !arr.isArray)
            {
                log.Add("FEHLER: Eigenschaft m_AlwaysIncludedShaders nicht gefunden.");
                ok = false;
            }
            else
            {
                foreach (var name in AlwaysIncludedShaders)
                {
                    var sh = Shader.Find(name);
                    if (sh == null) { log.Add("Warnung: Shader „" + name + "“ nicht gefunden – nicht in „Always Included Shaders“ eingetragen."); ok = false; continue; }
                    bool present = false;
                    for (int i = 0; i < arr.arraySize; i++)
                        if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) { present = true; break; }
                    if (present) continue;
                    arr.InsertArrayElementAtIndex(arr.arraySize);
                    arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
                    log.Add("„Always Included Shaders“ ergänzt: " + name);
                }
            }
            // Nebel wird erst zur Laufzeit eingeschaltet → Nebel-Varianten ausdrücklich behalten (Custom statt Automatic)
            int fog = 0;
            fog += SetInt(so, "m_FogStripping", 1);
            fog += SetBool(so, "m_FogKeepLinear", true);
            fog += SetBool(so, "m_FogKeepExp", true);
            fog += SetBool(so, "m_FogKeepExp2", true);
            if (fog > 0) log.Add("Nebel-Shadervarianten werden im Build behalten (Graphics › Fog Modes: Custom).");
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return ok;
        }

        static int SetInt(SerializedObject so, string prop, int value)
        {
            var p = so.FindProperty(prop);
            if (p == null) return 0;
            if (p.propertyType == SerializedPropertyType.Enum) { if (p.enumValueIndex == value) return 0; p.enumValueIndex = value; return 1; }
            if (p.intValue == value) return 0;
            p.intValue = value;
            return 1;
        }

        static int SetBool(SerializedObject so, string prop, bool value)
        {
            var p = so.FindProperty(prop);
            if (p == null) return 0;
            if (p.propertyType == SerializedPropertyType.Boolean)
            {
                if (p.boolValue == value) return 0;
                p.boolValue = value;
                return 1;
            }
            int iv = value ? 1 : 0;
            if (p.intValue == iv) return 0;
            p.intValue = iv;
            return 1;
        }

        // ================================================================== Player
        static bool EnsurePlayerSettings(List<string> log)
        {
            // Namen nur setzen, solange noch Unitys Vorgaben drinstehen (eigene Änderungen bleiben erhalten).
            // Sie bestimmen auch den Speicherort (Application.persistentDataPath).
            if (string.IsNullOrEmpty(PlayerSettings.companyName) || PlayerSettings.companyName == "DefaultCompany")
            {
                PlayerSettings.companyName = CompanyName;
                log.Add("Firmenname gesetzt: " + CompanyName);
            }
            string folder = Path.GetFileName(Path.GetDirectoryName(Application.dataPath));
            if (string.IsNullOrEmpty(PlayerSettings.productName) || PlayerSettings.productName == folder || PlayerSettings.productName == "New Unity Project")
            {
                PlayerSettings.productName = ProductName;
                log.Add("Produktname gesetzt: " + ProductName);
            }
            if (!PlayerSettings.runInBackground) { PlayerSettings.runInBackground = true; log.Add("„Run In Background“ aktiviert (Koop läuft weiter, wenn das Fenster im Hintergrund ist)."); }
            if (PlayerSettings.colorSpace != ColorSpace.Linear) { PlayerSettings.colorSpace = ColorSpace.Linear; log.Add("Farbraum auf Linear gestellt."); }
            if (PlayerSettings.fullScreenMode != FullScreenMode.FullScreenWindow) { PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow; log.Add("Standard-Anzeigemodus: Vollbild (randloses Fenster)."); }
            if (!PlayerSettings.defaultIsNativeResolution) { PlayerSettings.defaultIsNativeResolution = true; log.Add("Standardauflösung: native Bildschirmauflösung."); }
            if (!PlayerSettings.resizableWindow) { PlayerSettings.resizableWindow = true; log.Add("Fenstergröße veränderbar."); }

            // Eingabe: Das Spiel nutzt den klassischen Input Manager (UnityEngine.Input).
            try
            {
                var all = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
                if (all != null && all.Length > 0 && all[0] != null)
                {
                    var so = new SerializedObject(all[0]);
                    var p = so.FindProperty("activeInputHandler");
                    if (p != null && p.intValue == 1)
                    {
                        p.intValue = 0; // 0 = Input Manager (alt), 1 = Input System (neu), 2 = beide
                        so.ApplyModifiedPropertiesWithoutUndo();
                        log.Add("Eingabesystem auf „Input Manager (Old)“ gestellt. Hinweis: Unity übernimmt das ggf. erst nach einem Editor-Neustart.");
                        Debug.LogWarning(Tag + "Active Input Handling wurde auf den klassischen Input Manager umgestellt. Bitte den Editor neu starten, falls Unity dazu auffordert.");
                    }
                }
            }
            catch (Exception e)
            {
                log.Add("Hinweis: Eingabesystem konnte nicht geprüft werden (" + e.Message + "). Bitte unter Project Settings › Player › Active Input Handling „Input Manager (Old)“ oder „Both“ wählen.");
            }
            return true;
        }
    }
}
