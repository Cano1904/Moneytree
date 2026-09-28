using System;
using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Terrain = RePlanet.Core.Terrain;

namespace RePlanet
{
    /// <summary>
    /// Anflugszenen im Spiel (reine Darstellung, keine Spiellogik):
    /// <list type="bullet">
    /// <item><b>Schrottlieferung</b> (Effekt „delivery“): Ein großer Containerfrachter im Stil der Intro-Archen kommt aus dem Himmel,
    /// schwebt über dem Abladeplatz, fährt die Landebeine aus, setzt auf, öffnet die Bodenklappen der Container, die Teile
    /// fallen heraus (erst dann werden die echten Lieferobjekte im <see cref="TrashRenderer"/> sichtbar), schließt und startet wieder.</item>
    /// <item><b>Ankunft</b> (Effekt „arrive“ beim Reisen) und <b>Start eines neuen Spiels</b>: Das eigene Transportschiff
    /// („TransportShip“ unter <c>WorldView.I.Root</c>) fliegt ein und landet auf dem Landeplatz. Das statische Modell ist
    /// währenddessen ausgeblendet. Optional eine kurze Kamerafahrt (höchstens 6 s, mit beliebiger Taste überspringbar).</item>
    /// </list>
    /// Die Effekte gehen an alle Clients – im Koop sieht deshalb jeder die Szene. Im Solo-Pausenmenü steht die Szene still.
    /// Meldet sich selbst bei <see cref="GameApp.Components"/> an.
    /// </summary>
    public class ShipArrival : MonoBehaviour
    {
        public static ShipArrival I { get; private set; }

        /// <summary>Läuft gerade eine Kamerafahrt dieser Komponente? (Das HUD blendet sich dann aus.)</summary>
        public static bool CinematicActive { get { return I != null && I.cinematic; } }

        /// <summary>Kamerafahrt bei Landungen erlauben.</summary>
        public static bool AllowCinematic = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterComponent()
        {
            if (!GameApp.Components.Contains(typeof(ShipArrival))) GameApp.Components.Add(typeof(ShipArrival));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureComponent()
        {
            // Falls GameApp bereits ohne diese Komponente erzeugt wurde (Reihenfolge der Startmethoden ist nicht festgelegt)
            if (GameApp.I != null && GameApp.I.GetComponent<ShipArrival>() == null) GameApp.I.gameObject.AddComponent<ShipArrival>();
        }

        enum Scene { None, Delivery, Landing }

        sealed class Engine
        {
            public Transform Flame;
            public ParticleSystem Plume;
            public float Radius, Length;
            public float Seed;
        }

        sealed class Drop
        {
            public string Id;
            public TrashType T;
            public Vector3 From, To;
            public Quaternion Rot;
            public float Scale, Start, Dur, Spin;
            public Vector3 SpinAxis;
            public bool Landed;
        }

        Scene scene;
        float t;
        string scenePlanet;
        Vector3 ground;          // Aufsetzpunkt (Frachter: Mitte Abladeplatz; Landung: Drehpunkt des Schiffsmodells)
        Quaternion yaw = Quaternion.identity;

        // Frachter
        Transform freighter;
        readonly List<Transform> flaps = new List<Transform>();
        readonly List<float> flapSign = new List<float>();
        readonly List<Transform> legs = new List<Transform>();
        readonly List<Vector3> legBase = new List<Vector3>();
        readonly List<Engine> fLift = new List<Engine>(), fMain = new List<Engine>();
        readonly List<Vector3> containerBottoms = new List<Vector3>();
        Light[] fSpots;
        Light fEngineLight;
        Material beaconRed, beaconGreen, beaconWhite, nozzleMat, floodMat;
        readonly List<Drop> drops = new List<Drop>();
        bool touchedDown, flapsOpenedSound, liftoffSound;

        // Eigenes Transportschiff
        Transform landPivot;
        GameObject landClone, hiddenOriginal;
        readonly List<Engine> lLift = new List<Engine>(), lMain = new List<Engine>();
        Light lEngineLight;
        Vector3 landStart;

        // Staub und Material
        ParticleSystem dust;
        static Texture2D softTex, flameTex;
        Material flameMat, flameCoreMat, plumeMat, dustMat;

        // Ablauf
        bool pendingLanding, pendingCinematic;
        int pendingFrames;
        bool newGameLoading;
        AppMode lastMode = AppMode.Menu;

        // Kamerafahrt
        bool cinematic;
        float cineT, cineDur;
        Vector3 camA, camB, camLook;

        const float DeliveryHide = 9.5f;

        void Awake()
        {
            if (I != null && I != this) { Destroy(this); return; }
            I = this;
        }

        void Start()
        {
            var app = GameApp.I;
            if (app == null) return;
            app.OnFx += OnFx;
            app.OnSessionStarted += OnSessionStarted;
            app.OnSessionEnded += () => Abort();
            app.OnPlanetChanged += p => { if (scene != Scene.None && p != scenePlanet) Abort(); };
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            TrashRenderer.HideDeliveriesUntil = -1f;
            TrashRenderer.RevealedDeliveries.Clear();
        }

        // ================================================================== Auslöser
        void OnFx(JObj f)
        {
            try
            {
                switch (f.Str("k"))
                {
                    case "delivery": StartDelivery(); break;
                    case "arrive": QueueLanding(true); break;
                }
            }
            catch (Exception e) { Debug.LogException(e); Abort(); }
        }

        void OnSessionStarted()
        {
            // Neues Spiel (auch wenn Laden und Start in dasselbe Bild fallen: dann steht lastMode noch auf der Planetenwahl)
            if (newGameLoading || lastMode == AppMode.PlanetSelect || lastMode == AppMode.Intro) QueueLanding(true);
            newGameLoading = false;
        }

        void QueueLanding(bool withCamera)
        {
            pendingLanding = true;
            pendingCinematic = withCamera;
            pendingFrames = 2; // WorldView baut die Welt im selben Ereignis – ein, zwei Bilder abwarten
        }

        /// <summary>Spielt die Landung des eigenen Transportschiffs (z. B. nach der Planetenwahl). Darf jederzeit aufgerufen werden.</summary>
        public void PlayLanding(bool withCamera) { QueueLanding(withCamera); }

        // ================================================================== Schleife
        void Update()
        {
            var app = GameApp.I;
            if (app == null) return;
            // Neues Spiel erkennen: Planetenwahl → Laden → Spielen
            if (app.Mode != lastMode)
            {
                if (app.Mode == AppMode.Loading) newGameLoading = lastMode == AppMode.PlanetSelect || lastMode == AppMode.Intro;
                else if (app.Mode == AppMode.Menu) newGameLoading = false;
                lastMode = app.Mode;
            }
            if (!app.InGame) { if (scene != Scene.None) Abort(); pendingLanding = false; return; }

            if (pendingLanding && --pendingFrames <= 0)
            {
                pendingLanding = false;
                try { StartLanding(pendingCinematic); }
                catch (Exception e) { Debug.LogException(e); Abort(); }
            }
            if (scene == Scene.None) return;

            float dt = app.Paused ? 0f : Mathf.Min(Time.deltaTime, 0.1f);
            // Während der Pause steht die Szene – die Lieferung bleibt so lange verborgen
            if (app.Paused && scene == Scene.Delivery && TrashRenderer.HideDeliveriesUntil > 0f) TrashRenderer.HideDeliveriesUntil += Time.deltaTime;
            t += dt;
            try
            {
                if (scene == Scene.Delivery) AnimateDelivery(dt);
                else AnimateLanding(dt);
            }
            catch (Exception e) { Debug.LogException(e); Abort(); }
        }

        void LateUpdate()
        {
            if (!cinematic) return;
            var rig = CameraRig.I;
            var app = GameApp.I;
            if (rig == null || rig.Cam == null || app == null || !app.InGame) { EndCinematic(); return; }
            if (!app.Paused) cineT += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            bool skip = cineT > 0.35f && (Input.anyKeyDown || InputMap.NavBack());
            if (skip || cineT >= cineDur || scene != Scene.Landing || UIState.Screen != UIScreen.None || PhotoMode.Active) { EndCinematic(); return; }
            float e = Smooth(Mathf.Clamp01(cineT / cineDur));
            var pos = Vector3.Lerp(camA, camB, e);
            var target = landPivot != null ? landPivot.position + Vector3.up * 3f : camLook;
            camLook = Vector3.Lerp(camLook, target, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            rig.Cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation((camLook - pos).sqrMagnitude > 1e-4f ? camLook - pos : Vector3.forward));
        }

        void EndCinematic()
        {
            if (!cinematic) return;
            cinematic = false;
            if (CameraRig.I != null) CameraRig.I.Cinematic = false;
        }

        /// <summary>Bricht jede laufende Szene ab und stellt den Normalzustand her (Schiff sichtbar, Lieferung sichtbar, Kamera frei).</summary>
        public void Abort()
        {
            EndCinematic();
            TrashRenderer.HideDeliveriesUntil = -1f;
            TrashRenderer.RevealedDeliveries.Clear();
            TrashRenderer.RefreshSoon();
            drops.Clear();
            if (hiddenOriginal != null) hiddenOriginal.SetActive(true);
            hiddenOriginal = null;
            if (landClone != null) Destroy(landClone);
            landClone = null;
            if (landPivot != null) landPivot.gameObject.SetActive(false);
            if (freighter != null) freighter.gameObject.SetActive(false);
            SetDust(0f, Vector3.zero);
            AudioManager.Loop("shiparr_engine", "engine_loop", false);
            AudioManager.Loop("shiparr_hum", "drone_loop", false);
            scene = Scene.None;
        }

        // ================================================================== Schrottlieferung
        void StartDelivery()
        {
            var app = GameApp.I;
            var wv = WorldView.I;
            if (app == null || app.W == null || wv == null || wv.Layout == null) return;
            if (scene != Scene.None) Abort();
            EnsureFreighter();
            EnsureEffects();
            var planet = app.W.CurrentPlanet;
            var b = WorldGen.Get(planet).Base;
            var dz = new Vector3(b.DropZone.x, 0f, b.DropZone.z);
            dz.y = Terrain.HeightAt(planet, dz.x, dz.z);
            ground = dz;
            // Anflug von außerhalb des Stützpunkts, Nase zeigt zur Basis
            var toBase = new Vector3(b.Center.x - dz.x, 0f, b.Center.z - dz.z);
            if (toBase.sqrMagnitude < 1f) toBase = Vector3.forward;
            yaw = Quaternion.LookRotation(toBase.normalized, Vector3.up) * Quaternion.Euler(0f, 20f, 0f);
            scenePlanet = planet;
            scene = Scene.Delivery;
            t = 0f;
            touchedDown = flapsOpenedSound = liftoffSound = false;

            // Lieferobjekte bis zum Ausladen ausblenden (Sicherheitsgrenze: nach DeliveryHide Sekunden sind alle sichtbar)
            TrashRenderer.RevealedDeliveries.Clear();
            TrashRenderer.HideDeliveriesUntil = Time.time + DeliveryHide;
            TrashRenderer.RefreshSoon();
            drops.Clear();
            var ps = app.W.Cur;
            int i = 0;
            foreach (var d in ps.Dyn.Values)
            {
                if (!d.Delivery || d.CarriedBy != null) continue;
                TrashType tt;
                if (d.Type == null || !GameData.Trash.TryGetValue(d.Type, out tt)) continue;
                var ov = Rules.FromDyn(d);
                float s = tt.Oil || tt.Crane ? ov.Scale : ov.Scale * tt.Size;
                drops.Add(new Drop
                {
                    Id = d.Id, T = tt, To = new Vector3(d.Pos.x, d.Pos.y, d.Pos.z), Rot = Quaternion.Euler(0f, d.Rot * Mathf.Rad2Deg, 0f), Scale = s,
                    Start = 6.0f + i * 0.13f + UnityEngine.Random.Range(0f, 0.08f), Dur = 0.75f + UnityEngine.Random.Range(0f, 0.2f),
                    Spin = UnityEngine.Random.Range(180f, 540f), SpinAxis = UnityEngine.Random.onUnitSphere
                });
                i++;
            }
            freighter.gameObject.SetActive(true);
            SetFreighterPose(0f);
            AudioManager.Play("whoosh", FreighterPos(), 1f, 0.55f);
        }

        // Pfad: Anflug (0–3,8 s), Sinkflug (3,8–5,6 s), am Boden (5,6–8,9 s), Start (ab 8,9 s), Ende 14 s
        const float TApproach = 3.8f, TLand = 5.6f, TOpen = 5.7f, TClose = 8.1f, TLift = 8.9f, TEnd = 14f;

        Vector3 FreighterPos() { return freighter != null ? freighter.position : ground; }

        void SetFreighterPose(float time)
        {
            var fwd = yaw * Vector3.forward;
            var hover = ground + Vector3.up * 9f;
            Vector3 pos; float pitch = 0f, roll = 0f;
            if (time < TApproach)
            {
                float u = Mathf.Clamp01(time / TApproach);
                float e = 1f - (1f - u) * (1f - u) * (1f - u); // stark abbremsen
                var p0 = ground - fwd * 170f + Vector3.up * 120f + yaw * Vector3.right * 30f;
                var p1 = ground - fwd * 45f + Vector3.up * 40f;
                pos = Bezier(p0, p1, hover, e);
                pitch = -Mathf.Sin(u * Mathf.PI) * 9f;          // Nase beim Bremsen hoch
                roll = Mathf.Sin(u * Mathf.PI) * -6f * (1f - u);
            }
            else if (time < TLand)
            {
                float u = Smooth((time - TApproach) / (TLand - TApproach));
                pos = Vector3.Lerp(hover, ground, u);
                pos += Vector3.up * Mathf.Sin(time * 3.1f) * 0.08f * (1f - u);
            }
            else if (time < TLift) pos = ground;
            else
            {
                float s = time - TLift;
                pos = ground + Vector3.up * (0.9f * s * s + 0.6f * s) + fwd * (0.45f * s * s * s);
                pitch = Mathf.Clamp01(s / 3f) * 8f;
            }
            freighter.position = pos;
            freighter.rotation = yaw * Quaternion.Euler(pitch, 0f, roll);
        }

        void AnimateDelivery(float dt)
        {
            if (freighter == null) { Abort(); return; }
            SetFreighterPose(t);
            var pos = freighter.position;
            float height = pos.y - ground.y;

            // Landebeine: im Sinkflug ausfahren, nach dem Start einfahren
            float legOut = t < TApproach - 0.6f ? 0f : t < TLift + 0.3f ? Smooth((t - (TApproach - 0.6f)) / 1.4f) : 1f - Smooth((t - TLift - 0.3f) / 1.2f);
            for (int i = 0; i < legs.Count; i++) legs[i].localPosition = legBase[i] + Vector3.up * (1f - legOut) * 2.9f;

            // Bodenklappen
            float open = t < TOpen ? 0f : t < TClose ? Smooth((t - TOpen) / 0.6f) : 1f - Smooth((t - TClose) / 0.7f);
            for (int i = 0; i < flaps.Count; i++)
                flaps[i].localRotation = Quaternion.Euler(0f, 0f, flapSign[i] * open * (100f + i % 3 * 4f));

            // Triebwerke: Hauptdüsen beim Anflug und Abflug, Hubdüsen beim Schweben
            float main = t < TApproach ? Mathf.Lerp(1f, 0.15f, t / TApproach) : t < TLift ? 0.05f : Mathf.Clamp01((t - TLift) / 1.5f);
            float lift = t < 1.2f ? t / 1.2f * 0.6f : t < TApproach ? 0.6f + 0.4f * (t - 1.2f) / (TApproach - 1.2f) : t < TLand ? 1f : t < TLand + 0.8f ? 1f - (t - TLand) / 0.8f * 0.85f : t < TLift - 0.6f ? 0.15f : Mathf.Clamp01(0.15f + (t - (TLift - 0.6f)) / 0.8f);
            foreach (var e in fMain) DriveEngine(e, main, 0.8f);
            foreach (var e in fLift) DriveEngine(e, lift, 1f);
            if (nozzleMat != null) Mats.SetEmission(nozzleMat, new Color(0.9f, 1.6f, 2.6f) * (0.3f + 1.4f * Mathf.Max(main, lift)));
            if (fEngineLight != null) fEngineLight.intensity = (1.2f + 2.2f * lift) * (0.9f + 0.1f * Mathf.PerlinNoise(t * 9f, 0.3f));
            Blink(beaconRed, beaconGreen, beaconWhite, t);
            bool lamps = t > 1.5f && t < TLift + 3f;
            if (fSpots != null) foreach (var l in fSpots) if (l != null) l.enabled = lamps;
            if (floodMat != null) Mats.SetEmission(floodMat, lamps ? new Color(2.4f, 2.2f, 1.8f) : new Color(0.2f, 0.2f, 0.18f));

            // Staub am Boden
            float dustK = Mathf.Clamp01(1f - height / 16f) * lift;
            SetDust(dustK, ground);

            // Klänge
            AudioManager.Loop("shiparr_engine", "engine_loop", true, pos, 0.25f + 0.55f * Mathf.Max(main, lift), 0.45f + 0.25f * lift);
            AudioManager.Loop("shiparr_hum", "drone_loop", true, pos, 0.35f * lift, 0.55f);
            if (!touchedDown && t >= TLand)
            {
                touchedDown = true;
                AudioManager.Play("thunder", ground, 0.35f, 1.9f);
                AudioManager.Play("metal", ground, 0.7f, 0.6f);
                if (FxView.I != null) for (int i = 0; i < legs.Count; i++) FxView.I.Burst(legs[i].position + Vector3.down * 3.5f, new Color(0.6f, 0.55f, 0.48f, 0.5f), 10, 2.2f, 0.9f, 1.6f, -0.05f);
                ShakeIfNear(ground, 45f, 0.25f);
            }
            if (!flapsOpenedSound && t >= TOpen)
            {
                flapsOpenedSound = true;
                AudioManager.Play("gate_open", ground + Vector3.up * 4f, 0.9f, 0.8f);
                AudioManager.Play("crane", ground + Vector3.up * 4f, 0.5f, 0.7f);
            }
            if (!liftoffSound && t >= TLift - 0.4f)
            {
                liftoffSound = true;
                AudioManager.Play("whoosh", pos, 1f, 0.6f);
                AudioManager.Play("thunder", pos, 0.3f, 0.7f);
            }

            // Fallende Teile
            for (int i = 0; i < drops.Count; i++)
            {
                var d = drops[i];
                if (t < d.Start) continue;
                if (d.From == Vector3.zero) d.From = NearestContainerBottom(d.To);
                float u = (t - d.Start) / d.Dur;
                if (u >= 1f)
                {
                    if (!d.Landed)
                    {
                        d.Landed = true;
                        TrashRenderer.RevealedDeliveries.Add(d.Id);
                        TrashRenderer.RefreshSoon();
                        if (FxView.I != null) FxView.I.Burst(d.To + Vector3.up * 0.2f, new Color(0.62f, 0.57f, 0.5f, 0.45f), 5, 1.4f, 0.5f, 0.9f, -0.05f);
                        if (i % 3 == 0) AudioManager.Play("unload", d.To, 0.55f, UnityEngine.Random.Range(0.85f, 1.15f));
                    }
                    if (u < 1.35f) TrashRenderer.DrawTrash(d.T, Matrix4x4.TRS(d.To, d.Rot, Vector3.one * d.Scale));
                    continue;
                }
                // Gleiten aus dem Container: erst senkrecht, dann im Bogen zum Zielpunkt
                float fall = u * u;
                var p = Vector3.Lerp(d.From, d.To, Smooth(u));
                p.y = Mathf.Lerp(d.From.y, d.To.y, fall) + Mathf.Sin(u * Mathf.PI) * 0.6f;
                var rot = Quaternion.AngleAxis((1f - u) * d.Spin, d.SpinAxis) * d.Rot;
                TrashRenderer.DrawTrash(d.T, Matrix4x4.TRS(p, rot, Vector3.one * d.Scale));
            }
            if (t >= TLift - 0.5f && TrashRenderer.HideDeliveriesUntil > 0f)
            {
                // Spätestens beim Start ist alles ausgeladen
                foreach (var d in drops) TrashRenderer.RevealedDeliveries.Add(d.Id);
                TrashRenderer.HideDeliveriesUntil = -1f;
                TrashRenderer.RefreshSoon();
            }
            if (t >= TEnd) Abort();
        }

        Vector3 NearestContainerBottom(Vector3 to)
        {
            if (freighter == null || containerBottoms.Count == 0) return to + Vector3.up * 4f;
            Vector3 best = to + Vector3.up * 4f; float bd = float.MaxValue;
            foreach (var c in containerBottoms)
            {
                var w = freighter.TransformPoint(c);
                float d = (new Vector2(w.x - to.x, w.z - to.z)).sqrMagnitude;
                if (d < bd) { bd = d; best = w; }
            }
            return best + new Vector3(UnityEngine.Random.Range(-1.2f, 1.2f), -0.3f, UnityEngine.Random.Range(-1.4f, 1.4f));
        }

        // ================================================================== Landung des eigenen Transportschiffs
        void StartLanding(bool withCamera)
        {
            var app = GameApp.I;
            var wv = WorldView.I;
            if (app == null || app.W == null || wv == null || wv.Root == null || wv.Layout == null) return;
            if (wv.Planet != app.W.CurrentPlanet) return;
            var shipT = wv.Root.Find("TransportShip");
            if (shipT == null) return;
            if (scene != Scene.None) Abort();
            EnsureEffects();
            var b = wv.Layout.Base;
            var pad = new Vector3(b.ShipPad.x, b.Center.y + 0.2f, b.ShipPad.z);
            ground = pad;
            yaw = Quaternion.Euler(0f, -30f, 0f); // wie WorldViewBase.BuildShip
            scenePlanet = app.W.CurrentPlanet;

            if (landPivot == null)
            {
                landPivot = new GameObject("Landeanflug").transform;
                landPivot.SetParent(transform, false);
            }
            landPivot.gameObject.SetActive(true);
            landPivot.SetPositionAndRotation(pad, Quaternion.identity);
            landClone = Instantiate(shipT.gameObject);
            landClone.name = "TransportShip_Anflug";
            landClone.transform.SetPositionAndRotation(shipT.position, shipT.rotation);
            landClone.transform.SetParent(landPivot, true);
            landClone.SetActive(true);
            hiddenOriginal = shipT.gameObject;
            hiddenOriginal.SetActive(false);
            BuildLandingEngines();

            // Start: hinten oben, in Flugrichtung (Nase = +Z des Schiffs)
            var fwd = yaw * Vector3.forward;
            landStart = pad - fwd * 150f + Vector3.up * 95f + yaw * Vector3.right * 25f;
            scene = Scene.Landing;
            t = 0f;
            touchedDown = false;
            AnimateLanding(0f);
            AudioManager.Play("whoosh", landStart, 1f, 0.7f);

            if (withCamera && AllowCinematic && CameraRig.I != null && CameraRig.I.Cam != null && UIState.Screen == UIScreen.None && !PhotoMode.Active && !CameraRig.I.Cinematic)
            {
                var me = PlayerController.I != null ? PlayerController.I.RenderPos : new Vector3(b.Spawn.x, b.Spawn.y, b.Spawn.z);
                var away = me - pad; away.y = 0f;
                if (away.sqrMagnitude < 4f) away = -fwd;
                away.Normalize();
                var side = Vector3.Cross(Vector3.up, away);
                camA = me + away * 7f + side * 2.5f + Vector3.up * 2.2f;
                camB = me + away * 4.5f + side * 1f + Vector3.up * 3.2f;
                camA.y = Mathf.Max(camA.y, Terrain.HeightAt(scenePlanet, camA.x, camA.z) + 1.6f);
                camB.y = Mathf.Max(camB.y, Terrain.HeightAt(scenePlanet, camB.x, camB.z) + 1.6f);
                camLook = landPivot.position + Vector3.up * 3f;
                cineT = 0f;
                cineDur = 6f;
                cinematic = true;
                CameraRig.I.Cinematic = true;
            }
        }

        const float LApproach = 4.3f, LTouch = 6.3f, LEnd = 7.6f;

        void AnimateLanding(float dt)
        {
            if (landPivot == null || landClone == null) { Abort(); return; }
            var fwd = yaw * Vector3.forward;
            var hover = ground + Vector3.up * 6f;
            Vector3 pos; Quaternion rot = Quaternion.identity;
            if (t < LApproach)
            {
                float u = Mathf.Clamp01(t / LApproach);
                float e = 1f - (1f - u) * (1f - u) * (1f - u);
                pos = Bezier(landStart, ground - fwd * 35f + Vector3.up * 28f, hover, e);
                // Drehpunkt ist der Landeplatz → um den Schiffsmittelpunkt neigen
                rot = Quaternion.AngleAxis(-Mathf.Sin(u * Mathf.PI) * 10f, yaw * Vector3.right) * Quaternion.AngleAxis(Mathf.Sin(u * Mathf.PI) * (1f - u) * 8f, fwd);
            }
            else if (t < LTouch)
            {
                float u = Smooth((t - LApproach) / (LTouch - LApproach));
                pos = Vector3.Lerp(hover, ground, u) + Vector3.up * Mathf.Sin(t * 3.4f) * 0.05f * (1f - u);
            }
            else pos = ground;
            landPivot.SetPositionAndRotation(pos, rot);

            float lift = t < LApproach ? 0.5f + 0.5f * t / LApproach : t < LTouch ? 1f : Mathf.Clamp01(1f - (t - LTouch) / 1.1f);
            float main = t < LApproach ? Mathf.Lerp(1f, 0.1f, t / LApproach) : Mathf.Clamp01(0.1f - (t - LApproach) * 0.1f);
            foreach (var e in lLift) DriveEngine(e, lift, 0.8f);
            foreach (var e in lMain) DriveEngine(e, main, 0.7f);
            if (lEngineLight != null) lEngineLight.intensity = 2.2f * lift * (0.9f + 0.1f * Mathf.PerlinNoise(t * 9f, 0.7f));
            float height = pos.y - ground.y;
            SetDust(Mathf.Clamp01(1f - height / 12f) * lift * 0.8f, ground);
            AudioManager.Loop("shiparr_engine", "engine_loop", lift > 0.02f, pos, 0.2f + 0.5f * Mathf.Max(lift, main), 0.55f + 0.2f * lift);
            if (!touchedDown && t >= LTouch)
            {
                touchedDown = true;
                AudioManager.Play("thunder", ground, 0.25f, 2f);
                AudioManager.Play("metal", ground, 0.6f, 0.7f);
                if (FxView.I != null) FxView.I.Burst(ground + Vector3.up * 0.3f, new Color(0.62f, 0.57f, 0.5f, 0.5f), 18, 3f, 1.1f, 1.6f, -0.05f);
                ShakeIfNear(ground, 40f, 0.15f);
            }
            if (t >= LEnd) Abort();
        }

        void BuildLandingEngines()
        {
            // Die Engine-Objekte hängen am Klon (werden mit ihm zerstört). Lage wie in WorldViewBase.BuildShip (Wurzel = Landeplatz + yaw·(0,0,1,4)).
            lLift.Clear(); lMain.Clear();
            var holder = new GameObject("Triebwerke").transform;
            holder.SetParent(landClone.transform, false);
            holder.position = ground + yaw * new Vector3(0f, 0f, 1.4f);
            holder.rotation = yaw;
            const float bodyY = 3.6f;
            for (int s = -1; s <= 1; s += 2)
            {
                lLift.Add(MakeEngine(holder, new Vector3(s * 4.6f, bodyY - 1.1f, -2.0f), Vector3.down, 0.5f, 2.6f));
                lMain.Add(MakeEngine(holder, new Vector3(s * 1.1f, bodyY + 0.2f, -6.2f), Vector3.back, 0.5f, 3.2f));
            }
            var lg = new GameObject("Triebwerkslicht");
            lg.transform.SetParent(holder, false);
            lg.transform.localPosition = new Vector3(0f, 0.5f, -2f);
            lEngineLight = lg.AddComponent<Light>();
            lEngineLight.type = LightType.Point; lEngineLight.range = 22f; lEngineLight.color = new Color(0.55f, 0.8f, 1f); lEngineLight.intensity = 0f;
            lEngineLight.shadows = LightShadows.None;
        }

        // ================================================================== Gemeinsame Effekte
        void DriveEngine(Engine e, float k, float scale)
        {
            if (e == null) return;
            k = Mathf.Clamp01(k);
            if (e.Flame != null)
            {
                float fl = 0.85f + 0.15f * Mathf.PerlinNoise(t * 17f, e.Seed) + 0.08f * Mathf.Sin(t * 41f + e.Seed * 7f);
                float len = e.Length * scale * (0.25f + 0.75f * k) * fl;
                float rad = e.Radius * (0.6f + 0.4f * k);
                e.Flame.gameObject.SetActive(k > 0.02f);
                e.Flame.localScale = new Vector3(rad, len / 4f, rad);
            }
            if (e.Plume != null)
            {
                var em = e.Plume.emission;
                em.rateOverTime = k * 60f * Density;
                if (k > 0.02f && !e.Plume.isPlaying) e.Plume.Play();
                else if (k <= 0.02f && e.Plume.isPlaying) e.Plume.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        Engine MakeEngine(Transform parent, Vector3 localPos, Vector3 localDir, float radius, float length)
        {
            var e = new Engine { Radius = radius, Length = length, Seed = UnityEngine.Random.Range(0f, 100f) };
            var go = new GameObject("Flamme");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, localDir);
            // Äußerer, bläulicher Kegel und heller Kern (additiv, Verlauf zur Spitze)
            var outer = new GameObject("Mantel");
            outer.transform.SetParent(go.transform, false);
            outer.AddComponent<MeshFilter>().sharedMesh = FlameMesh();
            var r1 = outer.AddComponent<MeshRenderer>();
            r1.sharedMaterial = flameMat; r1.shadowCastingMode = ShadowCastingMode.Off; r1.receiveShadows = false;
            var core = new GameObject("Kern");
            core.transform.SetParent(go.transform, false);
            core.transform.localScale = new Vector3(0.5f, 0.6f, 0.5f);
            core.AddComponent<MeshFilter>().sharedMesh = FlameMesh();
            var r2 = core.AddComponent<MeshRenderer>();
            r2.sharedMaterial = flameCoreMat; r2.shadowCastingMode = ShadowCastingMode.Off; r2.receiveShadows = false;
            e.Flame = go.transform;
            go.SetActive(false);
            // Abgasfahne
            var pgo = new GameObject("Abgas");
            pgo.transform.SetParent(parent, false);
            pgo.transform.localPosition = localPos + localDir * length * 0.4f;
            pgo.transform.localRotation = Quaternion.LookRotation(localDir, Mathf.Abs(localDir.y) > 0.9f ? Vector3.forward : Vector3.up);
            var ps = pgo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(10f, 18f);
            main.startSize = new ParticleSystem.MinMaxCurve(radius * 1.4f, radius * 2.6f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.8f, 1f, 0.55f), new Color(1f, 0.75f, 0.45f, 0.4f));
            main.maxParticles = 160;
            var em = ps.emission; em.rateOverTime = 0f;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 7f; sh.radius = radius * 0.6f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.7f, 0.5f), 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.5f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
            var pr = pgo.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = plumeMat; pr.shadowCastingMode = ShadowCastingMode.Off; pr.receiveShadows = false; pr.maxParticleSize = 2f;
            e.Plume = ps;
            return e;
        }

        void EnsureEffects()
        {
            if (flameMat == null)
            {
                flameMat = TexMat(Mats.ParticleAdd, FlameTex(), new Color(0.45f, 0.7f, 1f, 0.8f), "Flammenmantel");
                flameCoreMat = TexMat(Mats.ParticleAdd, FlameTex(), new Color(1f, 0.95f, 0.85f, 1f), "Flammenkern");
                plumeMat = TexMat(Mats.ParticleAdd, SoftTex(), Color.white, "Abgas");
                dustMat = TexMat(Mats.Particle, SoftTex(), Color.white, "Landestaub");
            }
            if (dust == null)
            {
                var go = new GameObject("Landestaub");
                go.transform.SetParent(transform, false);
                go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                dust = go.AddComponent<ParticleSystem>();
                dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = dust.main;
                main.playOnAwake = false; main.loop = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f);
                main.startSize = new ParticleSystem.MinMaxCurve(2f, 5f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.66f, 0.6f, 0.52f, 0.4f), new Color(0.55f, 0.5f, 0.44f, 0.55f));
                main.gravityModifier = -0.02f;
                main.maxParticles = 500;
                var em = dust.emission; em.rateOverTime = 0f;
                var sh = dust.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 3.5f; sh.radiusThickness = 0.4f;
                var lim = dust.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 2.5f; lim.dampen = 0.08f;
                var col = dust.colorOverLifetime; col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
                col.color = new ParticleSystem.MinMaxGradient(g);
                var sz = dust.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 2.4f));
                var r = go.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = dustMat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.maxParticleSize = 3f;
            }
        }

        void SetDust(float k, Vector3 at)
        {
            if (dust == null) return;
            var em = dust.emission;
            em.rateOverTime = k * 110f * Density;
            if (k > 0.01f)
            {
                dust.transform.position = at + Vector3.up * 0.3f;
                if (!dust.isPlaying) dust.Play();
            }
            else if (dust.isPlaying) dust.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        static float Density
        {
            get { int p = GameApp.I != null ? GameApp.I.Settings.Particles : 2; return p <= 0 ? 0.35f : p == 1 ? 0.7f : 1f; }
        }

        static void ShakeIfNear(Vector3 at, float range, float amount)
        {
            if (CameraRig.I == null || CameraRig.I.Cam == null) return;
            float d = Vector3.Distance(CameraRig.I.Cam.transform.position, at);
            if (d < range) CameraRig.I.Shake(amount * (1f - d / range));
        }

        static void Blink(Material red, Material green, Material white, float time)
        {
            bool on = Mathf.Repeat(time * 1.2f, 1f) < 0.18f;
            bool strobe = Mathf.Repeat(time * 0.9f + 0.4f, 1f) < 0.08f;
            if (red != null) Mats.SetEmission(red, on ? new Color(3f, 0.3f, 0.2f) : new Color(0.3f, 0.03f, 0.02f));
            if (green != null) Mats.SetEmission(green, on ? new Color(0.3f, 3f, 0.5f) : new Color(0.03f, 0.3f, 0.05f));
            if (white != null) Mats.SetEmission(white, strobe ? new Color(4f, 4f, 4f) : new Color(0.2f, 0.2f, 0.2f));
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float u)
        {
            float v = 1f - u;
            return v * v * a + 2f * v * u * b + u * u * c;
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        static Material TexMat(string template, Texture tex, Color c, string name)
        {
            var m = new Material(Mats.Template(template)) { name = name };
            m.mainTexture = tex;
            m.color = c;
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", c * 0.5f);
            m.enableInstancing = true;
            return m;
        }

        static Texture2D SoftTex()
        {
            if (softTex != null) return softTex;
            const int n = 64;
            softTex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "SoftPuff", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            softTex.SetPixels32(px);
            softTex.Apply(true);
            return softTex;
        }

        /// <summary>Verlauf entlang v (0 = Düse hell, 1 = Spitze dunkel) mit weichem Rand in u – für additive Flammenkegel.</summary>
        static Texture2D FlameTex()
        {
            if (flameTex != null) return flameTex;
            const int w = 8, h = 64;
            flameTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "FlameGrad", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (h - 1f);
                float a = Mathf.Pow(1f - v, 1.6f);
                byte b = (byte)(a * 255);
                for (int x = 0; x < w; x++) px[y * w + x] = new Color32(b, b, b, b);
            }
            flameTex.SetPixels32(px);
            flameTex.Apply(false);
            return flameTex;
        }

        /// <summary>Flammenkegel entlang +Y (Länge 4, Radius 1 an der Düse), UV v läuft von der Düse zur Spitze.</summary>
        static Mesh FlameMesh()
        {
            return MeshKit.Get("shiparr_flame", b => b.Lathe(Vector3.zero, new[]
            {
                new Vector2(0.7f, 0f), new Vector2(1f, 0.35f), new Vector2(0.85f, 1.3f), new Vector2(0.55f, 2.5f), new Vector2(0.22f, 3.5f), new Vector2(0f, 4f)
            }, 14, true));
        }

        // ================================================================== Modell: Containerfrachter
        /// <summary>
        /// Containerfrachter (lokal: +Z = Nase, Ursprung = Bodenpunkt bei ausgefahrenen Beinen, ≈ 38 m lang):
        /// Rückgrat mit Aufbau, Brücke mit Fensterband, abgeflachte Nase mit Kanzel, drei Haupttriebwerke mit Glocken,
        /// vier Hubtriebwerke an Auslegern, Kühlrippen, Antennenmast, Positions- und Blinklichter, Scheinwerfer,
        /// ein Gestell mit 2 × 4 farbigen Wellblech-Containern (Rippen, Eckpfosten, Innenverkleidung, Innenlicht),
        /// je Container eine Bodenklappe mit Warnstreifen, vier Teleskop-Landebeine mit Tellern.
        /// </summary>
        void EnsureFreighter()
        {
            if (freighter != null) return;
            var root = new GameObject("Containerfrachter").transform;
            root.SetParent(transform, false);
            freighter = root;
            flaps.Clear(); flapSign.Clear(); legs.Clear(); legBase.Clear(); fLift.Clear(); fMain.Clear(); containerBottoms.Clear();
            EnsureEffects();

            var hull = Mats.Get(Mats.Metal, new Color(0.8f, 0.82f, 0.85f));
            var hullDark = Mats.Get(Mats.Metal, new Color(0.36f, 0.38f, 0.42f));
            var dark = Mats.Get(Mats.Opaque, new Color(0.13f, 0.14f, 0.16f));
            var inner = Mats.Get(Mats.Opaque, new Color(0.2f, 0.19f, 0.18f));
            var accent = Mats.Get(Mats.Opaque, new Color(1f, 0.55f, 0.18f));
            var teal = Mats.Get(Mats.Opaque, new Color(0.18f, 0.7f, 0.66f));
            var yellow = Mats.Get(Mats.Opaque, new Color(0.95f, 0.78f, 0.18f));
            var glass = Mats.Get(Mats.Emissive, new Color(0.1f, 0.2f, 0.28f), new Color(0.15f, 0.5f, 0.7f));
            var winGlow = Mats.Get(Mats.Emissive, new Color(1f, 0.85f, 0.55f), new Color(2.2f, 1.7f, 1f));
            nozzleMat = Mats.Unique(Mats.Emissive, new Color(0.6f, 0.85f, 1f));
            floodMat = Mats.Unique(Mats.Emissive, new Color(1f, 0.97f, 0.9f));
            beaconRed = Mats.Unique(Mats.Emissive, new Color(1f, 0.2f, 0.15f));
            beaconGreen = Mats.Unique(Mats.Emissive, new Color(0.2f, 1f, 0.35f));
            beaconWhite = Mats.Unique(Mats.Emissive, Color.white);

            var mb = new MultiBuilder { UsePalette = true };
            const float spineY = 8.9f;
            // Rückgrat und Aufbau
            mb.For(hull).Box(new Vector3(0f, spineY, -1.5f), new Vector3(4.4f, 2.6f, 30f));
            mb.For(hullDark).Box(new Vector3(0f, spineY + 1.6f, -3.5f), new Vector3(3.2f, 0.7f, 22f));
            mb.For(dark).Box(new Vector3(0f, spineY - 1.35f, -1.5f), new Vector3(4.6f, 0.1f, 29f));
            for (int s = -1; s <= 1; s += 2)
            {
                mb.For(accent).Box(new Vector3(s * 2.22f, spineY + 0.45f, -1.5f), new Vector3(0.06f, 0.42f, 28f));
                mb.For(teal).Box(new Vector3(s * 2.23f, spineY - 0.2f, -1.5f), new Vector3(0.05f, 0.14f, 28f));
                for (int k = 0; k < 7; k++) mb.For(hullDark).Box(new Vector3(s * 2.22f, spineY, -14f + k * 4.2f), new Vector3(0.08f, 2.5f, 0.08f)); // Plattenfugen
            }
            // Nase mit Kanzel
            mb.M = Matrix4x4.TRS(new Vector3(0f, spineY, 13.5f), Quaternion.Euler(90f, 0f, 0f), new Vector3(1f, 1f, 0.62f));
            mb.For(hull).Lathe(Vector3.zero, new[] { new Vector2(0f, 0f), new Vector2(2.25f, 0.02f), new Vector2(2.35f, 1.4f), new Vector2(2.05f, 3.2f), new Vector2(1.35f, 4.7f), new Vector2(0.5f, 5.5f), new Vector2(0f, 5.7f) }, 20);
            mb.M = Matrix4x4.TRS(new Vector3(0f, spineY + 0.75f, 16.3f), Quaternion.Euler(-14f, 0f, 0f), new Vector3(1f, 0.45f, 1.5f));
            mb.For(glass).Sphere(Vector3.zero, 1.25f, 16, 8);
            mb.M = Matrix4x4.identity;
            // Brücke mit Fensterband
            mb.For(hull).Box(new Vector3(0f, spineY + 2.4f, 8.5f), new Vector3(5.6f, 2.2f, 4.2f));
            mb.For(hullDark).Box(new Vector3(0f, spineY + 3.6f, 8.3f), new Vector3(6.2f, 0.25f, 4.8f));
            mb.For(glass).Box(new Vector3(0f, spineY + 2.6f, 10.62f), new Vector3(5f, 0.75f, 0.06f));
            for (int s = -1; s <= 1; s += 2)
            {
                mb.For(glass).Box(new Vector3(s * 2.82f, spineY + 2.6f, 8.6f), new Vector3(0.06f, 0.7f, 3.2f));
                for (int k = 0; k < 6; k++) mb.For(winGlow).Box(new Vector3(s * 2.24f, spineY + 0.9f, -10f + k * 2.6f), new Vector3(0.06f, 0.35f, 0.9f)); // Bullaugen
            }
            // Antennenmast, Radarschüssel, Kühlrippen
            mb.For(hullDark).Cylinder(new Vector3(1.2f, spineY + 3.7f, 7.4f), 0.07f, 3.2f, 6);
            mb.For(hullDark).Cylinder(new Vector3(-1.4f, spineY + 3.7f, 7.8f), 0.12f, 0.8f, 6);
            mb.M = Matrix4x4.TRS(new Vector3(-1.4f, spineY + 4.6f, 7.8f), Quaternion.Euler(-35f, 0f, 0f), Vector3.one);
            mb.For(hull).Lathe(Vector3.zero, new[] { new Vector2(0f, 0f), new Vector2(0.6f, 0.12f), new Vector2(1.1f, 0.4f) }, 14, true);
            mb.M = Matrix4x4.identity;
            for (int k = 0; k < 7; k++) mb.For(dark).Box(new Vector3(0f, spineY + 2.35f, -13f + k * 1.1f), new Vector3(3f, 0.9f, 0.1f));
            mb.For(hullDark).Box(new Vector3(0f, spineY + 1.95f, -9.7f), new Vector3(3.1f, 0.1f, 7.8f));
            // Heck: Triebwerksblock mit drei Glocken
            mb.For(hullDark).Box(new Vector3(0f, spineY, -17.6f), new Vector3(7.4f, 4.2f, 2.6f));
            mb.For(accent).Box(new Vector3(0f, spineY + 2.15f, -17.6f), new Vector3(7.5f, 0.12f, 2.7f));
            float[] bellX = { -2.5f, 0f, 2.5f };
            foreach (var bx in bellX)
            {
                mb.M = Matrix4x4.TRS(new Vector3(bx, spineY, -18.9f), Quaternion.Euler(-90f, 0f, 0f), Vector3.one);
                mb.For(dark).Lathe(Vector3.zero, new[] { new Vector2(0.75f, 0f), new Vector2(0.85f, 0.4f), new Vector2(1.05f, 1.2f), new Vector2(1.18f, 1.9f) }, 18, true);
                mb.M = Matrix4x4.identity;
                mb.For(nozzleMat).CylinderZ(new Vector3(bx, spineY, -19.05f), 0.72f, 0.1f, 16);
            }
            // Hubtriebwerke an Auslegern
            var liftPos = new[] { new Vector3(-7.4f, 8.2f, -9.5f), new Vector3(7.4f, 8.2f, -9.5f), new Vector3(-7.4f, 8.2f, 7f), new Vector3(7.4f, 8.2f, 7f) };
            foreach (var lp in liftPos)
            {
                float s = Mathf.Sign(lp.x);
                mb.For(hullDark).Beam(new Vector3(s * 2.1f, spineY + 0.3f, lp.z), new Vector3(lp.x - s * 1.1f, lp.y + 0.6f, lp.z), 0.9f, 0.5f);
                mb.For(hull).Cylinder(lp + Vector3.down * 1.1f, 1.25f, 2.3f, 16);
                mb.For(dark).Cylinder(lp + Vector3.down * 1.45f, 1.05f, 0.35f, 16, true, 1.25f);
                mb.For(accent).Cylinder(lp + Vector3.up * 1.0f, 1.28f, 0.25f, 16);
                mb.For(nozzleMat).Cylinder(lp + Vector3.down * 1.5f, 0.8f, 0.06f, 16);
                // Positionslichter außen
                mb.For(s < 0 ? beaconRed : beaconGreen).Sphere(lp + new Vector3(s * 1.32f, 0.3f, 0f), 0.16f, 8, 5);
            }
            mb.For(beaconWhite).Sphere(new Vector3(1.2f, spineY + 7f, 7.4f), 0.14f, 8, 5);
            mb.For(beaconWhite).Sphere(new Vector3(0f, spineY + 2.2f, -18.9f), 0.14f, 8, 5);

            // Containergestell
            const float rackBottom = 3.5f, rackTop = 7.6f;
            float[] rowZ = { -10.8f, -4.8f, 1.2f, 7.2f };
            for (int s = -1; s <= 1; s += 2)
            {
                mb.For(dark).Box(new Vector3(s * 5.55f, rackTop + 0.1f, -1.8f), new Vector3(0.35f, 0.35f, 25.4f));
                mb.For(dark).Box(new Vector3(s * 5.55f, rackBottom - 0.1f, -1.8f), new Vector3(0.3f, 0.3f, 25.4f));
                mb.For(yellow).Box(new Vector3(s * 5.72f, rackBottom - 0.1f, -1.8f), new Vector3(0.04f, 0.18f, 25.4f));
            }
            for (int k = 0; k <= 4; k++)
            {
                float z = -13.8f + k * 6f;
                mb.For(dark).Box(new Vector3(0f, rackTop + 0.1f, z), new Vector3(11.4f, 0.35f, 0.3f));
                for (int s = -1; s <= 1; s += 2) mb.For(dark).Box(new Vector3(s * 5.55f, (rackTop + rackBottom) * 0.5f, z), new Vector3(0.28f, rackTop - rackBottom + 0.4f, 0.28f));
            }
            // Aufhängung Rückgrat → Gestell
            for (int k = 0; k < 4; k++) mb.For(hullDark).Box(new Vector3(0f, rackTop + 0.35f, rowZ[k]), new Vector3(3.6f, 0.5f, 1.2f));

            Color[] cc =
            {
                new Color(0.88f, 0.42f, 0.14f), new Color(0.15f, 0.52f, 0.54f), new Color(0.68f, 0.17f, 0.14f), new Color(0.17f, 0.3f, 0.58f),
                new Color(0.86f, 0.68f, 0.16f), new Color(0.3f, 0.48f, 0.24f), new Color(0.52f, 0.54f, 0.56f), new Color(0.55f, 0.3f, 0.18f)
            };
            int ci = 0;
            foreach (var z in rowZ)
                for (int s = -1; s <= 1; s += 2)
                {
                    var col = Mats.Get(Mats.Opaque, cc[ci % cc.Length]);
                    var colDark = Mats.Get(Mats.Opaque, cc[ci % cc.Length] * 0.72f);
                    ci++;
                    float cx = s * 2.75f;
                    const float cw = 5.2f, ch = 4.0f, cl = 5.6f;
                    float cy = rackBottom + ch * 0.5f;
                    mb.For(col).BoxNoBottom(new Vector3(cx, cy, z), new Vector3(cw, ch, cl));
                    // Wellblech-Rippen außen und an den Enden
                    for (int r = 0; r < 11; r++)
                        mb.For(colDark).Box(new Vector3(cx + s * (cw * 0.5f + 0.03f), cy, z - cl * 0.5f + 0.35f + r * 0.49f), new Vector3(0.07f, ch - 0.35f, 0.16f));
                    for (int e = -1; e <= 1; e += 2)
                        for (int r = 0; r < 9; r++)
                            mb.For(colDark).Box(new Vector3(cx - cw * 0.5f + 0.4f + r * 0.55f, cy, z + e * (cl * 0.5f + 0.03f)), new Vector3(0.18f, ch - 0.35f, 0.07f));
                    // Eckpfosten und Kennstreifen
                    for (int a = -1; a <= 1; a += 2)
                        for (int c2 = -1; c2 <= 1; c2 += 2)
                            mb.For(dark).Box(new Vector3(cx + a * (cw * 0.5f - 0.1f), cy, z + c2 * (cl * 0.5f - 0.1f)), new Vector3(0.26f, ch + 0.05f, 0.26f));
                    mb.For(Mats.Get(Mats.Opaque, new Color(0.9f, 0.88f, 0.82f))).Box(new Vector3(cx + s * (cw * 0.5f + 0.07f), cy + 1.3f, z), new Vector3(0.03f, 0.45f, 2.2f));
                    // Innenverkleidung (sichtbar bei offener Klappe) und Innenlicht
                    mb.For(inner).Box(new Vector3(cx, rackBottom + ch - 0.08f, z), new Vector3(cw - 0.2f, 0.05f, cl - 0.2f));
                    for (int a = -1; a <= 1; a += 2)
                    {
                        mb.For(inner).Box(new Vector3(cx + a * (cw * 0.5f - 0.12f), cy, z), new Vector3(0.04f, ch - 0.1f, cl - 0.2f));
                        mb.For(inner).Box(new Vector3(cx, cy, z + a * (cl * 0.5f - 0.12f)), new Vector3(cw - 0.2f, ch - 0.1f, 0.04f));
                    }
                    mb.For(winGlow).Box(new Vector3(cx, rackBottom + ch - 0.14f, z), new Vector3(2.6f, 0.05f, 0.18f));
                    containerBottoms.Add(new Vector3(cx, rackBottom - 0.2f, z));

                    // Bodenklappe (eigenes Objekt, Scharnier an der Außenkante)
                    var hinge = new GameObject("Klappe").transform;
                    hinge.SetParent(root, false);
                    hinge.localPosition = new Vector3(cx + s * cw * 0.5f, rackBottom, z);
                    var fb = new MultiBuilder { UsePalette = true };
                    fb.For(col).Box(new Vector3(-s * cw * 0.5f, -0.06f, 0f), new Vector3(cw, 0.12f, cl));
                    for (int k = 0; k < 5; k++)
                        fb.For(k % 2 == 0 ? yellow : dark).BoxRot(new Vector3(-s * (0.8f + k * 0.8f), -0.13f, 0f), new Vector3(0.45f, 0.03f, cl * 0.92f), new Vector3(0f, 0f, 0f));
                    fb.For(dark).Box(new Vector3(-s * 0.1f, -0.1f, 0f), new Vector3(0.2f, 0.2f, cl));
                    fb.Build("KlappeMesh", hinge, true);
                    flaps.Add(hinge);
                    flapSign.Add(s);
                }
            // Warnstreifen an der Gestellfront
            for (int k = 0; k < 10; k++)
                mb.For(k % 2 == 0 ? yellow : dark).BoxRot(new Vector3(-5f + k * 1.1f, rackBottom + 0.35f, 10.2f), new Vector3(0.55f, 0.5f, 0.05f), new Vector3(0f, 0f, 35f));
            // Beinaufnahmen und Scheinwerfergehäuse
            var legXZ = new[] { new Vector2(-6.1f, -12.5f), new Vector2(6.1f, -12.5f), new Vector2(-6.1f, 9.6f), new Vector2(6.1f, 9.6f) };
            foreach (var l in legXZ)
            {
                mb.For(hullDark).Box(new Vector3(l.x, rackBottom + 0.8f, l.y), new Vector3(0.9f, 2.2f, 0.9f));
                mb.For(dark).Beam(new Vector3(l.x, rackBottom + 1.6f, l.y), new Vector3(Mathf.Sign(l.x) * 5.55f, rackTop, l.y + (l.y < 0 ? 1.2f : -1.2f)), 0.25f);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                mb.For(dark).Box(new Vector3(s * 1.2f, rackBottom - 0.35f, 10.4f), new Vector3(0.7f, 0.35f, 0.5f));
                mb.For(floodMat).Box(new Vector3(s * 1.2f, rackBottom - 0.53f, 10.4f), new Vector3(0.55f, 0.03f, 0.38f));
                mb.For(dark).Box(new Vector3(s * 1.4f, spineY - 0.6f, 16.2f), new Vector3(0.6f, 0.35f, 0.3f));
                mb.For(floodMat).Box(new Vector3(s * 1.4f, spineY - 0.6f, 16.36f), new Vector3(0.45f, 0.24f, 0.03f));
            }
            mb.Build("Rumpf", root, true);

            // Landebeine (Teleskop: fahren nach oben in die Aufnahme)
            foreach (var l in legXZ)
            {
                var leg = new GameObject("Landebein").transform;
                leg.SetParent(root, false);
                var basePos = new Vector3(l.x, 0f, l.y);
                leg.localPosition = basePos;
                var lb = new MultiBuilder { UsePalette = true };
                lb.For(hullDark).Cylinder(new Vector3(0f, 1.1f, 0f), 0.32f, 2.6f, 10);
                lb.For(Mats.Get(Mats.Metal, new Color(0.75f, 0.77f, 0.8f))).Cylinder(new Vector3(0f, 0.3f, 0f), 0.2f, 1.2f, 10);
                lb.For(yellow).Box(new Vector3(0f, 1.25f, 0f), new Vector3(0.72f, 0.3f, 0.72f));
                lb.For(dark).Cylinder(new Vector3(0f, 0f, 0f), 0.85f, 0.3f, 14, true, 0.55f);
                lb.For(dark).Beam(new Vector3(0f, 1.3f, 0f), new Vector3(-Mathf.Sign(l.x) * 0.2f, 3.4f, l.y < 0 ? 0.6f : -0.6f), 0.14f);
                lb.Build("BeinMesh", leg, true);
                legs.Add(leg);
                legBase.Add(basePos);
            }

            // Triebwerksflammen
            foreach (var bx in bellX) fMain.Add(MakeEngine(root, new Vector3(bx, spineY, -19.1f), Vector3.back, 0.7f, 5.5f));
            foreach (var lp in liftPos) fLift.Add(MakeEngine(root, lp + Vector3.down * 1.5f, Vector3.down, 0.8f, 3.6f));

            // Lichter: Triebwerkslicht, zwei Flutlichter nach unten, zwei Scheinwerfer nach vorn
            var el = new GameObject("Triebwerkslicht");
            el.transform.SetParent(root, false);
            el.transform.localPosition = new Vector3(0f, 5.5f, -1f);
            fEngineLight = el.AddComponent<Light>();
            fEngineLight.type = LightType.Point; fEngineLight.range = 30f; fEngineLight.color = new Color(0.55f, 0.78f, 1f); fEngineLight.intensity = 0f;
            fEngineLight.shadows = LightShadows.None;
            var spots = new List<Light>();
            for (int s = -1; s <= 1; s += 2)
            {
                spots.Add(Spot(root, new Vector3(s * 1.2f, rackBottom - 0.6f, 10.4f), Quaternion.Euler(80f, 0f, 0f), 30f, 95f, 2.4f));
                spots.Add(Spot(root, new Vector3(s * 1.4f, spineY - 0.6f, 16.5f), Quaternion.Euler(22f, s * 6f, 0f), 70f, 42f, 3f));
            }
            fSpots = spots.ToArray();
            root.gameObject.SetActive(false);
        }

        static Light Spot(Transform parent, Vector3 pos, Quaternion rot, float range, float angle, float intensity)
        {
            var go = new GameObject("Scheinwerfer");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot; l.range = range; l.spotAngle = angle; l.intensity = intensity;
            l.color = new Color(1f, 0.95f, 0.85f);
            l.shadows = LightShadows.None;
            l.enabled = false;
            return l;
        }
    }
}
