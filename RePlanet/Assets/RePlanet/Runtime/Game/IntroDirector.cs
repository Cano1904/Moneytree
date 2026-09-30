using System;
using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RePlanet
{
    /// <summary>
    /// Intro als Echtzeit-Zwischensequenz (~100 s), synchron zum Intro-Score und zur Erzählerstimme (<see cref="Narrator"/>).
    /// Die Handlung folgt der Grundidee von WALL·E – vermüllte Erde, ein Konsumkonzern, die Menschen fliehen auf Archen,
    /// die Roboter geben auf, nur einer arbeitet weiter und findet einen Keimling – mit eigenen Figuren, Namen und Bildern.
    /// <para>Die Bühne entsteht vollständig zur Laufzeit (Origin 3000/0/3000, je Einstellung ein eigener Satz):</para>
    /// <list type="bullet">
    /// <item>Skyline: Türme aus hunderten gepressten Müllwürfeln, Hochhausruinen mit Fensterreihen, KONSUMA-Tafeln,
    /// Müllhalden bis zum Horizont, Staub und Lichtbahnen im goldenen Gegenlicht.</item>
    /// <item>Megastore: riesige Leuchtreklame, Rolltreppen, Einkaufswagen-Berge, Menschenmassen (GPU-Instancing).</item>
    /// <item>Archen: große Raumschiffe mit Triebwerksflammen, Rauchsäulen und Staubwolken beim Start.</item>
    /// <item>Abschalten: Reihen kleiner Roboter, deren Augen nacheinander – auf den Klaviertönen des Scores – erlöschen.</item>
    /// <item>MIKOs Zuhause: Lieferwagen innen mit Lichterkette, Regalen und Sammelstücken; danach Würfel für Würfel.</item>
    /// <item>Keimling: Makro-Nahaufnahme im Lichtstrahl mit schwebendem Staub und Unschärfe-Lichtern.</item>
    /// <item>Aufbruch: MIKOs Schiff vor dem Planeten, Sonnenaufgang über dem Horizont, Titel.</item>
    /// </list>
    /// Kamera, Nebel, Licht und Himmelssonne werden nach Atmosphere und CameraRig gesetzt (spätere Ausführungsreihenfolge)
    /// und am Ende zurückgegeben. Robust: Fehler beenden höchstens das Intro, Überspringen funktioniert immer.
    /// </summary>
    [DefaultExecutionOrder(900)]
    public class IntroDirector : MonoBehaviour
    {
        static readonly Vector3 Origin = new Vector3(3000, 0, 3000);

        // ================================================================== Zustand
        Transform stage;
        Action done;
        bool playing, freeRun;
        /// <summary>Bühne wird im nächsten Bild aufgebaut (bis dahin schwarzes Bild mit Hinweis).</summary>
        bool pendingBuild, blackShown;
        int waitFrames;
        /// <summary>Musik lief schon (endet sie, läuft das Bild frei weiter statt 10 s zu warten).</summary>
        bool musicSeen;
        /// <summary>Überspringen erst, nachdem alle Überspringen-Eingaben seit dem Start einmal losgelassen waren.</summary>
        bool skipArmed;
        int stepErrors;
        readonly HashSet<string> reported = new HashSet<string>();
        /// <summary>Einstellungen, deren Animation einmal fehlschlug – nur diese bleiben danach stehen, die übrigen laufen weiter.</summary>
        readonly HashSet<string> animFailed = new HashSet<string>();
        float t, skipHold, startDelay, runTime;
        string activeShot;
        float detail = 1f;
        readonly Dictionary<string, Transform> shots = new Dictionary<string, Transform>();
        readonly Dictionary<string, List<ParticleSystem>> shotFx = new Dictionary<string, List<ParticleSystem>>();
        readonly List<GlowSet> glows = new List<GlowSet>();
        readonly List<Object> owned = new List<Object>();
        readonly HashSet<string> fired = new HashSet<string>();

        // gesicherter Darstellungszustand
        CameraClearFlags oldClear;
        Color oldBg;
        bool oldFog, savedCam;
        FogMode oldFogMode;
        float oldNear = 0.15f, oldFar = 800f;

        // Kamera der aktuellen Einstellung (Koordinaten im Raum camSpace)
        Transform camSpace;
        Vector3 camPos, camLook;
        float camFov = 50f, camRoll, camHand = 0.2f, camKick;

        struct Look
        {
            public float Phase, Storm, Fog, SunIntensity, SunSize, Far, Near, CloudCover, SkySun;
            public Color FogColor, AmbSky, AmbEquator, AmbGround, SunColor;
            public Vector3 SunDir;
            public bool Space;
        }
        Look look;

        // Texturen und Materialien (gehören dem Intro, werden am Ende zerstört)
        Texture2D texBale, texFacade, texFacadeLit, texGround, texGrime, texPanel, texSoft, texPuff, texRing, texPlanet, texClouds, texVignette, texGrain;
        Material matBale, matBale2, matFacade, matFacade2, matFacadeLit, matGround, matGroundDark, matConcrete, matRust, matHull, matShipHull;
        Material matSoftAdd, matSoftAlpha, matPuff, matPuffAdd, matRing, matPlanet, matClouds;

        void Start()
        {
            if (GameApp.I != null) GameApp.I.OnIntroRequested += Play;
        }

        void OnDestroy()
        {
            if (GameApp.I != null) GameApp.I.OnIntroRequested -= Play;
            Cleanup();
        }

        public void Play(Action onDone)
        {
            // Läuft noch ein Intro (doppelter Aufruf), wird es ersetzt – ohne den alten Rückruf auszulösen.
            if (playing) { done = null; Cleanup(); }
            done = onDone;
            t = 0; skipHold = 0; startDelay = 0; runTime = 0; freeRun = false; musicSeen = false; skipArmed = false; stepErrors = 0; animFailed.Clear();
            reported.Clear();
            activeShot = null;
            fired.Clear();
            var cam = Camera.main;
            if (cam != null) { oldClear = cam.clearFlags; oldBg = cam.backgroundColor; oldNear = cam.nearClipPlane; oldFar = cam.farClipPlane; savedCam = true; }
            oldFog = RenderSettings.fog;
            oldFogMode = RenderSettings.fogMode;
            // Der Bühnenaufbau dauert einige Sekunden. Er läuft deshalb nicht im Klick (OnGUI), sondern im nächsten Bild,
            // nachdem ein schwarzes Bild mit Hinweis gezeigt wurde – sonst stünde das Menü eingefroren da und ein ungeduldiger
            // zweiter Klick würde nach dem Einfrieren als „gehalten“ gelten und das Intro sofort überspringen.
            pendingBuild = true;
            blackShown = false;
            playing = true;
            if (CameraRig.I != null) CameraRig.I.Cinematic = true;
        }

        /// <summary>Baut die Bühne und startet Musik und Erzähler (einmal, im Bild nach <see cref="Play"/>).</summary>
        void BuildAndStart()
        {
            pendingBuild = false;
            try { Build(); }
            catch (Exception e) { LogError("Bühnenbau", e); }
            try { Narrator.Begin(Narrator.IntroCues()); }
            catch (Exception e) { LogError("Erzähler", e); }
            try { AudioManager.PlayIntro(); }
            catch (Exception e) { LogError("Musik", e); }
            // Uhren ab jetzt: Das Bild nach dem Aufbau hat eine lange Bildzeit (wird unten begrenzt); Eingaben, die während
            // des Aufbaus gedrückt wurden, zählen erst nach dem Loslassen.
            t = 0; skipHold = 0; startDelay = 0; runTime = 0; skipArmed = false;
        }

        void Finish()
        {
            if (!playing) return;
            playing = false;
            pendingBuild = false;
            AudioManager.StopIntro();
            Narrator.Stop(0.35f);
            AudioManager.Loop("intro_wind", "wind_loop", false);
            var cam = Camera.main;
            if (cam != null && savedCam) { cam.clearFlags = oldClear; cam.backgroundColor = oldBg; cam.nearClipPlane = oldNear; cam.farClipPlane = oldFar; }
            RenderSettings.fog = oldFog;
            RenderSettings.fogMode = oldFogMode;
            if (CameraRig.I != null) CameraRig.I.Cinematic = false;
            if (Atmosphere.I != null) { Atmosphere.I.ForcePhase = -1f; Atmosphere.I.ForcePlanet = null; Atmosphere.I.ForceStorm = -1f; }
            Cleanup();
            Hud.Subtitle = null;
            var d = done; done = null;
            try { d?.Invoke(); }
            catch (Exception e) { LogError("Abschluss", e); }
        }

        void Cleanup()
        {
            if (stage != null) Destroy(stage.gameObject);
            stage = null;
            foreach (var o in owned) if (o != null) Destroy(o);
            owned.Clear();
            shots.Clear(); shotFx.Clear(); glows.Clear();
            Geo.ClearCache();
            crowd = null; arks.Clear(); leftBehind.Clear(); bots.Clear(); fairy.Clear(); bokeh.Clear(); shelfGlow.Clear();
            neonLetters.Clear(); homeShafts.Clear(); beamShafts.Clear(); skyShafts.Clear(); shipLights.Clear();
            mikoHome = null; mikoNear = null; mikoShip = null; macro = null; sprout = null; leafL = leafR = null; ship = null; planet = clouds = null;
            doorL = doorR = pressCube = null; doorLight = null; tumbleBag = null; beam = null; neonLight = null; signWash = sproutHalo = ring = limbGlow = trailGlow = null;
        }

        // ================================================================== Ablauf
        /// <summary>Bildzeit, begrenzt: Das Bild nach dem Bühnenaufbau (oder nach einem Ruckler) zählt höchstens 0,1 s.</summary>
        static float Dt { get { return Mathf.Min(Time.unscaledDeltaTime, 0.1f); } }

        static bool SkipInput()
        {
            return Input.GetKey(KeyCode.Escape) || Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.Space)
                || Input.GetKey(KeyCode.JoystickButton0) || Input.GetMouseButton(0);
        }

        void Update()
        {
            if (!playing || pendingBuild) return;
            // Überspringen: Esc/Eingabe/Leertaste/A/Maus 1 s gedrückt halten – erst nachdem alle diese Eingaben seit dem Start
            // einmal losgelassen waren (der Klick auf „Los geht's!“ oder ein Klick während des Aufbaus zählt nicht) und
            // frühestens nach 1 s Laufzeit.
            bool hold = SkipInput();
            if (!hold) skipArmed = true;
            float dt = Dt;
            runTime += dt;
            skipHold = hold && skipArmed ? skipHold + dt : 0f;
            if (skipHold > 1.0f && runTime > 1.0f) Finish();
        }

        void LateUpdate()
        {
            if (!playing) return;
            if (pendingBuild)
            {
                // erst aufbauen, wenn das schwarze Bild mit Hinweis zu sehen war (bzw. spätestens nach einigen Bildern)
                if (blackShown || ++waitFrames > 3) { waitFrames = 0; BuildAndStart(); }
                return;
            }
            try { Step(); stepErrors = 0; }
            catch (Exception e)
            {
                // Einzelne Fehler beenden das Intro nicht (sonst stünde man ohne Intro in der Planetenwahl);
                // erst wenn der Ablauf dauerhaft scheitert (≈ 2 s lang jedes Bild), wird abgebrochen.
                Report("Ablauf", e);
                if (++stepErrors > 60) Finish();
            }
        }

        /// <summary>Fehler einmal je Stelle melden (nicht jedes Bild).</summary>
        void Report(string what, Exception e)
        {
            if (reported.Add(what + ":" + e.GetType().Name + ":" + e.Message)) LogError(what, e);
        }

        void Step()
        {
            // Warten, bis der Score läuft (max. 10 s), dann synchron zur Musik. Kommt die Musik nicht rechtzeitig,
            // läuft die Sequenz ohne sie weiter – ein verspäteter Start würde sonst zeitversetzt spielen.
            // Endet die Musik (Clip zu Ende, Audio gestoppt), läuft das Bild ab der letzten Musikzeit frei weiter.
            float dt = Dt;
            double at = freeRun ? -1 : AudioManager.IntroTime;
            if (at >= 0) { t = (float)at; musicSeen = true; }
            else
            {
                if (!freeRun && musicSeen) freeRun = true;
                startDelay += dt;
                if (!freeRun && startDelay > 10f) { freeRun = true; AudioManager.StopIntro(); }
                if (freeRun) t += dt;
            }
            if (t >= IntroTimeline.Total + 2f) { Finish(); return; }
            if (stage == null) return;

            IntroTimeline.Shot cur = IntroTimeline.Shots[0];
            foreach (var s in IntroTimeline.Shots) if (t >= s.Start) cur = s;
            if (cur.Id != activeShot)
            {
                try { Enter(cur.Id); }
                catch (Exception e) { activeShot = cur.Id; Report("Einstellung " + cur.Id, e); }
            }
            float local = t - cur.Start, len = cur.End - cur.Start, k = Mathf.Clamp01(local / len);

            // Erzähler und Untertitel (mit Aufnahme nur, wenn Untertitel eingeschaltet sind)
            try
            {
                Narrator.Tick(t);
                var app = GameApp.I;
                bool subs = !Narrator.HasRecordings || app == null || app.Settings == null || app.Settings.Subtitles;
                string line = subs ? Narrator.SubtitleAt(t) : null;
                if (line != null) Hud.Say(Loc.T(line), 0.3f); else Hud.Subtitle = null;
            }
            catch (Exception e) { Report("Untertitel", e); }

            if (!animFailed.Contains(cur.Id))
            {
                try { AnimateShot(cur.Id, local, k); }
                catch (Exception e) { animFailed.Add(cur.Id); LogError("Animation " + cur.Id, e); }
            }
            try { ApplyCamera(); } catch (Exception e) { Report("Kamera", e); }
            try { ApplyLook(); } catch (Exception e) { Report("Licht", e); }
            try { UpdateSuns(); } catch (Exception e) { Report("Sonne", e); }
            for (int i = glows.Count - 1; i >= 0; i--)
            {
                try { glows[i].Refresh(); }
                catch (Exception e) { LogError("Leuchtpunkte", e); glows.RemoveAt(i); }
            }
        }

        void Enter(string id)
        {
            activeShot = id;
            foreach (var kv in shots) kv.Value.gameObject.SetActive(kv.Key == id);
            List<ParticleSystem> fx;
            if (shotFx.TryGetValue(id, out fx))
                foreach (var ps in fx) if (ps != null) { ps.Clear(true); ps.Play(true); }
            Transform sh;
            camSpace = shots.TryGetValue(id, out sh) ? sh : stage;
            AudioManager.Loop("intro_wind", "wind_loop", id == "shutdown", null, 0.55f, 0.9f);
        }

        bool Once(string key)
        {
            if (fired.Contains(key)) return false;
            fired.Add(key);
            return true;
        }

        // ================================================================== Kamera und Licht
        void Cam(Vector3 pos, Vector3 lookAt, float fov, float roll = 0f, float hand = 0.2f)
        {
            camPos = pos; camLook = lookAt; camFov = fov; camRoll = roll; camHand = hand;
        }

        void ApplyCamera()
        {
            var c = Camera.main;
            if (c == null || camSpace == null) return;
            Vector3 p = camSpace.TransformPoint(camPos), l = camSpace.TransformPoint(camLook);
            var dir = l - p;
            if (dir.sqrMagnitude < 1e-6f) dir = camSpace.forward;
            var app = GameApp.I;
            bool shakeOk = app == null || app.Settings == null || app.Settings.CameraShake;
            // Handkamera: zwei überlagerte, langsame Rauschbewegungen; Rütteln beim Archenstart nur mit Kamerawackeln
            float tt = Time.unscaledTime;
            float hand = camHand + (shakeOk ? camKick : camKick * 0.15f);
            float yaw = ((Mathf.PerlinNoise(tt * 0.31f, 1.7f) - 0.5f) * 2f + (Mathf.PerlinNoise(tt * 1.13f, 5.1f) - 0.5f) * 0.6f) * hand;
            float pitch = ((Mathf.PerlinNoise(3.3f, tt * 0.27f) - 0.5f) * 2f + (Mathf.PerlinNoise(8.9f, tt * 1.07f) - 0.5f) * 0.6f) * hand * 0.8f;
            float rollN = (Mathf.PerlinNoise(tt * 0.19f, 9.4f) - 0.5f) * hand * 0.8f;
            if (camKick > 0f) { yaw += (Mathf.PerlinNoise(tt * 9f, 2f) - 0.5f) * camKick * 2f; pitch += (Mathf.PerlinNoise(4f, tt * 9f) - 0.5f) * camKick * 2f; }
            c.transform.position = p;
            c.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(pitch, yaw, camRoll + rollN);
            c.fieldOfView = camFov;
        }

        void SetLook(float phase, float storm, float fog, Color fogColor, Color sky, Color equator, Color ground, Vector3 sunDir, Color sunColor, float sunIntensity, float far, float near = 0.15f)
        {
            look = new Look
            {
                Phase = phase, Storm = storm, Fog = fog, FogColor = fogColor, AmbSky = sky, AmbEquator = equator, AmbGround = ground,
                SunDir = sunDir.normalized, SunColor = sunColor, SunIntensity = sunIntensity, Far = far, Near = near,
                SunSize = 0.045f, CloudCover = -1f, SkySun = 3.2f, Space = false
            };
        }

        void ApplyLook()
        {
            var app = GameApp.I;
            float bright = app != null && app.Settings != null ? app.Settings.Brightness : 1f;
            var a = Atmosphere.I;
            if (a != null) { a.ForcePlanet = "terra"; a.ForcePhase = look.Phase; a.ForceStorm = look.Storm; }
            var c = Camera.main;
            if (c != null)
            {
                if (look.Space) { c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.004f, 0.005f, 0.012f); }
                else c.clearFlags = CameraClearFlags.Skybox;
                c.farClipPlane = look.Far > 10f ? look.Far : 1500f;
                c.nearClipPlane = look.Near > 0.001f ? look.Near : 0.15f;
            }
            RenderSettings.fog = !look.Space && look.Fog > 0f;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = look.Fog;
            RenderSettings.fogColor = look.FogColor;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = look.AmbSky * bright;
            RenderSettings.ambientEquatorColor = look.AmbEquator * bright;
            RenderSettings.ambientGroundColor = look.AmbGround * bright;
            var sun = a != null ? a.Sun : RenderSettings.sun;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.LookRotation(-look.SunDir);
                sun.color = look.SunColor;
                sun.intensity = look.SunIntensity * bright;
            }
            var sky = RenderSettings.skybox;
            if (sky != null && !look.Space)
            {
                if (sky.HasProperty("_SunDir")) sky.SetVector("_SunDir", look.SunDir);
                if (sky.HasProperty("_SunColor")) sky.SetColor("_SunColor", look.SunColor);
                if (sky.HasProperty("_SunIntensity")) sky.SetFloat("_SunIntensity", look.SunDir.y > -0.03f ? look.SkySun : 0f);
                if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", look.SunSize);
                if (look.CloudCover >= 0f && sky.HasProperty("_CloudCover")) sky.SetFloat("_CloudCover", look.CloudCover);
            }
        }

        // ================================================================== Leuchtpunkte (Glühen, Lichtbahnen, Sterne)
        sealed class Glow
        {
            public Transform Anchor;   // null → Pos im Raum des GlowSets
            public Vector3 Pos;
            public float Size = 1f;
            public Color Color = Color.white;
            public float Gain = 1f;
            public Vector3 Stretch;    // nur bei gestreckten Sets: Richtung × Länge (Welt)
        }

        /// <summary>Kamerazugewandte Leuchtpunkte als Partikel mit festen Positionen (jedes Bild neu gesetzt, keine Simulation).</summary>
        sealed class GlowSet
        {
            public ParticleSystem Ps;
            public Transform Space;
            public bool Stretched, Static;
            public readonly List<Glow> Items = new List<Glow>();
            ParticleSystem.Particle[] buf = new ParticleSystem.Particle[0];
            bool started, staticDone;

            public Glow Add(Vector3 pos, float size, Color color, Transform anchor = null)
            {
                var g = new Glow { Pos = pos, Size = size, Color = color, Anchor = anchor };
                Items.Add(g);
                return g;
            }

            public void Refresh()
            {
                if (Ps == null || Space == null || !Ps.gameObject.activeInHierarchy) return;
                if (Static && staticDone) return;
                int need = Items.Count * (Stretched ? 2 : 1);
                if (buf.Length < need) buf = new ParticleSystem.Particle[need];
                var main = Ps.main;
                if (main.maxParticles < need) main.maxParticles = need + 32;
                // Das System läuft (ohne Emission), damit Unity Grenzen und Sichtbarkeit laufend neu berechnet;
                // die Teilchen werden jedes Bild nach der Simulation neu gesetzt und bewegen sich daher nicht selbst.
                if (!started || !Ps.isPlaying) { Ps.Play(); started = true; }
                int n = 0;
                for (int i = 0; i < Items.Count; i++)
                {
                    var g = Items[i];
                    if (g.Gain <= 0.002f) continue;
                    var anchor = g.Anchor != null ? g.Anchor : Space;
                    if (anchor == null || !anchor.gameObject.activeInHierarchy) continue;
                    float sc = Mathf.Abs(anchor.lossyScale.x);
                    var col = g.Color;
                    col.a *= Mathf.Clamp01(g.Gain) * (Stretched ? 0.6f : 1f);
                    var p = new ParticleSystem.Particle
                    {
                        position = anchor.TransformPoint(g.Pos), startSize = g.Size * sc, startColor = col,
                        startLifetime = 1e4f, remainingLifetime = 1e4f, randomSeed = (uint)(i + 1), velocity = g.Stretch
                    };
                    buf[n++] = p;
                    // gestreckt: zweites Teilchen in Gegenrichtung → symmetrisch um die Mitte
                    if (Stretched) { p.velocity = -g.Stretch; p.randomSeed = (uint)(i + 100001); buf[n++] = p; }
                }
                Ps.SetParticles(buf, n);
                staticDone = true;
            }
        }

        GlowSet NewGlows(Transform space, Material mat, bool stretched = false, bool isStatic = false)
        {
            var go = new GameObject(stretched ? "Lichtbahnen" : "Leuchten");
            go.transform.SetParent(space, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false; main.startLifetime = 1e4f; main.startSpeed = 0f; main.maxParticles = 256;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var em = ps.emission; em.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 50f;
            r.minParticleSize = 0f;
            if (stretched) { r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 1f; r.velocityScale = 1f; }
            else r.renderMode = ParticleSystemRenderMode.Billboard;
            var set = new GlowSet { Ps = ps, Space = space, Stretched = stretched, Static = isStatic };
            glows.Add(set);
            return set;
        }

        /// <summary>Simulierte Effekt-Partikel (Staub, Rauch, Flammen). auto = beim Betreten der Einstellung starten.</summary>
        ParticleSystem Fx(string shot, Transform parent, string name, Material mat, Vector3 pos, Quaternion rot, bool auto = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 20f;
            if (auto)
            {
                List<ParticleSystem> l;
                if (!shotFx.TryGetValue(shot, out l)) { l = new List<ParticleSystem>(); shotFx[shot] = l; }
                l.Add(ps);
            }
            return ps;
        }

        static void FadeInOut(ParticleSystem ps, float inT = 0.15f, float outT = 0.7f)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inT), new GradientAlphaKey(1f, outT), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gr);
        }

        static void Grow(ParticleSystem ps, float to)
        {
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, to));
        }

        /// <summary>Schwebende Teilchen in einem Quader (Staub, Dunst, Lichtstaub).</summary>
        ParticleSystem Drift(string shot, Transform parent, Material mat, Vector3 center, Vector3 box, float life, Vector2 size, Color c0, Color c1, Vector3 wind, float count, float noise = 0f)
        {
            var ps = Fx(shot, parent, "Schweben", mat, center, Quaternion.identity);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = new ParticleSystem.MinMaxGradient(c0, c1);
            main.maxParticles = Mathf.Max(16, (int)(count * 1.3f));
            main.prewarm = true;
            var em = ps.emission;
            em.rateOverTime = count / life;
            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = box;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(wind.x * 0.7f, wind.x * 1.3f);
            vel.y = new ParticleSystem.MinMaxCurve(wind.y * 0.7f, wind.y * 1.3f);
            vel.z = new ParticleSystem.MinMaxCurve(wind.z * 0.7f, wind.z * 1.3f);
            if (noise > 0f) { var nz = ps.noise; nz.enabled = true; nz.strength = noise; nz.frequency = 0.3f; nz.scrollSpeed = 0.2f; }
            FadeInOut(ps, 0.2f, 0.75f);
            return ps;
        }

        // ================================================================== Sonne (Kern, Hof, anamorphotischer Streifen)
        sealed class SunFx { public Transform Space; public Glow Core, Halo, Streak; public float Dist, StreakLen, Vis = 1f; public Vector3 Dir; }
        readonly List<SunFx> suns = new List<SunFx>();

        SunFx MakeSun(Transform space, float dist, float core, float halo, Color tint, float streakLen)
        {
            var bill = NewGlows(space, matSoftAdd);
            var str = NewGlows(space, matSoftAdd, true);
            var f = new SunFx { Space = space, Dist = dist, StreakLen = streakLen };
            f.Core = bill.Add(Vector3.zero, core, new Color(1f, 0.95f, 0.85f, 0.9f));
            f.Halo = bill.Add(Vector3.zero, halo, new Color(tint.r, tint.g, tint.b, 0.32f));
            f.Streak = str.Add(Vector3.zero, core * 0.08f, new Color(tint.r, tint.g * 0.95f, tint.b, 0.3f));
            suns.Add(f);
            return f;
        }

        void UpdateSuns()
        {
            var c = Camera.main;
            if (c == null) return;
            foreach (var f in suns)
            {
                if (f.Space == null || !f.Space.gameObject.activeInHierarchy) continue;
                var dir = f.Dir.sqrMagnitude > 0.5f ? f.Dir : look.SunDir;
                var w = c.transform.position + dir * f.Dist;
                var lp = f.Space.InverseTransformPoint(w);
                f.Core.Pos = f.Halo.Pos = f.Streak.Pos = lp;
                f.Streak.Stretch = c.transform.right * f.StreakLen;
                f.Core.Gain = f.Vis; f.Halo.Gain = f.Vis; f.Streak.Gain = f.Vis;
            }
        }

        // ================================================================== Bühnenbau
        Transform Shot(string id, Vector3 offset)
        {
            var go = new GameObject("Shot_" + id).transform;
            go.SetParent(stage, false);
            go.localPosition = offset;
            go.gameObject.SetActive(false);
            shots[id] = go;
            return go;
        }

        void Build()
        {
            Cleanup();
            suns.Clear();
            stage = new GameObject("IntroStage").transform;
            stage.position = Origin;
            var app = GameApp.I;
            int q = app != null && app.Settings != null ? app.Settings.Quality : 2;
            detail = q <= 0 ? 0.45f : q == 1 ? 0.7f : 1f;
            Safe("Texturen", MakeTextures);
            Safe("Materialien", MakeMaterials);
            Safe("Skyline", BuildSkyline);
            Safe("Megastore", BuildMegastore);
            Safe("Archen", BuildArks);
            Safe("Abschalten", BuildShutdown);
            Safe("Zuhause", BuildHome);
            Safe("Keimling", BuildSprout);
            Safe("Schiff", BuildShip);
            camSpace = stage;
        }

        /// <summary>Jeder Bauabschnitt für sich: ein Fehler lässt höchstens diese Einstellung unvollständig, nie die anderen.</summary>
        static void Safe(string what, Action a)
        {
            try { a(); }
            catch (Exception e) { LogError(what, e); }
        }

        /// <summary>Fehler mit vollständigem Stacktrace melden (Konsole/Player.log), gekennzeichnet als Intro-Fehler.</summary>
        static void LogError(string what, Exception e)
        {
            Debug.LogException(new Exception("[Intro] " + what + ": " + e.Message, e));
        }

        Material TexMat(string template, Texture2D tex, Color color, string name)
        {
            var m = new Material(Mats.Template(template)) { name = "Intro_" + name };
            if (tex != null) m.mainTexture = tex;
            m.color = color;
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", color);
            m.enableInstancing = true;
            owned.Add(m);
            return m;
        }

        Material Unique(string template, Color color, Color emission)
        {
            var m = new Material(Mats.Template(template)) { name = "Intro_Leuchte", color = color };
            Mats.SetEmission(m, emission);
            m.enableInstancing = true;
            owned.Add(m);
            return m;
        }

        void MakeMaterials()
        {
            matBale = TexMat(Mats.Opaque, texBale, new Color(0.96f, 0.93f, 0.9f), "Ballen");
            matBale2 = TexMat(Mats.Opaque, texBale, new Color(0.8f, 0.7f, 0.6f), "Ballen2");
            matFacade = TexMat(Mats.Opaque, texFacade, new Color(0.92f, 0.9f, 0.86f), "Fassade");
            matFacade2 = TexMat(Mats.Opaque, texFacade, new Color(0.7f, 0.66f, 0.62f), "Fassade2");
            matFacadeLit = TexMat(Mats.Emissive, texFacade, new Color(0.55f, 0.55f, 0.58f), "FassadeLicht");
            if (matFacadeLit.HasProperty("_EmissionMap"))
            {
                matFacadeLit.SetTexture("_EmissionMap", texFacadeLit);
                Mats.SetEmission(matFacadeLit, new Color(1.7f, 1.3f, 0.85f));
            }
            matGround = TexMat(Mats.Opaque, texGround, Color.white, "Boden");
            matGroundDark = TexMat(Mats.Opaque, texGround, new Color(0.42f, 0.41f, 0.43f), "Platz");
            matConcrete = TexMat(Mats.Opaque, texGrime, new Color(0.66f, 0.63f, 0.58f), "Beton");
            matRust = TexMat(Mats.Opaque, texGrime, new Color(0.78f, 0.5f, 0.27f), "Rost");
            matHull = TexMat(Mats.Metal, texPanel, new Color(0.9f, 0.91f, 0.94f), "Rumpf");
            matHull.mainTextureScale = new Vector2(10f, 16f);
            matShipHull = TexMat(Mats.Metal, texPanel, new Color(0.88f, 0.9f, 0.93f), "Schiff");
            matShipHull.mainTextureScale = new Vector2(3f, 3f);
            matSoftAdd = TexMat(Mats.ParticleAdd, texSoft, Color.white, "Glühen");
            matSoftAlpha = TexMat(Mats.Particle, texSoft, Color.white, "Weich");
            matPuff = TexMat(Mats.Particle, texPuff, Color.white, "Wolke");
            matPuffAdd = TexMat(Mats.ParticleAdd, texPuff, Color.white, "Flamme");
            matRing = TexMat(Mats.ParticleAdd, texRing, Color.white, "Atmosphäre");
            matPlanet = TexMat(Mats.Opaque, texPlanet, Color.white, "Planet");
            if (matPlanet.HasProperty("_Glossiness")) matPlanet.SetFloat("_Glossiness", 0.12f);
            matClouds = TexMat(Mats.Fade, texClouds, new Color(1f, 1f, 1f, 0.92f), "Wolken");
        }

        // ================================================================== Geometrie
        /// <summary>
        /// Sammelt Quader, Raster und vorhandene Meshes je Material zu wenigen großen Meshes (ein Draw-Call je Material).
        /// Quader mit Meter-UVs (Kacheln laufen über große Flächen) oder Atlas-Rechteck je Fläche.
        /// Dreiecke im Uhrzeigersinn von außen gesehen (Unity-Konvention); fremde Meshes werden beim Übernehmen an ihren Normalen ausgerichtet.
        /// </summary>
        sealed class Geo
        {
            sealed class Part
            {
                public readonly List<Vector3> V = new List<Vector3>();
                public readonly List<Vector3> N = new List<Vector3>();
                public readonly List<Vector2> U = new List<Vector2>();
                public readonly List<Color> C = new List<Color>();
                public readonly List<int> T = new List<int>();
            }
            sealed class MeshData { public Vector3[] V, N; public Vector2[] U; public int[] T; }

            readonly Dictionary<Material, Part> parts = new Dictionary<Material, Part>();
            readonly List<Material> order = new List<Material>();
            static readonly Dictionary<Mesh, MeshData> cache = new Dictionary<Mesh, MeshData>();
            public Color Tint = Color.white;

            public static void ClearCache() { cache.Clear(); }

            Part P(Material m)
            {
                Part p;
                if (!parts.TryGetValue(m, out p)) { p = new Part(); parts[m] = p; order.Add(m); }
                return p;
            }

            static readonly Vector3[] FN = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left, Vector3.up, Vector3.down };
            static readonly Vector3[] FU = { Vector3.left, Vector3.right, Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
            static readonly Vector3[] FV = { Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.forward, Vector3.forward };

            public void Box(Material m, Matrix4x4 mx, Vector3 size, Vector2 tile, Vector2 off, Rect rect)
            {
                if (m == null) return;
                var p = P(m);
                for (int f = 0; f < 6; f++)
                {
                    Vector3 n = FN[f], u = FU[f], v = FV[f];
                    float du = Mathf.Abs(Vector3.Dot(u, size)), dv = Mathf.Abs(Vector3.Dot(v, size)), dn = Mathf.Abs(Vector3.Dot(n, size));
                    Vector3 c = n * (dn * 0.5f), hu = u * (du * 0.5f), hv = v * (dv * 0.5f);
                    Vector2 t0, t2;
                    if (tile.x > 0f) { t0 = off; t2 = off + new Vector2(du / tile.x, dv / tile.y); }
                    else { t0 = rect.min; t2 = rect.max; }
                    var wn = mx.MultiplyVector(n).normalized;
                    int i = p.V.Count;
                    p.V.Add(mx.MultiplyPoint3x4(c - hu - hv));
                    p.V.Add(mx.MultiplyPoint3x4(c + hu - hv));
                    p.V.Add(mx.MultiplyPoint3x4(c + hu + hv));
                    p.V.Add(mx.MultiplyPoint3x4(c - hu + hv));
                    p.U.Add(t0); p.U.Add(new Vector2(t2.x, t0.y)); p.U.Add(t2); p.U.Add(new Vector2(t0.x, t2.y));
                    for (int k = 0; k < 4; k++) { p.N.Add(wn); p.C.Add(Tint); }
                    p.T.Add(i); p.T.Add(i + 3); p.T.Add(i + 2);
                    p.T.Add(i); p.T.Add(i + 2); p.T.Add(i + 1);
                }
            }

            public void Box(Material m, Matrix4x4 mx, Vector3 size) { Box(m, mx, size, Vector2.zero, Vector2.zero, new Rect(0, 0, 1, 1)); }
            public void Box(Material m, Vector3 c, Vector3 size) { Box(m, Matrix4x4.Translate(c), size); }
            public void Box(Material m, Vector3 c, Vector3 size, Vector3 euler) { Box(m, Matrix4x4.TRS(c, Quaternion.Euler(euler), Vector3.one), size); }
            public void Tiled(Material m, Matrix4x4 mx, Vector3 size, Vector2 tile, Vector2 off) { Box(m, mx, size, tile, off, default(Rect)); }

            /// <summary>Quader mit einem der 16 Ballen des Atlas auf jeder Fläche.</summary>
            public void Bale(Material m, Matrix4x4 mx, Vector3 size, int idx)
            {
                idx = ((idx % 16) + 16) % 16;
                const float inset = 0.004f;
                var r = new Rect((idx % 4) * 0.25f + inset, (idx / 4) * 0.25f + inset, 0.25f - inset * 2f, 0.25f - inset * 2f);
                Box(m, mx, size, Vector2.zero, Vector2.zero, r);
            }

            /// <summary>Höhenraster mit gegebenen Stützstellen (x nach rechts, z nach vorn), UV in Metern / tile.</summary>
            public void Grid(Material m, float[] xs, float[] zs, Func<float, float, float> h, float tile)
            {
                var p = P(m);
                int nx = xs.Length, nz = zs.Length, b = p.V.Count;
                // absteigende Stützstellen spiegeln das Raster – dann Dreiecke umdrehen, damit die Oberseite sichtbar bleibt
                bool flip = (xs[nx - 1] - xs[0]) * (zs[nz - 1] - zs[0]) < 0f;
                for (int j = 0; j < nz; j++)
                    for (int i = 0; i < nx; i++)
                    {
                        float x = xs[i], z = zs[j];
                        var n = new Vector3(h(x - 1f, z) - h(x + 1f, z), 2f, h(x, z - 1f) - h(x, z + 1f)).normalized;
                        p.V.Add(new Vector3(x, h(x, z), z)); p.N.Add(n); p.U.Add(new Vector2(x / tile, z / tile)); p.C.Add(Tint);
                    }
                for (int j = 0; j < nz - 1; j++)
                    for (int i = 0; i < nx - 1; i++)
                    {
                        int a = b + j * nx + i, br = a + 1, tl = a + nx, tr = tl + 1;
                        if (flip) { p.T.Add(a); p.T.Add(tr); p.T.Add(tl); p.T.Add(a); p.T.Add(br); p.T.Add(tr); }
                        else { p.T.Add(a); p.T.Add(tl); p.T.Add(tr); p.T.Add(a); p.T.Add(tr); p.T.Add(br); }
                    }
            }

            /// <summary>Vorhandenes Mesh übernehmen (z. B. Müllformen aus MeshKit); Dreiecke zeigen danach sicher nach außen.</summary>
            public void Mesh(Material m, Mesh mesh, Matrix4x4 mx)
            {
                if (m == null || mesh == null) return;
                MeshData d;
                if (!cache.TryGetValue(mesh, out d)) { d = new MeshData { V = mesh.vertices, N = mesh.normals, U = mesh.uv, T = mesh.triangles }; cache[mesh] = d; }
                var p = P(m);
                var nm = mx.inverse.transpose;
                int b = p.V.Count;
                for (int i = 0; i < d.V.Length; i++)
                {
                    p.V.Add(mx.MultiplyPoint3x4(d.V[i]));
                    p.N.Add(i < d.N.Length ? nm.MultiplyVector(d.N[i]).normalized : Vector3.up);
                    p.U.Add(i < d.U.Length ? d.U[i] : Vector2.zero);
                    p.C.Add(Tint);
                }
                for (int i = 0; i + 2 < d.T.Length; i += 3)
                {
                    int a = b + d.T[i], c1 = b + d.T[i + 1], c2 = b + d.T[i + 2];
                    var geo = Vector3.Cross(p.V[c1] - p.V[a], p.V[c2] - p.V[a]);
                    var avg = p.N[a] + p.N[c1] + p.N[c2];
                    if (Vector3.Dot(geo, avg) < 0f) { int tmp = c1; c1 = c2; c2 = tmp; }
                    p.T.Add(a); p.T.Add(c1); p.T.Add(c2);
                }
            }

            public Mesh ToMesh(Material m, string name, List<Object> owned)
            {
                Part p;
                if (m == null || !parts.TryGetValue(m, out p) || p.V.Count == 0) return null;
                var mesh = new Mesh { name = name };
                if (p.V.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(p.V);
                mesh.SetNormals(p.N);
                mesh.SetUVs(0, p.U);
                mesh.SetColors(p.C);
                mesh.SetTriangles(p.T, 0);
                mesh.RecalculateBounds();
                owned.Add(mesh);
                return mesh;
            }

            public List<KeyValuePair<Material, Mesh>> Meshes(string name, List<Object> owned)
            {
                var l = new List<KeyValuePair<Material, Mesh>>();
                foreach (var m in order)
                {
                    var mesh = ToMesh(m, name, owned);
                    if (mesh != null) l.Add(new KeyValuePair<Material, Mesh>(m, mesh));
                }
                return l;
            }

            public GameObject Build(string name, Transform parent, List<Object> owned, bool shadows = true)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                Place(go.transform, Meshes(name, owned), shadows);
                return go;
            }

            public static void Place(Transform parent, List<KeyValuePair<Material, Mesh>> list, bool shadows = true)
            {
                foreach (var kv in list)
                {
                    var child = new GameObject(kv.Key.name);
                    child.transform.SetParent(parent, false);
                    child.AddComponent<MeshFilter>().sharedMesh = kv.Value;
                    var r = child.AddComponent<MeshRenderer>();
                    r.sharedMaterial = kv.Key;
                    r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    r.receiveShadows = true;
                }
            }
        }

        static Matrix4x4 TR(Vector3 p, Vector3 euler) { return Matrix4x4.TRS(p, Quaternion.Euler(euler), Vector3.one); }
        static Matrix4x4 TRS(Vector3 p, Vector3 euler, Vector3 s) { return Matrix4x4.TRS(p, Quaternion.Euler(euler), s); }

        Mesh Shape(string key, Action<MeshBuilder> build)
        {
            var b = new MeshBuilder();
            build(b);
            var m = b.Build("Intro_" + key, true);
            owned.Add(m);
            return m;
        }

        /// <summary>Gelände mit nach außen gröber werdendem Raster: fein vor der Kamera, grob am Horizont.</summary>
        static void Ground(Geo g, Material m, float halfX, float z0, float z1, int nx, int nz, Func<float, float, float> h, float tile, float cx = 0f)
        {
            nx = Mathf.Max(8, nx); nz = Mathf.Max(8, nz);
            var xs = new float[nx + 1];
            for (int i = 0; i <= nx; i++) { float r = -1f + 2f * i / nx; xs[i] = cx + halfX * (0.15f * r + 0.85f * r * Mathf.Abs(r)); }
            var zs = new float[nz + 1];
            for (int j = 0; j <= nz; j++) { float s = j / (float)nz; zs[j] = z0 + (z1 - z0) * (0.12f * s + 0.88f * s * s); }
            g.Grid(m, xs, zs, h, tile);
        }

        static float Dunes(float x, float z, float amp, float scale, int seed)
        {
            return amp * (Fbm(x / scale, z / scale, seed, 4) - 0.5f) * 2f;
        }

        // ------------------------------------------------------------------ Blockschrift (Leuchtreklamen, Tafeln, Schiffsnamen)
        static readonly Dictionary<char, string> Font = MakeFont();

        static Dictionary<char, string> MakeFont()
        {
            var f = new Dictionary<char, string>();
            Action<char, string> G = (c, rows) => f[c] = rows;
            G('A', ".###.#...##...#######...##...##...#");
            G('B', "####.#...##...#####.#...##...#####.");
            G('C', ".#####....#....#....#....#.....####");
            G('D', "####.#...##...##...##...##...#####.");
            G('E', "######....#....####.#....#....#####");
            G('F', "######....#....####.#....#....#....");
            G('G', ".#####....#....#.####...##...#.####");
            G('H', "#...##...##...#######...##...##...#");
            G('I', "#####..#....#....#....#....#..#####");
            G('K', "#...##..#.#.#..##...#.#..#..#.#...#");
            G('L', "#....#....#....#....#....#....#####");
            G('M', "#...###.###.#.##.#.##...##...##...#");
            G('N', "#...###..##.#.##..###...##...##...#");
            G('O', ".###.#...##...##...##...##...#.###.");
            G('P', "####.#...##...#####.#....#....#....");
            G('R', "####.#...##...#####.#.#..#..#.#...#");
            G('S', ".#####....#.....###.....#....#####.");
            G('T', "#####..#....#....#....#....#....#..");
            G('U', "#...##...##...##...##...##...#.###.");
            G('V', "#...##...##...##...##...#.#.#...#..");
            G('W', "#...##...##...##.#.##.#.###.###...#");
            G('Y', "#...##...#.#.#...#....#....#....#..");
            G('Z', "#####....#...#...#...#...#....#####");
            G('.', "................................#..");
            G('!', "..#....#....#....#....#.........#..");
            G('-', "...............#####...............");
            G('%', "##..###..#...#...#...#...#..###..##");
            G('0', ".###.#...##..###.#.###..##...#.###.");
            G('7', "#####....#...#...#...#....#....#...");
            G(':', "......#....#.........#....#........");
            return f;
        }

        static float TextWidth(string s, float px) { return Mathf.Max(0, s.Length * 6 - 1) * px; }

        /// <summary>Text aus Quadern, zentriert in der lokalen XY-Ebene, lesbar von −Z. missing = Anteil fehlender Pixel (verwittert).</summary>
        static void BlockText(Geo g, Material m, string s, Matrix4x4 mx, float px, float depth, float missing, Rng r, Material alt = null, int altIndex = -1)
        {
            float w = TextWidth(s, px), x0 = -w * 0.5f + px * 0.5f, y0 = 3f * px;
            for (int ci = 0; ci < s.Length; ci++)
            {
                string gl;
                // Jede Glyphe hat 7×5 Zellen; eine zu kurze Zeichenkette würde sonst mitten im Bühnenbau abbrechen
                if (!Font.TryGetValue(char.ToUpperInvariant(s[ci]), out gl) || gl.Length < 35) continue;
                var mat = ci == altIndex && alt != null ? alt : m;
                for (int row = 0; row < 7; row++)
                    for (int col = 0; col < 5; col++)
                    {
                        if (gl[row * 5 + col] != '#') continue;
                        if (missing > 0f && r != null && r.Chance(missing)) continue;
                        var c = new Vector3(x0 + (ci * 6 + col) * px, y0 - row * px, 0f);
                        g.Box(mat, mx * Matrix4x4.Translate(c), new Vector3(px * 0.94f, px * 0.94f, depth));
                    }
            }
        }

        // ------------------------------------------------------------------ Bausteine
        /// <summary>Turm aus gepressten Müllwürfeln: Blöcke aus Ballen (gekachelt), einzelne vorstehende Ballen, unregelmäßige Krone.</summary>
        void BaleTower(Geo g, Rng r, Vector3 basePos, float cube, int n, int layers, float yaw)
        {
            var root = Matrix4x4.TRS(basePos + Vector3.down * cube * 0.4f, Quaternion.Euler(0f, yaw, 0f), Vector3.one);
            int w = n, d = Mathf.Max(2, n + r.Range(-1, 2));
            float y = 0f;
            int li = 0;
            Vector2 drift = Vector2.zero;
            var tile = new Vector2(cube * 4f, cube * 4f);
            while (li < layers)
            {
                int slab = Mathf.Min(layers - li, r.Range(2, 7));
                var size = new Vector3(w * cube, slab * cube, d * cube);
                drift += new Vector2(r.Range(-0.3f, 0.3f), r.Range(-0.3f, 0.3f)) * cube;
                var mx = root * TR(new Vector3(drift.x, y + size.y * 0.5f, drift.y), new Vector3(r.Range(-1f, 1f), r.Range(-3f, 3f), r.Range(-1f, 1f)));
                g.Tiled(r.Chance(0.3f) ? matBale2 : matBale, mx, size, tile, new Vector2(r.Range(0, 4) * 0.25f, r.Range(0, 4) * 0.25f));
                int extra = Mathf.RoundToInt((w + d) * slab * 0.2f * detail);
                for (int e = 0; e < extra; e++)
                {
                    int face = r.Range(0, 4);
                    Vector3 nrm = face == 0 ? Vector3.forward : face == 1 ? Vector3.back : face == 2 ? Vector3.right : Vector3.left;
                    Vector3 along = face < 2 ? Vector3.right : Vector3.forward;
                    int cells = face < 2 ? w : d;
                    float half = (face < 2 ? size.z : size.x) * 0.5f;
                    float a = (r.Range(0, cells) + 0.5f) * cube - cells * cube * 0.5f;
                    float yy = (r.Range(0, slab) + 0.5f) * cube - size.y * 0.5f;
                    float push = r.Range(0.1f, 0.55f) * cube;
                    var pos = nrm * (half - cube * 0.5f + push) + along * a + Vector3.up * yy;
                    g.Bale(r.Chance(0.25f) ? matBale2 : matBale, mx * TR(pos, new Vector3(r.Range(-5f, 5f), r.Range(-7f, 7f), r.Range(-5f, 5f))), Vector3.one * cube * r.Range(0.96f, 1.04f), r.Range(0, 16));
                }
                y += size.y; li += slab;
                if (r.Chance(0.4f) && w > 3) w--;
                if (r.Chance(0.4f) && d > 3) d--;
            }
            // Krone: lose Ballen, teils verrutscht
            int top = r.Range(2, 3 + w * d / 2);
            for (int i = 0; i < top; i++)
            {
                float x = (r.Range(0, w) + 0.5f - w * 0.5f) * cube + drift.x, z = (r.Range(0, d) + 0.5f - d * 0.5f) * cube + drift.y;
                int stack = r.Chance(0.3f) ? 2 : 1;
                for (int k = 0; k < stack; k++)
                    g.Bale(matBale, root * TR(new Vector3(x + r.Range(-0.2f, 0.2f) * cube, y + (k + 0.5f) * cube, z), new Vector3(r.Range(-6f, 6f), r.Range(-15f, 15f), r.Range(-6f, 6f))), Vector3.one * cube, r.Range(0, 16));
            }
        }

        /// <summary>Hochhaus (Ruine oder intakt) mit Fensterreihen, Gesimsen, zerbrochener Krone, Stahlträgern.</summary>
        void Tower(Geo g, Rng r, Vector3 pos, float w, float d, float h, Material facade, float yaw, bool ruined, float lean = 2.5f)
        {
            var root = Matrix4x4.TRS(pos + Vector3.down * 2f, Quaternion.Euler(r.Range(-lean, lean), yaw, r.Range(-lean, lean)), Vector3.one);
            var tile = new Vector2(32f, 28f);
            float u0 = r.Range(0, 8) / 8f;
            const float seg = 14f;
            int nseg = Mathf.Max(1, Mathf.RoundToInt(h / seg));
            var ledge = matConcrete;
            float y = 0f;
            for (int i = 0; i < nseg; i++)
            {
                g.Tiled(facade, root * Matrix4x4.Translate(new Vector3(0f, y + seg * 0.5f, 0f)), new Vector3(w, seg, d), tile, new Vector2(u0, y / tile.y));
                if (i % 2 == 1) g.Tiled(ledge, root * Matrix4x4.Translate(new Vector3(0f, y + seg, 0f)), new Vector3(w + 0.5f, 0.45f, d + 0.5f), new Vector2(6f, 6f), Vector2.zero);
                y += seg;
            }
            if (ruined)
            {
                int stumps = r.Range(3, 7);
                for (int i = 0; i < stumps; i++)
                {
                    float sw = w * r.Range(0.18f, 0.5f), sd = d * r.Range(0.2f, 0.55f), sh = r.Range(3.5f, 17f);
                    float sx = r.Range(-w * 0.5f + sw * 0.5f, w * 0.5f - sw * 0.5f), sz = r.Range(-d * 0.5f + sd * 0.5f, d * 0.5f - sd * 0.5f);
                    g.Tiled(facade, root * Matrix4x4.Translate(new Vector3(sx, y + sh * 0.5f, sz)), new Vector3(sw, sh, sd), tile, new Vector2(u0 + (sx - sw * 0.5f + w * 0.5f) / tile.x, y / tile.y));
                }
                int plates = r.Range(2, 6);
                for (int i = 0; i < plates; i++)
                {
                    float py = y - r.Range(0f, 20f);
                    int side = r.Range(0, 4);
                    var dir = side == 0 ? Vector3.forward : side == 1 ? Vector3.back : side == 2 ? Vector3.right : Vector3.left;
                    float ext = side < 2 ? d : w;
                    var c = dir * (ext * 0.5f + r.Range(0.5f, 2.5f)) + Vector3.up * py + (side < 2 ? Vector3.right : Vector3.forward) * r.Range(-w * 0.3f, w * 0.3f);
                    g.Box(ledge, root * TR(c, new Vector3(r.Range(-12f, 12f), r.Range(-10f, 10f), r.Range(-12f, 12f))), new Vector3(r.Range(3f, 7f), 0.45f, r.Range(2.5f, 5f)));
                }
                var steel = Mats.Get(Mats.Metal, new Color(0.32f, 0.27f, 0.24f));
                int rebar = r.Range(4, 10);
                for (int i = 0; i < rebar; i++)
                    g.Box(steel, root * TR(new Vector3(r.Range(-w * 0.45f, w * 0.45f), y + r.Range(1f, 5f), r.Range(-d * 0.45f, d * 0.45f)), new Vector3(r.Range(-25f, 25f), 0f, r.Range(-25f, 25f))), new Vector3(0.22f, r.Range(3f, 9f), 0.22f));
            }
            else
            {
                g.Box(ledge, root * Matrix4x4.Translate(new Vector3(0f, y + 1.2f, 0f)), new Vector3(w * 0.6f, 2.4f, d * 0.6f));
                g.Box(Mats.Get(Mats.Metal, new Color(0.3f, 0.3f, 0.32f)), root * Matrix4x4.Translate(new Vector3(w * 0.2f, y + 8f, 0f)), new Vector3(0.4f, 14f, 0.4f));
            }
        }

        /// <summary>Werbetafel auf zwei Masten; Rückgabe: Mitte der Tafel (lokal zu mx).</summary>
        void Billboard(Geo g, Rng r, Matrix4x4 mx, string text, string sub, float px, float poleH, Material panel, Material letters, float missing)
        {
            float w = Mathf.Max(TextWidth(text, px), sub != null ? TextWidth(sub, px * 0.36f) : 0f) + px * 5f;
            float h = px * 7f * 1.9f;
            var frame = Mats.Get(Mats.Metal, new Color(0.3f, 0.29f, 0.28f));
            for (int i = -1; i <= 1; i += 2)
                g.Box(frame, mx * Matrix4x4.Translate(new Vector3(i * w * 0.3f, poleH * 0.5f, 0.6f)), new Vector3(px * 0.9f, poleH + h * 0.5f, px * 0.9f));
            g.Box(frame, mx * Matrix4x4.Translate(new Vector3(0f, poleH + h * 0.5f, 0.45f)), new Vector3(w + px * 0.8f, h + px * 0.8f, 0.5f));
            g.Box(panel, mx * Matrix4x4.Translate(new Vector3(0f, poleH + h * 0.5f, 0f)), new Vector3(w, h, 0.35f));
            BlockText(g, letters, text, mx * Matrix4x4.Translate(new Vector3(0f, poleH + h * 0.6f, -0.3f)), px, 0.3f, missing, r);
            if (sub != null) BlockText(g, letters, sub, mx * Matrix4x4.Translate(new Vector3(0f, poleH + h * 0.2f, -0.3f)), px * 0.36f, 0.25f, missing * 0.5f, r);
            // Laufsteg
            g.Box(frame, mx * Matrix4x4.Translate(new Vector3(0f, poleH - px * 0.2f, -0.6f)), new Vector3(w, 0.2f, 1.4f));
        }

        static readonly string[] JunkShapes = { "car", "fridge", "barrel", "drum", "box", "bag", "cart", "appliance", "sheetmetal", "girder", "canister", "ewaste", "pipe", "tool", "pbottle", "sheet", "can" };
        static readonly Color[] JunkColors =
        {
            new Color(0.62f, 0.36f, 0.22f), new Color(0.45f, 0.45f, 0.47f), new Color(0.35f, 0.45f, 0.58f), new Color(0.7f, 0.66f, 0.58f),
            new Color(0.58f, 0.2f, 0.16f), new Color(0.38f, 0.48f, 0.34f), new Color(0.72f, 0.6f, 0.34f), new Color(0.3f, 0.29f, 0.28f)
        };

        void Junk(Geo g, Rng r, Vector3 p, float scale, string shape = null)
        {
            shape = shape ?? JunkShapes[r.Range(0, JunkShapes.Length)];
            var col = JunkColors[r.Range(0, JunkColors.Length)] * r.Range(0.8f, 1.1f);
            var mat = r.Chance(0.35f) ? Mats.Get(Mats.Metal, col) : Mats.Get(Mats.Opaque, col);
            var rot = new Vector3(r.Range(-25f, 25f), r.Range(0f, 360f), r.Range(-25f, 25f));
            g.Mesh(mat, MeshKit.Trash(shape), TRS(p + Vector3.down * 0.15f * scale, rot, Vector3.one * scale));
        }

        /// <summary>Haufen loser Ballen (Nahbereich, volle Atlas-Details).</summary>
        void BalePile(Geo g, Rng r, Vector3 c, float cube, int count, Func<float, float, float> h)
        {
            for (int i = 0; i < count; i++)
            {
                int layer = i < count * 0.6f ? 0 : i < count * 0.9f ? 1 : 2;
                float rad = (3 - layer) * cube * 0.9f;
                float a = r.Range(0f, Mathf.PI * 2f), rr = r.Range(0f, rad);
                float x = c.x + Mathf.Cos(a) * rr, z = c.z + Mathf.Sin(a) * rr;
                float y = h(x, z) + cube * (0.45f + layer * 0.95f);
                g.Bale(r.Chance(0.3f) ? matBale2 : matBale, TR(new Vector3(x, y, z), new Vector3(r.Range(-10f, 10f), r.Range(0f, 90f), r.Range(-10f, 10f))), Vector3.one * cube * r.Range(0.95f, 1.05f), r.Range(0, 16));
            }
        }

        // ================================================================== 1) Skyline
        SunFx sunSky;
        readonly List<Glow> skyShafts = new List<Glow>();
        static readonly Vector3 SkySun = new Vector3(0.12f, 0.085f, 1f);

        static float SkyHeight(float x, float z)
        {
            float h = Dunes(x, z, 4f, 90f, 11) + Dunes(x, z, 1.3f, 17f, 12);
            float mid = Mathf.Clamp01((z - 60f) / 300f);
            h += mid * Dunes(x, z, 7f, 140f, 14);
            float far = Mathf.Clamp01((z - 900f) / 1300f);
            h += far * far * 110f * Fbm(x / 520f, z / 520f, 13, 3);
            return h;
        }

        static bool SkyCorridor(float x, float z) { return Mathf.Abs(x - 0.12f * z) < 26f + 0.05f * z; }

        static float PickX(Rng r, float z, float range, Func<float, float, bool> blocked)
        {
            float x = 0f;
            for (int i = 0; i < 10; i++) { x = r.Range(-range, range); if (!blocked(x, z)) break; }
            return x;
        }

        void BuildSkyline()
        {
            var s = Shot("skyline", Vector3.zero);
            var r = new Rng(2101);
            Func<float, float, float> H = SkyHeight;
            Func<float, float, bool> blocked = SkyCorridor;
            var g = new Geo();
            Ground(g, matGround, 1700f, -160f, 2900f, (int)(150 * Mathf.Max(0.6f, detail)), (int)(140 * Mathf.Max(0.6f, detail)), H, 9f);
            g.Build("Gelaende", s, owned, false);

            // Mittelgrund: Würfeltürme und Ruinen, dazwischen eine Gasse ins Gegenlicht
            var tg = new Geo();
            int midT = (int)(26 * detail) + 8;
            for (int i = 0; i < midT; i++)
            {
                float z = r.Range(150f, 720f), x = PickX(r, z, 60f + z * 0.55f, blocked);
                BaleTower(tg, r, new Vector3(x, H(x, z), z), r.Range(2.2f, 2.8f), r.Range(4, 8), r.Range(12, 44), r.Range(0f, 90f));
            }
            int farT = (int)(22 * detail) + 8;
            for (int i = 0; i < farT; i++)
            {
                float z = r.Range(760f, 1700f), x = PickX(r, z, 200f + z * 0.5f, blocked);
                BaleTower(tg, r, new Vector3(x, H(x, z), z), r.Range(4.5f, 6f), r.Range(5, 9), r.Range(20, 56), r.Range(0f, 90f));
            }
            for (int i = 0; i < 10; i++)
            {
                float z = r.Range(1700f, 2500f), x = PickX(r, z, 1300f, blocked);
                BaleTower(tg, r, new Vector3(x, H(x, z), z), r.Range(8f, 10f), r.Range(5, 8), r.Range(18, 40), r.Range(0f, 90f));
            }
            int midR = (int)(14 * detail) + 6;
            for (int i = 0; i < midR; i++)
            {
                float z = r.Range(190f, 680f), x = PickX(r, z, 80f + z * 0.55f, blocked);
                Tower(tg, r, new Vector3(x, H(x, z), z), r.Range(16f, 30f), r.Range(16f, 28f), r.Range(50f, 135f), r.Chance(0.5f) ? matFacade : matFacade2, r.Range(-20f, 20f), true);
            }
            int farR = (int)(16 * detail) + 6;
            for (int i = 0; i < farR; i++)
            {
                float z = r.Range(700f, 1900f), x = PickX(r, z, 200f + z * 0.5f, blocked);
                Tower(tg, r, new Vector3(x, H(x, z), z), r.Range(22f, 40f), r.Range(22f, 36f), r.Range(90f, 230f), matFacade2, r.Range(-25f, 25f), true, 3.5f);
            }
            // Rooftop-Leuchtschrift auf einer Ruine (erloschen)
            {
                float x = 110f, z = 430f, y = H(x, z);
                Tower(tg, r, new Vector3(x, y, z), 30f, 24f, 84f, matFacade, -8f, false, 0f);
                var red = Mats.Get(Mats.Opaque, new Color(0.62f, 0.16f, 0.13f));
                var white = Mats.Get(Mats.Opaque, new Color(0.86f, 0.82f, 0.74f));
                Billboard(tg, r, TR(new Vector3(x, y + 82f, z - 2f), new Vector3(0f, -8f, 2f)), "KONSUMA", null, 1.5f, 3f, red, white, 0.08f);
            }
            tg.Build("Tuerme", s, owned);

            // Vordergrund: Müll, lose Ballen, Tafeln, Überführung, Laternen
            var fg = new Geo();
            int junk = (int)(420 * detail) + 60;
            for (int i = 0; i < junk; i++)
            {
                float z = Mathf.Lerp(-45f, 170f, r.Next() * r.Next()), x = r.Range(-120f, 120f);
                Junk(fg, r, new Vector3(x, H(x, z), z), r.Range(0.9f, 1.6f));
            }
            int midJunk = (int)(160 * detail) + 30;
            string[] big = { "car", "bus", "truck", "fridge", "car", "barrel" };
            for (int i = 0; i < midJunk; i++)
            {
                float z = r.Range(150f, 520f), x = r.Range(-260f, 260f);
                Junk(fg, r, new Vector3(x, H(x, z), z), r.Range(1f, 1.8f), big[r.Range(0, big.Length)]);
            }
            for (int i = 0; i < 12; i++)
            {
                float z = r.Range(8f, 140f), x = r.Range(-80f, 80f);
                BalePile(fg, r, new Vector3(x, 0f, z), 1.3f, r.Range(5, 15), H);
            }
            {
                // große Straßentafel links, leicht schief
                var red = Mats.Get(Mats.Opaque, new Color(0.58f, 0.16f, 0.12f));
                var white = Mats.Get(Mats.Opaque, new Color(0.88f, 0.84f, 0.76f));
                float x = -62f, z = 150f;
                Billboard(fg, r, TR(new Vector3(x, H(x, z) - 1f, z), new Vector3(0f, 14f, 3.5f)), "KONSUMA", "ALLES. SOFORT. IMMER NEU.", 0.95f, 24f, red, white, 0.05f);
                // umgestürzte Tafel im Vordergrund
                x = 24f; z = 26f;
                BlockText(fg, white, "KONSUMA", TR(new Vector3(x, H(x, z) + 0.9f, z), new Vector3(62f, -22f, 7f)), 0.38f, 0.14f, 0.12f, r);
                fg.Box(red, TR(new Vector3(x, H(x, z) + 0.9f, z) + Quaternion.Euler(62f, -22f, 7f) * new Vector3(0f, 0f, 0.24f), new Vector3(62f, -22f, 7f)), new Vector3(17.5f, 5.8f, 0.3f));
            }
            {
                // Überführung einer alten Stadtautobahn, abgebrochen
                var deck = TR(new Vector3(-70f, 13f, 104f), new Vector3(0f, 9f, 0f));
                for (int i = 0; i < 7; i++)
                {
                    float x0 = -110f + i * 26f;
                    var piece = deck * TR(new Vector3(x0, i == 6 ? -1.5f : 0f, 0f), new Vector3(0f, 0f, i == 6 ? -9f : r.Range(-0.6f, 0.6f)));
                    fg.Tiled(matConcrete, piece, new Vector3(i == 6 ? 17f : 26.2f, 1.6f, 13f), new Vector2(6f, 6f), new Vector2(r.Next(), r.Next()));
                    fg.Tiled(matConcrete, piece * Matrix4x4.Translate(new Vector3(0f, 1.3f, 6.2f)), new Vector3(i == 6 ? 17f : 26f, 1.1f, 0.5f), new Vector2(6f, 6f), Vector2.zero);
                    fg.Tiled(matConcrete, piece * Matrix4x4.Translate(new Vector3(0f, 1.3f, -6.2f)), new Vector3(i == 6 ? 17f : 26f, 1.1f, 0.5f), new Vector2(6f, 6f), Vector2.zero);
                    if (i < 6) fg.Tiled(matConcrete, deck * Matrix4x4.Translate(new Vector3(x0 + 13f, -7.5f, 0f)), new Vector3(2.6f, 15f, 5f), new Vector2(6f, 6f), Vector2.zero);
                    if (r.Chance(0.6f)) fg.Mesh(Mats.Get(Mats.Opaque, JunkColors[r.Range(0, JunkColors.Length)]), MeshKit.Trash("car"), deck * TR(new Vector3(x0 + r.Range(-8f, 8f), 0.8f, r.Range(-4f, 4f)), new Vector3(0f, r.Range(70f, 110f), 0f)));
                }
                // Laternen am alten Straßenrand, verbogen
                var pole = Mats.Get(Mats.Metal, new Color(0.26f, 0.25f, 0.25f));
                for (int i = 0; i < 12; i++)
                {
                    float z = -10f + i * 22f, x = -24f + r.Range(-1f, 1f);
                    var mx = TR(new Vector3(x, H(x, z) - 0.5f, z), new Vector3(r.Range(-9f, 9f), 0f, r.Range(-9f, 9f)));
                    fg.Box(pole, mx * Matrix4x4.Translate(new Vector3(0f, 4.8f, 0f)), new Vector3(0.28f, 9.6f, 0.28f));
                    fg.Box(pole, mx * TR(new Vector3(0.9f, 9.4f, 0f), new Vector3(0f, 0f, r.Range(-15f, 5f))), new Vector3(2f, 0.22f, 0.4f));
                }
            }
            fg.Build("Vordergrund", s, owned);

            // Staub, Dunst, Lichtbahnen, Sonne
            Drift("skyline", s, matPuff, new Vector3(0f, 45f, 850f), new Vector3(1700f, 100f, 1400f), 90f, new Vector2(90f, 230f),
                new Color(0.98f, 0.8f, 0.58f, 0.07f), new Color(0.9f, 0.7f, 0.5f, 0.13f), new Vector3(1.2f, 0f, 0f), 170f * detail + 40f);
            Drift("skyline", s, matPuff, new Vector3(0f, 3f, 90f), new Vector3(320f, 7f, 240f), 20f, new Vector2(10f, 32f),
                new Color(0.92f, 0.76f, 0.56f, 0.12f), new Color(0.85f, 0.68f, 0.5f, 0.18f), new Vector3(4f, 0.1f, 0f), 60f * detail + 20f);
            Drift("skyline", s, matSoftAdd, new Vector3(0f, 9f, -20f), new Vector3(90f, 22f, 110f), 14f, new Vector2(0.05f, 0.2f),
                new Color(1f, 0.85f, 0.6f, 0.5f), new Color(1f, 0.75f, 0.45f, 0.9f), new Vector3(0.7f, 0.05f, 0f), 700f * detail + 100f, 0.4f);
            var shafts = NewGlows(s, matSoftAdd, true);
            var sd = SkySun.normalized;
            float[] zs = { 240f, 360f, 480f, 640f, 820f, 1050f, 1300f, 1600f };
            foreach (var z in zs)
                skyShafts.Add(shafts.Add(new Vector3(0.12f * z + r.Range(-45f, 45f), r.Range(25f, 120f), z), r.Range(18f, 55f), new Color(1f, 0.78f, 0.5f, r.Range(0.05f, 0.09f))));
            foreach (var sh in skyShafts) sh.Stretch = -sd * r.Range(160f, 280f);
            sunSky = MakeSun(s, 450f, 60f, 560f, new Color(1f, 0.72f, 0.42f), 900f);
        }

        void AnimateSkyline(float lt, float k)
        {
            float e = M.Smooth(k);
            var sd = SkySun.normalized;
            SetLook(0.72f, 0.12f, 0.0016f, new Color(0.93f, 0.71f, 0.47f), new Color(0.5f, 0.45f, 0.46f), new Color(0.55f, 0.42f, 0.3f), new Color(0.2f, 0.15f, 0.11f),
                sd, new Color(1f, 0.72f, 0.42f), 1.7f, 3200f);
            look.SunSize = 0.05f;
            var p = new Vector3(-26f + 34f * e, 5.5f + 9f * e, -70f + 38f * e);
            p.y = Mathf.Max(p.y, SkyHeight(p.x, p.z) + 2.2f);
            Cam(p, new Vector3(6f + 22f * e, 30f + 16f * e, 420f), 40f - 3f * e, 0f, 0.22f);
            // Lichtbahnen „atmen“ leicht
            for (int i = 0; i < skyShafts.Count; i++) skyShafts[i].Gain = 0.75f + 0.25f * Mathf.Sin(t * 0.35f + i * 1.7f);
        }

        // ================================================================== 2) Megastore
        sealed class Crowd
        {
            public struct Agent { public Vector3 A, B; public float Speed, Phase, Scale; public bool Bag, Phone; }
            public Mesh Body, Bag, Phone;
            public Material BodyMat, BagMat, PhoneMat;
            public readonly List<Agent> Agents = new List<Agent>();
            readonly Matrix4x4[] buf = new Matrix4x4[1023];

            public void Draw(Transform space, float time)
            {
                if (space == null || Body == null || !SystemInfo.supportsInstancing) return;
                var L = space.localToWorldMatrix;
                Pass(L, time, 0); Pass(L, time, 1); Pass(L, time, 2);
            }

            void Pass(Matrix4x4 L, float time, int pass)
            {
                Mesh mesh = pass == 0 ? Body : pass == 1 ? Bag : Phone;
                Material mat = pass == 0 ? BodyMat : pass == 1 ? BagMat : PhoneMat;
                if (mesh == null || mat == null) return;
                int n = 0;
                for (int i = 0; i < Agents.Count; i++)
                {
                    var a = Agents[i];
                    if (pass == 1 && !a.Bag) continue;
                    if (pass == 2 && !a.Phone) continue;
                    buf[n++] = L * Pose(a, time);
                    if (n == buf.Length) { Graphics.DrawMeshInstanced(mesh, 0, mat, buf, n, null, ShadowCastingMode.Off, false, 0); n = 0; }
                }
                if (n > 0) Graphics.DrawMeshInstanced(mesh, 0, mat, buf, n, null, ShadowCastingMode.Off, false, 0);
            }

            static Matrix4x4 Pose(Agent a, float time)
            {
                var d = a.B - a.A;
                float len = d.magnitude;
                Vector3 p = a.A, dir = Vector3.forward;
                float bob = 0f;
                if (len > 0.01f)
                {
                    float f = a.Phase + time * a.Speed / len;
                    f -= Mathf.Floor(f);
                    p = a.A + d * f;
                    dir = d / len;
                    bob = Mathf.Abs(Mathf.Sin(time * 5.2f * a.Speed + a.Phase * 17f)) * 0.05f;
                }
                else dir = new Vector3(Mathf.Sin(a.Phase * 6.28f), 0f, Mathf.Cos(a.Phase * 6.28f));
                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
                return Matrix4x4.TRS(p + Vector3.up * bob, Quaternion.LookRotation(dir), Vector3.one * a.Scale);
            }
        }

        Crowd crowd;
        Transform megaShot;
        readonly List<Material> neonLetters = new List<Material>();
        Glow signWash;
        Light neonLight;
        SunFx sunMega;

        /// <summary>Kleinster Abstand (XZ) zwischen einem Laufweg a→b und der Kamerafahrt c→d (0, wenn sie sich kreuzen).</summary>
        static float PathNear(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            Func<Vector2, Vector2, Vector2, float> pd = (p, s0, s1) =>
            {
                var s = s1 - s0;
                float u = Mathf.Clamp01(Vector2.Dot(p - s0, s) / Mathf.Max(1e-6f, s.sqrMagnitude));
                return (p - (s0 + s * u)).magnitude;
            };
            Func<Vector2, Vector2, Vector2, float> side = (p, q, o) => (q.x - p.x) * (o.y - p.y) - (q.y - p.y) * (o.x - p.x);
            float d1 = side(a, b, c), d2 = side(a, b, d), d3 = side(c, d, a), d4 = side(c, d, b);
            if (d1 * d2 < 0f && d3 * d4 < 0f) return 0f;
            return Mathf.Min(Mathf.Min(pd(a, c, d), pd(b, c, d)), Mathf.Min(pd(c, a, b), pd(d, a, b)));
        }

        static float MegaHeight(float x, float z)
        {
            float plaza = Mathf.Clamp01((z - 300f) / 200f) + Mathf.Clamp01((Mathf.Abs(x) - 320f) / 200f);
            float h = Mathf.Clamp01(plaza) * Dunes(x, z, 6f, 110f, 21);
            float mt = Mathf.SmoothStep(0f, 1f, (z - 620f) / 520f);
            float ridge = 1f - Mathf.Abs(Fbm(x / 420f, z / 420f, 23, 3) * 2f - 1f);
            h += mt * (60f + 300f * ridge * ridge) * (0.7f + 0.3f * Mathf.Cos(x / 700f));
            return h;
        }

        void BuildMegastore()
        {
            var s = Shot("megastore", new Vector3(5000f, 0f, 0f));
            megaShot = s;
            var r = new Rng(2102);
            Func<float, float, float> H = MegaHeight;
            var g = new Geo();
            Ground(g, matGroundDark, 1500f, -220f, 2200f, (int)(120 * Mathf.Max(0.6f, detail)), (int)(120 * Mathf.Max(0.6f, detail)), H, 10f);
            g.Build("Gelaende", s, owned, false);

            var b = new Geo();
            var clad = Mats.Get(Mats.Opaque, new Color(0.8f, 0.78f, 0.74f));
            var dark = Mats.Get(Mats.Opaque, new Color(0.1f, 0.1f, 0.12f));
            var red = Mats.Get(Mats.Emissive, new Color(0.75f, 0.12f, 0.12f), new Color(1.1f, 0.12f, 0.12f));
            var ceiling = Mats.Get(Mats.Emissive, new Color(0.95f, 0.92f, 0.85f), new Color(2.3f, 2.1f, 1.8f));
            // Baukörper
            b.Box(clad, new Vector3(0f, 37f, 166f), new Vector3(226f, 14f, 96f));
            b.Box(red, new Vector3(0f, 32.5f, 117.9f), new Vector3(226f, 4.2f, 0.4f));
            b.Box(dark, new Vector3(0f, 39.8f, 117.9f), new Vector3(150f, 8f, 0.4f));
            for (int i = -1; i <= 1; i += 2) b.Box(clad, new Vector3(i * 112f, 15f, 166f), new Vector3(2.5f, 30f, 96f));
            b.Box(clad, new Vector3(0f, 15f, 213f), new Vector3(226f, 30f, 2f));
            // Geschossdecken des Atriums mit Lichtdecken
            for (int L = 1; L <= 2; L++)
            {
                b.Box(clad, new Vector3(0f, L * 10f, 140f), new Vector3(222f, 0.8f, 40f));
                b.Box(ceiling, new Vector3(0f, L * 10f - 0.45f, 141f), new Vector3(214f, 0.1f, 36f));
                b.Box(Mats.Get(Mats.Emissive, new Color(0.9f, 0.95f, 1f), new Color(2.5f, 2.7f, 3f)), new Vector3(0f, L * 10f + 0.2f, 120.3f), new Vector3(222f, 0.25f, 0.2f));
            }
            b.Box(ceiling, new Vector3(0f, 29.4f, 150f), new Vector3(214f, 0.1f, 56f));
            // Rückwand: leuchtende Werbeflächen
            Color[] ads = { new Color(1f, 0.9f, 0.7f), new Color(0.3f, 0.9f, 1f), new Color(1f, 0.3f, 0.6f), new Color(1f, 0.8f, 0.25f), new Color(0.55f, 0.45f, 1f) };
            string[] words = { "NEU!", "-70%", "JETZT", "MEHR", "KAUFEN", "SOFORT", "NEU!" };
            var ink = Mats.Get(Mats.Opaque, new Color(0.05f, 0.05f, 0.06f));
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 7; col++)
                {
                    var c = ads[r.Range(0, ads.Length)];
                    float x = -90f + col * 30f, y = 5f + row * 10f;
                    b.Box(Mats.Get(Mats.Emissive, c, c * 2.1f), new Vector3(x, y, 178f), new Vector3(27f, 8f, 0.4f));
                    if (r.Chance(0.55f)) BlockText(b, ink, words[r.Range(0, words.Length)], Matrix4x4.Translate(new Vector3(x, y, 177.6f)), 0.8f, 0.2f, 0f, r);
                }
            // Säulen der Glasfront
            for (int i = 0; i <= 10; i++) b.Box(dark, new Vector3(-100f + i * 20f, 15f, 119f), new Vector3(1.8f, 30f, 1.8f));
            // Rolltreppen im Atrium (im Profil sichtbar, mit leuchtenden Handläufen)
            var rail = Mats.Get(Mats.Emissive, new Color(0.7f, 0.95f, 1f), new Color(1.6f, 2.6f, 3f));
            var escalatorPaths = new List<Vector3[]>();
            for (int L = 0; L < 2; L++)
                for (int i = 0; i < 4; i++)
                {
                    float x = -75f + i * 50f, z = 128f + (i % 2) * 14f + L * 4f, dir = (i + L) % 2 == 0 ? 1f : -1f;
                    float y0 = L * 10f;
                    var from = new Vector3(x - dir * 8.7f, y0, z);
                    var to = new Vector3(x + dir * 8.7f, y0 + 10f, z);
                    var mid = (from + to) * 0.5f;
                    float ang = Mathf.Atan2(10f, 17.4f) * Mathf.Rad2Deg * dir;
                    var mx = TR(mid + Vector3.down * 0.5f, new Vector3(0f, 0f, ang));
                    b.Box(dark, mx, new Vector3(20.2f, 1f, 1.6f));
                    b.Box(rail, mx * Matrix4x4.Translate(new Vector3(0f, 1.4f, 0.85f)), new Vector3(20.2f, 0.12f, 0.1f));
                    b.Box(rail, mx * Matrix4x4.Translate(new Vector3(0f, 1.4f, -0.85f)), new Vector3(20.2f, 0.12f, 0.1f));
                    escalatorPaths.Add(new[] { from + Vector3.up * 0.1f, to + Vector3.up * 0.1f });
                }
            // Eingang
            b.Box(Mats.Get(Mats.Emissive, new Color(1f, 0.95f, 0.85f), new Color(3f, 2.7f, 2.2f)), new Vector3(0f, 4f, 122f), new Vector3(26f, 8f, 0.3f));
            // KONSUMA in riesiger Leuchtschrift auf dem Dach
            var truss = Mats.Get(Mats.Metal, new Color(0.22f, 0.22f, 0.24f));
            const string word = "KONSUMA";
            float px = 2.3f, sw = TextWidth(word, px), baseY = 44f, cy = baseY + 3f + px * 3.5f;
            for (int i = 0; i < 6; i++) b.Box(truss, new Vector3(-sw * 0.5f + i * sw / 5f, baseY + 9f, 126f), new Vector3(0.6f, 18f, 0.6f));
            b.Box(truss, new Vector3(0f, baseY + 1.5f, 126f), new Vector3(sw + 6f, 0.6f, 0.6f));
            b.Box(truss, new Vector3(0f, baseY + 17.5f, 126f), new Vector3(sw + 6f, 0.6f, 0.6f));
            var signGlow = NewGlows(s, matSoftAdd);
            for (int ci = 0; ci < word.Length; ci++)
            {
                var m = Unique(Mats.Emissive, new Color(1f, 0.3f, 0.4f), new Color(4.5f, 0.6f, 1.2f));
                neonLetters.Add(m);
                float lx = -sw * 0.5f + (ci * 6 + 2.5f) * px - px * 0.5f + px * 0.5f;
                BlockText(b, m, word[ci].ToString(), Matrix4x4.Translate(new Vector3(lx, cy, 124.5f)), px, 1.1f, 0f, null);
                for (int k = -1; k <= 1; k++) signGlow.Add(new Vector3(lx, cy + k * px * 2.4f, 122f), 21f, new Color(1f, 0.25f, 0.45f, 0.26f));
            }
            signWash = signGlow.Add(new Vector3(0f, cy, 121f), 150f, new Color(1f, 0.2f, 0.4f, 0.12f));
            var slogan = Mats.Get(Mats.Emissive, new Color(1f, 0.85f, 0.45f), new Color(3f, 2.3f, 1f));
            BlockText(b, slogan, "ALLES. SOFORT. IMMER NEU.", Matrix4x4.Translate(new Vector3(0f, 39.8f, 117.4f)), 0.85f, 0.3f, 0f, null);
            b.Build("Megastore", s, owned);

            // Licht der Leuchtreklame und der Glasfront
            neonLight = new GameObject("NeonLicht").AddComponent<Light>();
            neonLight.transform.SetParent(s, false);
            neonLight.transform.localPosition = new Vector3(0f, cy, 100f);
            neonLight.type = LightType.Point; neonLight.range = 150f; neonLight.intensity = 2.4f; neonLight.color = new Color(1f, 0.3f, 0.45f);
            for (int i = -1; i <= 1; i++)
            {
                var l = new GameObject("Schaufenster").AddComponent<Light>();
                l.transform.SetParent(s, false);
                l.transform.localPosition = new Vector3(i * 70f, 7f, 105f);
                l.type = LightType.Point; l.range = 85f; l.intensity = 2.2f; l.color = new Color(1f, 0.88f, 0.72f);
            }

            // Platz: Laternen, Einkaufswagen-Berge
            var pz = new Geo();
            var pole = Mats.Get(Mats.Metal, new Color(0.22f, 0.22f, 0.24f));
            var lampHead = Mats.Get(Mats.Emissive, new Color(1f, 0.9f, 0.7f), new Color(3f, 2.5f, 1.7f));
            var lampGlow = NewGlows(s, matSoftAdd);
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 8; i++)
                {
                    var p = new Vector3(-150f + i * 43f + (row % 2) * 20f, 0f, 10f + row * 36f);
                    pz.Box(pole, p + new Vector3(0f, 5f, 0f), new Vector3(0.3f, 10f, 0.3f));
                    pz.Box(lampHead, p + new Vector3(0f, 10.1f, 0f), new Vector3(1.2f, 0.3f, 0.6f));
                    lampGlow.Add(p + new Vector3(0f, 9.9f, 0f), 5f, new Color(1f, 0.82f, 0.55f, 0.55f));
                }
            var cartMat = Mats.Get(Mats.Metal, new Color(0.78f, 0.8f, 0.84f));
            var cartMesh = MeshKit.Trash("cart");
            Vector3[] heaps = { new Vector3(-165f, 0f, 95f), new Vector3(150f, 0f, 70f), new Vector3(-240f, 0f, 30f) };
            float[] heapR = { 24f, 20f, 14f }, heapH = { 15f, 12f, 8f };
            for (int hIdx = 0; hIdx < heaps.Length; hIdx++)
            {
                int n = (int)(heapR[hIdx] * heapR[hIdx] * 0.9f * detail) + 40;
                for (int i = 0; i < n; i++)
                {
                    float a = r.Range(0f, Mathf.PI * 2f), rr = heapR[hIdx] * Mathf.Sqrt(r.Next());
                    float y = heapH[hIdx] * (1f - rr / heapR[hIdx]) * r.Range(0.55f, 1f);
                    var p = heaps[hIdx] + new Vector3(Mathf.Cos(a) * rr, y - 0.4f, Mathf.Sin(a) * rr);
                    pz.Mesh(cartMat, cartMesh, TRS(p, new Vector3(r.Range(-70f, 70f), r.Range(0f, 360f), r.Range(-70f, 70f)), Vector3.one * 1.1f));
                }
            }
            // abgestellte Wagen und Autos
            for (int i = 0; i < 80; i++)
            {
                var p = new Vector3(r.Range(-220f, 220f), 0f, r.Range(-40f, 100f));
                if (Mathf.Abs(p.x) < 20f) continue;
                pz.Mesh(r.Chance(0.5f) ? cartMat : Mats.Get(Mats.Opaque, JunkColors[r.Range(0, JunkColors.Length)]), MeshKit.Trash(r.Chance(0.5f) ? "cart" : "car"), TR(p, new Vector3(0f, r.Range(0f, 360f), 0f)));
            }
            pz.Build("Platz", s, owned);

            // Die Stadt ringsum (Fenster erleuchtet) und dahinter die Müllberge mit Würfeltürmen
            var city = new Geo();
            int nb = (int)(30 * detail) + 10;
            for (int i = 0; i < nb; i++)
            {
                float side = r.Chance(0.5f) ? -1f : 1f;
                float x = side * r.Range(170f, 720f), z = r.Range(90f, 950f);
                Tower(city, r, new Vector3(x, H(x, z), z), r.Range(20f, 40f), r.Range(20f, 36f), r.Range(55f, 190f), r.Chance(0.8f) ? matFacadeLit : matFacade2, r.Range(-10f, 10f), false, 0f);
            }
            var warn = NewGlows(s, matSoftAdd);
            int nt = (int)(16 * detail) + 6;
            for (int i = 0; i < nt; i++)
            {
                float z = r.Range(700f, 1400f), x = r.Range(-900f, 900f);
                BaleTower(city, r, new Vector3(x, H(x, z), z), r.Range(5.5f, 7f), r.Range(5, 9), r.Range(20, 45), r.Range(0f, 90f));
            }
            city.Build("Stadt", s, owned);
            for (int i = 0; i < 18; i++) warn.Add(new Vector3(r.Range(-700f, 700f), r.Range(60f, 190f), r.Range(150f, 900f)), 4f, new Color(1f, 0.15f, 0.1f, 0.7f));

            // Menschenmassen: Silhouetten mit KONSUMA-Tüten und leuchtenden Bildschirmen
            crowd = new Crowd
            {
                Body = Shape("Mensch", mb =>
                {
                    mb.Cylinder(Vector3.zero, 0.16f, 0.86f, 8, true, 0.2f);
                    mb.Cylinder(new Vector3(0f, 0.86f, 0f), 0.2f, 0.58f, 8, true, 0.23f);
                    mb.Sphere(new Vector3(0f, 1.44f, 0f), 0.21f, 8, 5, 0.45f);
                    mb.Sphere(new Vector3(0f, 1.63f, 0f), 0.115f, 8, 6);
                }),
                BodyMat = Mats.Get(Mats.Opaque, new Color(0.05f, 0.05f, 0.065f)),
                BagMat = Mats.Get(Mats.Emissive, new Color(0.8f, 0.14f, 0.16f), new Color(0.35f, 0.04f, 0.05f)),
                PhoneMat = Mats.Get(Mats.Emissive, new Color(0.7f, 0.9f, 1f), new Color(2.2f, 2.8f, 3.2f)),
            };
            var bagGeo = new Geo();
            bagGeo.Box(crowd.BagMat, new Vector3(0.3f, 0.6f, 0f), new Vector3(0.12f, 0.36f, 0.32f));
            crowd.Bag = bagGeo.ToMesh(crowd.BagMat, "Tuete", owned);
            var phoneGeo = new Geo();
            phoneGeo.Box(crowd.PhoneMat, new Vector3(0.06f, 1.3f, 0.24f), new Vector3(0.08f, 0.14f, 0.02f));
            crowd.Phone = phoneGeo.ToMesh(crowd.PhoneMat, "Bildschirm", owned);
            int walkers = (int)(900 * detail) + 150;
            // Startpunkte hinter der Kamera oder seitlich außerhalb des Bildes (dort fällt der Neubeginn einer Runde nicht auf);
            // Wege, die der Kamerafahrt näher als 1,6 m kommen, werden verworfen – niemand läuft durch die Linse.
            var camA = new Vector2(-12f, -32f); var camB = new Vector2(-4f, 2f);
            for (int i = 0, tries = 0; i < walkers && tries < walkers * 4; tries++)
            {
                var a = r.Chance(0.7f) ? new Vector3(r.Range(-240f, 240f), 0f, r.Range(-95f, -38f)) : new Vector3((r.Chance(0.5f) ? -1f : 1f) * r.Range(175f, 260f), 0f, r.Range(-30f, 100f));
                var door = new Vector3(r.Range(-100f, 100f), 0f, 119f);
                if (PathNear(new Vector2(a.x, a.z), new Vector2(door.x, door.z), camA, camB) < 1.6f) continue;
                i++;
                crowd.Agents.Add(new Crowd.Agent { A = a, B = door, Speed = r.Range(1.1f, 1.7f), Phase = r.Next(), Scale = r.Range(0.9f, 1.08f), Bag = r.Chance(0.45f), Phone = r.Chance(0.3f) });
            }
            foreach (var path in escalatorPaths)
                for (int i = 0; i < 9; i++)
                    crowd.Agents.Add(new Crowd.Agent { A = path[0], B = path[1], Speed = 0.9f, Phase = i / 9f + r.Range(0f, 0.05f), Scale = r.Range(0.92f, 1.05f), Bag = r.Chance(0.5f), Phone = r.Chance(0.3f) });
            for (int L = 0; L < 3; L++)
                for (int i = 0; i < (int)(70 * detail) + 10; i++)
                {
                    var a = new Vector3(r.Range(-105f, 105f), L * 10f + (L > 0 ? 0.4f : 0f), r.Range(124f, 158f));
                    var bb = a + new Vector3(r.Range(-30f, 30f), 0f, r.Range(-4f, 4f));
                    crowd.Agents.Add(new Crowd.Agent { A = a, B = r.Chance(0.25f) ? a : bb, Speed = r.Range(0.6f, 1.2f), Phase = r.Next(), Scale = r.Range(0.9f, 1.05f), Bag = r.Chance(0.5f), Phone = r.Chance(0.25f) });
                }

            Drift("megastore", s, matPuff, new Vector3(0f, 30f, 500f), new Vector3(1400f, 60f, 900f), 70f, new Vector2(60f, 160f),
                new Color(0.55f, 0.35f, 0.4f, 0.08f), new Color(0.4f, 0.28f, 0.35f, 0.12f), new Vector3(1f, 0f, 0f), 90f * detail + 20f);
            sunMega = MakeSun(s, 600f, 40f, 420f, new Color(1f, 0.45f, 0.3f), 500f);
        }

        void AnimateMegastore(float lt, float k)
        {
            var sd = new Vector3(0.05f, 0.025f, 1f);
            SetLook(0.785f, 0.1f, 0.0011f, new Color(0.34f, 0.22f, 0.26f), new Color(0.2f, 0.17f, 0.3f), new Color(0.26f, 0.17f, 0.2f), new Color(0.06f, 0.05f, 0.06f),
                sd, new Color(1f, 0.5f, 0.32f), 0.55f, 3000f);
            look.SkySun = 2f;
            if (sunMega != null) sunMega.Vis = 0.6f;
            if (lt < 8.5f)
            {
                float e = M.Smooth(lt / 8.5f);
                Cam(new Vector3(-12f + 8f * e, 1.75f, -32f + 34f * e), new Vector3(8f - 5f * e, 20f + 12f * e, 120f), 52f, 0f, 0.32f);
            }
            else
            {
                float e = M.Smooth((lt - 8.5f) / 7.5f);
                Cam(new Vector3(40f - 60f * e, 22f + 90f * e, -80f - 170f * e), new Vector3(0f, 32f + 140f * e, 140f + 680f * e), 50f - 6f * e, 0f, 0.25f);
            }
            // Neon flackert beim Einschalten, das „S“ bleibt unruhig
            bool calm = GameApp.I != null && GameApp.I.Settings != null && GameApp.I.Settings.ReduceFlashing;
            for (int i = 0; i < neonLetters.Count; i++)
            {
                float on = lt < 0.4f + i * 0.12f ? 0.08f : 1f;
                if (i == 3 && !calm) on *= Mathf.PerlinNoise(t * 7f, 3f) > 0.28f ? 1f : 0.15f;
                Mats.SetEmission(neonLetters[i], new Color(4.5f, 0.6f, 1.2f) * on);
            }
            if (signWash != null) signWash.Gain = lt < 1.2f ? lt / 1.2f : 1f;
            if (crowd != null) crowd.Draw(megaShot, t);
        }

        // ================================================================== 3) Archen
        sealed class Ark
        {
            public Transform T;
            public Vector3 Base;
            public float Ignite, Accel, Yaw;
            public Material Nozzle;
            public ParticleSystem Flame, Smoke, Dust;
            public Light L;
            public Glow Glow, Core;
            public bool Lit;
        }
        readonly List<Ark> arks = new List<Ark>();
        readonly List<RobotModel> leftBehind = new List<RobotModel>();
        SunFx sunArks;

        static float ArkHeight(float x, float z)
        {
            float field = Mathf.Clamp01((Mathf.Abs(z - 650f) - 260f) / 200f);
            float h = Dunes(x, z, 3f, 70f, 31) * (0.3f + 0.7f * field);
            float far = Mathf.Clamp01((z - 1200f) / 1300f);
            h += far * 140f * Fbm(x / 600f, z / 600f, 33, 3);
            return h;
        }

        void BuildArkModel(Geo g, string name)
        {
            var hullDark = Mats.Get(Mats.Metal, new Color(0.42f, 0.44f, 0.48f));
            var glowWin = Mats.Get(Mats.Emissive, new Color(1f, 0.85f, 0.55f), new Color(2.4f, 1.9f, 1.1f));
            var paint = Mats.Get(Mats.Opaque, new Color(0.16f, 0.22f, 0.4f));
            Vector2[] prof =
            {
                new Vector2(0f, 0f), new Vector2(17f, 0f), new Vector2(22f, 6f), new Vector2(24.5f, 20f), new Vector2(26f, 42f), new Vector2(26f, 150f),
                new Vector2(24.5f, 184f), new Vector2(19.5f, 210f), new Vector2(11f, 229f), new Vector2(4f, 239f), new Vector2(0f, 241f)
            };
            g.Mesh(matHull, Shape("Arche", b => b.Lathe(Vector3.zero, prof, 40)), Matrix4x4.identity);
            var torus = Shape("Ring", b => b.Torus(Vector3.zero, 26.8f, 1.8f, 48, 8));
            foreach (var y in new[] { 55f, 100f, 145f }) g.Mesh(hullDark, torus, Matrix4x4.Translate(new Vector3(0f, y, 0f)));
            var booster = Shape("Booster", b => b.Lathe(Vector3.zero, new[] { new Vector2(0f, 0f), new Vector2(6.5f, 0f), new Vector2(7f, 4f), new Vector2(7f, 78f), new Vector2(5f, 92f), new Vector2(0f, 100f) }, 20));
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f;
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                g.Mesh(matHull, booster, Matrix4x4.Translate(dir * 31f + Vector3.up * 3f));
                g.Box(hullDark, TR(dir * 28.5f + Vector3.up * 50f, new Vector3(0f, a, 0f)), new Vector3(2f, 6f, 5f));
                var fin = Quaternion.Euler(0f, a + 45f, 0f) * Vector3.forward;
                g.Box(hullDark, TR(fin * 36f + Vector3.up * 22f, new Vector3(0f, a + 45f, 0f)), new Vector3(1.6f, 40f, 22f));
            }
            // Fensterbänder
            for (float y = 118f; y < 200f; y += 7f)
            {
                float rad = y < 150f ? 26f : Mathf.Lerp(26f, 19.5f, (y - 150f) / 60f);
                int n = 40;
                for (int i = 0; i < n; i++)
                {
                    float a = i * 360f / n;
                    var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                    g.Box(glowWin, TR(dir * (rad + 0.05f) + Vector3.up * y, new Vector3(0f, a, 0f)), new Vector3(2f, 1.1f, 0.3f));
                }
            }
            // Brücke
            g.Box(hullDark, new Vector3(0f, 205f, -18f), new Vector3(16f, 8f, 8f));
            for (int i = 0; i < 8; i++) g.Box(glowWin, new Vector3(-6.3f + i * 1.8f, 206f, -22.1f), new Vector3(1.2f, 1.6f, 0.2f));
            // Name senkrecht auf dem Rumpf (zur Kamera)
            string nm = name;
            for (int i = 0; i < nm.Length; i++)
                BlockText(g, paint, nm[i].ToString(), Matrix4x4.Translate(new Vector3(0f, 140f - i * 7.4f, -26.2f)), 0.9f, 0.3f, 0f, null);
            // Triebwerksglocken
            var bell = Shape("Duese", b => b.Lathe(Vector3.zero, new[] { new Vector2(6.5f, -9f), new Vector2(5f, -6f), new Vector2(3f, -1f), new Vector2(2.5f, 0.5f) }, 18));
            for (int i = 0; i < 7; i++)
            {
                var p = i == 0 ? Vector3.zero : Quaternion.Euler(0f, i * 60f, 0f) * Vector3.forward * 12f;
                g.Mesh(hullDark, bell, Matrix4x4.Translate(p));
            }
        }

        void BuildArks()
        {
            var s = Shot("arks", new Vector3(10000f, 0f, 0f));
            var r = new Rng(2103);
            Func<float, float, float> H = ArkHeight;
            var g = new Geo();
            Ground(g, matGround, 1700f, -120f, 2800f, (int)(130 * Mathf.Max(0.6f, detail)), (int)(120 * Mathf.Max(0.6f, detail)), H, 9f);
            g.Build("Gelaende", s, owned, false);

            var bg = new Geo();
            int nt = (int)(24 * detail) + 8;
            for (int i = 0; i < nt; i++)
            {
                float z = r.Range(1200f, 2400f), x = r.Range(-1500f, 1500f);
                if (r.Chance(0.6f)) BaleTower(bg, r, new Vector3(x, H(x, z), z), r.Range(6f, 9f), r.Range(5, 8), r.Range(15, 40), r.Range(0f, 90f));
                else Tower(bg, r, new Vector3(x, H(x, z), z), r.Range(25f, 45f), r.Range(25f, 40f), r.Range(90f, 220f), matFacade2, r.Range(-20f, 20f), true, 4f);
            }
            for (int i = 0; i < (int)(220 * detail) + 40; i++)
            {
                float z = Mathf.Lerp(-30f, 380f, r.Next() * r.Next()), x = r.Range(-160f, 160f);
                if (Mathf.Abs(x) < 14f && z < 9f) continue; // Standort der zurückgelassenen Roboter und der Kamera
                Junk(bg, r, new Vector3(x, H(x, z), z), r.Range(1f, 1.6f));
            }
            for (int i = 0; i < 6; i++) BalePile(bg, r, new Vector3(r.Range(-60f, 60f), 0f, r.Range(15f, 90f)), 1.3f, r.Range(4, 12), H);
            // Startrampen und Türme
            var steel = Mats.Get(Mats.Metal, new Color(0.35f, 0.33f, 0.32f));
            Vector3[] pads = { new Vector3(-280f, 0f, 640f), new Vector3(40f, 0f, 780f), new Vector3(340f, 0f, 600f) };
            var padMesh = Shape("Rampe", b => b.Cylinder(Vector3.zero, 62f, 2.2f, 36));
            foreach (var p in pads)
            {
                bg.Mesh(matConcrete, padMesh, Matrix4x4.Translate(p + Vector3.down * 0.6f));
                var tower = p + new Vector3(52f, 0f, 10f);
                for (int i = 0; i < 4; i++)
                {
                    var c = tower + new Vector3((i % 2) * 10f - 5f, 90f, (i / 2) * 10f - 5f);
                    bg.Box(steel, c, new Vector3(1.2f, 180f, 1.2f));
                }
                for (float y = 6f; y < 180f; y += 12f)
                {
                    bg.Box(steel, tower + new Vector3(0f, y, -5f), new Vector3(11f, 0.8f, 0.8f), new Vector3(0f, 0f, (y % 24f) < 12f ? 40f : -40f));
                    bg.Box(steel, tower + new Vector3(0f, y, 5f), new Vector3(11f, 0.8f, 0.8f));
                }
                bg.Box(steel, tower + new Vector3(-18f, 150f, 0f), new Vector3(26f, 3f, 3f));
            }
            bg.Build("Umgebung", s, owned);

            string[] names = { "HORIZONT", "AURORA", "ZENIT" };
            float[] accel = { 5.2f, 4.6f, 5.8f };
            for (int i = 0; i < 3; i++)
            {
                var ark = new Ark { Base = pads[i] + Vector3.up * 1.6f, Ignite = 0.3f + i * 1.7f, Accel = accel[i] };
                var at = new GameObject("Arche_" + names[i]).transform;
                at.SetParent(s, false);
                at.localPosition = ark.Base + Vector3.up * 9f;
                ark.Yaw = i == 0 ? 0f : r.Range(-40f, 40f);
                at.localRotation = Quaternion.Euler(0f, ark.Yaw, 0f);
                ark.T = at;
                var mg = new Geo();
                BuildArkModel(mg, names[i]);
                ark.Nozzle = Unique(Mats.Emissive, new Color(0.9f, 0.95f, 1f), Color.black);
                var core = Shape("Kern", b => b.Cylinder(Vector3.zero, 2.4f, 0.4f, 16));
                for (int k = 0; k < 7; k++)
                {
                    var p = k == 0 ? Vector3.zero : Quaternion.Euler(0f, k * 60f, 0f) * Vector3.forward * 12f;
                    mg.Mesh(ark.Nozzle, core, Matrix4x4.Translate(p + Vector3.down * 1.2f));
                }
                mg.Build("Rumpf", at, owned);
                // Flamme (am Schiff), Rauchsäule (bleibt in der Welt stehen), Staubwalze am Boden
                var fl = Fx("arks", at, "Flamme", matPuffAdd, new Vector3(0f, -6f, 0f), Quaternion.Euler(90f, 0f, 0f), false);
                var fm = fl.main;
                fm.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                fm.startSpeed = new ParticleSystem.MinMaxCurve(70f, 120f);
                fm.startSize = new ParticleSystem.MinMaxCurve(16f, 30f);
                fm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.55f, 0.9f), new Color(1f, 0.6f, 0.3f, 0.7f));
                fm.maxParticles = 400;
                var fe = fl.emission; fe.rateOverTime = 160f * Mathf.Max(0.5f, detail);
                var fs = fl.shape; fs.enabled = true; fs.shapeType = ParticleSystemShapeType.Cone; fs.angle = 7f; fs.radius = 12f;
                Grow(fl, 2.4f);
                FadeInOut(fl, 0.05f, 0.4f);
                ark.Flame = fl;
                var sm = Fx("arks", at, "Rauch", matPuff, new Vector3(0f, -10f, 0f), Quaternion.Euler(90f, 0f, 0f), false);
                var smm = sm.main;
                smm.startLifetime = new ParticleSystem.MinMaxCurve(14f, 20f);
                smm.startSpeed = new ParticleSystem.MinMaxCurve(25f, 45f);
                smm.startSize = new ParticleSystem.MinMaxCurve(30f, 55f);
                smm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.86f, 0.8f, 0.72f, 0.5f), new Color(0.7f, 0.66f, 0.6f, 0.6f));
                smm.gravityModifier = -0.015f;
                smm.maxParticles = 1200;
                var sme = sm.emission; sme.rateOverTime = 50f * Mathf.Max(0.5f, detail);
                var sms = sm.shape; sms.enabled = true; sms.shapeType = ParticleSystemShapeType.Cone; sms.angle = 22f; sms.radius = 14f;
                var lim = sm.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 3f; lim.dampen = 0.06f;
                Grow(sm, 4f);
                FadeInOut(sm, 0.05f, 0.6f);
                ark.Smoke = sm;
                var du = Fx("arks", s, "Staubwalze", matPuff, ark.Base + Vector3.up * 4f, Quaternion.Euler(-90f, 0f, 0f), false);
                var dm = du.main;
                dm.loop = false; dm.duration = 6f;
                dm.startLifetime = new ParticleSystem.MinMaxCurve(12f, 18f);
                dm.startSpeed = new ParticleSystem.MinMaxCurve(8f, 18f);
                dm.startSize = new ParticleSystem.MinMaxCurve(40f, 100f);
                dm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 0.66f, 0.52f, 0.55f), new Color(0.66f, 0.56f, 0.44f, 0.65f));
                dm.gravityModifier = -0.01f;
                dm.maxParticles = 500;
                var de = du.emission; de.rateOverTime = 40f * Mathf.Max(0.5f, detail); de.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(60 * Mathf.Max(0.5f, detail))) });
                var ds = du.shape; ds.enabled = true; ds.shapeType = ParticleSystemShapeType.Circle; ds.radius = 30f; ds.radiusThickness = 0.2f;
                var dv = du.velocityOverLifetime; dv.enabled = true; dv.space = ParticleSystemSimulationSpace.Local;
                dv.radial = new ParticleSystem.MinMaxCurve(22f, 45f);
                dv.x = new ParticleSystem.MinMaxCurve(0f, 0f); dv.y = new ParticleSystem.MinMaxCurve(0f, 0f); dv.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                var dl = du.limitVelocityOverLifetime; dl.enabled = true; dl.limit = 2f; dl.dampen = 0.05f;
                Grow(du, 2.6f);
                FadeInOut(du, 0.05f, 0.55f);
                ark.Dust = du;
                var light = new GameObject("Triebwerkslicht").AddComponent<Light>();
                light.transform.SetParent(at, false);
                light.transform.localPosition = new Vector3(0f, -30f, 0f);
                light.type = LightType.Point; light.range = 650f; light.intensity = 0f; light.color = new Color(1f, 0.7f, 0.4f);
                ark.L = light;
                arks.Add(ark);
            }
            var eg = NewGlows(s, matSoftAdd);
            foreach (var ark in arks)
            {
                ark.Glow = eg.Add(new Vector3(0f, -14f, 0f), 170f, new Color(1f, 0.68f, 0.35f, 0.5f), ark.T);
                ark.Core = eg.Add(new Vector3(0f, -8f, 0f), 55f, new Color(1f, 0.95f, 0.85f, 0.9f), ark.T);
                ark.Glow.Gain = ark.Core.Gain = 0f;
            }
            // zurückgelassene Reinigungsroboter (Vordergrund der zweiten Hälfte)
            for (int i = 0; i < 11; i++)
            {
                var rb = RobotModel.Create(s, "Reiniger");
                rb.transform.localPosition = new Vector3(-7.5f + i * 1.5f + r.Range(-0.2f, 0.2f), 0f, 2f + (i % 3) * 1.4f + r.Range(-0.3f, 0.3f));
                rb.transform.localRotation = Quaternion.Euler(0f, r.Range(-12f, 12f), 0f);
                rb.SetCosmetics("c_nachtblau", "a_weiss", "s_none", "x_none");
                leftBehind.Add(rb);
            }
            Drift("arks", s, matPuff, new Vector3(0f, 40f, 900f), new Vector3(1800f, 90f, 1400f), 80f, new Vector2(80f, 200f),
                new Color(0.95f, 0.78f, 0.58f, 0.06f), new Color(0.85f, 0.68f, 0.5f, 0.11f), new Vector3(1.5f, 0f, 0f), 120f * detail + 30f);
            Drift("arks", s, matSoftAdd, new Vector3(0f, 4f, 0f), new Vector3(40f, 10f, 30f), 12f, new Vector2(0.04f, 0.14f),
                new Color(1f, 0.85f, 0.6f, 0.4f), new Color(1f, 0.75f, 0.5f, 0.8f), new Vector3(0.8f, 0.1f, 0f), 250f * detail + 50f, 0.4f);
            sunArks = MakeSun(s, 500f, 50f, 480f, new Color(1f, 0.66f, 0.38f), 700f);
        }

        void AnimateArks(float lt, float k)
        {
            var sd = new Vector3(-0.38f, 0.1f, 1f);
            SetLook(0.7f, 0.25f, 0.0009f, new Color(0.86f, 0.66f, 0.48f), new Color(0.46f, 0.42f, 0.46f), new Color(0.5f, 0.4f, 0.32f), new Color(0.17f, 0.13f, 0.1f),
                sd, new Color(1f, 0.74f, 0.46f), 1.4f, 3500f);
            look.CloudCover = 0.72f;
            if (sunArks != null) sunArks.Vis = 0.75f;
            float kick = 0f;
            for (int i = 0; i < arks.Count; i++)
            {
                var a = arks[i];
                float since = lt - a.Ignite;
                if (since >= 0f && !a.Lit)
                {
                    a.Lit = true;
                    if (a.Flame != null) a.Flame.Play();
                    if (a.Smoke != null) a.Smoke.Play();
                    if (a.Dust != null) a.Dust.Play();
                }
                float ign = Mathf.Clamp01(since / 1.2f);
                float fly = Mathf.Max(0f, since - 1.8f);
                float h = 0.5f * a.Accel * fly * fly;
                float pitch = Mathf.Clamp01((fly - 5f) / 8f) * 9f * (i == 1 ? -1f : 1f);
                a.T.localPosition = a.Base + Vector3.up * (9f + h) + new Vector3(pitch * fly * 0.9f, 0f, fly * fly * 0.25f);
                a.T.localRotation = Quaternion.Euler(0f, 0f, -pitch) * Quaternion.Euler(0f, a.Yaw, 0f); // neigt sich in Flugrichtung
                Mats.SetEmission(a.Nozzle, new Color(2.2f, 2.6f, 3.2f) * ign * 1.6f);
                if (a.Glow != null) { a.Glow.Gain = ign; a.Core.Gain = ign; }
                if (a.L != null) a.L.intensity = ign * (5.5f + Mathf.PerlinNoise(t * 8f, i) * 1.5f);
                if (since > 0f && since < 4f) kick = Mathf.Max(kick, (1f - since / 4f) * 0.9f);
            }
            camKick = kick;
            if (lt < 8f)
            {
                float e = M.Smooth(lt / 8f);
                float follow = arks.Count > 0 ? arks[0].T.localPosition.y : 100f;
                Cam(new Vector3(-60f + 22f * e, 7f, -230f + 25f * e), new Vector3(20f, Mathf.Lerp(90f, Mathf.Min(follow + 40f, 300f), e), 660f), 46f, 0f, 0.3f);
            }
            else
            {
                float e = M.Smooth((lt - 8f) / 8f);
                float elev = Mathf.Lerp(14f, 19f, e) * Mathf.Deg2Rad;
                Cam(new Vector3(0.6f - 0.3f * e, 1.15f, -6.5f - 1.8f * e), new Vector3(40f, 1.15f + Mathf.Tan(elev) * 700f, 650f), 58f, 0f, 0.25f);
            }
            for (int i = 0; i < leftBehind.Count; i++)
            {
                var rb = leftBehind[i];
                if (rb == null) continue;
                Vector3 target = arks.Count > 0 ? arks[i % arks.Count].T.position : rb.transform.position + Vector3.up * 100f;
                var dir = (target - rb.transform.position).normalized;
                rb.Animate(Time.deltaTime, 0f, 0.2f, "grab", false, false, false, false, 0.3f, dir);
                if (lt > 13.5f && i == 5 && Once("sad5")) rb.Emote("sad");
            }
        }

        // ================================================================== 4) Die Roboter schalten ab
        sealed class Bot { public Transform Root, Head; public Material Eye; public Glow L, R; public float Off; public bool Hero; }
        readonly List<Bot> bots = new List<Bot>();
        Transform tumbleBag;
        // Klaviertöne des Scores (Synth.IntroScore: 47 s + k · 2,6 s) → relativ zum Beginn der Einstellung
        static readonly float[] ShutdownNotes = { 1.0f, 3.6f, 6.2f, 8.8f, 11.4f };
        static readonly Color EyeOn = new Color(0.35f, 1.5f, 2.1f);

        static float ShutHeight(float x, float z) { return Dunes(x, z, 2.2f, 40f, 41) + Dunes(x, z, 0.4f, 7f, 42) + Mathf.Clamp01((z - 60f) / 200f) * Dunes(x, z, 12f, 120f, 43); }

        void BuildShutdown()
        {
            var s = Shot("shutdown", new Vector3(0f, 0f, -6000f));
            var r = new Rng(2104);
            Func<float, float, float> H = ShutHeight;
            var g = new Geo();
            Ground(g, matGround, 500f, -60f, 900f, (int)(110 * Mathf.Max(0.6f, detail)), (int)(110 * Mathf.Max(0.6f, detail)), H, 7f);
            for (int i = 0; i < (int)(260 * detail) + 40; i++)
            {
                float z = r.Range(-20f, 160f), x = r.Range(-70f, 70f);
                if (z > -6f && z < 25f && Mathf.Abs(x) < 32f) continue; // Roboterreihen und Kamerafahrt frei halten
                Junk(g, r, new Vector3(x, H(x, z), z), r.Range(0.8f, 1.4f));
            }
            for (int i = 0; i < 14; i++)
            {
                float z = r.Range(140f, 420f), x = r.Range(-300f, 300f);
                BaleTower(g, r, new Vector3(x, H(x, z), z), r.Range(2.2f, 3f), r.Range(4, 7), r.Range(8, 30), r.Range(0f, 90f));
            }
            for (int i = 0; i < 6; i++) BalePile(g, r, new Vector3(r.Range(-40f, 40f), 0f, r.Range(28f, 60f)), 1.25f, r.Range(4, 10), H);
            g.Build("Gelaende", s, owned);

            // Modell „Reiniger“: Rumpf gemeinsam, Kopf je Roboter beweglich, Augen mit eigenem Material
            var paints = new[] { Mats.Get(Mats.Opaque, new Color(0.62f, 0.54f, 0.36f)), Mats.Get(Mats.Opaque, new Color(0.5f, 0.52f, 0.5f)), Mats.Get(Mats.Opaque, new Color(0.56f, 0.4f, 0.28f)) };
            var rubber = Mats.Get(Mats.Opaque, new Color(0.11f, 0.11f, 0.12f));
            var metal = Mats.Get(Mats.Metal, new Color(0.5f, 0.51f, 0.53f));
            var roller = Shape("Rolle", b => b.CylinderX(Vector3.zero, 0.15f, 0.26f, 10));
            var eyeMesh = Shape("Augen", b => { b.CylinderZ(new Vector3(-0.1f, 0.08f, -0.16f), 0.055f, 0.03f, 12); b.CylinderZ(new Vector3(0.1f, 0.08f, -0.16f), 0.055f, 0.03f, 12); });
            var heads = new List<KeyValuePair<Material, Mesh>>[paints.Length];
            var bodies = new Geo();
            for (int pi = 0; pi < paints.Length; pi++)
            {
                var hg = new Geo();
                hg.Box(paints[pi], new Vector3(0f, 0.08f, 0f), new Vector3(0.46f, 0.24f, 0.3f));
                hg.Box(rubber, new Vector3(0f, 0.08f, -0.152f), new Vector3(0.4f, 0.17f, 0.02f));
                hg.Box(metal, new Vector3(0.24f, 0.12f, 0.05f), new Vector3(0.03f, 0.3f, 0.03f));
                heads[pi] = hg.Meshes("ReinigerKopf" + pi, owned);
            }
            var eyeGlow = NewGlows(s, matSoftAdd);
            int rows = 3, per = (int)(14 * Mathf.Max(0.7f, detail)) + 2;
            for (int row = 0; row < rows; row++)
                for (int i = 0; i < per; i++)
                {
                    float x = -24f + i * 2.7f + row * 1.2f + r.Range(-0.3f, 0.3f), z = 8f + row * 5.5f + r.Range(-0.4f, 0.4f);
                    bool hero = row == 0 && i == per - 3;
                    if (hero) { x = 7.2f; z = 7.2f; }
                    float y = H(x, z);
                    int pi = r.Range(0, paints.Length);
                    var root = new GameObject(hero ? "Reiniger_Letzter" : "Reiniger").transform;
                    root.SetParent(s, false);
                    root.localPosition = new Vector3(x, y, z);
                    root.localRotation = Quaternion.Euler(r.Range(-3f, 3f), r.Range(-25f, 25f) + (hero ? -20f : 0f), r.Range(-3f, 3f));
                    var mx = Matrix4x4.TRS(root.localPosition, root.localRotation, Vector3.one);
                    var paint = paints[pi];
                    for (int sd = -1; sd <= 1; sd += 2)
                    {
                        bodies.Box(rubber, mx * Matrix4x4.Translate(new Vector3(sd * 0.33f, 0.16f, 0f)), new Vector3(0.24f, 0.3f, 0.86f));
                        bodies.Mesh(metal, roller, mx * Matrix4x4.Translate(new Vector3(sd * 0.33f, 0.16f, 0.38f)));
                        bodies.Mesh(metal, roller, mx * Matrix4x4.Translate(new Vector3(sd * 0.33f, 0.16f, -0.38f)));
                        bodies.Box(metal, mx * TR(new Vector3(sd * 0.41f, 0.55f, -0.12f), new Vector3(-8f, 0f, sd * 6f)), new Vector3(0.07f, 0.5f, 0.07f));
                        bodies.Box(metal, mx * Matrix4x4.Translate(new Vector3(sd * 0.43f, 0.28f, -0.16f)), new Vector3(0.12f, 0.08f, 0.14f));
                    }
                    bodies.Box(paint, mx * Matrix4x4.Translate(new Vector3(0f, 0.62f, 0f)), new Vector3(0.72f, 0.6f, 0.64f));
                    bodies.Box(rubber, mx * Matrix4x4.Translate(new Vector3(0f, 0.6f, -0.33f)), new Vector3(0.5f, 0.38f, 0.03f));
                    bodies.Box(Mats.Get(Mats.Opaque, new Color(0.75f, 0.6f, 0.15f)), mx * Matrix4x4.Translate(new Vector3(0f, 0.86f, -0.325f)), new Vector3(0.72f, 0.06f, 0.02f));
                    bodies.Box(metal, mx * Matrix4x4.Translate(new Vector3(0f, 1.02f, 0.08f)), new Vector3(0.09f, 0.22f, 0.09f));
                    var head = new GameObject("Kopf").transform;
                    head.SetParent(root, false);
                    head.localPosition = new Vector3(0f, 1.1f, 0.06f);
                    Geo.Place(head, heads[pi]);
                    var eyeMat = Unique(Mats.Emissive, new Color(0.2f, 0.5f, 0.6f), EyeOn);
                    var eyes = new GameObject("Augen");
                    eyes.transform.SetParent(head, false);
                    eyes.AddComponent<MeshFilter>().sharedMesh = eyeMesh;
                    eyes.AddComponent<MeshRenderer>().sharedMaterial = eyeMat;
                    var bot = new Bot { Root = root, Head = head, Eye = eyeMat, Hero = hero };
                    bot.L = eyeGlow.Add(new Vector3(-0.1f, 0.08f, -0.2f), 0.42f, new Color(0.4f, 0.9f, 1f, 0.55f), head);
                    bot.R = eyeGlow.Add(new Vector3(0.1f, 0.08f, -0.2f), 0.42f, new Color(0.4f, 0.9f, 1f, 0.55f), head);
                    bots.Add(bot);
                }
            bodies.Build("Reiniger", s, owned);
            // Reihenfolge des Erlöschens: von hinten nach vorn in Gruppen auf den Klaviertönen, der Letzte allein
            var order = new List<Bot>(bots);
            order.Remove(bots.Find(b => b.Hero));
            order.Sort((a, b2) => b2.Root.localPosition.z.CompareTo(a.Root.localPosition.z) != 0 ? b2.Root.localPosition.z.CompareTo(a.Root.localPosition.z) : a.Root.localPosition.x.CompareTo(b2.Root.localPosition.x));
            for (int i = 0; i < order.Count; i++)
            {
                int grp = Mathf.Min(3, i * 4 / Mathf.Max(1, order.Count));
                order[i].Off = ShutdownNotes[grp] + r.Range(0f, 0.9f);
            }
            var heroBot = bots.Find(b => b.Hero);
            if (heroBot != null) heroBot.Off = ShutdownNotes[4];

            // Wind: Staubfahnen, Schleier, eine treibende Plastiktüte
            var streak = Drift("shutdown", s, matSoftAlpha, new Vector3(0f, 1.5f, 14f), new Vector3(70f, 3f, 40f), 3.5f, new Vector2(0.04f, 0.1f),
                new Color(0.85f, 0.85f, 0.9f, 0.25f), new Color(0.7f, 0.72f, 0.8f, 0.35f), new Vector3(13f, 0.2f, 1f), 500f * detail + 80f);
            var sr = streak.GetComponent<ParticleSystemRenderer>();
            sr.renderMode = ParticleSystemRenderMode.Stretch; sr.velocityScale = 0.12f; sr.lengthScale = 2f;
            Drift("shutdown", s, matPuff, new Vector3(0f, 2f, 30f), new Vector3(120f, 4f, 70f), 12f, new Vector2(4f, 10f),
                new Color(0.62f, 0.64f, 0.72f, 0.14f), new Color(0.5f, 0.52f, 0.6f, 0.2f), new Vector3(7f, 0.1f, 0f), 70f * detail + 20f);
            var bag = new Geo();
            bag.Mesh(Mats.Get(Mats.Opaque, new Color(0.85f, 0.85f, 0.82f)), MeshKit.Trash("bag"), Matrix4x4.identity);
            tumbleBag = bag.Build("Tuete", s, owned, false).transform;
        }

        void AnimateShutdown(float lt, float k)
        {
            // blaue Stunde: Sonne unter dem Horizont (am Himmel unsichtbar), kaltes Restlicht von flach vorn
            SetLook(0.8f, 0.2f, 0.012f, new Color(0.3f, 0.32f, 0.4f), new Color(0.28f, 0.31f, 0.42f), new Color(0.24f, 0.24f, 0.3f), new Color(0.08f, 0.08f, 0.1f),
                new Vector3(0.3f, 0.12f, 1f), new Color(0.55f, 0.62f, 0.85f), 0.35f, 900f);
            look.SkySun = 0f;
            // Kamera: langsame Fahrt entlang der Reihe, am Ende auf dem letzten Roboter
            float e = M.Smooth(Mathf.Clamp01(lt / 11f));
            var p = new Vector3(-20f + 24.5f * e, 0.95f + 0.1f * e, 1.2f + 2.4f * e);
            var target = Vector3.Lerp(new Vector3(-4f, 1.1f, 16f), new Vector3(7.2f, 1.15f, 7.2f), M.Smooth(Mathf.Clamp01((lt - 4f) / 8f)));
            Cam(p, target, Mathf.Lerp(42f, 34f, e), 0f, 0.18f);
            bool calm = GameApp.I != null && GameApp.I.Settings != null && GameApp.I.Settings.ReduceFlashing;
            foreach (var b in bots)
            {
                float since = lt - b.Off;
                float eye;
                if (since < 0f) eye = 1f;
                else if (since < 0.45f && !calm) eye = Mathf.PerlinNoise(t * 22f, b.Off * 10f) > 0.45f ? 1f : 0.1f;
                else eye = Mathf.Clamp01(1f - (since - 0.45f) / (b.Hero ? 1.4f : 0.6f));
                Mats.SetEmission(b.Eye, EyeOn * eye);
                if (b.L != null) { b.L.Gain = eye; b.R.Gain = eye; }
                float droop = M.Smooth(Mathf.Clamp01((since - 0.3f) / (b.Hero ? 2.2f : 1.3f)));
                b.Head.localRotation = Quaternion.Euler(-19f * droop, 0f, 4f * droop);
            }
            if (tumbleBag != null)
            {
                float bt = Mathf.Clamp01((lt - 3.5f) / 6.5f);
                tumbleBag.localPosition = new Vector3(Mathf.Lerp(-16f, 14f, bt), ShutHeight(0f, 6f) + 0.2f + Mathf.Abs(Mathf.Sin(bt * 11f)) * 0.9f, 5.5f + Mathf.Sin(bt * 4f) * 1.2f);
                tumbleBag.localRotation = Quaternion.Euler(bt * 900f, bt * 300f, bt * 500f);
                tumbleBag.gameObject.SetActive(lt > 3.5f && lt < 10f);
            }
        }

        // ================================================================== 5) MIKOs Zuhause
        RobotModel mikoHome;
        Transform doorL, doorR, pressCube, homeShot;
        Light doorLight;
        readonly List<Glow> fairy = new List<Glow>();
        readonly List<Glow> homeShafts = new List<Glow>();
        readonly List<Glow> shelfGlow = new List<Glow>();
        SunFx sunHome;
        const float VanFloor = 0.55f;

        static float HomeHeight(float x, float z) { return Mathf.Clamp01((Vector2.Distance(new Vector2(x, z), new Vector2(0f, -4f)) - 8f) / 20f) * Dunes(x, z, 3f, 40f, 51) + Dunes(x, z, 0.15f, 3f, 52); }

        void BuildHome()
        {
            var s = Shot("home", new Vector3(-6000f, 0f, -6000f));
            homeShot = s;
            var r = new Rng(2105);
            Func<float, float, float> H = HomeHeight;
            var g = new Geo();
            Ground(g, matGround, 600f, 60f, -950f, (int)(90 * Mathf.Max(0.6f, detail)), (int)(90 * Mathf.Max(0.6f, detail)), H, 6f);
            g.Build("Gelaende", s, owned, false);

            // Lieferwagen: dünne Wände (innen und außen sichtbar), Fahrerhaus, Räder
            var v = new Geo();
            var paint = matRust;
            float x0 = -1.15f, x1 = 1.15f, zb = -2.6f, zf = 2.6f, y0 = VanFloor, y1 = 2.65f;
            v.Tiled(paint, Matrix4x4.Translate(new Vector3(x0 - 0.03f, (y0 + y1) * 0.5f, 0f)), new Vector3(0.06f, y1 - y0, zf - zb), new Vector2(2f, 2f), Vector2.zero);
            v.Tiled(paint, Matrix4x4.Translate(new Vector3(x1 + 0.03f, (y0 + y1) * 0.5f, 0f)), new Vector3(0.06f, y1 - y0, zf - zb), new Vector2(2f, 2f), new Vector2(0.3f, 0f));
            v.Tiled(paint, Matrix4x4.Translate(new Vector3(0f, y1 + 0.03f, 0f)), new Vector3(x1 - x0 + 0.12f, 0.06f, zf - zb), new Vector2(2f, 2f), Vector2.zero);
            v.Tiled(paint, Matrix4x4.Translate(new Vector3(0f, (y0 + y1) * 0.5f, zf + 0.03f)), new Vector3(x1 - x0, y1 - y0, 0.06f), new Vector2(2f, 2f), Vector2.zero);
            var wood = Mats.Get(Mats.Opaque, new Color(0.36f, 0.25f, 0.16f));
            for (int i = 0; i < 6; i++) v.Box(wood, new Vector3(x0 + 0.19f + i * 0.385f, y0 - 0.03f, 0f), new Vector3(0.37f, 0.06f, zf - zb));
            v.Box(Mats.Get(Mats.Opaque, new Color(0.2f, 0.17f, 0.15f)), new Vector3(0f, y0 - 0.25f, 0f), new Vector3(2.3f, 0.4f, 5.2f));
            // Fahrerhaus
            v.Tiled(paint, Matrix4x4.Translate(new Vector3(0f, 1.3f, 3.5f)), new Vector3(2.3f, 1.6f, 1.8f), new Vector2(2f, 2f), Vector2.zero);
            v.Box(Mats.Get(Mats.Opaque, new Color(0.08f, 0.09f, 0.1f)), new Vector3(0f, 1.75f, 4.41f), new Vector3(2f, 0.7f, 0.04f));
            v.Box(Mats.Get(Mats.Metal, new Color(0.5f, 0.5f, 0.5f)), new Vector3(0f, 0.55f, 4.45f), new Vector3(2.35f, 0.25f, 0.2f));
            var wheel = Shape("Rad", b => b.CylinderX(Vector3.zero, 0.45f, 0.3f, 16));
            foreach (var wz in new[] { -1.7f, 3.3f })
                for (int sd = -1; sd <= 1; sd += 2) v.Mesh(Mats.Get(Mats.Opaque, new Color(0.1f, 0.1f, 0.1f)), wheel, TR(new Vector3(sd * 1.12f, 0.4f, wz), new Vector3(0f, 0f, 0f)));
            // Rampe aus einem Brett
            v.Box(wood, TR(new Vector3(0.3f, VanFloor * 0.5f - 0.02f, -3.25f), new Vector3(-Mathf.Atan2(VanFloor, 1.3f) * Mathf.Rad2Deg, 0f, 0f)), new Vector3(0.8f, 0.05f, 1.45f));
            // Regale mit Sammelstücken
            var plank = Mats.Get(Mats.Opaque, new Color(0.45f, 0.32f, 0.2f));
            var shelfGlowSet = NewGlows(s, matSoftAdd);
            foreach (var side in new[] { -1f, 1f })
                foreach (var sy in new[] { 1.05f, 1.55f, 2.05f })
                {
                    float sx = side * 0.95f;
                    v.Box(plank, new Vector3(sx, sy, 0.2f), new Vector3(0.36f, 0.035f, 3.8f));
                    for (float bz = -1.6f; bz <= 2f; bz += 1.2f) v.Box(Mats.Get(Mats.Metal, new Color(0.4f, 0.4f, 0.42f)), new Vector3(side * 1.1f, sy - 0.08f, bz), new Vector3(0.05f, 0.15f, 0.04f));
                    float z = -1.6f;
                    while (z < 1.95f)
                    {
                        float step = Collectible(v, r, new Vector3(sx + r.Range(-0.05f, 0.05f), sy + 0.018f, z), side, shelfGlowSet);
                        z += step + r.Range(0.03f, 0.12f);
                    }
                }
            // Kram am Boden
            for (int i = 0; i < 10; i++) Junk(v, r, new Vector3(r.Range(-0.8f, 0.8f) * (i % 2 == 0 ? 1f : -1f), VanFloor + 0.1f, r.Range(0.6f, 2.3f)), r.Range(0.35f, 0.6f), i % 3 == 0 ? "box" : i % 3 == 1 ? "can" : "pbottle");
            v.Build("Lieferwagen", s, owned);

            // Hecktüren (drehen um die Scharniere)
            doorL = Door(s, new Vector3(x0, 0f, zb), 1f, paint);
            doorR = Door(s, new Vector3(x1, 0f, zb), -1f, paint);

            // Lichterkette: zwei Bögen an den oberen Kanten, einer quer
            var bulbCols = new[] { new Color(1f, 0.72f, 0.32f), new Color(1f, 0.38f, 0.28f), new Color(0.5f, 1f, 0.45f), new Color(0.45f, 0.62f, 1f), new Color(1f, 0.85f, 0.5f) };
            var bulbs = new Geo();
            var wire = Mats.Get(Mats.Opaque, new Color(0.08f, 0.08f, 0.08f));
            var fg = NewGlows(s, matSoftAdd);
            Action<Vector3, Vector3, float, int> chain = (a, b2, sag, n) =>
            {
                Vector3 prev = a;
                for (int i = 0; i <= n; i++)
                {
                    float f = i / (float)n;
                    var p = Vector3.Lerp(a, b2, f) + Vector3.down * sag * 4f * f * (1f - f);
                    if (i > 0)
                    {
                        var mid = (prev + p) * 0.5f;
                        var d = p - prev;
                        bulbs.Box(wire, Matrix4x4.TRS(mid, Quaternion.LookRotation(d.normalized == Vector3.zero ? Vector3.forward : d.normalized), Vector3.one), new Vector3(0.01f, 0.01f, d.magnitude));
                    }
                    var c = bulbCols[(i + fairy.Count) % bulbCols.Length];
                    bulbs.Mesh(Mats.Get(Mats.Emissive, c, c * 2.6f), MeshKit.Sphere, TRS(p + Vector3.down * 0.03f, Vector3.zero, new Vector3(0.035f, 0.05f, 0.035f)));
                    fairy.Add(fg.Add(p + Vector3.down * 0.03f, 0.26f, new Color(c.r, c.g, c.b, 0.55f)));
                    prev = p;
                }
            };
            chain(new Vector3(-1.05f, 2.55f, -2.45f), new Vector3(-1.05f, 2.55f, 2.45f), 0.14f, 16);
            chain(new Vector3(1.05f, 2.55f, -2.45f), new Vector3(1.05f, 2.55f, 2.45f), 0.14f, 16);
            chain(new Vector3(-1.05f, 2.58f, 1.2f), new Vector3(1.05f, 2.58f, -0.4f), 0.18f, 9);
            bulbs.Build("Lichterkette", s, owned, false);
            foreach (var lx in new[] { -0.55f, 0.55f })
            {
                var l = new GameObject("Lichterkette_Licht").AddComponent<Light>();
                l.transform.SetParent(s, false);
                l.transform.localPosition = new Vector3(lx, 2.2f, lx * 1.4f);
                l.type = LightType.Point; l.range = 3.6f; l.intensity = 1.3f; l.color = new Color(1f, 0.72f, 0.45f);
            }
            // Morgenlicht durch die Hecktür
            doorLight = new GameObject("Tuerlicht").AddComponent<Light>();
            doorLight.transform.SetParent(s, false);
            doorLight.transform.localPosition = new Vector3(0.2f, 2.1f, -6.5f);
            doorLight.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, VanFloor + 0.4f, 0.5f) - doorLight.transform.localPosition);
            doorLight.type = LightType.Spot; doorLight.spotAngle = 55f; doorLight.range = 16f; doorLight.intensity = 0f; doorLight.color = new Color(1f, 0.82f, 0.6f);
            var hs = NewGlows(s, matSoftAdd, true);
            for (int i = 0; i < 3; i++)
            {
                var gl = hs.Add(new Vector3(-0.5f + i * 0.5f, 1.3f + i * 0.15f, -1.2f), 0.5f + i * 0.2f, new Color(1f, 0.85f, 0.6f, 0.14f));
                gl.Stretch = new Vector3(0f, -0.32f, 1f).normalized * 1.6f;
                gl.Gain = 0f;
                homeShafts.Add(gl);
            }
            Drift("home", s, matSoftAdd, new Vector3(0f, 1.5f, -0.8f), new Vector3(2f, 1.8f, 3.6f), 10f, new Vector2(0.006f, 0.018f),
                new Color(1f, 0.88f, 0.65f, 0.45f), new Color(1f, 0.8f, 0.55f, 0.8f), new Vector3(0.02f, 0.03f, 0.01f), 160f * detail + 40f, 0.05f);

            // MIKO
            mikoHome = RobotModel.Create(s, "MIKO_Intro");
            mikoHome.transform.localPosition = new Vector3(0.15f, VanFloor, -1.1f);

            // Außen: MIKOs Würfeltürme, Müll, ferne Skyline im Morgendunst
            var o = new Geo();
            for (int i = 0; i < 9; i++)
            {
                float a = -2.2f + i * 0.5f, dist = r.Range(8f, 24f);
                var p = new Vector3(Mathf.Sin(a) * dist * 0.8f - 2f, 0f, -6f - Mathf.Cos(a) * dist * 0.2f - dist * 0.8f);
                int hgt = r.Range(3, 14);
                for (int k = 0; k < hgt; k++)
                    o.Bale(r.Chance(0.3f) ? matBale2 : matBale, TR(p + new Vector3(r.Range(-0.08f, 0.08f), H(p.x, p.z) + 0.4f + k * 0.8f, r.Range(-0.08f, 0.08f)), new Vector3(0f, r.Range(-8f, 8f), 0f)), Vector3.one * 0.8f, r.Range(0, 16));
            }
            for (int k = 0; k < 2; k++) o.Bale(matBale, TR(new Vector3(-1.2f, 0.4f + k * 0.8f, -7.6f), new Vector3(0f, k * 7f, 0f)), Vector3.one * 0.8f, 3 + k);
            for (int i = 0; i < (int)(240 * detail) + 40; i++)
            {
                float z = r.Range(-60f, 6f), x = r.Range(-40f, 40f);
                if (x > -3.5f && x < 6.5f && z > -10.5f) continue; // Wagen, MIKOs Arbeitsplatz und Kamera frei halten
                Junk(o, r, new Vector3(x, H(x, z), z), r.Range(0.7f, 1.3f));
            }
            for (int i = 0; i < 18; i++)
            {
                float z = -r.Range(260f, 900f), x = r.Range(-600f, 600f);
                BaleTower(o, r, new Vector3(x, H(x, z) - 3f, z), r.Range(3f, 5f), r.Range(4, 8), r.Range(12, 40), r.Range(0f, 90f));
            }
            o.Build("Aussen", s, owned);
            var pc = new Geo();
            pc.Bale(matBale, Matrix4x4.identity, Vector3.one, 7);
            pressCube = pc.Build("Presswuerfel", s, owned).transform;
            Drift("home", s, matPuff, new Vector3(0f, 6f, -120f), new Vector3(500f, 16f, 220f), 50f, new Vector2(20f, 60f),
                new Color(1f, 0.86f, 0.66f, 0.08f), new Color(0.95f, 0.8f, 0.6f, 0.13f), new Vector3(0.8f, 0f, 0f), 50f * detail + 15f);
            sunHome = MakeSun(s, 300f, 36f, 260f, new Color(1f, 0.78f, 0.5f), 420f);
        }

        Transform Door(Transform parent, Vector3 hinge, float dir, Material paint)
        {
            var pivot = new GameObject("Tuer").transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = hinge;
            var d = new Geo();
            d.Tiled(paint, Matrix4x4.Translate(new Vector3(dir * 0.575f, VanFloor + 1.03f, -0.03f)), new Vector3(1.15f, 2.06f, 0.06f), new Vector2(2f, 2f), new Vector2(0.5f, 0f));
            d.Box(Mats.Get(Mats.Opaque, new Color(0.08f, 0.09f, 0.1f)), new Vector3(dir * 0.575f, VanFloor + 1.6f, -0.065f), new Vector3(0.7f, 0.4f, 0.02f));
            d.Box(Mats.Get(Mats.Metal, new Color(0.6f, 0.6f, 0.6f)), new Vector3(dir * 1.02f, VanFloor + 1.0f, -0.08f), new Vector3(0.05f, 0.25f, 0.04f));
            d.Build("Blatt", pivot, owned);
            return pivot;
        }

        /// <summary>Ein Sammelstück auf dem Regal; Rückgabe: belegte Länge entlang des Regals.</summary>
        float Collectible(Geo g, Rng r, Vector3 p, float side, GlowSet glow)
        {
            int kind = r.Range(0, 12);
            Color[] glass = { new Color(0.2f, 0.55f, 0.35f), new Color(0.25f, 0.4f, 0.7f), new Color(0.7f, 0.45f, 0.15f), new Color(0.75f, 0.75f, 0.72f) };
            switch (kind)
            {
                case 0: // Flasche
                    g.Mesh(Mats.Get(Mats.Opaque, glass[r.Range(0, glass.Length)], null, 0.8f), MeshKit.Trash("bottle"), TRS(p, new Vector3(0f, r.Range(0f, 360f), 0f), Vector3.one * 0.45f));
                    return 0.12f;
                case 1: // Gummiente
                    var yel = Mats.Get(Mats.Opaque, new Color(1f, 0.82f, 0.15f), null, 0.6f);
                    g.Mesh(yel, MeshKit.Sphere, TRS(p + new Vector3(0f, 0.045f, 0f), Vector3.zero, new Vector3(0.1f, 0.08f, 0.13f)));
                    g.Mesh(yel, MeshKit.Sphere, TRS(p + new Vector3(0f, 0.11f, -0.035f), Vector3.zero, Vector3.one * 0.065f));
                    g.Box(Mats.Get(Mats.Opaque, new Color(1f, 0.45f, 0.1f)), p + new Vector3(-side * 0.02f, 0.105f, -0.072f), new Vector3(0.025f, 0.012f, 0.03f));
                    return 0.15f;
                case 2: // Glühbirne
                    g.Mesh(Mats.Get(Mats.Emissive, new Color(1f, 0.95f, 0.8f), new Color(0.5f, 0.42f, 0.25f)), MeshKit.Sphere, TRS(p + new Vector3(0f, 0.06f, 0f), Vector3.zero, new Vector3(0.07f, 0.09f, 0.07f)));
                    g.Mesh(Mats.Get(Mats.Metal, new Color(0.7f, 0.68f, 0.6f)), MeshKit.Cylinder, TRS(p + new Vector3(0f, 0.012f, 0f), Vector3.zero, new Vector3(0.035f, 0.025f, 0.035f)));
                    glow.Add(p + new Vector3(0f, 0.06f, 0f), 0.12f, new Color(1f, 0.85f, 0.55f, 0.25f));
                    return 0.09f;
                case 3: // Spielzeugauto
                    g.Mesh(Mats.Get(Mats.Opaque, new Color(0.8f, 0.12f, 0.1f), null, 0.7f), MeshKit.Trash("car"), TRS(p, new Vector3(0f, r.Range(-20f, 20f), 0f), Vector3.one * 0.05f));
                    return 0.22f;
                case 4: // Globus – die Erde, wie sie einmal war
                    g.Mesh(Mats.Get(Mats.Opaque, new Color(0.25f, 0.5f, 0.75f), null, 0.5f), MeshKit.Sphere, TRS(p + new Vector3(0f, 0.14f, 0f), new Vector3(0f, 0f, 23f), Vector3.one * 0.16f));
                    g.Mesh(Mats.Get(Mats.Opaque, new Color(0.35f, 0.6f, 0.3f)), MeshKit.Sphere, TRS(p + new Vector3(0.01f, 0.16f, -0.02f), new Vector3(0f, 30f, 23f), new Vector3(0.1f, 0.09f, 0.14f)));
                    g.Mesh(Mats.Get(Mats.Metal, new Color(0.55f, 0.45f, 0.25f)), MeshKit.Cylinder, TRS(p + new Vector3(0f, 0.02f, 0f), Vector3.zero, new Vector3(0.08f, 0.04f, 0.08f)));
                    return 0.2f;
                case 5: // Kassette
                    g.Box(Mats.Get(Mats.Opaque, new Color(0.12f, 0.12f, 0.13f)), p + new Vector3(0f, 0.035f, 0f), new Vector3(0.015f, 0.07f, 0.1f), new Vector3(0f, 0f, side * -12f));
                    g.Box(Mats.Get(Mats.Opaque, new Color(0.9f, 0.85f, 0.7f)), p + new Vector3(-side * 0.009f, 0.04f, 0f), new Vector3(0.002f, 0.035f, 0.08f), new Vector3(0f, 0f, side * -12f));
                    return 0.05f;
                case 6: // Stiefel
                    var leather = Mats.Get(Mats.Opaque, new Color(0.32f, 0.2f, 0.12f));
                    g.Box(leather, p + new Vector3(0f, 0.03f, 0.02f), new Vector3(0.09f, 0.06f, 0.24f));
                    g.Box(leather, p + new Vector3(0f, 0.11f, -0.06f), new Vector3(0.09f, 0.18f, 0.09f));
                    return 0.26f;
                case 7: // Radkappe (angelehnt)
                    g.Mesh(Mats.Get(Mats.Metal, new Color(0.8f, 0.8f, 0.82f), null, 0.85f), MeshKit.Cylinder, TRS(p + new Vector3(side * 0.08f, 0.14f, 0f), new Vector3(0f, 0f, 90f - side * 12f), new Vector3(0.28f, 0.03f, 0.28f)));
                    return 0.3f;
                case 8: // Kunstblume im Glas
                    g.Mesh(Mats.Get(Mats.Opaque, glass[3], null, 0.8f), MeshKit.Cylinder, TRS(p + new Vector3(0f, 0.05f, 0f), Vector3.zero, new Vector3(0.06f, 0.1f, 0.06f)));
                    g.Box(Mats.Get(Mats.Opaque, new Color(0.2f, 0.5f, 0.2f)), p + new Vector3(0f, 0.15f, 0f), new Vector3(0.008f, 0.14f, 0.008f));
                    g.Mesh(Mats.Get(Mats.Opaque, new Color(1f, 0.4f, 0.65f)), MeshKit.Sphere, TRS(p + new Vector3(0f, 0.23f, 0f), Vector3.zero, new Vector3(0.06f, 0.04f, 0.06f)));
                    return 0.1f;
                case 9: // Dose mit Knöpfen
                    g.Mesh(Mats.Get(Mats.Metal, new Color(0.6f, 0.2f, 0.15f)), MeshKit.Cylinder, TRS(p + new Vector3(0f, 0.05f, 0f), Vector3.zero, new Vector3(0.09f, 0.1f, 0.09f)));
                    return 0.11f;
                case 10: // kleiner Bildschirm (flimmert)
                    g.Box(Mats.Get(Mats.Opaque, new Color(0.25f, 0.24f, 0.22f)), p + new Vector3(0f, 0.08f, 0f), new Vector3(0.14f, 0.15f, 0.18f));
                    g.Box(Mats.Get(Mats.Emissive, new Color(0.5f, 0.7f, 0.9f), new Color(0.9f, 1.3f, 1.8f)), p + new Vector3(-side * 0.071f, 0.085f, 0f), new Vector3(0.004f, 0.1f, 0.13f));
                    shelfGlow.Add(glow.Add(p + new Vector3(-side * 0.1f, 0.085f, 0f), 0.35f, new Color(0.5f, 0.75f, 1f, 0.22f)));
                    return 0.21f;
                default: // Glasflaschen-Gruppe
                    for (int i = 0; i < 3; i++)
                        g.Mesh(Mats.Get(Mats.Opaque, glass[r.Range(0, glass.Length)], null, 0.8f), MeshKit.Trash("pbottle"), TRS(p + new Vector3(r.Range(-0.03f, 0.03f), 0f, i * 0.07f), new Vector3(0f, r.Range(0f, 360f), 0f), Vector3.one * 0.3f));
                    return 0.2f;
            }
        }

        void AnimateHome(float lt, float k)
        {
            bool inside = lt < 7.5f;
            var sd = new Vector3(0.05f, 0.16f, -1f);
            if (inside)
            {
                float open = M.Smooth(Mathf.Clamp01((lt - 2.4f) / 2f));
                SetLook(0.3f, 0f, 0.002f, new Color(0.8f, 0.68f, 0.55f), new Color(0.2f, 0.17f, 0.16f) * (0.6f + 0.6f * open), new Color(0.22f, 0.16f, 0.12f) * (0.6f + 0.6f * open), new Color(0.08f, 0.06f, 0.05f),
                    sd, new Color(1f, 0.8f, 0.58f), 0.5f + 0.9f * open, 600f, 0.05f);
                if (doorL != null) { doorL.localRotation = Quaternion.Euler(0f, 108f * open, 0f); doorR.localRotation = Quaternion.Euler(0f, -108f * open, 0f); }
                if (doorLight != null) doorLight.intensity = 3.4f * open;
                foreach (var gl in homeShafts) gl.Gain = open;
                if (sunHome != null) sunHome.Vis = open * 0.8f;
                // MIKO erwacht, dreht sich zur Tür und rollt hinaus ins Licht
                bool awake = lt > 1.8f;
                if (lt > 1.8f && Once("awaken")) { AudioManager.Play("awaken", null, 0.6f); mikoHome.Emote("curious"); }
                float turn = M.Smooth(Mathf.Clamp01((lt - 3.6f) / 1.4f));
                float roll = M.Smooth(Mathf.Clamp01((lt - 4.8f) / 2.7f));
                float z = Mathf.Lerp(-1.1f, -4.6f, roll);
                float y = z > -2.6f ? VanFloor : Mathf.Lerp(VanFloor, 0f, Mathf.Clamp01((-2.6f - z) / 1.3f));
                mikoHome.transform.localPosition = new Vector3(0.15f + 0.15f * roll, y, z);
                mikoHome.transform.localRotation = Quaternion.Euler(0f, 180f * turn, 0f);
                var camW = Camera.main != null ? Camera.main.transform.position : mikoHome.transform.position + Vector3.forward;
                var lookDir = turn < 0.5f ? (camW - mikoHome.transform.position).normalized : homeShot.TransformDirection(Vector3.back);
                mikoHome.Animate(Time.deltaTime, roll > 0f && roll < 1f ? 1.2f : 0f, 0.3f, "grab", false, false, !awake, false, awake ? 0.55f : 0f, lookDir);
                float e = M.Smooth(lt / 7.5f);
                Cam(new Vector3(0.45f - 0.3f * e, 1.5f - 0.12f * e, 2.35f - 1.3f * e), new Vector3(0.05f, 1.15f - 0.1f * e, -2.6f - 1.5f * e), 58f - 4f * e, 0f, 0.14f);
            }
            else
            {
                float lx = lt - 7.5f;
                SetLook(0.28f, 0.05f, 0.0035f, new Color(0.96f, 0.8f, 0.62f), new Color(0.5f, 0.46f, 0.45f), new Color(0.56f, 0.44f, 0.34f), new Color(0.2f, 0.15f, 0.11f),
                    sd, new Color(1f, 0.8f, 0.55f), 1.6f, 1600f);
                look.SunSize = 0.05f;
                if (doorL != null) { doorL.localRotation = Quaternion.Euler(0f, 108f, 0f); doorR.localRotation = Quaternion.Euler(0f, -108f, 0f); }
                if (doorLight != null) doorLight.intensity = 0f;
                foreach (var gl in homeShafts) gl.Gain = 0f;
                if (sunHome != null) sunHome.Vis = 1f;
                // Würfel für Würfel: Müll pressen, anheben, auf den Stapel setzen
                var start = new Vector3(0.5f, 0f, -6.3f);
                bool press = lx > 0.8f && lx < 3.2f;
                bool carry = lx >= 3.2f && lx < 6.2f;
                if (lx > 1.6f && Once("press")) AudioManager.Play("press", null, 0.7f);
                if (lx > 5.9f && Once("bale")) AudioManager.Play("bale", null, 0.7f);
                float squeeze = M.Smooth(Mathf.Clamp01((lx - 0.9f) / 1.6f));
                float cs = Mathf.Lerp(1.15f, 0.8f, squeeze);
                var cubeFrom = new Vector3(0.5f, cs * 0.5f, -7.3f);
                var cubeTo = new Vector3(-1.2f, 0.4f + 2f * 0.8f, -7.6f);
                float lift = M.Smooth(Mathf.Clamp01((lx - 3.2f) / 2.8f));
                var cp = Vector3.Lerp(cubeFrom, cubeTo, lift) + Vector3.up * Mathf.Sin(lift * Mathf.PI) * 0.9f;
                if (pressCube != null)
                {
                    pressCube.localPosition = lx < 3.2f ? cubeFrom : cp;
                    pressCube.localScale = new Vector3(cs * (1f + (1f - squeeze) * 0.1f), cs, cs * (1f + (1f - squeeze) * 0.06f));
                    pressCube.localRotation = Quaternion.Euler(0f, lift * 20f, 0f);
                }
                mikoHome.transform.localPosition = Vector3.Lerp(start, new Vector3(-0.2f, 0f, -6.4f), M.Smooth(Mathf.Clamp01((lx - 3f) / 2.5f)));
                mikoHome.transform.localRotation = Quaternion.Euler(0f, 180f + Mathf.Lerp(0f, 35f, lift), 0f);
                var toCube = pressCube != null ? (pressCube.position - mikoHome.transform.position).normalized : Vector3.back;
                mikoHome.Animate(Time.deltaTime, lx > 3f && lx < 5.5f ? 0.6f : 0f, 0.5f, "grab", press || carry, false, false, false, 0f, toCube);
                if (lx > 6.6f && Once("happyHome")) mikoHome.Emote("happy");
                float e = M.Smooth(lx / 7.5f);
                Cam(new Vector3(3.4f - 0.8f * e, 0.75f + 0.1f * e, -3.6f - 0.9f * e), new Vector3(-0.2f, 0.95f + 0.2f * e, -7.2f), 50f - 4f * e, 0f, 0.3f);
            }
            // Lichterkette funkelt, der kleine Bildschirm flimmert
            for (int i = 0; i < fairy.Count; i++) fairy[i].Gain = 0.65f + 0.35f * Mathf.PerlinNoise(t * 1.3f, i * 0.37f);
            foreach (var sg in shelfGlow) sg.Gain = 0.6f + 0.4f * Mathf.PerlinNoise(t * 9f, 1.3f);
        }

        // ================================================================== 6) Der Keimling (Makro-Satz, 4-fach vergrößert für saubere Genauigkeit)
        const float MacroScale = 4f;
        Transform macro, sprout, leafL, leafR, beamSpot;
        RobotModel mikoNear;
        Light beam;
        readonly List<Glow> bokeh = new List<Glow>();
        readonly List<Glow> beamShafts = new List<Glow>();
        Glow sproutHalo;

        static float SproutHeight(float x, float z)
        {
            float h = Dunes(x, z, 0.06f, 1.2f, 61) + Dunes(x, z, 0.015f, 0.25f, 62);
            float crack = Mathf.Clamp01(1f - new Vector2(x, z * 0.6f).magnitude / 0.25f);
            return h - crack * 0.03f + Mathf.Clamp01((z - 3f) / 6f) * 1.5f;
        }

        void BuildSprout()
        {
            var s = Shot("sprout", new Vector3(-6000f, 0f, 0f));
            var r = new Rng(2106);
            macro = new GameObject("Makro").transform;
            macro.SetParent(s, false);
            macro.localScale = Vector3.one * MacroScale;
            Func<float, float, float> H = SproutHeight;
            var g = new Geo();
            var xs = new float[81];
            for (int i = 0; i <= 80; i++) { float q = -1f + 2f * i / 80f; xs[i] = 7f * (0.25f * q + 0.75f * q * Mathf.Abs(q)); }
            var zs = new float[81];
            for (int j = 0; j <= 80; j++) { float q = -1f + 2f * j / 80f; zs[j] = (q < 0f ? 4f : 10f) * (0.25f * q + 0.75f * q * Mathf.Abs(q)); }
            g.Grid(matGround, xs, zs, H, 1.4f);
            // Betonplatten und Trümmer, dazwischen der Riss
            for (int i = 0; i < 16; i++)
            {
                float a = r.Range(0f, Mathf.PI * 2f), d = r.Range(0.35f, 2.8f);
                var p = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d + 0.6f);
                if (MacroClear(p, 1.1f)) continue;
                g.Tiled(matConcrete, TR(p + Vector3.up * (H(p.x, p.z) + 0.03f), new Vector3(r.Range(-14f, 14f), r.Range(0f, 360f), r.Range(-14f, 14f))), new Vector3(r.Range(0.4f, 1.3f), r.Range(0.08f, 0.2f), r.Range(0.3f, 1f)), new Vector2(1f, 1f), new Vector2(r.Next(), r.Next()));
            }
            var brick = Mats.Get(Mats.Opaque, new Color(0.55f, 0.28f, 0.2f));
            for (int i = 0; i < 26; i++)
            {
                var p = new Vector3(r.Range(-2.5f, 2.5f), 0f, r.Range(-1.5f, 3.5f));
                if (p.magnitude < 0.3f || MacroClear(p, 0.55f)) continue;
                g.Box(brick, TR(p + Vector3.up * (H(p.x, p.z) + 0.03f), new Vector3(r.Range(-20f, 20f), r.Range(0f, 360f), r.Range(-20f, 20f))), new Vector3(0.22f, 0.07f, 0.1f) * r.Range(0.6f, 1f));
            }
            for (int i = 0; i < 30; i++)
            {
                var p = new Vector3(r.Range(-3.5f, 3.5f), 0f, r.Range(-1f, 5f));
                if (p.magnitude < 0.5f || MacroClear(p, 0.7f)) continue;
                Junk(g, r, new Vector3(p.x, H(p.x, p.z), p.z), r.Range(0.35f, 0.8f), i % 4 == 0 ? "can" : i % 4 == 1 ? "pbottle" : i % 4 == 2 ? "sheetmetal" : "box");
            }
            // zerbrochene Wand im Hintergrund, durch deren Lücke das Licht fällt
            for (int i = 0; i < 9; i++)
            {
                float x = -4f + i * 1f;
                if (i == 4 || i == 5) continue;
                float hgt = r.Range(1.6f, 3.4f);
                g.Tiled(matConcrete, TR(new Vector3(x, H(x, 3.4f) + hgt * 0.5f - 0.1f, 3.4f + r.Range(-0.1f, 0.1f)), new Vector3(0f, r.Range(-4f, 4f), r.Range(-3f, 3f))), new Vector3(1.02f, hgt, 0.3f), new Vector2(1f, 1f), new Vector2(r.Next(), 0f));
            }
            g.Build("Truemmer", macro, owned);

            // Der Keimling: Erdkrume, gebogener Stiel, zwei Keimblätter, winziges erstes Blattpaar
            sprout = new GameObject("Keimling").transform;
            sprout.SetParent(macro, false);
            sprout.localPosition = new Vector3(0f, H(0f, 0f) + 0.005f, 0f);
            var sg = new Geo();
            var soil = Mats.Get(Mats.Opaque, new Color(0.2f, 0.13f, 0.08f));
            sg.Mesh(soil, Shape("Krume", b => b.Blob(Vector3.zero, 0.07f, 0.02f, 14, 5, 7, 0.3f)), Matrix4x4.identity);
            for (int i = 0; i < 8; i++) sg.Box(Mats.Get(Mats.Opaque, new Color(0.45f, 0.42f, 0.38f)), TR(new Vector3(r.Range(-0.06f, 0.06f), 0.012f, r.Range(-0.06f, 0.06f)), new Vector3(r.Range(0f, 90f), r.Range(0f, 90f), 0f)), Vector3.one * r.Range(0.006f, 0.014f));
            var stemMat = Mats.Get(Mats.Emissive, new Color(0.55f, 0.78f, 0.35f), new Color(0.05f, 0.1f, 0.02f));
            var stemSeg = Shape("Stiel", b => b.Cylinder(Vector3.zero, 0.0042f, 1f, 8, false, 0.0036f));
            Vector3 sp = Vector3.zero;
            float ang = 0f;
            for (int i = 0; i < 6; i++)
            {
                float len = 0.016f;
                var rot = Quaternion.Euler(ang, 0f, 4f - i * 1.6f);
                sg.Mesh(stemMat, stemSeg, Matrix4x4.TRS(sp, rot, new Vector3(1f, len, 1f)));
                sp += rot * Vector3.up * len;
                ang += i < 3 ? -3f : 5f;
            }
            sg.Mesh(Mats.Get(Mats.Opaque, new Color(0.45f, 0.3f, 0.16f)), MeshKit.Sphere, TRS(new Vector3(0.012f, 0.012f, 0.004f), new Vector3(0f, 0f, 30f), new Vector3(0.014f, 0.009f, 0.011f)));
            sg.Build("Stiel", sprout, owned);
            var leafMat = Mats.Get(Mats.Emissive, new Color(0.36f, 0.8f, 0.24f), new Color(0.07f, 0.2f, 0.035f), 0.45f);
            var leafMesh = Shape("Blatt", b => b.Sphere(new Vector3(0f, 0f, 0.022f), 0.022f, 14, 8, 0.2f));
            leafL = Leaf(sprout, leafMesh, leafMat, sp, 0f);
            leafR = Leaf(sprout, leafMesh, leafMat, sp, 180f);
            var tiny = new Geo();
            tiny.Mesh(leafMat, leafMesh, TRS(sp + new Vector3(0f, 0.003f, 0f), new Vector3(-55f, 90f, 0f), Vector3.one * 0.35f));
            tiny.Mesh(leafMat, leafMesh, TRS(sp + new Vector3(0f, 0.003f, 0f), new Vector3(-55f, -90f, 0f), Vector3.one * 0.35f));
            tiny.Build("Blattpaar", sprout, owned, false);

            // Lichtstrahl von oben durch die Lücke, Staub darin
            beamSpot = new GameObject("Strahl").transform;
            beamSpot.SetParent(macro, false);
            beamSpot.localPosition = new Vector3(0.35f, 2.9f, 3.1f);
            beam = beamSpot.gameObject.AddComponent<Light>();
            beam.type = LightType.Spot; beam.spotAngle = 14f; beam.range = 6f * MacroScale; beam.intensity = 3.4f; beam.color = new Color(1f, 0.9f, 0.72f);
            var target = sprout.localPosition + new Vector3(0f, 0.05f, 0f);
            beamSpot.localRotation = Quaternion.LookRotation(target - beamSpot.localPosition);
            var bs = NewGlows(macro, matSoftAdd, true);
            var bdir = (target - beamSpot.localPosition);
            for (int i = 0; i < 3; i++)
            {
                var gl = bs.Add(Vector3.Lerp(beamSpot.localPosition, target, 0.55f) + new Vector3(r.Range(-0.04f, 0.04f), 0f, r.Range(-0.04f, 0.04f)), 0.14f + i * 0.05f, new Color(1f, 0.9f, 0.7f, 0.07f));
                gl.Stretch = bdir.normalized * bdir.magnitude * 0.5f * MacroScale;
                beamShafts.Add(gl);
            }
            var motes = Drift("sprout", macro, matSoftAdd, Vector3.Lerp(beamSpot.localPosition, target, 0.7f), new Vector3(0.35f, 1.4f, 0.35f), 12f, new Vector2(0.0015f, 0.005f),
                new Color(1f, 0.9f, 0.7f, 0.5f), new Color(1f, 0.85f, 0.6f, 0.9f), new Vector3(0.004f, -0.006f, 0f), 220f * detail + 60f, 0.01f);
            motes.transform.localRotation = Quaternion.FromToRotation(Vector3.up, -bdir.normalized);
            var halo = NewGlows(macro, matSoftAdd);
            sproutHalo = halo.Add(sprout.localPosition + new Vector3(0f, 0.08f, 0f), 0.16f, new Color(0.7f, 1f, 0.55f, 0.16f));
            // Unschärfe-Lichter im Vordergrund der Nahaufnahme
            for (int i = 0; i < 14; i++)
            {
                var c = r.Chance(0.5f) ? new Color(1f, 0.85f, 0.6f, r.Range(0.07f, 0.14f)) : new Color(0.7f, 1f, 0.6f, r.Range(0.05f, 0.1f));
                bokeh.Add(halo.Add(new Vector3(r.Range(-0.12f, 0.24f), r.Range(0.02f, 0.3f), r.Range(-0.1f, -0.01f)), r.Range(0.02f, 0.05f), c));
            }
            mikoNear = RobotModel.Create(macro, "MIKO_Nah");
            mikoNear.transform.localPosition = new Vector3(2.2f, 0f, 0.6f);
        }

        /// <summary>true, wenn p der Nahaufnahme (Kamera → Keimling → MIKO dahinter) oder MIKOs Anfahrt im Weg läge.</summary>
        static bool MacroClear(Vector3 p, float r)
        {
            var q = new Vector2(p.x, p.z);
            return PathNear(q, q, new Vector2(0.2f, -0.45f), new Vector2(0.1f, 2.1f)) < r || PathNear(q, q, new Vector2(2.7f, 0.9f), new Vector2(0.6f, 0.45f)) < r * 0.8f;
        }

        Transform Leaf(Transform parent, Mesh mesh, Material mat, Vector3 at, float yaw)
        {
            var p = new GameObject("Keimblatt").transform;
            p.SetParent(parent, false);
            p.localPosition = at;
            p.localRotation = Quaternion.Euler(-30f, yaw, 0f);
            var go = new GameObject("Blatt");
            go.transform.SetParent(p, false);
            go.transform.localScale = new Vector3(1.1f, 1f, 1.5f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return p;
        }

        void AnimateSprout(float lt, float k)
        {
            bool close = lt >= 6.5f;
            var sd = new Vector3(-0.25f, 0.55f, -0.6f);
            var sproutTop = sprout != null ? sprout.localPosition + new Vector3(0f, 0.09f, 0f) : Vector3.zero;
            if (!close)
            {
                SetLook(0.35f, 0.15f, 0.12f / MacroScale, new Color(0.46f, 0.39f, 0.3f), new Color(0.36f, 0.32f, 0.3f), new Color(0.32f, 0.26f, 0.21f), new Color(0.12f, 0.1f, 0.08f),
                    sd, new Color(0.9f, 0.8f, 0.66f), 0.45f, 400f, 0.05f);
                float roll = M.Smooth(Mathf.Clamp01(lt / 3.2f));
                mikoNear.transform.localPosition = Vector3.Lerp(new Vector3(2.6f, 0f, 0.9f), new Vector3(0.62f, 0f, 0.45f), roll);
                var toSprout = macro.TransformPoint(sproutTop) - mikoNear.transform.position;
                toSprout.y = 0f;
                mikoNear.transform.rotation = Quaternion.Slerp(macro.rotation * Quaternion.Euler(0f, -90f, 0f), Quaternion.LookRotation(toSprout.normalized), roll);
                if (lt > 3.3f && Once("curious")) { mikoNear.Emote("curious"); AudioManager.Play("beep_curious", null, 0.6f); }
                mikoNear.Animate(Time.deltaTime, roll > 0f && roll < 1f ? 1.4f : 0f, 0.3f, "grab", lt > 4.5f, false, false, false, 0.1f, (macro.TransformPoint(sproutTop) - mikoNear.transform.position).normalized);
                float e = M.Smooth(lt / 6.5f);
                Cam(new Vector3(-1.1f + 0.35f * e, 0.55f - 0.12f * e, -1.9f + 0.55f * e) * 1f, new Vector3(0.25f, 0.25f - 0.08f * e, 0.2f), 40f, 0f, 0.16f);
                foreach (var b in bokeh) b.Gain = 0f;
            }
            else
            {
                float lc = lt - 6.5f;
                SetLook(0.35f, 0.15f, 0.3f / MacroScale, new Color(0.42f, 0.36f, 0.27f), new Color(0.34f, 0.3f, 0.28f), new Color(0.3f, 0.24f, 0.2f), new Color(0.11f, 0.09f, 0.07f),
                    sd, new Color(0.9f, 0.8f, 0.66f), 0.4f, 200f, 0.03f);
                mikoNear.transform.localPosition = new Vector3(0.1f, 0f, 2f);
                mikoNear.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                if (lt > 8.2f && Once("happy")) { mikoNear.Emote("happy"); AudioManager.Play("beep_happy", null, 0.55f); }
                var toSprout = (macro.TransformPoint(sproutTop) - mikoNear.transform.position).normalized;
                mikoNear.Animate(Time.deltaTime, 0f, 0.3f, "grab", false, false, false, false, 0.15f, toSprout);
                float e = M.Smooth(lc / 6.5f);
                // Keimling im unteren Drittel, dahinter – weich im Dunst – MIKOs leuchtende Augen
                Cam(new Vector3(0.2f - 0.07f * e, 0.045f, -0.3f + 0.1f * e), new Vector3(0.02f, 0.28f - 0.05f * e, 0.6f), 34f - 4f * e, 0f, 0.07f);
                for (int i = 0; i < bokeh.Count; i++)
                {
                    bokeh[i].Gain = Mathf.Clamp01(lc / 1.2f) * (0.7f + 0.3f * Mathf.Sin(t * 0.6f + i));
                    bokeh[i].Pos += new Vector3(0.0015f, 0.0006f * Mathf.Sin(t + i), 0f) * Time.deltaTime;
                }
                // die Keimblätter öffnen sich ein wenig
                float unfold = M.Smooth(Mathf.Clamp01(lc / 5f));
                if (leafL != null) { leafL.localRotation = Quaternion.Euler(Mathf.Lerp(-40f, -18f, unfold), 0f, 0f); leafR.localRotation = Quaternion.Euler(Mathf.Lerp(-40f, -18f, unfold), 180f, 0f); }
            }
            if (sprout != null) sprout.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.8f) * 1.5f, 0f, Mathf.Sin(t * 1.1f) * 2f);
            if (sproutHalo != null) sproutHalo.Gain = 0.8f + 0.2f * Mathf.Sin(t * 1.7f);
            camSpace = macro;
        }

        // ================================================================== 7) Aufbruch: MIKOs Schiff vor dem Planeten
        Transform shipShot, ship, planet, clouds;
        RobotModel mikoShip;
        Vector3 planetC;
        float planetR;
        Glow ring, limbGlow, trailGlow;
        readonly List<Glow> shipLights = new List<Glow>();
        SunFx sunSpace, sunSpaceFront;

        void BuildShip()
        {
            var s = Shot("ship", new Vector3(0f, 3000f, 8000f));
            shipShot = s;
            var r = new Rng(2107);
            planetC = new Vector3(0f, -2100f, 3400f);
            planetR = 2000f;
            planet = new GameObject("Planet").transform;
            planet.SetParent(s, false);
            planet.localPosition = planetC;
            planet.localRotation = Quaternion.Euler(18f, 40f, 12f);
            int seg = detail < 0.6f ? 64 : 96;
            var pm = Shape("PlanetKugel", b => b.Sphere(Vector3.zero, 1f, seg, seg / 2));
            var pgo = new GameObject("Oberflaeche");
            pgo.transform.SetParent(planet, false);
            pgo.transform.localScale = Vector3.one * planetR;
            pgo.AddComponent<MeshFilter>().sharedMesh = pm;
            pgo.AddComponent<MeshRenderer>().sharedMaterial = matPlanet;
            clouds = new GameObject("Wolken").transform;
            clouds.SetParent(planet, false);
            clouds.localScale = Vector3.one * planetR * 1.008f;
            clouds.gameObject.AddComponent<MeshFilter>().sharedMesh = pm;
            var cr = clouds.gameObject.AddComponent<MeshRenderer>();
            cr.sharedMaterial = matClouds;
            cr.shadowCastingMode = ShadowCastingMode.Off;
            var atm = NewGlows(s, matRing);
            ring = atm.Add(planetC, planetR * 2.4f, new Color(1f, 0.66f, 0.42f, 0.6f));
            var lg = NewGlows(s, matSoftAdd);
            limbGlow = lg.Add(Vector3.zero, 1600f, new Color(1f, 0.55f, 0.28f, 0.4f));

            // Sterne, Milchstraßenband, farbige Nebel
            var stars = NewGlows(s, matSoftAdd, false, true);
            var band = Quaternion.Euler(62f, 20f, 0f);
            int ns = (int)(2400 * Mathf.Max(0.6f, detail));
            for (int i = 0; i < ns; i++)
            {
                Vector3 d;
                if (i % 3 == 0) { float a = r.Range(0f, Mathf.PI * 2f); d = band * new Vector3(Mathf.Cos(a), r.Range(-0.12f, 0.12f), Mathf.Sin(a)); }
                else d = new Vector3(r.Range(-1f, 1f), r.Range(-1f, 1f), r.Range(-1f, 1f));
                if (d.sqrMagnitude < 1e-4f) continue;
                d.Normalize();
                var c = Color.Lerp(new Color(0.75f, 0.85f, 1f), new Color(1f, 0.85f, 0.7f), r.Next());
                c.a = r.Range(0.35f, 1f);
                stars.Add(d * 7500f, r.Range(6f, 20f) * (r.Chance(0.03f) ? 2.2f : 1f), c);
            }
            Color[] neb = { new Color(0.5f, 0.3f, 0.9f, 0.05f), new Color(0.2f, 0.5f, 0.9f, 0.05f), new Color(0.9f, 0.35f, 0.5f, 0.04f) };
            for (int i = 0; i < 9; i++)
            {
                var d = (band * new Vector3(Mathf.Cos(i * 0.7f), r.Range(-0.1f, 0.1f), Mathf.Sin(i * 0.7f))).normalized;
                if (d.y < -0.1f) d.y = -d.y;
                stars.Add(d * 7400f, r.Range(1200f, 2600f), neb[i % neb.Length]);
            }

            // Das Transportschiff mit MIKO in der Ladewiege
            ship = new GameObject("Schiff").transform;
            ship.SetParent(s, false);
            var sg = new Geo();
            var dark = Mats.Get(Mats.Metal, new Color(0.3f, 0.32f, 0.36f));
            var hullMesh = Shape("SchiffRumpf", b => b.Lathe(Vector3.zero, new[] { new Vector2(0f, -12f), new Vector2(2.6f, -11.5f), new Vector2(3.6f, -8f), new Vector2(3.8f, 2f), new Vector2(3f, 7f), new Vector2(1.6f, 10.5f), new Vector2(0f, 12f) }, 28));
            sg.Mesh(matShipHull, hullMesh, TR(Vector3.zero, new Vector3(90f, 0f, 0f)));
            for (int sd = -1; sd <= 1; sd += 2)
            {
                sg.Box(matShipHull, TR(new Vector3(sd * 6.5f, -0.4f, -3f), new Vector3(0f, sd * -18f, sd * -6f)), new Vector3(8f, 0.45f, 5f));
                sg.Box(dark, TR(new Vector3(sd * 10.2f, -0.9f, -4.6f), new Vector3(0f, 0f, 0f)), new Vector3(0.6f, 2.2f, 3.6f));
                sg.Mesh(dark, Shape("Gondel", b => b.CylinderZ(Vector3.zero, 1.2f, 7f, 16)), Matrix4x4.Translate(new Vector3(sd * 4.2f, -1.6f, -8f)));
            }
            sg.Mesh(dark, Shape("Haupttriebwerk", b => b.CylinderZ(Vector3.zero, 1.9f, 3f, 20)), Matrix4x4.Translate(new Vector3(0f, 0f, -12.5f)));
            sg.Box(Mats.Get(Mats.Metal, new Color(0.1f, 0.12f, 0.16f), null, 0.95f), TR(new Vector3(0f, 2.1f, 6.4f), new Vector3(-24f, 0f, 0f)), new Vector3(2.4f, 0.9f, 3.2f));
            // Ladewiege
            sg.Box(dark, new Vector3(0f, 3.75f, -2.2f), new Vector3(2.6f, 0.25f, 2.6f));
            for (int sd = -1; sd <= 1; sd += 2) sg.Box(dark, new Vector3(sd * 1.25f, 4.2f, -2.2f), new Vector3(0.15f, 0.8f, 2.6f));
            var nozzle = Mats.Get(Mats.Emissive, new Color(0.6f, 0.85f, 1f), new Color(2.5f, 3.4f, 4.5f));
            sg.Mesh(nozzle, Shape("Glut", b => b.CylinderZ(Vector3.zero, 1.5f, 0.2f, 20)), Matrix4x4.Translate(new Vector3(0f, 0f, -14.05f)));
            for (int sd = -1; sd <= 1; sd += 2) sg.Mesh(nozzle, Shape("GlutKlein", b => b.CylinderZ(Vector3.zero, 0.95f, 0.2f, 16)), Matrix4x4.Translate(new Vector3(sd * 4.2f, -1.6f, -11.55f)));
            BlockText(sg, Mats.Get(Mats.Opaque, new Color(0.12f, 0.18f, 0.35f)), "ZWEITE CHANCE", TR(new Vector3(3.62f, 0.4f, 0f), new Vector3(0f, -90f, 0f)), 0.12f, 0.05f, 0f, null);
            sg.Build("Rumpf", ship, owned);
            mikoShip = RobotModel.Create(ship, "MIKO_Schiff");
            mikoShip.transform.localPosition = new Vector3(0f, 3.88f, -2.2f);
            // Aufhelllicht (Cockpit-/Planetenschein), damit MIKO im Gegenlicht nicht absäuft
            var fill = new GameObject("Aufhelllicht").AddComponent<Light>();
            fill.transform.SetParent(ship, false);
            fill.transform.localPosition = new Vector3(3f, 7f, 3f);
            fill.type = LightType.Point; fill.range = 16f; fill.intensity = 1.3f; fill.color = new Color(0.62f, 0.72f, 1f);
            var eng = NewGlows(s, matSoftAdd);
            eng.Add(new Vector3(0f, 0f, -14.4f), 7f, new Color(0.6f, 0.85f, 1f, 0.9f), ship);
            trailGlow = eng.Add(new Vector3(0f, 0f, -16f), 26f, new Color(0.45f, 0.7f, 1f, 0.35f), ship);
            for (int sd = -1; sd <= 1; sd += 2) eng.Add(new Vector3(sd * 4.2f, -1.6f, -11.8f), 4f, new Color(0.6f, 0.85f, 1f, 0.8f), ship);
            shipLights.Add(eng.Add(new Vector3(-10.5f, -0.9f, -4.6f), 1.2f, new Color(1f, 0.2f, 0.15f, 0.9f), ship));
            shipLights.Add(eng.Add(new Vector3(10.5f, -0.9f, -4.6f), 1.2f, new Color(0.2f, 1f, 0.35f, 0.9f), ship));
            var trail = Fx("ship", ship, "Abgasstrahl", matPuffAdd, new Vector3(0f, 0f, -14.5f), Quaternion.Euler(0f, 180f, 0f));
            var tm = trail.main;
            tm.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            tm.startSpeed = new ParticleSystem.MinMaxCurve(8f, 14f);
            tm.startSize = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
            tm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.8f, 1f, 0.5f), new Color(0.8f, 0.9f, 1f, 0.35f));
            tm.maxParticles = 300;
            var te = trail.emission; te.rateOverTime = 90f;
            var ts = trail.shape; ts.enabled = true; ts.shapeType = ParticleSystemShapeType.Cone; ts.angle = 4f; ts.radius = 1.2f;
            Grow(trail, 3f);
            FadeInOut(trail, 0.05f, 0.3f);
            sunSpace = MakeSun(s, 7000f, 260f, 2600f, new Color(1f, 0.85f, 0.65f), 9000f);
            sunSpaceFront = MakeSun(s, 1500f, 0.01f, 900f, new Color(1f, 0.7f, 0.45f), 2600f);
        }

        void AnimateShip(float lt, float k)
        {
            // Sonne geht über dem Planetenrand auf: erst vom Rand verdeckt (nur Dämmerungssaum), dann frei – kurz vor dem Titel
            var toPlanet = planetC.normalized;
            float angR = Mathf.Asin(Mathf.Clamp01(planetR / planetC.magnitude));
            float limbElev = Mathf.Atan2(planetC.y, planetC.z) * Mathf.Rad2Deg + angR * Mathf.Rad2Deg;
            float rise = Mathf.Lerp(-3.8f, 2.4f, M.Smooth(Mathf.Clamp01((lt - 1f) / 7f))); // bricht bei ≈ 92 s über den Rand
            var sd = Quaternion.Euler(-(limbElev + rise), 9f, 0f) * Vector3.forward;
            float sep = Vector3.Angle(sd, toPlanet) * Mathf.Deg2Rad;
            float vis = Mathf.Clamp01((sep - angR + 0.004f) / 0.03f);
            // Umgebungslicht: schwarzer Himmel, von unten etwas warmes Planetenlicht
            SetLook(0.5f, 0f, 0f, Color.black, new Color(0.03f, 0.035f, 0.05f), new Color(0.05f, 0.045f, 0.05f), new Color(0.075f, 0.06f, 0.045f),
                shipShot.TransformDirection(sd), new Color(1f, 0.9f, 0.78f), 2.2f, 9500f, 0.3f);
            look.Space = true;
            if (sunSpace != null) { sunSpace.Dir = shipShot.TransformDirection(sd); sunSpace.Vis = 1f; }
            if (sunSpaceFront != null) { sunSpaceFront.Dir = shipShot.TransformDirection(sd); sunSpaceFront.Vis = vis * 0.8f; }
            // Atmosphärenring um die Silhouette und Dämmerungssaum an der Stelle, an der die Sonne aufgeht
            var cam = Camera.main;
            Vector3 camLocal = cam != null ? shipShot.InverseTransformPoint(cam.transform.position) : Vector3.zero;
            float d = (planetC - camLocal).magnitude;
            float rc = planetR / Mathf.Sqrt(Mathf.Max(0.01f, 1f - (planetR * planetR) / (d * d)));
            if (ring != null) { ring.Pos = planetC; ring.Size = rc * 2f / 0.82f; }
            if (limbGlow != null)
            {
                var toC = (planetC - camLocal).normalized;
                var side = sd - toC * Vector3.Dot(sd, toC);
                side = side.sqrMagnitude > 1e-6f ? side.normalized : Vector3.up;
                limbGlow.Pos = planetC + side * rc * 0.98f - toC * planetR * 0.3f;
                limbGlow.Gain = 0.5f + 0.5f * vis;
            }
            if (planet != null) planet.Rotate(0f, 0.6f * Time.deltaTime, 0f, Space.Self);
            if (clouds != null) clouds.Rotate(0f, 0.25f * Time.deltaTime, 0f, Space.Self);
            // Das Schiff beschleunigt Richtung Sonnenaufgang
            float z = 7f + 3f * lt + 5.5f * lt * lt;
            var pos = new Vector3(1.8f + z * 0.05f, -1f - z * 0.035f, z);
            var vel = new Vector3(0.05f, -0.035f, 1f);
            ship.localPosition = pos;
            ship.localRotation = Quaternion.LookRotation(vel) * Quaternion.Euler(0f, 0f, Mathf.Sin(lt * 0.7f) * 4f - 6f);
            mikoShip.Animate(Time.deltaTime, 0f, 0.3f, "grab", false, false, false, false, 0.3f, shipShot.TransformDirection(vel));
            if (lt > 0.4f && Once("whoosh")) AudioManager.Play("whoosh", null, 0.5f);
            foreach (var sl in shipLights) sl.Gain = Mathf.Repeat(t * 1.1f, 1f) < 0.15f ? 1f : 0.15f;
            if (trailGlow != null) trailGlow.Gain = 0.8f + 0.2f * Mathf.PerlinNoise(t * 12f, 0.5f);
            var miko = pos + ship.localRotation * new Vector3(0f, 4.6f, -2.2f);
            var vista = new Vector3(40f, -160f, 1500f);
            float e = M.Smooth(Mathf.Clamp01((lt - 1.4f) / 4.8f));
            Cam(new Vector3(3.4f, 2.3f, 0.8f + Mathf.Min(lt, 2f) * 2.5f), Vector3.Lerp(miko, vista, e), Mathf.Lerp(46f, 40f, e), -4f * (1f - e), 0.12f);
        }

        // ================================================================== Einstellungen abspielen
        void AnimateShot(string id, float lt, float k)
        {
            camKick = 0f;
            Transform sh;
            camSpace = shots.TryGetValue(id, out sh) ? sh : stage;
            switch (id)
            {
                case "skyline": AnimateSkyline(lt, k); break;
                case "megastore": AnimateMegastore(lt, k); break;
                case "arks": AnimateArks(lt, k); break;
                case "shutdown": AnimateShutdown(lt, k); break;
                case "home": AnimateHome(lt, k); break;
                case "sprout": AnimateSprout(lt, k); break;
                case "ship": AnimateShip(lt, k); break;
            }
        }

        // ================================================================== Prozedurale Texturen
        static float H01(int x, int y, int s) { return MeshBuilder.Hash01(x, y, s); }
        static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }

        static float Noise(float x, float y, int seed, int period = 0)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            int x0 = xi, x1 = xi + 1, y0 = yi, y1 = yi + 1;
            if (period > 0) { x0 = Mod(x0, period); x1 = Mod(x1, period); y0 = Mod(y0, period); y1 = Mod(y1, period); }
            float a = H01(x0, y0, seed), b = H01(x1, y0, seed), c = H01(x0, y1, seed), d = H01(x1, y1, seed);
            float u = fx * fx * (3f - 2f * fx), v = fy * fy * (3f - 2f * fy);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        static float Fbm(float x, float y, int seed, int oct, int period = 0)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < oct; i++)
            {
                sum += Noise(x, y, seed + i * 17, period) * amp;
                norm += amp; x *= 2f; y *= 2f; amp *= 0.5f;
                if (period > 0) period *= 2;
            }
            return sum / norm;
        }

        static float Noise3(Vector3 p, int seed)
        {
            int xi = Mathf.FloorToInt(p.x), yi = Mathf.FloorToInt(p.y), zi = Mathf.FloorToInt(p.z);
            float fx = p.x - xi, fy = p.y - yi, fz = p.z - zi;
            float u = fx * fx * (3f - 2f * fx), v = fy * fy * (3f - 2f * fy), w = fz * fz * (3f - 2f * fz);
            float c000 = H01(xi, yi, seed + zi * 1013), c100 = H01(xi + 1, yi, seed + zi * 1013), c010 = H01(xi, yi + 1, seed + zi * 1013), c110 = H01(xi + 1, yi + 1, seed + zi * 1013);
            float c001 = H01(xi, yi, seed + (zi + 1) * 1013), c101 = H01(xi + 1, yi, seed + (zi + 1) * 1013), c011 = H01(xi, yi + 1, seed + (zi + 1) * 1013), c111 = H01(xi + 1, yi + 1, seed + (zi + 1) * 1013);
            float a = Mathf.Lerp(Mathf.Lerp(c000, c100, u), Mathf.Lerp(c010, c110, u), v);
            float b = Mathf.Lerp(Mathf.Lerp(c001, c101, u), Mathf.Lerp(c011, c111, u), v);
            return Mathf.Lerp(a, b, w);
        }

        static float Fbm3(Vector3 p, int seed, int oct)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < oct; i++) { sum += Noise3(p, seed + i * 31) * amp; norm += amp; p *= 2.03f; amp *= 0.5f; }
            return sum / norm;
        }

        Texture2D NewTex(string name, int w, int h, bool repeat, Color32[] px, bool mips = true)
        {
            var tx = new Texture2D(w, h, TextureFormat.RGBA32, mips)
            {
                name = "Intro_" + name,
                wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear,
                anisoLevel = 4
            };
            tx.SetPixels32(px);
            tx.Apply(mips, true);
            owned.Add(tx);
            return tx;
        }

        static Color32 C32(Color c) { c.a = Mathf.Clamp01(c.a); return (Color32)new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), c.a); }

        void MakeTextures()
        {
            texBale = BaleAtlas();
            MakeFacade(out texFacade, out texFacadeLit);
            texGround = GroundTexture();
            texGrime = GrimeTexture();
            texPanel = PanelTexture();
            texSoft = SoftTexture();
            texPuff = PuffTexture();
            texRing = RingTexture();
            MakePlanet(out texPlanet, out texClouds);
            texVignette = VignetteTexture();
            texGrain = GrainTexture();
        }

        /// <summary>16 gepresste Müllballen (4×4, je 128 px): Schichten zerknautschter Teile, Fugen, Stahlbänder, Staub.</summary>
        Texture2D BaleAtlas()
        {
            const int N = 512, B = 128;
            var px = new Color32[N * N];
            Color[][] pals =
            {
                new[] { new Color(0.62f, 0.4f, 0.24f), new Color(0.35f, 0.42f, 0.52f), new Color(0.7f, 0.66f, 0.58f), new Color(0.48f, 0.46f, 0.44f), new Color(0.66f, 0.28f, 0.2f), new Color(0.3f, 0.44f, 0.3f), new Color(0.78f, 0.72f, 0.52f) },
                new[] { new Color(0.52f, 0.5f, 0.48f), new Color(0.62f, 0.38f, 0.22f), new Color(0.4f, 0.38f, 0.37f), new Color(0.7f, 0.52f, 0.34f), new Color(0.3f, 0.29f, 0.29f) },
                new[] { new Color(0.74f, 0.62f, 0.44f), new Color(0.8f, 0.77f, 0.7f), new Color(0.6f, 0.47f, 0.32f), new Color(0.52f, 0.4f, 0.28f), new Color(0.7f, 0.7f, 0.66f) },
                new[] { new Color(0.36f, 0.5f, 0.66f), new Color(0.72f, 0.32f, 0.26f), new Color(0.8f, 0.78f, 0.72f), new Color(0.4f, 0.58f, 0.4f), new Color(0.82f, 0.66f, 0.3f), new Color(0.5f, 0.5f, 0.52f) },
            };
            var dust = new Color(0.62f, 0.52f, 0.4f);
            var strapCol = new Color(0.34f, 0.33f, 0.32f);
            for (int b = 0; b < 16; b++)
            {
                int bx = (b % 4) * B, by = (b / 4) * B;
                var pal = pals[b % pals.Length];
                float dustAmt = 0.12f + H01(b, 5, 91) * 0.3f;
                float rowH = 5f + H01(b, 7, 3) * 5f;
                float s1 = 34f + H01(b, 8, 3) * 6f, s2 = 90f + H01(b, 9, 3) * 6f;
                for (int y = 0; y < B; y++)
                {
                    float wob = (Noise(y * 0.08f, b * 3.1f, 17) - 0.5f) * 5f;
                    for (int x = 0; x < B; x++)
                    {
                        float n1 = Noise(x * 0.12f, y * 0.12f, 31 + b);
                        float fy = y + (n1 - 0.5f) * 6f;
                        int row = Mathf.FloorToInt(fy / rowH);
                        float fr = fy / rowH - row;
                        float segLen = 8f + H01(row, b, 11) * 22f;
                        float fx = x + wob + H01(row, b, 13) * 40f + (Noise(x * 0.2f, y * 0.05f, 51 + b) - 0.5f) * 6f;
                        int sgi = Mathf.FloorToInt(fx / segLen);
                        float fs = fx / segLen - sgi;
                        var col = pal[(int)(H01(sgi, row + b * 57, 19) * pal.Length) % pal.Length];
                        float shade = 0.7f + 0.45f * Fbm(x * 0.09f, y * 0.09f, 71 + b, 3);
                        float edge = Mathf.Min(Mathf.Min(fr, 1f - fr) * rowH, Mathf.Min(fs, 1f - fs) * segLen);
                        shade *= Mathf.Lerp(0.42f, 1f, Mathf.Clamp01(edge / 1.6f));
                        col = Color.Lerp(col, dust, dustAmt * (0.6f + 0.8f * Noise(x * 0.05f, y * 0.05f, 97 + b))) * shade;
                        float strap = Mathf.Min(Mathf.Abs(x - s1), Mathf.Abs(x - s2));
                        if (strap < 2.3f) col = Color.Lerp(strapCol, col, 0.2f) * (strap < 1f ? 1.12f : 0.82f);
                        float border = Mathf.Min(Mathf.Min(x, B - 1 - x), Mathf.Min(y, B - 1 - y));
                        col *= Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(border / 5f));
                        col.a = 1f;
                        px[(by + y) * N + bx + x] = C32(col);
                    }
                }
            }
            return NewTex("Ballen", N, N, true, px);
        }

        /// <summary>Hochhausfassade (8 × 8 Fenster je Kachel = 32 m × 28 m) und passende Leuchtkarte für erleuchtete Fenster.</summary>
        void MakeFacade(out Texture2D albedo, out Texture2D lit)
        {
            const int N = 256, C = 32;
            var px = new Color32[N * N];
            var em = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int cx = x / C, cy = y / C, lx = x % C, ly = y % C;
                    float n = Fbm(x / 32f, y / 32f, 5, 4, 8);
                    var concrete = Color.Lerp(new Color(0.46f, 0.43f, 0.39f), new Color(0.64f, 0.6f, 0.54f), n);
                    float streak = Noise(x * 0.45f, y * 0.025f, 9, 0);
                    concrete *= 0.82f + 0.25f * streak;
                    Color col = concrete, e = Color.black;
                    bool win = lx >= 6 && lx < 26 && ly >= 9 && ly < 27;
                    if (win)
                    {
                        float kind = H01(cx, cy, 5);
                        float gy = (ly - 9) / 18f;
                        if (kind < 0.5f) col = Color.Lerp(new Color(0.05f, 0.06f, 0.07f), new Color(0.18f, 0.2f, 0.22f), gy * 0.8f + (lx - 6) / 60f);
                        else if (kind < 0.72f) col = new Color(0.02f, 0.02f, 0.02f);
                        else if (kind < 0.84f) col = new Color(0.34f, 0.25f, 0.17f) * (0.8f + 0.3f * ((ly / 3) % 2));
                        else col = Color.Lerp(new Color(0.28f, 0.31f, 0.34f), new Color(0.45f, 0.48f, 0.5f), gy);
                        if (H01(cx, cy, 77) < 0.5f)
                        {
                            float warm = 0.55f + 0.45f * H01(cx, cy, 78);
                            e = Color.Lerp(new Color(1f, 0.78f, 0.45f), new Color(0.85f, 0.9f, 1f), H01(cx, cy, 79) < 0.2f ? 1f : 0f) * warm * (0.7f + 0.3f * gy);
                        }
                    }
                    else
                    {
                        if (ly < 3) col *= 0.72f;
                        if (lx >= 8 && lx < 24 && ly < 9) col *= 0.8f + 0.2f * Noise(x * 0.6f, y * 0.1f, 13);
                    }
                    col.a = 1f; e.a = 1f;
                    px[y * N + x] = C32(col);
                    em[y * N + x] = C32(e);
                }
            albedo = NewTex("Fassade", N, N, true, px);
            lit = NewTex("FassadeLicht", N, N, true, em);
        }

        /// <summary>Verdichteter Müllboden (Kachel 256 px): Staub, Risse, bunte Splitter.</summary>
        Texture2D GroundTexture()
        {
            const int N = 256;
            var px = new Color32[N * N];
            var cols = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float n = Fbm(x / 32f, y / 32f, 3, 5, 8);
                    float ridge = 1f - Mathf.Abs(Fbm(x / 21.33f, y / 21.33f, 4, 3, 12) * 2f - 1f);
                    var c = Color.Lerp(new Color(0.4f, 0.32f, 0.24f), new Color(0.6f, 0.5f, 0.37f), n);
                    c *= 1f - Mathf.Pow(ridge, 10f) * 0.45f;
                    cols[y * N + x] = c;
                }
            var r = new Rng(77);
            for (int i = 0; i < 700; i++)
            {
                int cx = r.Range(0, N), cy = r.Range(0, N), w = r.Range(1, 6), h = r.Range(1, 4);
                var c = JunkColors[r.Range(0, JunkColors.Length)] * r.Range(0.75f, 1.15f);
                for (int yy = 0; yy < h; yy++)
                    for (int xx = 0; xx < w; xx++)
                    {
                        int idx = Mod(cy + yy, N) * N + Mod(cx + xx, N);
                        cols[idx] = Color.Lerp(cols[idx], c, 0.75f);
                    }
            }
            for (int i = 0; i < cols.Length; i++) { var c = cols[i]; c.a = 1f; px[i] = C32(c); }
            return NewTex("Boden", N, N, true, px);
        }

        /// <summary>Schmutz/Rost/Beton (Kachel 128 px), wird eingefärbt.</summary>
        Texture2D GrimeTexture()
        {
            const int N = 128;
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float n = Fbm(x / 16f, y / 16f, 91, 5, 8);
                    float sp = Noise(x * 0.5f, y * 0.5f, 92, 64);
                    float streak = Noise(x * 0.3f, y * 0.04f, 93, 0);
                    float v = 0.62f + 0.45f * n - (sp > 0.82f ? 0.25f : 0f) - streak * 0.12f;
                    px[y * N + x] = C32(new Color(v, v * 0.97f, v * 0.93f, 1f));
                }
            return NewTex("Schmutz", N, N, true, px);
        }

        /// <summary>Rumpfplatten mit Fugen und Lüftungsgittern (Kachel 256 px).</summary>
        Texture2D PanelTexture()
        {
            const int N = 256;
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int px0 = x / 32, py0 = y / 16;
                    int span = H01(px0, py0, 5) < 0.3f ? 2 : 1;
                    int ex = x % (32 * span);
                    float v = 0.8f + 0.12f * H01(px0 / span, py0, 7) + 0.05f * Noise(x * 0.1f, y * 0.1f, 8);
                    if ((x % 32) == 0 && span == 1 || (y % 16) == 0 || ex == 0) v *= 0.55f;
                    if (H01(px0, py0, 9) < 0.08f && (x % 32) > 6 && (x % 32) < 26 && (y % 16) > 4 && (y % 16) < 12 && (y % 2) == 0) v *= 0.45f;
                    px[y * N + x] = C32(new Color(v, v, v * 1.02f, 1f));
                }
            return NewTex("Platten", N, N, true, px);
        }

        Texture2D SoftTexture()
        {
            const int N = 64;
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Clamp01(1f - (dx * dx + dy * dy));
                    float a = d * d * (0.4f + 0.6f * d);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            return NewTex("Weich", N, N, false, px);
        }

        Texture2D PuffTexture()
        {
            const int N = 128;
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float n = Fbm(x / 20f, y / 20f, 121, 4);
                    float a = Mathf.Clamp01((1f - d) * 1.6f - (1f - n) * 0.7f);
                    a = a * a * (3f - 2f * a);
                    float shade = 0.82f + 0.18f * ((y / (float)N) * 0.6f + n * 0.4f);
                    px[y * N + x] = C32(new Color(shade, shade, shade, a));
                }
            return NewTex("Wolke", N, N, false, px);
        }

        Texture2D RingTexture()
        {
            const int N = 256;
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = d < 0.82f ? Mathf.Exp(-Mathf.Pow((d - 0.82f) / 0.035f, 2f)) : Mathf.Exp(-Mathf.Pow((d - 0.82f) / 0.075f, 2f));
                    a *= Mathf.Clamp01((1f - d) / 0.05f);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            return NewTex("Ring", N, N, false, px);
        }

        /// <summary>Die vermüllte Erde von oben: ockerbraune Kontinente, trübe Meere, Staubstürme – und Wolkenschicht mit Alpha.</summary>
        void MakePlanet(out Texture2D surface, out Texture2D cloudTex)
        {
            int W = detail < 0.6f ? 512 : 768, H = W / 2;
            var px = new Color32[W * H];
            var cl = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                float lat = (y + 0.5f) / H * Mathf.PI - Mathf.PI * 0.5f;
                float cl0 = Mathf.Cos(lat), sl = Mathf.Sin(lat);
                for (int x = 0; x < W; x++)
                {
                    float lon = (x + 0.5f) / W * Mathf.PI * 2f;
                    var p = new Vector3(cl0 * Mathf.Cos(lon), sl, cl0 * Mathf.Sin(lon));
                    float cont = Fbm3(p * 1.6f + new Vector3(10f, 10f, 10f), 7, 4);
                    float detailN = Fbm3(p * 7f + new Vector3(3f, 3f, 3f), 8, 3);
                    Color c;
                    if (cont < 0.47f)
                    {
                        float depth = Mathf.Clamp01((0.47f - cont) / 0.12f);
                        c = Color.Lerp(new Color(0.3f, 0.34f, 0.32f), new Color(0.16f, 0.2f, 0.24f), depth);
                    }
                    else
                    {
                        float hgt = Mathf.Clamp01((cont - 0.47f) / 0.25f);
                        c = Color.Lerp(new Color(0.62f, 0.5f, 0.34f), new Color(0.46f, 0.36f, 0.26f), hgt);
                        c = Color.Lerp(c, new Color(0.72f, 0.62f, 0.46f), Mathf.Clamp01((detailN - 0.55f) * 3f));
                        c *= 0.85f + 0.3f * detailN;
                    }
                    float pole = Mathf.Clamp01((Mathf.Abs(sl) - 0.86f) / 0.08f);
                    c = Color.Lerp(c, new Color(0.78f, 0.76f, 0.72f), pole * 0.8f);
                    c.a = 1f;
                    px[y * W + x] = C32(c);
                    float cn = Fbm3(p * 3.2f + new Vector3(1f, 5f, 2f) + new Vector3(Mathf.Sin(lat * 6f) * 0.3f, 0f, 0f), 9, 4);
                    float ca = Mathf.Clamp01((cn - 0.52f) * 3.2f);
                    float band = Mathf.Clamp01(1f - Mathf.Abs(sl - 0.25f) / 0.2f);
                    float dustBand = band > 0f ? band * Mathf.Clamp01((Fbm3(p * 5f, 10, 2) - 0.45f) * 3f) : 0f;
                    var ccol = Color.Lerp(new Color(0.95f, 0.93f, 0.9f), new Color(0.85f, 0.7f, 0.5f), dustBand);
                    ccol.a = Mathf.Clamp01(ca * 0.85f + dustBand * 0.45f);
                    cl[y * W + x] = C32(ccol);
                }
            }
            surface = NewTex("Planet", W, H, false, px);
            surface.wrapModeU = TextureWrapMode.Repeat;
            cloudTex = NewTex("Wolkenschicht", W, H, false, cl);
            cloudTex.wrapModeU = TextureWrapMode.Repeat;
        }

        Texture2D VignetteTexture()
        {
            const int N = 128;
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 1.1f);
                    float a = Mathf.SmoothStep(0f, 1f, (d - 0.55f) / 0.75f);
                    px[y * N + x] = new Color32(0, 0, 0, (byte)(Mathf.Clamp01(a) * 255f));
                }
            return NewTex("Vignette", N, N, false, px, false);
        }

        Texture2D GrainTexture()
        {
            const int N = 128;
            var px = new Color32[N * N];
            var r = new Rng(99);
            for (int i = 0; i < px.Length; i++) { byte v = (byte)(r.Next() * 255f); px[i] = new Color32(v, v, v, 255); }
            var tx = NewTex("Korn", N, N, true, px, false);
            tx.filterMode = FilterMode.Point;
            return tx;
        }

        // ================================================================== Bildschirm: Kinobalken, Blenden, Vignette, Korn, Titel, Untertitel
        void OnGUI()
        {
            if (!playing) return;
            GUI.depth = -500;
            if (pendingBuild)
            {
                // Bühnenaufbau steht bevor: schwarzes Bild mit Hinweis (bleibt während des Aufbaus stehen)
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                float sc = Screen.height / 1080f * (GameApp.I != null && GameApp.I.Settings != null ? GameApp.I.Settings.TextScale : 1f);
                var ps = new GUIStyle(GUI.skin.label) { fontSize = (int)(26 * sc), alignment = TextAnchor.MiddleCenter };
                GUI.color = new Color(1f, 1f, 1f, 0.55f);
                GUI.Label(new Rect(0, Screen.height * 0.5f - 20 * sc, Screen.width, 40 * sc), Loc.T("Intro wird vorbereitet …"), ps);
                GUI.color = Color.white;
                if (Event.current.type == EventType.Repaint) blackShown = true;
                return;
            }
            float bar = Screen.height * 0.1f;
            // Vignette und feines Filmkorn (unter den Balken)
            if (texVignette != null)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.62f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), texVignette, ScaleMode.StretchToFill, true);
            }
            if (texGrain != null && Event.current.type == EventType.Repaint)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.035f);
                float ox = UnityEngine.Random.value, oy = UnityEngine.Random.value;
                GUI.DrawTextureWithTexCoords(new Rect(0, 0, Screen.width, Screen.height), texGrain, new Rect(ox, oy, Screen.width / 256f, Screen.height / 256f), true);
            }
            // Kinobalken
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, bar), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, Screen.height - bar, Screen.width, bar), Texture2D.whiteTexture);
            // Überblendungen am Anfang/Ende jeder Einstellung
            foreach (var s in IntroTimeline.Shots)
            {
                if (t < s.Start || t >= s.End) continue;
                float fade = Mathf.Max(Mathf.Clamp01(1f - (t - s.Start) / 0.8f), Mathf.Clamp01(1f - (s.End - t) / 0.8f));
                if (s.Id == "ship" && t > s.End - 0.8f) fade = 0;
                if (fade > 0.01f) { GUI.color = new Color(0, 0, 0, fade); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); }
            }
            if (t >= IntroTimeline.Total) { GUI.color = new Color(0, 0, 0, Mathf.Clamp01((t - IntroTimeline.Total) / 1.5f)); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); }
            float scale = Screen.height / 1080f * (GameApp.I != null && GameApp.I.Settings != null ? GameApp.I.Settings.TextScale : 1f);
            // Titel
            if (t > 93f)
            {
                float a = Mathf.Clamp01((t - 93f) / 2f) * Mathf.Clamp01((IntroTimeline.Total + 1.5f - t) / 1.5f);
                var ts = new GUIStyle(GUI.skin.label) { fontSize = (int)(110 * scale), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                float rise = (1f - a) * 12f * scale;
                GUI.color = new Color(0, 0, 0, a * 0.5f);
                GUI.Label(new Rect(4, Screen.height * 0.3f + 4 + rise, Screen.width, 160 * scale), "RE:PLANET", ts);
                GUI.color = new Color(0.55f, 1f, 0.95f, a);
                GUI.Label(new Rect(0, Screen.height * 0.3f + rise, Screen.width, 160 * scale), "RE:PLANET", ts);
                var ss = new GUIStyle(ts) { fontSize = (int)(44 * scale), fontStyle = FontStyle.Normal };
                float a2 = Mathf.Clamp01((t - 94.2f) / 2f) * Mathf.Clamp01((IntroTimeline.Total + 1.5f - t) / 1.5f);
                GUI.color = new Color(1f, 0.75f, 0.45f, a2);
                GUI.Label(new Rect(0, Screen.height * 0.3f + 150 * scale, Screen.width, 70 * scale), Loc.T("Eine zweite Chance"), ss);
            }
            // Untertitel (ohne Aufnahme immer sichtbar, weil sie die Geschichte erzählen)
            if (!string.IsNullOrEmpty(Hud.Subtitle))
            {
                var st = new GUIStyle(GUI.skin.label) { fontSize = (int)(34 * scale), alignment = TextAnchor.MiddleCenter, wordWrap = true };
                var r = new Rect(Screen.width * 0.1f, Screen.height - bar - 110 * scale, Screen.width * 0.8f, 100 * scale);
                GUI.color = new Color(0, 0, 0, 0.8f);
                GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), Hud.Subtitle, st);
                GUI.color = Color.white;
                GUI.Label(r, Hud.Subtitle, st);
            }
            // Musik wird beim allerersten Start noch erzeugt
            if (!freeRun && t <= 0f && startDelay > 0.6f)
            {
                var ls = new GUIStyle(GUI.skin.label) { fontSize = (int)(24 * scale), alignment = TextAnchor.MiddleCenter };
                GUI.color = new Color(1, 1, 1, 0.5f + 0.3f * Mathf.Sin(Time.unscaledTime * 3f));
                GUI.Label(new Rect(0, Screen.height * 0.5f - 20 * scale, Screen.width, 40 * scale), Loc.T("Musik wird vorbereitet …"), ls);
            }
            // Überspringen-Hinweis
            var hs = new GUIStyle(GUI.skin.label) { fontSize = (int)(20 * scale), alignment = TextAnchor.MiddleRight };
            GUI.color = new Color(1, 1, 1, 0.6f + (skipHold > 0 ? 0.4f : 0));
            GUI.Label(new Rect(0, Screen.height - bar + 10 * scale, Screen.width - 30, 30 * scale), skipHold > 0 ? Loc.F("Überspringen … {0} %", (int)(skipHold * 100)) : Loc.T("Gedrückt halten zum Überspringen"), hs);
            GUI.color = Color.white;
        }
    }
}
