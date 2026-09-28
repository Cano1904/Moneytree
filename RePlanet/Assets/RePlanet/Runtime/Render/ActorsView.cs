using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using Terrain = RePlanet.Core.Terrain;

namespace RePlanet
{
    /// <summary>
    /// Bewegliche Darstellung: Roboter aller Spieler (inkl. Interpolation), Fahrzeuge mit Ladung und Kranlast,
    /// Sammeldrohnen, gebaute Anlagen am Stützpunkt (mit Animation) und Anhänger.
    /// </summary>
    public class ActorsView : MonoBehaviour
    {
        public static ActorsView I { get; private set; }
        readonly Dictionary<string, RobotModel> robots = new Dictionary<string, RobotModel>();
        readonly Dictionary<string, Vector3> lastPos = new Dictionary<string, Vector3>();
        readonly Dictionary<string, Transform> trailers = new Dictionary<string, Transform>();
        readonly Dictionary<string, GameObject> vehicles = new Dictionary<string, GameObject>();
        readonly Dictionary<string, Transform> vehicleParts = new Dictionary<string, Transform>();
        readonly List<Transform> drones = new List<Transform>();
        readonly Dictionary<int, GameObject> buildings = new Dictionary<int, GameObject>();
        readonly Dictionary<int, Transform> buildingAnim = new Dictionary<int, Transform>();
        Transform root;
        string buildingSig = "", planet;
        RobotModel menuRobot;
        GameObject towDrone;

        void Awake() { I = this; root = new GameObject("Actors").transform; root.SetParent(transform, false); }

        public RobotModel LocalRobot
        {
            get
            {
                var c = GameApp.I != null ? GameApp.I.Client : null;
                RobotModel r;
                return c != null && c.Pid != null && robots.TryGetValue(c.Pid, out r) ? r : null;
            }
        }

        public RobotModel RobotOf(string pid) { RobotModel r; return pid != null && robots.TryGetValue(pid, out r) ? r : null; }

        public Vector3 VehiclePos(string id)
        {
            GameObject g;
            return vehicles.TryGetValue(id, out g) && g != null ? g.transform.position : Vector3.zero;
        }

        void ClearAll()
        {
            foreach (var r in robots.Values) if (r != null) Destroy(r.gameObject);
            robots.Clear(); lastPos.Clear();
            foreach (var t in trailers.Values) if (t != null) Destroy(t.gameObject);
            trailers.Clear();
            foreach (var v in vehicles.Values) if (v != null) Destroy(v);
            vehicles.Clear(); vehicleParts.Clear();
            foreach (var d in drones) if (d != null) Destroy(d.gameObject);
            drones.Clear();
            foreach (var b in buildings.Values) if (b != null) Destroy(b);
            buildings.Clear(); buildingAnim.Clear(); buildingSig = "";
        }

        void Update()
        {
            var app = GameApp.I;
            var wv = WorldView.I;
            if (app == null || wv == null || wv.Layout == null) return;
            var w = app.W;
            if (w == null || !app.InGame && app.Mode != AppMode.Ending)
            {
                if (robots.Count > 0 || vehicles.Count > 0) ClearAll();
                MenuRobot(wv);
                return;
            }
            if (menuRobot != null) { Destroy(menuRobot.gameObject); menuRobot = null; }
            if (planet != w.CurrentPlanet) { ClearAll(); planet = w.CurrentPlanet; }
            float dt = Time.deltaTime;
            var client = app.Client;
            float dark = Atmosphere.I != null ? Atmosphere.I.Darkness : 0f;
            // Spieler
            var seen = new HashSet<string>();
            foreach (var p in w.Players.Values)
            {
                if (!p.Online) continue;
                seen.Add(p.Id);
                RobotModel r;
                if (!robots.TryGetValue(p.Id, out r) || r == null)
                {
                    r = RobotModel.Create(root, "MIKO_" + p.Name);
                    robots[p.Id] = r;
                }
                r.SetCosmetics(p.Color, p.Accent, p.Sticker, p.Attach);
                r.SetTech(w);
                Vector3 pos; float yaw; int flags = p.Flags;
                bool me = p.Id == client.Pid;
                if (me && PlayerController.I != null) { pos = PlayerController.I.RenderPos; yaw = PlayerController.I.RenderYaw; flags = PlayerController.I.Flags; }
                else
                {
                    Interp ip; V3 v; float y; int f;
                    if (client.Players.TryGetValue(p.Id, out ip) && ip.Sample(client.RenderTime, out v, out y, out f)) { pos = new Vector3(v.x, v.y, v.z); yaw = y; flags = f; }
                    else { pos = new Vector3(p.Pos.x, p.Pos.y, p.Pos.z); yaw = p.Yaw; }
                }
                bool inVehicle = p.Vehicle != null;
                r.gameObject.SetActive(!inVehicle);
                Vector3 prev;
                float speed = lastPos.TryGetValue(p.Id, out prev) ? Vector3.Distance(new Vector3(pos.x, 0, pos.z), new Vector3(prev.x, 0, prev.z)) / Mathf.Max(dt, 0.001f) : 0f;
                lastPos[p.Id] = pos;
                if (p.TowTimer > 0)
                {
                    // Abschleppdrohne hebt den abgeschalteten Roboter an
                    float lift = Mathf.Clamp01((6f - p.TowTimer) / 6f);
                    pos += Vector3.up * lift * 8f;
                    TowDrone(pos + Vector3.up * 2.2f);
                }
                r.transform.position = pos;
                r.transform.rotation = Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0);
                float load = Mathf.Clamp01(Item.Volume(p.Bin) / Mathf.Max(1f, w.TechVal("bin")));
                bool swimming = Terrain.WaterLevel(w.CurrentPlanet) > -50 && pos.y < Terrain.WaterLevel(w.CurrentPlanet) - 0.1f && Terrain.HeightAt(w.CurrentPlanet, pos.x, pos.z) < -0.6f;
                var look = me && Camera.main != null ? Camera.main.transform.forward : r.transform.forward;
                r.SetToolHead(p.Tool);
                r.Animate(dt, speed, load, p.Tool, (flags & 1) != 0, swimming, p.Sleeping, p.TowTimer > 0, dark, look);
                // Anhänger
                if (w.TechLevel("trailer") > 0 && !inVehicle) Trailer(p.Id, r.transform, dt);
                else if (trailers.ContainsKey(p.Id)) { Destroy(trailers[p.Id].gameObject); trailers.Remove(p.Id); }
            }
            foreach (var id in new List<string>(robots.Keys))
                if (!seen.Contains(id)) { if (robots[id] != null) Destroy(robots[id].gameObject); robots.Remove(id); }
            bool anyTow = false;
            foreach (var p in w.Players.Values) if (p.Online && p.TowTimer > 0) anyTow = true;
            if (!anyTow && towDrone != null) towDrone.SetActive(false);

            UpdateVehicles(w, client, dt);
            UpdateDrones(client, dt);
            UpdateBuildings(w, wv, dt);
        }

        void MenuRobot(WorldView wv)
        {
            if (menuRobot == null)
            {
                menuRobot = RobotModel.Create(root, "MIKO_Menue");
                var b = wv.Layout.Base;
                menuRobot.transform.position = new Vector3(b.Spawn.x + 3, b.Spawn.y, b.Spawn.z + 14);
                menuRobot.transform.rotation = Quaternion.Euler(0, 200, 0);
            }
            float dark = Atmosphere.I != null ? Atmosphere.I.Darkness : 0f;
            var cam = Camera.main;
            menuRobot.Animate(Time.deltaTime, 0, 0.4f, "grab", false, false, false, false, dark, cam != null ? (cam.transform.position - menuRobot.transform.position).normalized : Vector3.forward);
        }

        void TowDrone(Vector3 at)
        {
            if (towDrone == null)
            {
                towDrone = new GameObject("TowDrone");
                towDrone.transform.SetParent(root, false);
                var mf = towDrone.AddComponent<MeshFilter>(); mf.sharedMesh = MeshKit.Trash("drone");
                towDrone.AddComponent<MeshRenderer>().sharedMaterials = DroneMats(new Color(1f, 0.55f, 0.2f));
                towDrone.transform.localScale = Vector3.one * 3f;
            }
            towDrone.SetActive(true);
            towDrone.transform.position = at;
            towDrone.transform.Rotate(0, 90 * Time.deltaTime, 0);
        }

        void Trailer(string pid, Transform robot, float dt)
        {
            Transform t;
            if (!trailers.TryGetValue(pid, out t) || t == null)
            {
                var mb = new MultiBuilder { UsePalette = true };
                var body = Mats.Get(Mats.Opaque, new Color(0.9f, 0.55f, 0.2f));
                var dark = Mats.Get(Mats.Opaque, new Color(0.2f, 0.2f, 0.22f));
                mb.For(body).Box(new Vector3(0, 0.45f, 0), new Vector3(1.0f, 0.4f, 1.2f));
                mb.For(dark).CylinderX(new Vector3(0, 0.25f, 0), 0.25f, 1.2f, 10);
                mb.For(dark).Box(new Vector3(0, 0.35f, 0.9f), new Vector3(0.08f, 0.08f, 0.8f));
                t = mb.Build("Trailer", root).transform;
                t.position = robot.position - robot.forward * 2f;
                trailers[pid] = t;
            }
            var hitch = robot.position - robot.forward * 0.9f;
            var dir = hitch - t.position; dir.y = 0;
            if (dir.magnitude > 1.5f) t.position = hitch - dir.normalized * 1.5f;
            t.position = new Vector3(t.position.x, robot.position.y, t.position.z);
            if (dir.sqrMagnitude > 0.01f) t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(dir.normalized), dt * 8f);
        }

        // ------------------------------------------------------------ Fahrzeuge
        static Material Paint(Color c, float gloss = 0.45f) { return Mats.Surface(SurfKind.Paint, c, gloss); }
        static Material Steel(Color c) { return Mats.Surface(SurfKind.Paint, c, 0.55f, true); }
        static Material Rubber(Color c) { return Mats.Surface(SurfKind.Rubber, c, 0.25f); }
        static Material VGlow(Color c, float k) { return Mats.Get(Mats.Emissive, c, c * k); }

        /// <summary>Geländereifen (Achse X) mit Rundprofil, Stollen, Felge, Nabe und Radmuttern; side = Außenseite (±1).</summary>
        static void VWheel(MultiBuilder mb, Vector3 c, float r, float w, int side)
        {
            var o = mb.M;
            var rubber = Rubber(new Color(0.08f, 0.08f, 0.085f));
            var rim = Steel(new Color(0.62f, 0.64f, 0.67f));
            mb.M = o * Matrix4x4.TRS(c, Quaternion.Euler(0, 0, 90), Vector3.one);
            float hw = w * 0.5f;
            mb.For(rubber).Lathe(Vector3.zero, new[]
            {
                new Vector2(r * 0.64f, -hw), new Vector2(r * 0.86f, -hw - 0.01f), new Vector2(r * 0.97f, -hw + 0.03f), new Vector2(r, -hw * 0.5f),
                new Vector2(r, hw * 0.5f), new Vector2(r * 0.97f, hw - 0.03f), new Vector2(r * 0.86f, hw + 0.01f), new Vector2(r * 0.64f, hw),
            }, 24);
            mb.M = o * Matrix4x4.TRS(c, Quaternion.identity, Vector3.one);
            int n = 18;
            for (int k = 0; k < n; k++)
                for (int s = -1; s <= 1; s += 2)
                {
                    float a = k * 360f / n + (s > 0 ? 180f / n : 0f);
                    mb.For(rubber).BoxRot(Quaternion.Euler(a, 0, 0) * new Vector3(s * w * 0.22f, 0, r + 0.015f), new Vector3(w * 0.38f, 0.04f, r * 0.2f), new Vector3(a, s * 16f, 0));
                }
            mb.For(rim).CylinderX(Vector3.zero, r * 0.64f, w * 0.84f, 18);
            mb.For(Paint(new Color(0.15f, 0.16f, 0.18f))).CylinderX(new Vector3(side * w * 0.43f, 0, 0), r * 0.5f, 0.02f, 18);
            for (int k = 0; k < 6; k++)
            {
                float a = k * 60f;
                mb.For(rim).BoxRot(new Vector3(side * w * 0.44f, 0, 0) + Quaternion.Euler(a, 0, 0) * new Vector3(0, r * 0.3f, 0), new Vector3(0.03f, r * 0.4f, r * 0.12f), new Vector3(a, 0, 0));
            }
            mb.For(Paint(new Color(1f, 0.55f, 0.18f))).CylinderX(new Vector3(side * w * 0.47f, 0, 0), r * 0.22f, 0.04f, 14);
            for (int k = 0; k < 6; k++) mb.For(rim).Box(new Vector3(side * w * 0.5f, 0, 0) + Quaternion.Euler(k * 60f + 30f, 0, 0) * new Vector3(0, r * 0.15f, 0), new Vector3(0.03f, 0.03f, 0.03f));
            mb.M = o;
        }

        /// <summary>Kettenlaufwerk (entlang Z) mit Bodenplatten, Laufrollen, Antriebs- und Leitrad.</summary>
        static void Track(MultiBuilder mb, Vector3 c, float len, float h, float w)
        {
            var rubber = Rubber(new Color(0.1f, 0.1f, 0.105f));
            var steel = Steel(new Color(0.35f, 0.36f, 0.38f));
            var frame = Paint(new Color(0.2f, 0.21f, 0.23f));
            mb.For(frame).BevelBox(c + new Vector3(0, h * 0.5f, 0), new Vector3(w * 0.7f, h * 0.55f, len - h * 0.6f), 0.05f);
            foreach (var z in new[] { -1f, 1f }) mb.For(steel).CylinderX(c + new Vector3(0, h * 0.5f, z * (len * 0.5f - h * 0.5f)), h * 0.46f, w * 0.8f, 14);
            for (int k = 0; k < 4; k++) mb.For(steel).CylinderX(c + new Vector3(0, h * 0.2f, -len * 0.3f + k * len * 0.2f), h * 0.16f, w * 0.85f, 10);
            int shoes = Mathf.RoundToInt(len / 0.22f);
            for (int k = 0; k < shoes; k++)
            {
                float z = -len * 0.5f + h * 0.5f + (k + 0.5f) * (len - h) / shoes;
                mb.For(rubber).Box(c + new Vector3(0, 0.04f, z), new Vector3(w, 0.08f, (len - h) / shoes * 0.85f));
                mb.For(rubber).Box(c + new Vector3(0, h - 0.04f, z), new Vector3(w, 0.08f, (len - h) / shoes * 0.85f));
            }
            for (int k = 0; k < 8; k++)
                foreach (var z in new[] { -1f, 1f })
                {
                    float a = k / 7f * 180f;
                    var p = c + new Vector3(0, h * 0.5f, z * (len * 0.5f - h * 0.5f)) + Quaternion.Euler(z > 0 ? -a : a, 0, 0) * new Vector3(0, -h * 0.5f, 0);
                    mb.For(rubber).BoxRot(p, new Vector3(w, 0.08f, 0.18f), new Vector3(z > 0 ? -a : a, 0, 0));
                }
        }

        GameObject BuildVehicle(string id)
        {
            var mb = new MultiBuilder { UsePalette = true, GroundY = 0f, GrimeHeight = 1.4f };
            var dark = Paint(new Color(0.2f, 0.21f, 0.23f), 0.4f);
            var orange = Paint(new Color(1f, 0.55f, 0.18f), 0.5f);
            var teal = Paint(new Color(0.18f, 0.7f, 0.66f), 0.5f);
            var steel = Steel(new Color(0.6f, 0.62f, 0.65f));
            var glass = Mats.Get(Mats.Fade, new Color(0.35f, 0.55f, 0.65f, 0.55f), null, 0.95f);
            var head = VGlow(new Color(1f, 0.95f, 0.82f), 1.6f);
            var tail = VGlow(new Color(1f, 0.15f, 0.1f), 1.5f);
            var amber = VGlow(new Color(1f, 0.6f, 0.15f), 1.8f);
            GameObject go;
            switch (id)
            {
                case "crane":
                    {
                        // Raupenkran: Kettenlaufwerke, Drehkranz, Oberwagen mit Maschinenhaus, Kabine, Gegengewicht
                        Track(mb, new Vector3(-1.3f, 0, 0), 4.6f, 1.0f, 0.9f);
                        Track(mb, new Vector3(1.3f, 0, 0), 4.6f, 1.0f, 0.9f);
                        mb.For(dark).BevelBox(new Vector3(0, 0.75f, 0), new Vector3(1.8f, 0.4f, 3.6f), 0.05f);
                        mb.For(steel).Cylinder(new Vector3(0, 0.95f, -0.3f), 1.3f, 0.12f, 24);
                        mb.For(orange).BevelBox(new Vector3(0, 1.45f, -0.3f), new Vector3(3.2f, 1.0f, 3.6f), 0.1f);
                        mb.For(dark).BevelBox(new Vector3(0, 1.35f, -2.3f), new Vector3(2.9f, 0.8f, 0.6f), 0.06f); // Gegengewicht
                        HazardBandV(mb, new Vector3(0, 1.35f, -2.61f), 180f, 2.8f, 0.2f);
                        for (int k = 0; k < 6; k++) mb.For(dark).Box(new Vector3(1.61f, 1.5f, -1.4f + k * 0.18f), new Vector3(0.02f, 0.5f, 0.08f)); // Lüftung
                        mb.For(teal).BevelBox(new Vector3(-0.8f, 2.55f, 0.6f), new Vector3(1.4f, 1.3f, 1.6f), 0.08f);
                        mb.For(glass).Box(new Vector3(-0.8f, 2.65f, 1.41f), new Vector3(1.2f, 0.8f, 0.04f));
                        mb.For(glass).Box(new Vector3(-1.51f, 2.65f, 0.6f), new Vector3(0.04f, 0.8f, 1.2f));
                        mb.For(steel).Box(new Vector3(-0.8f, 3.25f, 0.6f), new Vector3(1.3f, 0.06f, 1.5f));
                        mb.For(amber).Cylinder(new Vector3(-0.3f, 3.28f, 0.2f), 0.1f, 0.14f, 10);
                        mb.For(head).Box(new Vector3(-0.8f, 3.1f, 1.43f), new Vector3(0.9f, 0.06f, 0.03f));
                        mb.For(steel).Tube(new Vector3(0.9f, 1.95f, -1.8f), new Vector3(0.9f, 3.2f, -1.8f), 0.1f, 8); // Auspuff
                        go = mb.Build("Vehicle_crane", root);
                        var boomPivot = new GameObject("Boom").transform;
                        boomPivot.SetParent(go.transform, false);
                        boomPivot.localPosition = new Vector3(0.6f, 2.2f, -0.8f);
                        // Gitterausleger: vier Gurte mit Diagonalen, Rollenkopf
                        var bb = new MultiBuilder { UsePalette = true };
                        var yb = Paint(new Color(1f, 0.6f, 0.15f), 0.5f);
                        for (int k = 0; k < 4; k++)
                        {
                            float x = k % 2 == 0 ? -0.22f : 0.22f, y = k < 2 ? -0.25f : 0.25f;
                            bb.For(yb).Beam(new Vector3(x, y, 0), new Vector3(x * 0.6f, y * 0.6f, 7f), 0.07f);
                        }
                        for (int k = 0; k < 9; k++)
                        {
                            float z0 = k * 0.78f, z1 = z0 + 0.78f, s0 = 1f - z0 / 7f * 0.4f, s1 = 1f - z1 / 7f * 0.4f;
                            bb.For(yb).Beam(new Vector3(-0.22f * s0, 0.25f * s0, z0), new Vector3(0.22f * s1, 0.25f * s1, z1), 0.03f);
                            bb.For(yb).Beam(new Vector3(-0.22f * s0, -0.25f * s0, z0), new Vector3(0.22f * s1, -0.25f * s1, z1), 0.03f);
                            bb.For(yb).Beam(new Vector3(-0.22f * s0, -0.25f * s0, z0), new Vector3(-0.22f * s1, 0.25f * s1, z1), 0.03f);
                            bb.For(yb).Beam(new Vector3(0.22f * s0, -0.25f * s0, z0), new Vector3(0.22f * s1, 0.25f * s1, z1), 0.03f);
                        }
                        bb.For(dark).CylinderX(new Vector3(0, -0.1f, 7f), 0.22f, 0.3f, 14);
                        bb.For(steel).CylinderX(new Vector3(0, -0.1f, 7f), 0.1f, 0.34f, 10);
                        bb.For(tail).Sphere(new Vector3(0, 0.25f, 7f), 0.08f, 6, 4);
                        bb.Build("BoomMesh", boomPivot);
                        boomPivot.localRotation = Quaternion.Euler(-25, 0, 0);
                        vehicleParts["crane_boom"] = boomPivot;
                        var hook = new GameObject("Hook").transform;
                        hook.SetParent(go.transform, false);
                        vehicleParts["crane_hook"] = hook;
                        var line = hook.gameObject.AddComponent<LineRenderer>();
                        line.sharedMaterial = Mats.Get(Mats.Line, new Color(0.15f, 0.15f, 0.15f));
                        line.startWidth = line.endWidth = 0.06f;
                        line.positionCount = 2;
                        line.useWorldSpace = true;
                        break;
                    }
                case "boat":
                    {
                        var hullC = Paint(new Color(0.92f, 0.92f, 0.9f), 0.55f);
                        mb.For(hullC).Lathe(new Vector3(0, -0.4f, 0), new[] { new Vector2(0, 0), new Vector2(1.0f, 0.1f), new Vector2(1.35f, 0.45f), new Vector2(1.6f, 1.0f), new Vector2(1.62f, 1.06f) }, 16, true);
                        mb.For(Paint(new Color(0.8f, 0.25f, 0.2f), 0.5f)).Torus(new Vector3(0, 0.25f, 0), 1.43f, 0.06f, 20, 4); // Wasserpass
                        mb.For(Mats.Surface(SurfKind.Wood, new Color(0.55f, 0.4f, 0.27f))).Cylinder(new Vector3(0, 0.55f, 0), 1.45f, 0.06f, 20);
                        mb.For(orange).BevelBox(new Vector3(0, 1.05f, -0.8f), new Vector3(1.6f, 1.1f, 1.6f), 0.08f);
                        mb.For(dark).BevelBox(new Vector3(0, 1.65f, -0.8f), new Vector3(1.75f, 0.1f, 1.8f), 0.03f);
                        mb.For(glass).Box(new Vector3(0, 1.2f, 0.02f), new Vector3(1.4f, 0.5f, 0.04f));
                        mb.For(glass).Box(new Vector3(0.81f, 1.2f, -0.8f), new Vector3(0.04f, 0.5f, 1.2f));
                        mb.For(glass).Box(new Vector3(-0.81f, 1.2f, -0.8f), new Vector3(0.04f, 0.5f, 1.2f));
                        mb.For(steel).Cylinder(new Vector3(0.4f, 1.7f, -1.2f), 0.03f, 1.1f, 6);
                        mb.For(amber).Sphere(new Vector3(0.4f, 2.85f, -1.2f), 0.07f, 6, 4);
                        // Reling, Fender, Fangnetz vorn
                        for (int k = 0; k < 10; k++)
                        {
                            float a = k / 10f * Mathf.PI * 2f;
                            mb.For(steel).Cylinder(new Vector3(Mathf.Cos(a) * 1.5f, 0.6f, Mathf.Sin(a) * 1.5f), 0.018f, 0.45f, 5);
                        }
                        mb.For(steel).Torus(new Vector3(0, 1.05f, 0), 1.5f, 0.02f, 24, 3);
                        foreach (var sx in new[] { -1f, 1f }) mb.For(Rubber(new Color(0.1f, 0.1f, 0.1f))).TorusRot(new Vector3(sx * 1.58f, 0.45f, -0.3f), new Vector3(0, 0, 90), 0.16f, 0.07f, 10, 4);
                        mb.For(dark).Box(new Vector3(0, 0.7f, 1.8f), new Vector3(2.6f, 0.08f, 0.08f));
                        for (int i = 0; i < 5; i++) mb.For(Paint(new Color(0.3f, 0.5f, 0.7f))).Box(new Vector3(-1f + i * 0.5f, 0.35f, 1.8f), new Vector3(0.04f, 0.7f, 0.04f));
                        for (int i = 0; i < 4; i++) mb.For(Rubber(new Color(0.25f, 0.45f, 0.35f))).Box(new Vector3(0, 0.12f + i * 0.18f, 1.8f), new Vector3(2.0f, 0.015f, 0.015f));
                        mb.For(head).Box(new Vector3(0, 1.55f, 0.03f), new Vector3(0.4f, 0.06f, 0.03f));
                        go = mb.Build("Vehicle_boat", root);
                        go.transform.localScale = new Vector3(1.2f, 1f, 1.8f);
                        break;
                    }
                default:
                    {
                        // Transportrover: Leiterrahmen, Karosserie, Fahrerhaus mit Scheiben, Ladefläche mit Bordwänden
                        mb.For(dark).BevelBox(new Vector3(0, 0.62f, 0), new Vector3(1.4f, 0.24f, 4.3f), 0.04f);
                        mb.For(teal).BevelBox(new Vector3(0, 0.95f, 0), new Vector3(2.2f, 0.5f, 4.4f), 0.1f);
                        mb.For(orange).BevelBox(new Vector3(0, 1.72f, 1.4f), new Vector3(2.1f, 1.05f, 1.5f), 0.12f);
                        mb.For(glass).BoxRot(new Vector3(0, 1.85f, 2.16f), new Vector3(1.85f, 0.62f, 0.04f), new Vector3(-8, 0, 0));
                        foreach (var sx in new[] { -1f, 1f })
                        {
                            mb.For(glass).Box(new Vector3(sx * 1.06f, 1.85f, 1.45f), new Vector3(0.04f, 0.55f, 1.0f));
                            mb.For(dark).BevelBox(new Vector3(sx * 1.2f, 1.9f, 2.0f), new Vector3(0.06f, 0.22f, 0.16f), 0.02f); // Spiegel
                            // Kotflügel über den Rädern
                            foreach (var z in new[] { 1.4f, -1.4f })
                                for (int k = 0; k < 5; k++)
                                {
                                    float a = Mathf.Lerp(20f, 160f, k / 4f) * Mathf.Deg2Rad;
                                    mb.For(dark).BevelBoxRot(new Vector3(sx * 1.15f, 0.5f + Mathf.Sin(a) * 0.62f, z + Mathf.Cos(a) * 0.62f), new Vector3(0.46f, 0.05f, 0.34f), new Vector3(90f - a * Mathf.Rad2Deg, 0, 0), 0.015f);
                                }
                            // Scheinwerfer, Rückleuchten, Seitenstreifen
                            mb.For(head).Box(new Vector3(sx * 0.75f, 1.05f, 2.215f), new Vector3(0.36f, 0.14f, 0.03f));
                            mb.For(tail).Box(new Vector3(sx * 0.85f, 1.0f, -2.215f), new Vector3(0.24f, 0.14f, 0.03f));
                            mb.For(orange).Box(new Vector3(sx * 1.105f, 0.9f, 0), new Vector3(0.02f, 0.08f, 3.6f));
                        }
                        mb.For(steel).BevelBox(new Vector3(0, 0.7f, 2.3f), new Vector3(2.2f, 0.22f, 0.16f), 0.04f); // Stoßfänger
                        mb.For(dark).CylinderX(new Vector3(0, 0.75f, 2.42f), 0.12f, 0.6f, 12); // Seilwinde
                        for (int k = 0; k < 5; k++) mb.For(dark).Box(new Vector3(-0.4f + k * 0.2f, 1.1f, 2.216f), new Vector3(0.1f, 0.26f, 0.02f)); // Kühlergrill
                        mb.For(amber).Box(new Vector3(0, 2.3f, 1.3f), new Vector3(0.9f, 0.1f, 0.18f)); // Rundumleuchtenbalken
                        mb.For(steel).Cylinder(new Vector3(-0.8f, 2.25f, 0.9f), 0.015f, 1.1f, 5); // Antenne
                        // Ladefläche mit Riffelblech, Bordwänden, Rungen, Heckklappe mit Warnstreifen
                        mb.For(Mats.Surface(SurfKind.Tiles, new Color(0.3f, 0.31f, 0.33f), 0.45f, true)).Box(new Vector3(0, 1.3f, -0.9f), new Vector3(2.2f, 0.3f, 2.4f));
                        foreach (var sx in new[] { -1f, 1f })
                        {
                            mb.For(dark).BevelBox(new Vector3(sx * 1.08f, 1.6f, -0.9f), new Vector3(0.06f, 0.5f, 2.4f), 0.015f);
                            for (int k = 0; k < 4; k++) mb.For(steel).Box(new Vector3(sx * 1.12f, 1.6f, -1.95f + k * 0.7f), new Vector3(0.04f, 0.52f, 0.06f));
                        }
                        mb.For(dark).BevelBox(new Vector3(0, 1.6f, -2.1f), new Vector3(2.2f, 0.5f, 0.06f), 0.015f);
                        HazardBandV(mb, new Vector3(0, 1.45f, -2.14f), 180f, 2.1f, 0.14f);
                        for (int i = 0; i < 4; i++) VWheel(mb, new Vector3(i % 2 == 0 ? 1.15f : -1.15f, 0.5f, i < 2 ? 1.4f : -1.4f), 0.5f, 0.42f, i % 2 == 0 ? 1 : -1);
                        go = mb.Build("Vehicle_rover", root);
                        var cmb = new MultiBuilder { UsePalette = true };
                        var cols = new[] { new Color(0.55f, 0.5f, 0.42f), new Color(0.35f, 0.5f, 0.62f), new Color(0.7f, 0.35f, 0.25f), new Color(0.45f, 0.55f, 0.35f) };
                        cmb.For(Mats.Get(Mats.Opaque, new Color(0.55f, 0.5f, 0.42f))).Box(new Vector3(0, 0, 0), new Vector3(1f, 1f, 1f));
                        for (int k = 0; k < 12; k++) cmb.For(Mats.Get(Mats.Opaque, cols[k % cols.Length])).Crumple(new Vector3(-0.35f + (k % 4) * 0.23f, 0.5f, -0.35f + (k / 4) * 0.35f), 0.14f, 0.6f, k, 0.3f, 6, 4);
                        var cargo = cmb.Build("Cargo", go.transform);
                        cargo.transform.localPosition = new Vector3(0, 1.45f, -0.9f);
                        vehicleParts["rover_cargo"] = cargo.transform;
                        break;
                    }
            }
            return go;
        }

        /// <summary>Warnstreifen (gelb/schwarz) quer auf einer senkrechten Fläche, Blickrichtung yaw.</summary>
        static void HazardBandV(MultiBuilder mb, Vector3 c, float yaw, float len, float h)
        {
            var o = mb.M;
            mb.M = o * Matrix4x4.TRS(c, Quaternion.Euler(0, yaw, 0), Vector3.one);
            mb.For(Paint(new Color(0.95f, 0.75f, 0.12f), 0.4f)).Box(Vector3.zero, new Vector3(len, h, 0.02f));
            int n = Mathf.Max(2, Mathf.RoundToInt(len / (h * 1.4f)));
            for (int k = 0; k < n; k++) mb.For(Paint(new Color(0.08f, 0.08f, 0.09f))).BoxRot(new Vector3(-len * 0.5f + (k + 0.5f) * len / n, 0, 0.012f), new Vector3(len / n * 0.45f, h * 1.25f, 0.006f), new Vector3(0, 0, 40));
            mb.M = o;
        }

        void UpdateVehicles(WorldState w, GameClient client, float dt)
        {
            var ps = w.Cur;
            var seen = new HashSet<string>();
            foreach (var v in ps.Vehicles.Values)
            {
                seen.Add(v.Id);
                GameObject go;
                if (!vehicles.TryGetValue(v.Id, out go) || go == null) { go = BuildVehicle(v.Id); vehicles[v.Id] = go; }
                Vector3 pos; float yaw;
                bool mine = v.Driver != null && v.Driver == client.Pid && PlayerController.I != null && PlayerController.I.InVehicle;
                if (mine) { pos = PlayerController.I.RenderPos; yaw = PlayerController.I.RenderYaw; }
                else
                {
                    Interp ip; V3 p; float y; int f;
                    if (client.Vehicles.TryGetValue(v.Id, out ip) && ip.Sample(client.RenderTime, out p, out y, out f)) { pos = new Vector3(p.x, p.y, p.z); yaw = y; }
                    else { pos = new Vector3(v.Pos.x, v.Pos.y, v.Pos.z); yaw = v.Yaw; }
                }
                if (v.Id == "boat") pos.y = Terrain.WaterLevel(w.CurrentPlanet) + Mathf.Sin(Time.time * 1.5f) * 0.08f;
                go.transform.position = pos;
                go.transform.rotation = Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0);
                // Ladung / Last
                DynObj carried = null;
                if (v.Carry != null) ps.Dyn.TryGetValue(v.Carry, out carried);
                if (v.Id == "rover")
                {
                    Transform cargo;
                    if (vehicleParts.TryGetValue("rover_cargo", out cargo))
                    {
                        float fill = Mathf.Clamp01(Item.Volume(v.Cargo) / Mathf.Max(1, v.Def.Capacity));
                        cargo.gameObject.SetActive(fill > 0.01f);
                        cargo.localScale = new Vector3(2f, Mathf.Max(0.05f, fill * 0.9f), 2.2f);
                        cargo.localPosition = new Vector3(0, 1.45f + fill * 0.45f, -0.9f);
                    }
                    if (carried != null) DrawCarried(carried, go.transform.TransformPoint(new Vector3(0, 1.6f, -0.9f)), go.transform.rotation);
                }
                else if (v.Id == "crane")
                {
                    Transform boom, hook;
                    if (vehicleParts.TryGetValue("crane_boom", out boom) && vehicleParts.TryGetValue("crane_hook", out hook))
                    {
                        float lift = carried != null ? -38f : -22f;
                        boom.localRotation = Quaternion.Slerp(boom.localRotation, Quaternion.Euler(lift, 0, 0), dt * 2f);
                        var tipPos = boom.TransformPoint(new Vector3(0, -0.2f, 7f));
                        var hookPos = tipPos + Vector3.down * (carried != null ? 2.5f : 4f);
                        var lr = hook.GetComponent<LineRenderer>();
                        lr.SetPosition(0, tipPos); lr.SetPosition(1, hookPos);
                        if (carried != null) DrawCarried(carried, hookPos + Vector3.down * 1.2f, go.transform.rotation);
                    }
                }
            }
            foreach (var id in new List<string>(vehicles.Keys))
                if (!seen.Contains(id)) { if (vehicles[id] != null) Destroy(vehicles[id]); vehicles.Remove(id); }
        }

        void DrawCarried(DynObj d, Vector3 at, Quaternion rot)
        {
            var t = d.Def;
            TrashRenderer.DrawTrash(t, Matrix4x4.TRS(at, rot, Vector3.one * (t.Crane ? t.Size : 1f)));
        }

        /// <summary>Materialien für das mehrteilige Drohnen-Mesh (Rumpf in Wunschfarbe, Nebenteile wie beim Müll).</summary>
        static Material[] DroneMats(Color body)
        {
            var mesh = MeshKit.Trash("drone");
            var arr = new Material[Mathf.Max(1, mesh.subMeshCount)];
            arr[0] = Mats.Get(Mats.Opaque, body);
            for (int i = 1; i < arr.Length; i++) arr[i] = i == MeshKit.Signal ? Mats.Get(Mats.Emissive, new Color(0.4f, 1f, 1f), new Color(0.6f, 2f, 2f)) : TrashRenderer.SlotMaterial(i);
            return arr;
        }

        void UpdateDrones(GameClient client, float dt)
        {
            var list = client.Drones;
            while (drones.Count < list.Count)
            {
                var go = new GameObject("Drohne");
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = MeshKit.Trash("drone");
                go.AddComponent<MeshRenderer>().sharedMaterials = DroneMats(new Color(0.35f, 0.65f, 0.9f));
                go.transform.localScale = Vector3.one * 1.6f;
                var light = new GameObject("Licht");
                light.transform.SetParent(go.transform, false);
                light.transform.localPosition = new Vector3(0, -0.05f, 0);
                light.AddComponent<MeshFilter>().sharedMesh = MeshKit.Sphere;
                light.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get(Mats.Emissive, new Color(0.4f, 1f, 1f), new Color(0.6f, 2f, 2f));
                light.transform.localScale = Vector3.one * 0.12f;
                drones.Add(go.transform);
            }
            while (drones.Count > list.Count) { Destroy(drones[drones.Count - 1].gameObject); drones.RemoveAt(drones.Count - 1); }
            for (int i = 0; i < list.Count; i++)
            {
                var target = new Vector3(list[i][0], list[i][1], list[i][2]);
                var t = drones[i];
                if ((t.position - target).sqrMagnitude > 400) t.position = target;
                var dir = target - t.position;
                t.position = Vector3.Lerp(t.position, target, dt * 6f) + Vector3.up * Mathf.Sin(Time.time * 4f + i) * 0.01f;
                if (dir.sqrMagnitude > 0.01f) t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z).normalized + Vector3.forward * 0.001f), dt * 4f);
            }
        }

        // ------------------------------------------------------------ Anlagen am Stützpunkt
        void UpdateBuildings(WorldState w, WorldView wv, float dt)
        {
            var ps = w.Cur;
            var sb = new System.Text.StringBuilder();
            foreach (var b in ps.Buildings) sb.Append(b.Id).Append(b.Type).Append(b.Gx).Append(',').Append(b.Gz).Append(',').Append(b.Rot).Append(';');
            string sig = sb.ToString();
            // Wurde die Welt neu aufgebaut, sind die Anlagen mit ihr verschwunden → neu erzeugen
            foreach (var g in buildings.Values) if (g == null) { buildingSig = ""; break; }
            if (sig != buildingSig)
            {
                buildingSig = sig;
                foreach (var g in buildings.Values) if (g != null) Destroy(g);
                buildings.Clear(); buildingAnim.Clear();
                Rules.UpdateConnectivity(ps);
                foreach (var b in ps.Buildings) buildings[b.Id] = BuildMachine(b, wv.Layout.Base);
            }
            var energy = Rules.Energy(ps);
            foreach (var b in ps.Buildings)
            {
                Transform anim;
                if (!buildingAnim.TryGetValue(b.Id, out anim) || anim == null) continue;
                bool running = (b.Connected || !b.Def.Machine) && energy.Efficiency > 0.05f;
                if (!running) continue;
                switch (b.Type)
                {
                    case "sortierer": anim.Rotate(0, 0, 120f * dt * energy.Efficiency, Space.Self); break;
                    case "presse": anim.localPosition = new Vector3(0, 2.2f + Mathf.Abs(Mathf.Sin(Time.time * 1.2f)) * 0.9f, 0); break;
                    case "foerderband": anim.localPosition = new Vector3(0, 0.55f, Mathf.Repeat(Time.time * 0.8f, 1.6f) - 0.8f); break;
                    case "drohnenhangar": anim.Rotate(0, 30f * dt, 0, Space.Self); break;
                    case "generator": anim.Rotate(0, 200f * dt, 0, Space.Self); break;
                }
            }
        }

        public static GameObject BuildMachine(Building b, BaseLayout bl)
        {
            var def = b.Def;
            float w = b.W * bl.Cell, h = b.H * bl.Cell;
            var c = bl.CellCenter(b.Gx, b.Gz, b.W, b.H);
            var gy = Terrain.HeightAt(GameApp.I != null && GameApp.I.W != null ? GameApp.I.W.CurrentPlanet : "terra", c.x, c.z);
            var col = Paint(Mats.C(def.Color == 0 ? 0x888888u : def.Color), 0.45f);
            var dark = Paint(new Color(0.22f, 0.23f, 0.25f), 0.4f);
            var steel = Steel(new Color(0.6f, 0.62f, 0.65f));
            var mb = new MultiBuilder { UsePalette = true, GroundY = gy, GrimeHeight = 1.2f };
            mb.M = Matrix4x4.TRS(new Vector3(c.x, gy, c.z), Quaternion.Euler(0, b.Rot * 90, 0), Vector3.one);
            float lw = b.Rot % 2 == 0 ? w : h, lh = b.Rot % 2 == 0 ? h : w; // lokale Maße
            Transform anim = null;
            GameObject go;
            switch (b.Type)
            {
                case "foerderband":
                    mb.For(dark).BevelBox(new Vector3(0, 0.25f, 0), new Vector3(1.6f, 0.5f, 1.9f), 0.04f);
                    mb.For(Rubber(new Color(0.1f, 0.1f, 0.1f))).Box(new Vector3(0, 0.51f, 0), new Vector3(1.3f, 0.04f, 1.9f));
                    for (int k = 0; k < 5; k++) mb.For(steel).CylinderX(new Vector3(0, 0.46f, -0.8f + k * 0.4f), 0.05f, 1.4f, 8);
                    foreach (var sx in new[] { -0.78f, 0.78f }) { mb.For(Paint(new Color(0.95f, 0.75f, 0.15f))).Box(new Vector3(sx, 0.56f, 0), new Vector3(0.06f, 0.12f, 1.9f)); }
                    go = mb.Build("B_" + b.Id, WorldView.I.Root);
                    anim = Child(go, MeshKit.Cube, Mats.Get(Mats.Opaque, new Color(0.6f, 0.55f, 0.4f)), new Vector3(c.x, gy, c.z), b.Rot, new Vector3(0, 0.55f, 0), new Vector3(0.4f, 0.25f, 0.4f));
                    break;
                case "sortierer":
                    mb.For(col).BevelBox(new Vector3(0, 1.4f, 0), new Vector3(lw - 0.4f, 2.8f, lh - 0.4f), 0.1f);
                    mb.For(dark).BevelBox(new Vector3(0, 3.0f, 0), new Vector3(lw * 0.6f, 0.4f, lh * 0.6f), 0.05f);
                    for (int k = 0; k < 6; k++) mb.For(dark).Box(new Vector3(-lw * 0.25f + k * lw * 0.1f, 0.9f, lh * 0.5f - 0.19f), new Vector3(0.05f, 0.9f, 0.02f));
                    mb.For(Rubber(new Color(0.1f, 0.1f, 0.1f))).BoxRot(new Vector3(lw * 0.5f - 0.1f, 0.8f, 0), new Vector3(0.6f, 0.08f, 0.9f), new Vector3(0, 0, -25));
                    mb.For(Mats.Get(Mats.Emissive, new Color(0.3f, 0.9f, 1f), new Color(0.3f, 1.2f, 1.4f))).Box(new Vector3(0, 2.2f, lh * 0.5f - 0.18f), new Vector3(1.2f, 0.5f, 0.05f));
                    go = mb.Build("B_" + b.Id, WorldView.I.Root);
                    anim = Child(go, MeshKit.Cylinder, Mats.Get(Mats.Metal, new Color(0.7f, 0.72f, 0.75f)), new Vector3(c.x, gy, c.z), b.Rot, new Vector3(0, 3.6f, 0), new Vector3(1.6f, 0.6f, 1.6f), new Vector3(0, 0, 90));
                    break;
                case "presse":
                    mb.For(col).BevelBox(new Vector3(0, 1.0f, 0), new Vector3(lw - 0.3f, 2f, lh - 0.3f), 0.08f);
                    mb.For(dark).BevelBox(new Vector3(-lw * 0.4f, 2.5f, 0), new Vector3(0.3f, 3f, 0.3f), 0.03f);
                    mb.For(dark).BevelBox(new Vector3(lw * 0.4f, 2.5f, 0), new Vector3(0.3f, 3f, 0.3f), 0.03f);
                    mb.For(dark).BevelBox(new Vector3(0, 4.05f, 0), new Vector3(lw * 0.9f, 0.3f, 0.5f), 0.03f);
                    foreach (var sx in new[] { -0.3f, 0.3f }) mb.For(steel).Cylinder(new Vector3(lw * sx * 0.6f, 2.6f, 0), 0.09f, 1.3f, 10);
                    HazardBandV(mb, new Vector3(0, 1.9f, lh * 0.5f - 0.13f), 0f, lw - 0.5f, 0.18f);
                    go = mb.Build("B_" + b.Id, WorldView.I.Root);
                    anim = Child(go, MeshKit.Cube, dark, new Vector3(c.x, gy, c.z), b.Rot, new Vector3(0, 2.2f, 0), new Vector3(lw - 0.6f, 0.4f, lh - 0.6f));
                    break;
                case "lager":
                    mb.For(Mats.Surface(SurfKind.Cladding, Mats.C(def.Color == 0 ? 0x888888u : def.Color), 0.35f)).BevelBox(new Vector3(0, 1.8f, 0), new Vector3(lw - 0.3f, 3.6f, lh - 0.3f), 0.08f);
                    mb.For(dark).BoxRot(new Vector3(0, 3.8f, 0), new Vector3(lw, 0.3f, lh * 0.6f), new Vector3(10, 0, 0));
                    mb.For(steel).Box(new Vector3(0, 1.3f, lh * 0.5f - 0.1f), new Vector3(1.8f, 2.6f, 0.1f));
                    for (int k = 1; k < 8; k++) mb.For(dark).Box(new Vector3(0, k * 0.33f, lh * 0.5f - 0.04f), new Vector3(1.8f, 0.03f, 0.02f));
                    SurfaceIcon(mb, new Vector3(0, 3.0f, lh * 0.5f - 0.13f), 0.7f, SurfaceLook.Icon.Crate);
                    go = mb.Build("B_" + b.Id, WorldView.I.Root);
                    break;
                case "solar":
                    for (int i = 0; i < 2; i++)
                    {
                        mb.For(Mats.Surface(SurfKind.Tiles, new Color(0.13f, 0.19f, 0.4f), 0.9f)).BoxRot(new Vector3(0, 1.1f, -lh * 0.22f + i * lh * 0.44f), new Vector3(lw - 0.3f, 0.08f, lh * 0.4f), new Vector3(25, 0, 0));
                        mb.For(steel).BoxRot(new Vector3(0, 1.06f, -lh * 0.22f + i * lh * 0.44f), new Vector3(lw - 0.24f, 0.05f, lh * 0.42f), new Vector3(25, 0, 0));
                    }
                    mb.For(dark).Box(new Vector3(0, 0.5f, 0), new Vector3(0.2f, 1f, lh - 0.5f));
                    go = mb.Build("B_" + b.Id, WorldView.I.Root);
                    break;
                case "generator":
                    mb.For(col).RoundCylinder(new Vector3(-0.8f, 0, 0), 0.9f, 2.6f, 0.12f, 16);
                    mb.For(col).RoundCylinder(new Vector3(0.8f, 0, 0), 0.9f, 2.6f, 0.12f, 16);
                    foreach (var sx in new[] { -0.8f, 0.8f }) for (int k = 0; k < 3; k++) mb.For(dark).Cylinder(new Vector3(sx, 0.5f + k * 0.7f, 0), 0.92f, 0.08f, 16, false);
                    mb.For(steel).Tube(new Vector3(-0.8f, 2.2f, 0), new Vector3(0.8f, 2.2f, 0), 0.12f, 8);
                    go = mb.Build("B_" + b.Id, WorldView.I.Root);
                    anim = Child(go, MeshKit.Cube, Mats.Get(Mats.Emissive, new Color(1f, 0.5f, 0.2f), new Color(2f, 0.8f, 0.2f)), new Vector3(c.x, gy, c.z), b.Rot, new Vector3(0, 3f, 0), new Vector3(1.2f, 0.15f, 0.15f));
                    break;
                case "drohnenhangar":
                    mb.For(dark).Cylinder(Vector3.zero, lw * 0.45f, 0.3f, 16);
                    mb.For(Mats.Get(Mats.Opaque, new Color(0.95f, 0.8f, 0.2f))).Torus(new Vector3(0, 0.32f, 0), lw * 0.35f, 0.08f, 16, 4);
                    for (int i = 0; i < 4; i++) mb.For(col).Box(new Vector3(i % 2 == 0 ? -lw * 0.4f : lw * 0.4f, 1.5f, i < 2 ? -lh * 0.4f : lh * 0.4f), new Vector3(0.2f, 3f, 0.2f));
                    go = mb.Build("B_" + b.Id, WorldView.I.Root);
                    anim = Child(go, MeshKit.Cube, col, new Vector3(c.x, gy, c.z), b.Rot, new Vector3(0, 3.1f, 0), new Vector3(lw * 0.9f, 0.15f, 0.4f));
                    break;
                case "ladestation":
                    mb.For(dark).BevelBox(new Vector3(0, 1f, 0), new Vector3(0.6f, 2f, 0.6f), 0.05f);
                    SurfaceIcon(mb, new Vector3(0, 1.6f, 0.305f), 0.4f, SurfaceLook.Icon.Bolt);
                    go = mb.Build("B_" + b.Id, WorldView.I.Root);
                    break;
                case "laterne":
                    mb.For(dark).Cylinder(Vector3.zero, 0.16f, 0.3f, 10, true, 0.1f);
                    mb.For(dark).Cylinder(Vector3.zero, 0.07f, 3f, 10);
                    mb.For(dark).Cylinder(new Vector3(0, 2.9f, 0), 0.2f, 0.08f, 10);
                    mb.For(Mats.Get(Mats.Emissive, new Color(1f, 0.85f, 0.5f), new Color(2f, 1.5f, 0.8f))).Sphere(new Vector3(0, 3.18f, 0), 0.26f, 12, 8);
                    mb.For(dark).Cylinder(new Vector3(0, 3.4f, 0), 0.3f, 0.08f, 12, true, 0.05f);
                    go = mb.Build("B_" + b.Id, WorldView.I.Root);
                    break;
                case "bank":
                    mb.For(col).Box(new Vector3(0, 0.45f, 0), new Vector3(1.6f, 0.1f, 0.5f));
                    mb.For(col).Box(new Vector3(0, 0.8f, -0.22f), new Vector3(1.6f, 0.5f, 0.08f));
                    mb.For(dark).Box(new Vector3(-0.7f, 0.22f, 0), new Vector3(0.08f, 0.45f, 0.45f));
                    mb.For(dark).Box(new Vector3(0.7f, 0.22f, 0), new Vector3(0.08f, 0.45f, 0.45f));
                    go = mb.Build("B_" + b.Id, WorldView.I.Root);
                    break;
                case "beet":
                    mb.For(Mats.Get(Mats.Opaque, new Color(0.45f, 0.3f, 0.2f))).Box(new Vector3(0, 0.15f, 0), new Vector3(1.8f, 0.3f, 1.8f));
                    for (int i = 0; i < 5; i++) mb.For(Mats.Get(Mats.Opaque, new[] { new Color(1f, 0.4f, 0.5f), new Color(1f, 0.85f, 0.3f), new Color(0.6f, 0.4f, 1f) }[i % 3])).Sphere(new Vector3(-0.6f + i * 0.3f, 0.45f, (i % 2) * 0.4f - 0.2f), 0.18f, 6, 4);
                    go = mb.Build("B_" + b.Id, WorldView.I.Root);
                    break;
                default:
                    mb.For(col).Box(new Vector3(0, 1f, 0), new Vector3(lw - 0.3f, 2f, lh - 0.3f));
                    go = mb.Build("B_" + b.Id, WorldView.I.Root);
                    break;
            }
            if (anim != null && I != null) I.buildingAnim[b.Id] = anim;
            return go;
        }

        /// <summary>Leuchtendes Piktogramm auf der +Z-Seite (lokal) eines Bauteils.</summary>
        static void SurfaceIcon(MultiBuilder mb, Vector3 c, float size, int icon)
        {
            var b = mb.For(SurfaceLook.SignMaterial());
            b.UVRect = SurfaceLook.IconRect(icon);
            float h = size * 0.5f;
            b.Quad(c + new Vector3(h, -h, 0), c + new Vector3(-h, -h, 0), c + new Vector3(-h, h, 0), c + new Vector3(h, h, 0), Vector3.forward);
        }

        static Transform Child(GameObject parent, Mesh mesh, Material mat, Vector3 center, int rot, Vector3 local, Vector3 scale, Vector3? euler = null)
        {
            var pivot = new GameObject("pivot").transform;
            pivot.SetParent(parent.transform, false);
            pivot.position = center;
            pivot.rotation = Quaternion.Euler(0, rot * 90, 0);
            var go = new GameObject("anim");
            go.transform.SetParent(pivot, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        void OnGUI()
        {
            // Namensschilder der Mitspieler
            var app = GameApp.I;
            if (app == null || !app.InGame || UIState.Screen != UIScreen.None || (PhotoMode.Active && PhotoMode.HideHud)) return;
            var cam = Camera.main;
            if (cam == null) return;
            var w = app.W;
            GUI.depth = 10;
            foreach (var kv in robots)
            {
                if (kv.Key == app.Client.Pid || kv.Value == null || !kv.Value.gameObject.activeSelf) continue;
                PlayerData p;
                if (!w.Players.TryGetValue(kv.Key, out p)) continue;
                var sp = cam.WorldToScreenPoint(kv.Value.transform.position + Vector3.up * 2.2f);
                if (sp.z < 0 || sp.z > 60) continue;
                var r = new Rect(sp.x - 80, Screen.height - sp.y - 12, 160, 24);
                var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                GUI.color = new Color(0, 0, 0, 0.6f); GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), p.Name + (p.Sleeping ? " (schläft)" : ""), style);
                GUI.color = Color.white; GUI.Label(r, p.Name + (p.Sleeping ? " (schläft)" : ""), style);
            }
        }
    }
}
