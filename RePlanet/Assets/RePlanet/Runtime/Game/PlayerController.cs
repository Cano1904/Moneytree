using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using Terrain = RePlanet.Core.Terrain;

namespace RePlanet
{
    /// <summary>
    /// Steuerung des eigenen Roboters: Bewegung (lokal vorhergesagt, vom Server geprüft), Fahrzeuge, Werkzeuge,
    /// kontextabhängige Interaktion, Bauansicht und HUD-Hinweise. Alle Zustandsänderungen gehen als Aktion an den Server.
    /// </summary>
    public partial class PlayerController : MonoBehaviour
    {
        public static PlayerController I { get; private set; }
        public Vector3 RenderPos { get; private set; }
        public float RenderYaw { get; private set; }
        public int Flags { get; private set; }
        public bool InVehicle { get; private set; }
        public string Tool { get { return tool; } }
        /// <summary>Kollisionsumgebung (Tore, Dünen, gebaute Anlagen) – die Kamera nutzt sie, um nicht in Hindernisse zu rücken.</summary>
        public MotorEnv Env { get { return env; } }

        MoverState ms;
        MotorEnv env;
        string envPlanet;
        float envSync, sendTimer, sendAccum, actTimer, stuckTime, resetHold;
        string tool = "grab";
        bool toolToggled;
        float magnetCharge = -1f;
        bool pending;
        GameClient hookedClient;
        ObjView target;
        string targetErr;
        Interaction current;
        GameObject preview;
        string previewKey;
        /// <summary>Im Hangar bzw. Laderaum (geschützt, keine Werkzeuge).</summary>
        bool indoors;

        class Interaction
        {
            public string Label, Kind, Id, Station, Reason;
            public bool Hold;
            public Vector3 Pos;
            /// <summary>Kurzform fürs ruhige HUD: Taste (null = keine), ein Wort, Ankerpunkt in der Welt.</summary>
            public string Key, Word;
            public Vector3? At;
        }

        void Awake() { I = this; }

        void Start()
        {
            if (GameApp.I == null) return;
            GameApp.I.OnSessionStarted += ResetFromServer;
            GameApp.I.OnPlanetChanged += p => ResetFromServer();
        }

        void ResetFromServer()
        {
            var app = GameApp.I;
            var me = app.Me;
            if (me == null) return;
            ms = new MoverState { Pos = me.Pos, Yaw = me.Yaw };
            envPlanet = null;
            InVehicle = me.Vehicle != null;
            if (hookedClient != app.Client)
            {
                hookedClient = app.Client;
                app.Client.Corrected += p => { ms.Pos = p; ms.Vel = V3.Zero; ms.Speed = 0; };
                app.Client.Teleported += p => { ms.Pos = p; ms.Vel = V3.Zero; ms.Speed = 0; };
            }
            if (CameraRig.I != null) CameraRig.I.Yaw = me.Yaw * Mathf.Rad2Deg;
        }

        static Vector3 U(V3 v) { return new Vector3(v.x, v.y, v.z); }
        static V3 C(Vector3 v) { return new V3(v.x, v.y, v.z); }

        void Act(JObj a, System.Action<ActResult> cb = null) { GameApp.I.Act(a, cb); }

        void Update()
        {
            var app = GameApp.I;
            Hud.ClearTransient();
            if (app == null || !app.InGame) { if (TrashRenderer.I != null) TrashRenderer.I.HighlightKey = null; DestroyPreview(); return; }
            var me = app.Me;
            var w = app.W;
            if (me == null || w == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (envPlanet != w.CurrentPlanet || env == null) { env = new MotorEnv(w.CurrentPlanet); envPlanet = w.CurrentPlanet; ms.Pos = me.Pos; envSync = 0; }
            envSync -= dt;
            if (envSync <= 0)
            {
                envSync = 0.5f;
                var l = WorldGen.Get(w.CurrentPlanet);
                var scales = new float[l.Mounds.Count];
                for (int i = 0; i < scales.Length; i++) scales[i] = WorldView.MoundScale(w.Cur, l.Mounds[i].Area);
                env.Sync(w, scales);
            }
            InVehicle = me.Vehicle != null;
            Hud.InVehicle = InVehicle; Hud.VehicleId = me.Vehicle;
            bool blocked = UIState.BlocksGameplay;
            bool frozen = me.TowTimer > 0 || me.Sleeping;
            VehicleState veh = null;
            if (InVehicle) w.Cur.Vehicles.TryGetValue(me.Vehicle, out veh);

            // ------------------------------------------------ Bewegung
            // In der Bauansicht steuert der linke Stick den Bau-Cursor, nicht MIKO
            var move = blocked || frozen || PhotoMode.Active || (BuildMode.Active && InputMap.UsingPad) ? Vector2.zero : InputMap.Move();
            float camYaw = CameraRig.I != null ? CameraRig.I.Yaw * Mathf.Deg2Rad : 0f;
            bool sprint = !blocked && InputMap.Held(GameAction.Sprint) && me.Energy > 1f;
            if (veh != null)
            {
                if (Vector3.Distance(U(ms.Pos), U(veh.Pos)) > 3f && ms.Speed == 0) { ms.Pos = veh.Pos; ms.Yaw = veh.Yaw; }
                Motor.StepVehicle(ref ms, env, veh.Def, move.y, move.x, dt);
                stuckTime = ms.Blocked ? stuckTime + dt : 0f;
                Hud.VehicleStuck = stuckTime > 2f;
                Hud.InVehicle = true; Hud.VehicleId = veh.Id;
            }
            else
            {
                float fx = Mathf.Sin(camYaw), fz = Mathf.Cos(camYaw);
                float mx = move.x * fz + move.y * fx, mz = -move.x * fx + move.y * fz;
                float load = Mathf.Clamp01(Item.Volume(me.Bin) / Mathf.Max(1f, w.BinCapacity));
                float speedMul = (me.Energy <= 0.01f ? 0.55f : 1f) * (1f - 0.2f * load);
                float vertical = 0f;
                if (!blocked && !frozen)
                {
                    if (InputMap.Held(GameAction.DiveDown) || Input.GetKey(KeyCode.LeftControl)) vertical -= 1f;
                    if (InputMap.Held(GameAction.DiveUp)) vertical += 1f;
                }
                float windX = 0, windZ = 0;
                if (me.Exposed || w.Cur.StormActive)
                {
                    float dx, dz;
                    float wind = Rules.Wind(w, w.CurrentPlanet, out dx, out dz);
                    float push = Rules.ShelterKind(w, w.Cur, ms.Pos) > 0 ? 0f : wind * (w.Cur.StormActive ? 2.4f : 0.5f);
                    windX = dx * push; windZ = dz * push;
                }
                Motor.StepRobot(ref ms, env, mx, mz, sprint, vertical, speedMul, w.TechLevel("dive") > 0, dt, windX, windZ);
                Hud.Swimming = ms.Swimming; Hud.Diving = ms.Diving;
            }
            RenderPos = U(ms.Pos);
            RenderYaw = ms.Yaw;

            // ------------------------------------------------ Werkzeuge & Interaktion
            indoors = Rules.Indoors(Rules.ShelterKind(w, w.Cur, ms.Pos));
            bool acting = false;
            if (!blocked && !frozen && !PhotoMode.Active)
            {
                if (BuildMode.Active) HandleBuildMode(app, w, me);
                else
                {
                    DestroyPreview();
                    HandleToolSelect(w);
                    acting = veh != null ? HandleVehicle(app, w, me, veh, dt) : HandleTools(app, w, me, dt);
                    HandleInteraction(app, w, me, veh, dt);
                    HandleKeys(app, w, me, veh, dt);
                }
            }
            else if (!BuildMode.Active) DestroyPreview();
            if (!BuildMode.Active) { HidePadMarker(); BuildMode.PadCursorInit = false; }
            if (TrashRenderer.I != null)
            {
                TrashRenderer.I.HighlightKey = !blocked && target != null && veh == null ? target.Key : null;
                TrashRenderer.I.HighlightBlocked = targetErr != null;
            }
            Flags = (acting ? 1 : 0) | (sprint ? 2 : 0) | (ms.Diving ? 4 : 0);
            AudioManager.Loop("vacuum", "vacuum_loop", acting && tool == "vacuum" && veh == null, RenderPos, 0.7f);
            AudioManager.Loop("cutter", tool == "heat" ? "heat_loop" : tool == "filter" ? "filter_loop" : "cut_loop", acting && (tool == "cutter" || tool == "heat" || tool == "filter") && veh == null, RenderPos, 0.7f);
            AudioManager.Loop("engine", veh != null && veh.Id == "boat" ? "engine_loop" : "engine_loop", veh != null, RenderPos, 0.35f + Mathf.Abs(ms.Speed) * 0.04f, 0.8f + Mathf.Abs(ms.Speed) * 0.05f);
            AudioManager.Loop("wheels", "wheels_loop", veh == null && ms.Speed > 0.5f && !ms.Swimming, RenderPos, Mathf.Clamp01(ms.Speed / 8f) * 0.4f, 0.8f + ms.Speed * 0.05f);

            // ------------------------------------------------ Unterschlupf-Hinweis
            V3 sh; float sd;
            if (Rules.NearestShelter(w, w.Cur, ms.Pos, out sh, out sd)) { Hud.ShelterPos = U(sh); Hud.ShelterDist = sd; }

            // ------------------------------------------------ Senden (15×/s)
            sendTimer -= dt; sendAccum += dt;
            if (sendTimer <= 0)
            {
                sendTimer = 1f / 15f;
                app.Client.SendInput(ms.Pos, ms.Yaw, sprint, Flags, tool, sendAccum);
                sendAccum = 0;
            }
        }

        // ================================================================== Werkzeugwahl
        void HandleToolSelect(WorldState w)
        {
            var keys = new[] { GameAction.Tool1, GameAction.Tool2, GameAction.Tool3, GameAction.Tool4, GameAction.Tool5, GameAction.Tool6, GameAction.Tool7 };
            for (int i = 0; i < keys.Length && i < Rules.ToolIds.Length; i++)
                if (InputMap.Down(keys[i])) SelectTool(w, Rules.ToolIds[i]);
            if (InputMap.Down(GameAction.ToolNext)) Cycle(w, 1);
            if (InputMap.Down(GameAction.ToolPrev)) Cycle(w, -1);
            if (!Rules.HasTool(w, tool)) tool = "grab";
        }

        void SelectTool(WorldState w, string id)
        {
            if (!Rules.HasTool(w, id)) { Hud.Show(Loc.T(Rules.ToolName(id)) + Loc.T(" ist noch nicht vorhanden – in der Werkstatt kaufen."), ToastKind.Info, 2.5f); return; }
            if (tool != id) AudioManager.Ui("ui_click");
            tool = id; toolToggled = false; magnetCharge = -1f;
        }

        void Cycle(WorldState w, int dir)
        {
            int idx = System.Array.IndexOf(Rules.ToolIds, tool);
            for (int k = 1; k <= Rules.ToolIds.Length; k++)
            {
                var t = Rules.ToolIds[(idx + dir * k + Rules.ToolIds.Length * 2) % Rules.ToolIds.Length];
                if (Rules.HasTool(w, t)) { SelectTool(w, t); return; }
            }
        }

        bool UseHeld()
        {
            var s = GameApp.I.Settings;
            if (s.HoldActions) return InputMap.Held(GameAction.UseTool);
            if (InputMap.Down(GameAction.UseTool)) toolToggled = !toolToggled;
            return toolToggled;
        }

        // ================================================================== Werkzeuge zu Fuß
        ObjView FindTarget(WorldState w, PlayerData me, string forTool, out string err)
        {
            err = null;
            ObjView best = null, bestAny = null; float bd = float.MaxValue, bdAny = float.MaxValue; string bestErr = null;
            float fx = Mathf.Sin(ms.Yaw), fz = Mathf.Cos(ms.Yaw);
            float reach = GameData.InteractRange + (forTool == "cutter" || forTool == "heat" || forTool == "filter" ? 0.5f : 0f);
            float used = Item.Volume(me.Bin);
            foreach (var o in Rules.All(w.Cur))
            {
                float dx = o.Pos.x - ms.Pos.x, dz = o.Pos.z - ms.Pos.z;
                float d = Mathf.Sqrt(dx * dx + dz * dz) - Rules.ObjRadius(o.T);
                if (d > reach || Mathf.Abs(o.Pos.y - ms.Pos.y) > 3.5f) continue;
                float facing = d > 0.6f ? (dx * fx + dz * fz) / Mathf.Max(0.01f, d + Rules.ObjRadius(o.T)) : 1f;
                if (facing < -0.2f) continue;
                float score = d - facing * 1.2f;
                string e;
                switch (forTool)
                {
                    case "cutter": e = Rules.CutCheck(w, o); break;
                    case "heat": e = o.Frozen ? (w.TechLevel("heat") > 0 ? null : Loc.T("Wärmemodul nötig.")) : Loc.T("Nicht eingefroren."); break;
                    case "filter": e = o.T.Oil ? null : Loc.T("Kein Ölteppich."); break;
                    default: e = Rules.CollectCheck(w, o, forTool == "vacuum" ? "vacuum" : forTool == "magnet" ? "magnet" : "grab", ms.Pos, used); break;
                }
                if (e == null && score < bd) { bd = score; best = o; }
                if (score < bdAny) { bdAny = score; bestAny = o; bestErr = e; }
            }
            if (best != null) return best;
            err = bestErr;
            return bestAny;
        }

        bool HandleTools(GameApp app, WorldState w, PlayerData me, float dt)
        {
            actTimer -= dt;
            if (indoors)
            {
                // Im Unterschlupf ruhen die Werkzeuge (der Server lehnt Sammelaktionen hier ohnehin ab)
                target = null; targetErr = null; magnetCharge = -1f; toolToggled = false;
                if (InputMap.Held(GameAction.UseTool) || InputMap.Held(GameAction.AltTool)) Hud.SetBlocked(Rules.IndoorsDenied, "Im Unterschlupf");
                return false;
            }
            string forTool = tool == "seeder" ? "grab" : tool;
            scanTimer -= dt;
            if (scanTimer <= 0 || forTool != lastScanTool)
            {
                scanTimer = 0.1f; lastScanTool = forTool;
                target = FindTarget(w, me, forTool, out targetErr);
            }
            else if (target != null && Rules.Obj(w.Cur, target.Key) == null) { target = null; targetErr = null; }
            if (target != null && targetErr == null && forTool == "grab") Hud.SetPrompt("[" + InputMap.Label(GameAction.UseTool) + Loc.T("] Aufheben: ") + target.T.Name, InputMap.Label(GameAction.UseTool), Loc.T("Aufheben"), AboveObj(target));
            else if (target != null && targetErr != null) Hud.SetBlocked(target.T.Name + ": " + Loc.T(targetErr), ShortReason(Loc.T(targetErr)), AboveObj(target));
            bool held = UseHeld();
            bool acting = false;
            switch (tool)
            {
                case "grab":
                    if (held && actTimer <= 0 && target != null)
                    {
                        actTimer = 0.3f;
                        if (targetErr == null) Act(new JObj().Set("a", "grab").Set("o", target.Key));
                        else { Hud.Show(targetErr, ToastKind.Warning, 2.5f); AudioManager.Ui("beep_error"); actTimer = 0.8f; }
                    }
                    acting = held;
                    break;
                case "vacuum":
                    acting = held;
                    if (held && actTimer <= 0)
                    {
                        actTimer = 0.2f;
                        var cam = Camera.main;
                        var f = cam != null ? cam.transform.forward : new Vector3(Mathf.Sin(ms.Yaw), 0, Mathf.Cos(ms.Yaw));
                        Act(new JObj().Set("a", "vacuum").Set("dir", Json.Arr(Json.R(f.x, 3), Json.R(f.z, 3))));
                    }
                    if (held && FxView.I != null && Random.value < 0.5f) FxView.I.Burst(RenderPos + new Vector3(Mathf.Sin(ms.Yaw), 0.6f, Mathf.Cos(ms.Yaw)) * 1.6f, new Color(0.9f, 0.9f, 0.85f, 0.4f), 2, 0.5f, 0.12f, 0.3f, 0f);
                    break;
                case "magnet":
                    {
                        bool charging = held || InputMap.Held(GameAction.AltTool);
                        if (charging)
                        {
                            if (magnetCharge < 0) { magnetCharge = 0; AudioManager.Play("magnet_charge", RenderPos, 0.8f); }
                            magnetCharge = Mathf.Min(1f, magnetCharge + dt / 1.1f);
                            Hud.MagnetCharge = magnetCharge;
                            acting = true;
                        }
                        else if (magnetCharge >= 0)
                        {
                            var f = new Vector3(Mathf.Sin(ms.Yaw), 0, Mathf.Cos(ms.Yaw));
                            var cam = Camera.main;
                            if (cam != null) { f = cam.transform.forward; f.y = 0; f.Normalize(); }
                            Act(new JObj().Set("a", "magnet").Set("charge", Json.R(Mathf.Max(0.2f, magnetCharge), 2)).Set("dir", Json.Arr(Json.R(f.x, 3), Json.R(f.z, 3))));
                            magnetCharge = -1f;
                        }
                        Hud.SetPrompt("[" + InputMap.Label(GameAction.UseTool) + Loc.T(" halten] Magnetwelle aufladen, loslassen zum Auslösen"), InputMap.Label(GameAction.UseTool), Loc.T("Magnetwelle"), null, true);
                        break;
                    }
                case "cutter":
                case "heat":
                case "filter":
                    {
                        string act = tool == "cutter" ? "cut" : tool == "heat" ? "thaw" : "filter";
                        string label = tool == "cutter" ? Loc.T("Zerlegen") : tool == "heat" ? Loc.T("Auftauen") : Loc.T("Filtern");
                        if (target != null && targetErr == null) Hud.SetPrompt("[" + InputMap.Label(GameAction.UseTool) + Loc.T(" halten] ") + label + ": " + target.T.Name, InputMap.Label(GameAction.UseTool), label, AboveObj(target), true);
                        if (held && target != null && targetErr == null)
                        {
                            acting = true;
                            if (actTimer <= 0 && !pending)
                            {
                                actTimer = 0.25f; pending = true;
                                var key = target.Key;
                                Act(new JObj().Set("a", act).Set("o", key).Set("dt", 0.25f), r =>
                                {
                                    pending = false;
                                    if (r.Ok && r.Data != null && r.Data.Has("p")) { lastProgress = r.Data.Float("p"); progressUntil = Time.time + 0.6f; progressLabel = label; }
                                    if (r.Ok && r.Data != null && r.Data.Bool("done")) { lastProgress = 1f; progressUntil = Time.time + 0.4f; }
                                });
                            }
                            if (tool == "cutter" && FxView.I != null && Random.value < 0.4f) FxView.I.Burst(U(target.Pos) + Vector3.up * 0.6f, new Color(1f, 0.75f, 0.3f), 3, 3f, 0.06f, 0.4f, 1.2f, true);
                            if (tool == "heat" && FxView.I != null && Random.value < 0.3f) FxView.I.Burst(U(target.Pos) + Vector3.up * 0.8f, new Color(1f, 1f, 1f, 0.5f), 2, 0.6f, 0.5f, 1.2f, -0.2f);
                        }
                        if (Time.time < progressUntil) { Hud.Progress = lastProgress; Hud.ProgressLabel = progressLabel; }
                        break;
                    }
                case "seeder":
                    {
                        var l = WorldGen.Get(w.CurrentPlanet);
                        Spot spot = null; float best = 4.5f;
                        foreach (var s in l.Eco) { float d = V3.DistXZ(s.Pos, ms.Pos); if (d < best && !w.Cur.Eco.ContainsKey(s.Id)) { best = d; spot = s; } }
                        if (spot != null)
                        {
                            Hud.SetPrompt("[" + InputMap.Label(GameAction.UseTool) + "] " + GameData.Planets[w.CurrentPlanet].EcoName + Loc.T(" (15 Credits)"), InputMap.Label(GameAction.UseTool), Loc.T("Pflanzen"), U(spot.Pos) + Vector3.up * 1.2f);
                            if (InputMap.Down(GameAction.UseTool)) Act(new JObj().Set("a", "plant").Set("s", spot.Id));
                        }
                        else Hud.SetPrompt(Loc.T("Bio-Modul: Pflanzstelle suchen (Karte)"), null, Loc.T("Pflanzstelle suchen"));
                        break;
                    }
            }
            if (tool != "magnet") magnetCharge = -1f;
            return acting;
        }

        float lastProgress, progressUntil, scanTimer, interactScan;
        string progressLabel, lastScanTool;
        Interaction cachedInteraction;
        string cachedStation;

        // ================================================================== Fahrzeuge
        bool HandleVehicle(GameApp app, WorldState w, PlayerData me, VehicleState v, float dt)
        {
            target = null; targetErr = null;
            actTimer -= dt;
            bool held = InputMap.Held(GameAction.UseTool);
            var bl = WorldGen.Get(w.CurrentPlanet).Base;
            bool atBase = V3.DistXZ(ms.Pos, bl.DropZone) <= bl.DropRadius + 5f || V3.DistXZ(ms.Pos, bl.Stations["storage"]) < 10f || (v.Id == "boat" && V3.DistXZ(ms.Pos, bl.BoatSpot) < 14f);
            if (v.Id == "rover")
            {
                string fill = Item.Volume(v.Cargo).ToString("0") + "/" + v.Def.Capacity.ToString("0");
                if (atBase && (v.Cargo.Count > 0 || v.Carry != null)) Hud.SetPrompt("[" + InputMap.Label(GameAction.Interact) + Loc.T("] Abladen"), InputMap.Label(GameAction.Interact), Loc.T("Abladen"));
                else if (indoors) Hud.SetPrompt(Loc.T("Im Unterschlupf – Ansaugen erst draußen (") + fill + ")", null, Loc.T("Im Unterschlupf"));
                else Hud.SetPrompt("[" + InputMap.Label(GameAction.UseTool) + Loc.T(" halten] Ansaugen (") + fill + ")", InputMap.Label(GameAction.UseTool), Loc.T("Ansaugen ") + fill, null, true);
                if (held && actTimer <= 0 && !indoors) { actTimer = 0.25f; Act(new JObj().Set("a", "vcollect")); }
                if (InputMap.Down(GameAction.Interact) && atBase) Act(new JObj().Set("a", "vunload"));
                return held;
            }
            if (v.Id == "boat")
            {
                if (Mathf.Abs(ms.Speed) > 0.5f && actTimer <= 0) { actTimer = 0.25f; Act(new JObj().Set("a", "boatnet")); }
                string fill = Item.Volume(v.Cargo).ToString("0") + "/" + v.Def.Capacity.ToString("0");
                if (atBase) Hud.SetPrompt("[" + InputMap.Label(GameAction.Interact) + Loc.T("] Im Hafen abladen"), InputMap.Label(GameAction.Interact), Loc.T("Abladen"));
                else Hud.SetPrompt(Loc.T("Über Treibgut fahren (") + fill + ")", null, Loc.T("Treibgut ") + fill);
                if (InputMap.Down(GameAction.Interact) && atBase) Act(new JObj().Set("a", "vunload"));
                return Mathf.Abs(ms.Speed) > 0.5f;
            }
            // Kran
            if (indoors) { Hud.SetPrompt("Im Unterschlupf – der Kran arbeitet nur draußen", null, "Im Unterschlupf"); return false; }
            if (v.Carry != null)
            {
                VehicleState rover;
                bool nearRover = w.Cur.Vehicles.TryGetValue("rover", out rover) && rover.Carry == null && V3.DistXZ(rover.Pos, ms.Pos) < 11f;
                Hud.SetPrompt("[" + InputMap.Label(GameAction.Interact) + "] " + (nearRover ? Loc.T("Auf den Transportrover setzen") : atBase ? Loc.T("Am Stützpunkt verwerten") : Loc.T("Absetzen")),
                    InputMap.Label(GameAction.Interact), nearRover ? Loc.T("Auf Rover") : atBase ? Loc.T("Verwerten") : Loc.T("Absetzen"));
                if (InputMap.Down(GameAction.Interact)) Act(new JObj().Set("a", "cdrop"));
                return false;
            }
            ObjView wreck = null; float best = float.MaxValue;
            foreach (var o in Rules.All(w.Cur))
            {
                if (!o.T.Crane) continue;
                float d = V3.DistXZ(o.Pos, ms.Pos) - Rules.ObjRadius(o.T);
                if (d < 9f && d < best) { best = d; wreck = o; }
            }
            if (wreck == null) { Hud.SetPrompt(Loc.T("An ein großes Wrack heranfahren"), null, Loc.T("Wrack suchen")); return false; }
            Hud.SetPrompt("[" + InputMap.Label(GameAction.Interact) + Loc.T(" halten] Anheben: ") + wreck.T.Name + Loc.T(" (Mitspieler können helfen)"), InputMap.Label(GameAction.Interact), Loc.T("Anheben"), AboveObj(wreck), true);
            bool lift = InputMap.Held(GameAction.Interact);
            if (lift && actTimer <= 0 && !pending)
            {
                actTimer = 0.25f; pending = true;
                Act(new JObj().Set("a", "clift").Set("o", wreck.Key).Set("dt", 0.25f), r =>
                {
                    pending = false;
                    if (r.Ok && r.Data != null && r.Data.Has("p")) { lastProgress = r.Data.Float("p"); progressUntil = Time.time + 0.6f; progressLabel = Loc.T("Anheben") + (r.Data.Int("help") > 0 ? " (" + r.Data.Int("help") + Loc.T(" Helfer)") : ""); }
                });
                if (CameraRig.I != null) CameraRig.I.Shake(0.05f);
                AudioManager.Play("crane", RenderPos, 0.5f);
            }
            if (Time.time < progressUntil) { Hud.Progress = lastProgress; Hud.ProgressLabel = progressLabel; }
            return lift;
        }

        // ================================================================== Interaktion (E)
        void HandleInteraction(GameApp app, WorldState w, PlayerData me, VehicleState veh, float dt)
        {
            if (veh != null) return;
            var l = WorldGen.Get(w.CurrentPlanet);
            interactScan -= dt;
            if (interactScan > 0 && cachedInteraction != null && !cachedInteraction.Hold)
            {
                current = cachedInteraction;
                if (current.Kind != "none" && current.Kind != "sleep" || Hud.Prompt == null) ApplyPrompt(current);
                Hud.NearStation = cachedStation;
                if (InputMap.Down(GameAction.Interact)) RunInteraction(current);
                return;
            }
            interactScan = 0.12f;
            current = null;
            // Stationen
            foreach (var kv in l.Base.Stations)
            {
                if (kv.Key == "build") continue;
                float r = kv.Key == "ship" ? GameData.StationRange + 3f : GameData.StationRange;
                if (V3.DistXZ(ms.Pos, kv.Value) > r) continue;
                Hud.NearStation = kv.Key;
                string ek = InputMap.Label(GameAction.Interact);
                string key = "[" + ek + "] ";
                switch (kv.Key)
                {
                    case "storage":
                        current = me.Bin.Count > 0 ? new Interaction { Kind = "deposit", Station = "storage", Label = key + Loc.T("Einlagern (") + me.Bin.Count + ")", Key = ek, Word = Loc.T("Einlagern") }
                                                   : new Interaction { Kind = "deposit", Station = "storage", Label = Loc.T("Lager: Behälter leer"), Word = Loc.T("Behälter leer") };
                        break;
                    case "sell": current = new Interaction { Kind = "menu", Station = "sell", Label = key + Loc.T("Verkaufen"), Key = ek, Word = Loc.T("Verkaufen") }; break;
                    case "workshop": current = new Interaction { Kind = "menu", Station = "workshop", Label = key + Loc.T("Werkstatt"), Key = ek, Word = Loc.T("Werkstatt") }; break;
                    case "sort": current = new Interaction { Kind = "sort", Hold = true, Label = "[" + ek + Loc.T(" halten] Sortieren"), Key = ek, Word = Loc.T("Sortieren") }; break;
                    case "trader": current = new Interaction { Kind = "menu", Station = "trader", Label = key + Loc.T("Materialhändler"), Key = ek, Word = Loc.T("Händler") }; break;
                    case "disposal": current = new Interaction { Kind = "dispose", Label = key + Loc.T("Gefahrstoffe entsorgen"), Key = ek, Word = Loc.T("Entsorgen") }; break;
                    case "contracts": current = new Interaction { Kind = "menu", Station = "contracts", Label = key + Loc.T("Aufträge & Lieferungen"), Key = ek, Word = Loc.T("Aufträge") }; break;
                    case "ship": current = new Interaction { Kind = "menu", Station = "ship", Label = key + Loc.T("Reisen (Transportschiff)"), Key = ek, Word = Loc.T("Reisen") }; break;
                    case "garage": current = new Interaction { Kind = "menu", Station = "garage", Label = key + Loc.T("Garage"), Key = ek, Word = Loc.T("Garage") }; break;
                    case "charge":
                        {
                            string c = me.Energy < w.MaxEnergy - 0.5f ? Loc.T("Lädt ") + (me.Energy / w.MaxEnergy * 100f).ToString("0") + " %" : Loc.T("Akku voll");
                            current = new Interaction { Kind = "none", Label = c, Word = c };
                            break;
                        }
                }
                if (current != null) { current.At = U(kv.Value) + Vector3.up * (kv.Key == "ship" ? 5.5f : 2.4f); break; }
            }
            if (current == null && l.Base.InBase(ms.Pos.x, ms.Pos.z) && ms.Pos.z > l.Base.GridZ0 - 2)
                if (Hud.Prompt == null) Hud.SetPrompt("[" + InputMap.Label(GameAction.Build) + Loc.T("] Bauansicht"), InputMap.Label(GameAction.Build), Loc.T("Bauen"));
            // Fahrzeuge
            if (current == null)
                foreach (var v in w.Cur.Vehicles.Values)
                {
                    float d = V3.DistXZ(ms.Pos, v.Pos);
                    if (d > v.Def.Radius + 3.2f) continue;
                    if (v.Id == "rover" && me.Bin.Count > 0) current = new Interaction { Kind = "vload", Label = "[" + InputMap.Label(GameAction.Interact) + Loc.T("] Auf die Ladefläche umladen · [") + InputMap.Label(GameAction.Vehicle) + Loc.T("] Einsteigen"), Key = InputMap.Label(GameAction.Interact), Word = Loc.T("Umladen") };
                    else current = new Interaction { Kind = "none", Label = "[" + InputMap.Label(GameAction.Vehicle) + Loc.T("] Einsteigen: ") + v.Def.Name, Key = InputMap.Label(GameAction.Vehicle), Word = Loc.T("Einsteigen") };
                    current.At = U(v.Pos) + Vector3.up * 3f;
                    break;
                }
            // Projektplätze
            if (current == null)
                for (int a = 0; a < 3; a++)
                {
                    if (V3.DistXZ(ms.Pos, l.ProjectSites[a]) > 9f) continue;
                    var pid = GameData.ProjectId(w.CurrentPlanet, a);
                    var pd = GameData.Projects[pid];
                    var why = Rules.ProjectCheck(w, pid);
                    if (why == null) current = new Interaction { Kind = "project", Id = a.ToString(), Label = "[" + InputMap.Label(GameAction.Interact) + Loc.T("] Projekt starten: „") + pd.Name + "“ (" + pd.Credits + Loc.T(" Credits + Material)"), Key = InputMap.Label(GameAction.Interact), Word = Loc.T("Projekt starten") };
                    else current = new Interaction { Kind = "none", Label = "„" + pd.Name + "“: " + Loc.T(why), Word = ShortReason(Loc.T(why)) };
                    current.At = U(l.ProjectSites[a]) + Vector3.up * 3f;
                    break;
                }
            // Punkte in der Welt
            if (current == null)
            {
                foreach (var s in l.Repairs)
                    if (!w.Cur.Repaired.Contains(s.Id) && V3.DistXZ(ms.Pos, s.Pos) < 4.5f)
                    {
                        var cost = Rules.RepairCost(w.CurrentPlanet);
                        var sb = new System.Text.StringBuilder();
                        foreach (var kv in cost) sb.Append(kv.Value).Append(' ').Append(GameData.Materials[kv.Key].Name).Append(' ');
                        current = new Interaction { Kind = "repair", Id = s.Id, Hold = true, Label = "[" + InputMap.Label(GameAction.Interact) + Loc.T(" halten] ") + Loc.T(s.Name) + " (" + sb.ToString().Trim() + Loc.T(" aus dem Lager)"), Key = InputMap.Label(GameAction.Interact), Word = Loc.T("Reparieren"), At = U(s.Pos) + Vector3.up * 2.2f };
                        break;
                    }
            }
            if (current == null)
                foreach (var s in l.LoreSpots)
                    if (!w.Lore.Contains(s.Id) && V3.DistXZ(ms.Pos, s.Pos) < 3.5f) { current = new Interaction { Kind = "lore", Id = s.Id, Label = "[" + InputMap.Label(GameAction.Interact) + Loc.T("] Fundstück aufheben"), Key = InputMap.Label(GameAction.Interact), Word = Loc.T("Fundstück"), At = U(s.Pos) + Vector3.up * 1.2f }; break; }
            if (current == null)
                foreach (var s in l.Viewpoints)
                    if (V3.DistXZ(ms.Pos, s.Pos) < 7f) { current = new Interaction { Kind = "view", Id = s.Id, Label = "[" + InputMap.Label(GameAction.Interact) + "] " + Loc.T(s.Name) + Loc.T(" merken (Fotomodus)"), Key = InputMap.Label(GameAction.Interact), Word = Loc.T("Aussicht merken"), At = U(s.Pos) + Vector3.up * 2f }; break; }
            if (current == null)
                foreach (var o in Rules.All(w.Cur))
                    if (o.T.Crane && V3.DistXZ(ms.Pos, o.Pos) < 8f)
                    {
                        current = new Interaction { Kind = "help", Id = o.Key, Hold = true, Label = "[" + InputMap.Label(GameAction.Interact) + Loc.T(" halten] Beim Anheben helfen (Kran nötig)"), Key = InputMap.Label(GameAction.Interact), Word = Loc.T("Mitheben"), At = AboveObj(o) };
                        break;
                    }
            // Helferroboter (reparieren, mitnehmen, absetzen)
            if (current == null) current = BotInteraction(w, me);
            // Unterschlupf: drinnen Schlafen anbieten, draußen am Tor/an der Rampe auf den Schutzraum hinweisen
            bool night = Rules.IsNight(w, w.CurrentPlanet);
            int shelterKind = Rules.ShelterKind(w, w.Cur, ms.Pos);
            bool passive = current == null || current.Kind == "none" || (current.Kind == "deposit" && me.Bin.Count == 0);
            if (shelterKind > 0 && (night || w.Cur.StormActive) && (current == null || Rules.Indoors(shelterKind) && passive))
            {
                // Tagsüber im Sturm wird nicht geschlafen, sondern abgewartet (Zeitraffer, der Sturm läuft weiter)
                string sw = Loc.T(!night && w.Cur.StormActive ? "Sturm abwarten" : "Schlafen");
                current = new Interaction { Kind = "sleep", Label = "[" + InputMap.Label(GameAction.Sleep) + "] " + sw, Key = InputMap.Label(GameAction.Sleep), Word = sw };
            }
            else if (passive && !Rules.Indoors(shelterKind) && (night || w.Cur.StormActive || w.Cur.StormWarn))
            {
                var room = EntranceNear(l, ms.Pos);
                if (room != null)
                    current = new Interaction
                    {
                        Kind = "none", Word = Loc.T("Unterschlupf"), At = EntranceAnchor(room),
                        Label = Loc.T(room.Kind == Rules.ShelterShip ? (w.Cur.StormActive || w.Cur.StormWarn ? "Unterschlupf: über die Rampe ins Transportschiff fahren – geschützt vor dem Sturm" : "Unterschlupf: über die Rampe ins Transportschiff fahren – geschützt vor der Nacht")
                            : (w.Cur.StormActive || w.Cur.StormWarn ? "Unterschlupf: durchs Rolltor in den Hangar fahren – geschützt vor dem Sturm" : "Unterschlupf: durchs Rolltor in den Hangar fahren – geschützt vor der Nacht")),
                    };
            }
            if (current != null && Hud.Prompt == null || current != null && current.Kind != "none" && current.Kind != "sleep") ApplyPrompt(current);
            else if (current != null && current.Kind == "none" && Hud.Prompt == null) ApplyPrompt(current);

            cachedInteraction = current;
            cachedStation = Hud.NearStation;
            if (current == null) return;
            if (current.Hold)
            {
                if (InputMap.Held(GameAction.Interact) && actTimer <= 0 && !pending)
                {
                    actTimer = 0.25f;
                    var a = current.Kind == "sort" ? new JObj().Set("a", "sortm").Set("dt", 0.25f)
                          : current.Kind == "repair" ? new JObj().Set("a", "repair").Set("s", current.Id).Set("dt", 0.25f)
                          : current.Kind == "botfix" ? new JObj().Set("a", "botfix").Set("s", current.Id).Set("dt", 0.25f)
                          : new JObj().Set("a", "help").Set("o", current.Id);
                    pending = true;
                    string label = current.Kind == "repair" || current.Kind == "botfix" ? Loc.T("Reparieren") : current.Kind == "sort" ? Loc.T("Sortieren") : null;
                    Act(a, r =>
                    {
                        pending = false;
                        if (r.Ok && r.Data != null && r.Data.Has("p") && label != null) { lastProgress = r.Data.Float("p"); progressUntil = Time.time + 0.6f; progressLabel = label; }
                    });
                }
                if (Time.time < progressUntil) { Hud.Progress = lastProgress; Hud.ProgressLabel = progressLabel; }
                return;
            }
            if (!InputMap.Down(GameAction.Interact)) return;
            RunInteraction(current);
        }

        /// <summary>Schutzraum, dessen Eingang (Tor bzw. Rampe) höchstens 9 m entfernt ist, oder null.</summary>
        static ShelterRoom EntranceNear(PlanetLayout l, V3 p)
        {
            ShelterRoom best = null; float bd = 9f;
            foreach (var r in l.Base.Rooms)
            {
                var a = EntranceAnchor(r);
                float d = Mathf.Sqrt((a.x - p.x) * (a.x - p.x) + (a.z - p.z) * (a.z - p.z));
                if (d < bd) { bd = d; best = r; }
            }
            return best;
        }

        /// <summary>Hinweis-Anker über der Mitte des Eingangs (Torsturz bzw. Oberkante der Heckluke).</summary>
        static Vector3 EntranceAnchor(ShelterRoom r)
        {
            var i = r.Inner;
            float x = Mathf.Abs(r.OutX) > 0.5f ? i.Cx + r.OutX * i.Hx : r.Door.Cx;
            float z = Mathf.Abs(r.OutZ) > 0.5f ? i.Cz + r.OutZ * i.Hz : r.Door.Cz;
            return new Vector3(x + r.OutX * 0.6f, i.Y0 + Mathf.Min(i.H, 3.4f), z + r.OutZ * 0.6f);
        }

        static void ApplyPrompt(Interaction i)
        {
            Hud.SetPrompt(i.Label, i.Key, i.Word, i.At, i.Hold);
        }

        /// <summary>Ankerpunkt für Hinweise über einem Objekt.</summary>
        Vector3 AboveObj(ObjView o)
        {
            return U(o.Pos) + Vector3.up * (Rules.ObjRadius(o.T) * 1.3f + 0.7f);
        }

        /// <summary>Kurzform eines Grundes für das ruhige HUD: erster Satzteil, höchstens gut 30 Zeichen.</summary>
        static string ShortReason(string why)
        {
            if (string.IsNullOrEmpty(why)) return why;
            string s = why.Trim();
            foreach (var sep in new[] { " – ", ". ", " (", ", " })
            {
                int i = s.IndexOf(sep, System.StringComparison.Ordinal);
                if (i > 6) s = s.Substring(0, i);
            }
            s = s.TrimEnd('.', ' ');
            if (s.Length > 34) s = s.Substring(0, 32).TrimEnd() + "…";
            return s;
        }

        void RunInteraction(Interaction current)
        {
            switch (current.Kind)
            {
                case "deposit": Act(new JObj().Set("a", "deposit")); break;
                case "dispose": Act(new JObj().Set("a", "dispose")); break;
                case "botfollow": Act(new JObj().Set("a", "botfollow").Set("s", current.Id)); break;
                case "botstay": Act(new JObj().Set("a", "botstay").Set("s", current.Id)); break;
                case "vload": Act(new JObj().Set("a", "vload").Set("v", "rover")); break;
                case "project": Act(new JObj().Set("a", "project").Set("area", int.Parse(current.Id))); break;
                case "lore": Act(new JObj().Set("a", "lore").Set("s", current.Id), r => { if (r.Ok) { UIState.MenuTab = "archive"; } }); break;
                case "view": Act(new JObj().Set("a", "view").Set("s", current.Id), r => { if (r.Ok) Hud.Show(Loc.T("Aussichtspunkt gemerkt – im Fotomodus [") + InputMap.Label(GameAction.Photo) + Loc.T("] anspringbar."), ToastKind.Success, 4f); }); break;
                case "menu":
                    UIState.Station = current.Station;
                    UIState.MenuTab = current.Station == "workshop" || current.Station == "ship" || current.Station == "garage" ? "workshop" : "storage";
                    UIState.Open(UIScreen.Menu);
                    AudioManager.Ui("ui_click");
                    break;
            }
        }

        // ================================================================== Tasten
        void HandleKeys(GameApp app, WorldState w, PlayerData me, VehicleState veh, float dt)
        {
            if (InputMap.Down(GameAction.Vehicle))
            {
                if (veh != null) Act(new JObj().Set("a", "vexit"));
                else
                {
                    VehicleState best = null; float bd = float.MaxValue;
                    foreach (var v in w.Cur.Vehicles.Values) { float d = V3.DistXZ(ms.Pos, v.Pos) - v.Def.Radius; if (d < 3.5f && d < bd) { bd = d; best = v; } }
                    if (best != null) Act(new JObj().Set("a", "venter").Set("v", best.Id), r => { if (r.Ok) { ms.Pos = best.Pos; ms.Yaw = best.Yaw; ms.Speed = 0; } });
                    else Hud.Show(Loc.T("Kein Fahrzeug in der Nähe."), ToastKind.Info, 2f);
                }
            }
            if (InputMap.Down(GameAction.Press) && veh == null) Act(new JObj().Set("a", "press"));
            if (InputMap.Down(GameAction.Sleep)) Act(new JObj().Set("a", me.Sleeping || me.Waiting ? "wake" : "sleep"));
            if (InputMap.Down(GameAction.Shelter)) Act(new JObj().Set("a", "shelter"));
            if (InputMap.Down(GameAction.Emote))
            {
                app.Client.SendEmote("happy");
                if (ActorsView.I != null && ActorsView.I.LocalRobot != null) ActorsView.I.LocalRobot.Emote("happy");
                AudioManager.Play("beep_happy", RenderPos);
            }
            // Fahrzeug zurücksetzen (X) bzw. zu Fuß 2 s halten: zurück zum Stützpunkt
            if (veh != null && InputMap.Down(GameAction.VehicleReset)) Act(new JObj().Set("a", "vreset").Set("v", veh.Id));
            if (veh == null && InputMap.Held(GameAction.VehicleReset))
            {
                resetHold += dt;
                Hud.Progress = resetHold / 2f; Hud.ProgressLabel = Loc.T("Zum Stützpunkt zurückkehren");
                if (resetHold >= 2f) { resetHold = 0; Act(new JObj().Set("a", "respawn")); }
            }
            else resetHold = 0;
        }

        // ================================================================== Bauansicht
        void HandleBuildMode(GameApp app, WorldState w, PlayerData me)
        {
            target = null;
            var bl = WorldGen.Get(w.CurrentPlanet).Base;
            if (!bl.InBase(ms.Pos.x, ms.Pos.z)) { BuildMode.Active = false; Hud.Show(Loc.T("Bauansicht nur am Stützpunkt."), ToastKind.Info); return; }
            if (InputMap.Down(GameAction.RotateBuild)) BuildMode.Rot = (BuildMode.Rot + 1) % 4;
            var cam = Camera.main;
            BuildMode.HasCursor = false;
            if (cam == null) return;
            Vector3 hit;
            bool pad = InputMap.UsingPad;
            if (pad)
            {
                hit = PadBuildCursor(bl);
                ShowPadMarker(bl, hit);
            }
            else
            {
                HidePadMarker();
                var ray = cam.ScreenPointToRay(Input.mousePosition);
                var plane = new Plane(Vector3.up, new Vector3(0, bl.Center.y, 0));
                float enter;
                if (!plane.Raycast(ray, out enter)) { DestroyPreview(); return; }
                hit = ray.GetPoint(enter);
                BuildMode.PadCursor = hit; // Controller setzt dort fort, wo die Maus zuletzt war
                BuildMode.PadCursorInit = true;
            }
            int gx = Mathf.FloorToInt((hit.x - bl.GridX0) / bl.Cell), gz = Mathf.FloorToInt((hit.z - bl.GridZ0) / bl.Cell);
            // Überfahrenes Gebäude (für Umsetzen/Abreißen)
            BuildMode.HoverBuildingId = -1;
            foreach (var b in w.Cur.Buildings)
                if (gx >= b.Gx && gx < b.Gx + b.W && gz >= b.Gz && gz < b.Gz + b.H) { BuildMode.HoverBuildingId = b.Id; break; }
            if (BuildMode.RequestDemolish)
            {
                BuildMode.RequestDemolish = false;
                int id = BuildMode.MoveId >= 0 ? BuildMode.MoveId : BuildMode.HoverBuildingId;
                if (id >= 0) Act(new JObj().Set("a", "demolish").Set("b", id));
                BuildMode.MoveId = -1;
            }
            string type = BuildMode.Type;
            if (BuildMode.MoveId >= 0)
            {
                var mb = w.Cur.Buildings.Find(x => x.Id == BuildMode.MoveId);
                if (mb == null) BuildMode.MoveId = -1; else type = mb.Type;
            }
            if (string.IsNullOrEmpty(type) || !GameData.Buildings.ContainsKey(type)) { DestroyPreview(); return; }
            var def = GameData.Buildings[type];
            int wdt = BuildMode.Rot % 2 == 0 ? def.W : def.H, hgt = BuildMode.Rot % 2 == 0 ? def.H : def.W;
            gx -= wdt / 2; gz -= hgt / 2;
            BuildMode.Gx = gx; BuildMode.Gz = gz; BuildMode.HasCursor = true;
            string reason = Rules.CanPlace(w, w.Cur, type, gx, gz, BuildMode.Rot, BuildMode.MoveId);
            if (reason == null && BuildMode.MoveId < 0)
            {
                if (Rules.CountOf(w.Cur, type) >= def.MaxCount) reason = Loc.T("Maximal ") + def.MaxCount + Loc.T("× pro Stützpunkt.");
                else
                {
                    var miss = Rules.MissingText(w.Cur, def.Mats, def.Cost, w.Credits);
                    if (miss.Length > 0) reason = Loc.T("Es fehlen: ") + miss + ".";
                }
            }
            BuildMode.Valid = reason == null;
            BuildMode.Reason = reason;
            ShowPreview(type, bl, gx, gz, BuildMode.Rot, BuildMode.Valid);
            bool click = (!pad && Input.GetMouseButtonDown(0) && !MouseOverUi()) || InputMap.Down(GameAction.Interact) || BuildMode.RequestPlace;
            BuildMode.RequestPlace = false;
            if (click)
            {
                if (!BuildMode.Valid) { Hud.Show(reason, ToastKind.Warning, 2.5f); AudioManager.Ui("beep_error"); }
                else if (BuildMode.MoveId >= 0) { Act(new JObj().Set("a", "move").Set("b", BuildMode.MoveId).Set("x", gx).Set("z", gz).Set("r", BuildMode.Rot)); BuildMode.MoveId = -1; }
                else Act(new JObj().Set("a", "build").Set("t", type).Set("x", gx).Set("z", gz).Set("r", BuildMode.Rot));
            }
            if (Input.GetMouseButtonDown(1)) { BuildMode.MoveId = -1; BuildMode.Type = null; }
        }

        // ------------------------------------------------------------ Controller-Cursor in der Bauansicht
        GameObject padMarker;

        /// <summary>Bewegt den Bau-Cursor mit dem linken Stick (relativ zur Kamera), rastet im Stillstand auf die Zellmitte ein.</summary>
        Vector3 PadBuildCursor(BaseLayout bl)
        {
            float y = bl.Center.y;
            if (!BuildMode.PadCursorInit)
            {
                BuildMode.PadCursor = new Vector3(bl.GridX0 + bl.GridW * bl.Cell * 0.5f, y, bl.GridZ0 + bl.GridH * bl.Cell * 0.5f);
                BuildMode.PadCursorInit = true;
            }
            var c = BuildMode.PadCursor;
            var st = InputMap.PadStick();
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (st.sqrMagnitude > 0.0001f)
            {
                float yaw = CameraRig.I != null ? CameraRig.I.Yaw * Mathf.Deg2Rad : 0f;
                float fx = Mathf.Sin(yaw), fz = Mathf.Cos(yaw);
                float speed = 16f * (0.35f + 0.65f * st.magnitude);
                c.x += (st.x * fz + st.y * fx) * speed * dt;
                c.z += (-st.x * fx + st.y * fz) * speed * dt;
            }
            else
            {
                // Einrasten: sanft auf die Mitte der aktuellen Zelle
                float cx = bl.GridX0 + (Mathf.Floor((c.x - bl.GridX0) / bl.Cell) + 0.5f) * bl.Cell;
                float cz = bl.GridZ0 + (Mathf.Floor((c.z - bl.GridZ0) / bl.Cell) + 0.5f) * bl.Cell;
                float k = 1f - Mathf.Exp(-dt * 14f);
                c.x += (cx - c.x) * k; c.z += (cz - c.z) * k;
            }
            c.x = Mathf.Clamp(c.x, bl.GridX0 + 0.01f, bl.GridX0 + bl.GridW * bl.Cell - 0.01f);
            c.z = Mathf.Clamp(c.z, bl.GridZ0 + 0.01f, bl.GridZ0 + bl.GridH * bl.Cell - 0.01f);
            c.y = y;
            BuildMode.PadCursor = c;
            return c;
        }

        /// <summary>Flache Markierung der Zelle unter dem Controller-Cursor (auch ohne gewähltes Bauwerk – zum Umsetzen/Abreißen).</summary>
        void ShowPadMarker(BaseLayout bl, Vector3 at)
        {
            if (padMarker == null)
            {
                padMarker = new GameObject("BuildPadCursor");
                padMarker.AddComponent<MeshFilter>().sharedMesh = MeshKit.Cube;
                padMarker.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get(Mats.Fade, new Color(1f, 1f, 1f, 0.45f));
            }
            if (!padMarker.activeSelf) padMarker.SetActive(true);
            int gx = Mathf.FloorToInt((at.x - bl.GridX0) / bl.Cell), gz = Mathf.FloorToInt((at.z - bl.GridZ0) / bl.Cell);
            var c = bl.CellCenter(gx, gz, 1, 1);
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f);
            padMarker.transform.position = new Vector3(c.x, bl.Center.y + 0.08f, c.z);
            padMarker.transform.localScale = new Vector3(bl.Cell * pulse, 0.12f, bl.Cell * pulse);
        }

        void HidePadMarker() { if (padMarker != null && padMarker.activeSelf) padMarker.SetActive(false); }

        /// <summary>Bauleiste (mit Kategorie-Reitern) liegt am unteren Bildrand – dort keine Platzierung.</summary>
        static bool MouseOverUi() { return Input.mousePosition.y < Screen.height * 0.25f; }

        void ShowPreview(string type, BaseLayout bl, int gx, int gz, int rot, bool valid)
        {
            string key = type + rot + valid;
            var def = GameData.Buildings[type];
            int w = rot % 2 == 0 ? def.W : def.H, h = rot % 2 == 0 ? def.H : def.W;
            if (preview == null || previewKey != key)
            {
                DestroyPreview();
                previewKey = key;
                preview = new GameObject("BuildPreview");
                preview.AddComponent<MeshFilter>().sharedMesh = MeshKit.Cube;
                preview.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get(Mats.Fade, valid ? new Color(0.3f, 1f, 0.5f, 0.35f) : new Color(1f, 0.3f, 0.25f, 0.35f));
            }
            var c = bl.CellCenter(gx, gz, w, h);
            preview.transform.position = new Vector3(c.x, bl.Center.y + 1.2f, c.z);
            preview.transform.localScale = new Vector3(w * bl.Cell - 0.1f, 2.4f, h * bl.Cell - 0.1f);
        }

        void DestroyPreview()
        {
            if (preview != null) Destroy(preview);
            preview = null; previewKey = null;
        }
    }
}
