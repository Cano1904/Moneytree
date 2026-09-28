using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using Terrain = RePlanet.Core.Terrain;

namespace RePlanet
{
    /// <summary>
    /// Gebäude und Mauern aus den Kollisionsboxen des Layouts – mit Fassaden-Details: Fensterreihen mit Rahmen und
    /// Fensterbänken, Balkone, Läden mit Markisen und Schildern, Klimageräte, Dachaufbauten, Risse, Ruinenkanten;
    /// Industriehallen mit Wellblech, Satteldach, Toren, Rohren und Leitern; Kaimauern mit Blocksteinen, Pollern,
    /// Reifenfendern, Geländern, Laternen und Containern; zerklüftete Eis- und Felswände.
    /// Die Geometrie wird in 60-m-Blöcke zusammengefasst (Culling je Block, wenige Materialien je Block).
    /// </summary>
    public partial class WorldView
    {
        // ------------------------------------------------------------------ Fassaden-Hilfen
        struct Facade
        {
            public Vector3 C, T, N;
            public float Len, Half, Yaw;
            public Vector3 P(float u, float y, float d) { return C + T * u + N * (Half + d) + Vector3.up * y; }
        }

        static Facade FaceOf(Box bx, int f)
        {
            var c = new Vector3(bx.Cx, 0, bx.Cz);
            switch (f)
            {
                case 0: return new Facade { C = c, T = Vector3.right, N = Vector3.forward, Len = bx.Hx * 2, Half = bx.Hz, Yaw = 0 };
                case 1: return new Facade { C = c, T = Vector3.left, N = Vector3.back, Len = bx.Hx * 2, Half = bx.Hz, Yaw = 180 };
                case 2: return new Facade { C = c, T = Vector3.back, N = Vector3.right, Len = bx.Hz * 2, Half = bx.Hx, Yaw = 90 };
                default: return new Facade { C = c, T = Vector3.forward, N = Vector3.left, Len = bx.Hz * 2, Half = bx.Hx, Yaw = 270 };
            }
        }

        /// <summary>Quader auf einer Fassade: u entlang der Wand, y Höhe, d Abstand vor der Wand; Größe lokal (Breite, Höhe, Tiefe).</summary>
        static void FBox(MeshBuilder b, Facade f, float u, float y, float d, float w, float h, float depth, float roll = 0f, float tilt = 0f)
        {
            b.BoxRot(f.P(u, y, d), new Vector3(w, h, depth), new Vector3(tilt, f.Yaw, roll));
        }

        static Material Mat(Color c, float gloss = -1f)
        {
            return Mats.Get(Mats.Opaque, new Color(Mathf.Round(c.r * 20) / 20f, Mathf.Round(c.g * 20) / 20f, Mathf.Round(c.b * 20) / 20f, 1f), null, gloss);
        }
        static Material MetalMat(Color c) { return Mats.Get(Mats.Metal, new Color(Mathf.Round(c.r * 20) / 20f, Mathf.Round(c.g * 20) / 20f, Mathf.Round(c.b * 20) / 20f, 1f)); }

        static readonly Color[] SignCols = { new Color(0.85f, 0.25f, 0.2f), new Color(0.2f, 0.45f, 0.75f), new Color(0.95f, 0.72f, 0.2f), new Color(0.25f, 0.6f, 0.4f) };
        static readonly Color[] ContainerCols = { new Color(0.62f, 0.22f, 0.17f), new Color(0.2f, 0.4f, 0.62f), new Color(0.25f, 0.5f, 0.35f), new Color(0.85f, 0.5f, 0.18f), new Color(0.82f, 0.82f, 0.78f), new Color(0.55f, 0.35f, 0.25f) };

        Material trimMat, darkMat, roofMat, woodMat, acMat, railMat, rubbleMat, plinthMat, glassDark, signLight;

        void InitBuildingMats()
        {
            trimMat = Mat(new Color(0.86f, 0.84f, 0.78f));
            darkMat = Mat(new Color(0.16f, 0.16f, 0.17f));
            roofMat = Mat(new Color(0.3f, 0.29f, 0.29f));
            woodMat = Mat(new Color(0.46f, 0.33f, 0.22f));
            acMat = MetalMat(new Color(0.74f, 0.76f, 0.78f));
            railMat = MetalMat(new Color(0.3f, 0.31f, 0.33f));
            rubbleMat = Mat(new Color(0.5f, 0.48f, 0.45f));
            plinthMat = Mat(new Color(0.4f, 0.39f, 0.37f));
            glassDark = Mat(new Color(0.08f, 0.09f, 0.11f), 0.85f);
            signLight = Mat(new Color(0.95f, 0.93f, 0.85f));
        }

        // ================================================================== Aufbau
        void BuildBoxes()
        {
            InitBuildingMats();
            var cb = new ChunkBuilder(60f);
            var rng = new Rng(Def.Seed + 77);
            foreach (var bx in Layout.Colliders)
            {
                if (bx.Gate >= 0 || bx.Kind == "terminal" || bx.Kind == "gateblock") continue;
                if (bx.DuneSet >= 0) { BuildDune(bx); continue; }
                var mb = cb.At(bx.Cx, bx.Cz);
                mb.M = Matrix4x4.identity;
                var col = Mats.C(bx.Color == 0 ? 0x999999u : bx.Color);
                int area = Mathf.Clamp(bx.Area < 0 ? PlanetLayout.AreaOf(bx.Cz) : bx.Area, 0, 2);
                switch (bx.Kind)
                {
                    case "house":
                        House(mb, bx, rng, area, col, 3f, false);
                        break;
                    case "serverhall":
                        ServerHall(mb, bx, rng, area, col);
                        break;
                    case "tower":
                        if (Planet == "nivalis") ControlTower(mb, bx, rng, area, col);
                        else House(mb, bx, rng, area, col, 3f, false);
                        break;
                    case "cityedge":
                        CityEdge(cb, bx, rng, col);
                        break;
                    case "areawall":
                        if (Planet == "terra") House(mb, bx, rng, area, col, 3f, false, bx.Hx > bx.Hz ? 3 : 12);
                        else if (Planet == "pelagia") Quay(mb, bx, rng, area, false);
                        else if (Planet == "nivalis") IceWall(cb, bx, rng);
                        else RockWall(cb, bx, rng);
                        break;
                    case "seawall":
                        Quay(mb, bx, rng, area, true);
                        break;
                    case "icewall":
                        IceWall(cb, bx, rng);
                        break;
                    case "cliff":
                        RockWall(cb, bx, rng);
                        break;
                    case "mall":
                        House(mb, bx, rng, area, col, 4.5f, true);
                        break;
                    case "core":
                    case "garage":
                        break; // Stützpunkt wird separat aufgebaut
                    case "pavilion":
                        Pavilion(mb, bx, rng, col);
                        break;
                    case "greenhouse":
                        BuildGreenhouse(bx);
                        break;
                    case "shed":
                    case "hall":
                    case "warehouse":
                    case "foundry":
                        IndustrialHall(mb, bx, rng, area, col);
                        break;
                    case "furnace":
                    case "silo":
                        SiloFurnace(mb, bx, rng, col);
                        break;
                    case "stilt":
                        StiltHouse(mb, bx, rng, area, col);
                        break;
                    case "ruin":
                        SunkenRuin(mb, bx, rng);
                        break;
                    case "dome":
                        Dome(mb, bx, rng, area);
                        break;
                    case "hangar":
                        Hangar(mb, bx, rng, area, col);
                        break;
                    case "launchtower":
                        LaunchTower(mb, bx);
                        break;
                    default:
                        mb.For(Mat(col)).Box(new Vector3(bx.Cx, bx.Y0 + bx.H * 0.5f, bx.Cz), new Vector3(bx.Hx * 2, bx.H, bx.Hz * 2));
                        break;
                }
            }
            Debug.Log("[RE:PLANET] Gebäude " + Planet + ": " + cb.VertexCount + " Ecken in " + cb.ChunkCount + " Blöcken.");
            cb.Build("Buildings", Root, true);
        }

        // ================================================================== Wohn- und Geschäftshäuser
        /// <summary>
        /// Haus mit Sockel, Gesimsen, Fensterreihen (Rahmen, Bänke, Läden, vernagelte/zerbrochene Scheiben), Balkonen,
        /// Läden mit Markise und Schild, Türen, Klimageräten, Fallrohren, Plakaten, Rissen, Attika und Dachaufbauten.
        /// Ruinen haben abgebrochene Obergeschosse mit Moniereisen und Schutt.
        /// </summary>
        void House(MultiBuilder mb, Box bx, Rng rng, int area, Color col, float floorH, bool mall, int faceMask = 15)
        {
            float gy = Terrain.HeightAt(Planet, bx.Cx, bx.Cz);
            float top = bx.Y0 + bx.H;
            var wall = Mat(col);
            var trim = rng.Chance(0.5f) ? trimMat : Mat(col * 0.72f);
            var win = WindowMat(area);
            int style = rng.Range(0, 4);
            bool ruin = !mall && rng.Chance(0.3f) && top - gy > 7f;
            // Körper: drei Scheiben entlang X, bei Ruinen unterschiedlich hoch abgebrochen
            var slice = new float[3];
            for (int i = 0; i < 3; i++) slice[i] = top;
            if (ruin)
            {
                int keep = rng.Range(0, 3);
                for (int i = 0; i < 3; i++) if (i != keep) slice[i] = Mathf.Max(gy + 4f, top - rng.Range(2.5f, Mathf.Min(9f, (top - gy) * 0.5f)));
            }
            float sw = bx.Hx * 2f / 3f;
            System.Func<float, float> wallTop = x => slice[Mathf.Clamp(Mathf.FloorToInt((x - (bx.Cx - bx.Hx)) / sw), 0, 2)];
            float minTop = Mathf.Min(slice[0], Mathf.Min(slice[1], slice[2]));
            if (!ruin) mb.For(wall).Box(new Vector3(bx.Cx, (bx.Y0 + top) * 0.5f, bx.Cz), new Vector3(bx.Hx * 2, top - bx.Y0, bx.Hz * 2));
            else
                for (int i = 0; i < 3; i++)
                {
                    float x0 = bx.Cx - bx.Hx + i * sw;
                    mb.For(wall).Box(new Vector3(x0 + sw * 0.5f, (bx.Y0 + slice[i]) * 0.5f, bx.Cz), new Vector3(sw + (i == 1 ? 0.01f : 0f), slice[i] - bx.Y0, bx.Hz * 2));
                    if (slice[i] < top - 0.5f)
                    {
                        // Bruchkante: Deckenplatte ragt heraus, Moniereisen, Schuttbrocken
                        mb.For(plinthMat).BoxRot(new Vector3(x0 + sw * 0.5f + rng.Range(-0.5f, 0.5f), slice[i] + 0.1f, bx.Cz + rng.Range(-0.4f, 0.4f)), new Vector3(sw * 0.9f, 0.25f, bx.Hz * 1.6f), new Vector3(rng.Range(-8f, 8f), rng.Range(-6f, 6f), rng.Range(-10f, 10f)));
                        for (int k = 0; k < 5; k++)
                        {
                            var p = new Vector3(x0 + rng.Range(0.3f, sw - 0.3f), slice[i], bx.Cz + rng.Range(-bx.Hz + 0.3f, bx.Hz - 0.3f));
                            mb.For(railMat).Beam(p, p + new Vector3(rng.Range(-0.6f, 0.6f), rng.Range(0.8f, 1.8f), rng.Range(-0.6f, 0.6f)), 0.05f);
                        }
                        for (int k = 0; k < 3; k++)
                            mb.For(wall).BoxJ(new Vector3(x0 + rng.Range(0.5f, sw - 0.5f), slice[i] + 0.4f, bx.Cz + rng.Range(-bx.Hz + 1f, bx.Hz - 1f)), new Vector3(rng.Range(0.8f, 1.6f), rng.Range(0.5f, 1f), rng.Range(0.8f, 1.6f)), new Vector3(rng.Range(-20f, 20f), rng.Range(0f, 90f), rng.Range(-20f, 20f)), 0.15f, k);
                    }
                }
            // Sockel
            mb.For(plinthMat).Box(new Vector3(bx.Cx, (bx.Y0 + gy + 0.9f) * 0.5f, bx.Cz), new Vector3(bx.Hx * 2 + 0.16f, gy + 0.9f - bx.Y0, bx.Hz * 2 + 0.16f));
            // Gesimse je Geschoss
            for (float y = gy + floorH; y < minTop - 0.8f; y += floorH)
                mb.For(trim).Box(new Vector3(bx.Cx, y, bx.Cz), new Vector3(bx.Hx * 2 + 0.22f, 0.16f, bx.Hz * 2 + 0.22f));
            // Attika
            for (int i = 0; i < 3; i++)
                {
                    if (slice[i] < top - 0.5f) continue;
                    float x0 = bx.Cx - bx.Hx + i * sw;
                    mb.For(trim).Box(new Vector3(x0 + sw * 0.5f, slice[i] + 0.3f, bx.Cz + bx.Hz - 0.12f), new Vector3(sw, 0.6f, 0.3f));
                    mb.For(trim).Box(new Vector3(x0 + sw * 0.5f, slice[i] + 0.3f, bx.Cz - bx.Hz + 0.12f), new Vector3(sw, 0.6f, 0.3f));
                    if (i == 0) mb.For(trim).Box(new Vector3(bx.Cx - bx.Hx + 0.12f, slice[i] + 0.3f, bx.Cz), new Vector3(0.3f, 0.6f, bx.Hz * 2));
                    if (i == 2) mb.For(trim).Box(new Vector3(bx.Cx + bx.Hx - 0.12f, slice[i] + 0.3f, bx.Cz), new Vector3(0.3f, 0.6f, bx.Hz * 2));
                }
            if (mall) // Werbeband unter dem Dach
                mb.For(Mat(new Color(0.85f, 0.3f, 0.2f))).Box(new Vector3(bx.Cx, top - 1.2f, bx.Cz), new Vector3(bx.Hx * 2 + 0.2f, 1.4f, bx.Hz * 2 + 0.2f));

            float winW = mall ? 3.2f : 1.2f, winH = mall ? 2.2f : 1.5f, spacing = mall ? 4.5f : 3f;
            bool balconies = !mall && style == 0;
            bool shutters = !mall && style == 1;
            var shutterMat = Mat(SignCols[rng.Range(0, SignCols.Length)] * 0.8f);
            for (int f = 0; f < 4; f++)
            {
                if ((faceMask & (1 << f)) == 0) continue;
                var fc = FaceOf(bx, f);
                float len = fc.Len;
                if (len < 3f) continue;
                int cols = Mathf.Max(1, Mathf.FloorToInt((len - 1.2f) / spacing));
                bool shop = mall || (len > 7f && rng.Chance(0.4f));
                int doorCol = rng.Range(0, cols);
                // Erdgeschoss
                if (shop) ShopFront(mb, fc, gy, len, rng, mall, area);
                for (int fl = 0; fl < 40; fl++)
                {
                    float y = gy + fl * floorH;
                    if (y + floorH > top + 0.1f) break;
                    for (int i = 0; i < cols; i++)
                    {
                        float u = -len * 0.5f + (i + 0.5f) * len / cols;
                        var wp = fc.P(u, 0, 0);
                        if (y + (mall ? 4.4f : 2.6f) > wallTop(wp.x) - 0.4f) continue;
                        if (fl == 0)
                        {
                            if (shop) continue;
                            if (i == doorCol) { Door(mb, fc, u, gy); continue; }
                        }
                        bool balc = balconies && fl > 0 && (f < 2) && i % 2 == 0 && y + 3f < wallTop(wp.x);
                        Window(mb, fc, u, y + (balc ? 0.1f : 0.95f), winW, balc ? 2.2f : winH, rng, win, trim, shutters ? shutterMat : null);
                        if (balc) Balcony(mb, fc, u, y, rng);
                    }
                }
                // Klimageräte, Fallrohr, Plakat, Risse
                int acs = mall ? 0 : rng.Range(0, 3);
                for (int k = 0; k < acs; k++)
                {
                    float u = rng.Range(-len * 0.4f, len * 0.4f), y = gy + floorH * rng.Range(1, 4) + 0.2f;
                    if (y > wallTop(fc.P(u, 0, 0).x) - 1f) continue;
                    FBox(mb.For(acMat), fc, u, y, 0.22f, 0.8f, 0.55f, 0.44f);
                    FBox(mb.For(darkMat), fc, u - 0.12f, y, 0.45f, 0.4f, 0.4f, 0.02f);
                }
                if (rng.Chance(0.5f)) FBox(mb.For(railMat), fc, (rng.Chance(0.5f) ? -1 : 1) * (len * 0.5f - 0.25f), (gy + minTop) * 0.5f, 0.06f, 0.12f, minTop - gy, 0.12f);
                if (rng.Chance(0.45f)) FBox(mb.For(Mat(SignCols[rng.Range(0, SignCols.Length)])), fc, rng.Range(-len * 0.35f, len * 0.35f), gy + 1.6f, 0.02f, 0.7f, 1.0f, 0.02f, rng.Range(-4f, 4f));
                if (rng.Chance(ruin ? 0.9f : 0.4f))
                {
                    float u = rng.Range(-len * 0.4f, len * 0.4f), y = gy + rng.Range(2f, Mathf.Max(2.5f, minTop - gy - 2f));
                    for (int k = 0; k < 3; k++) FBox(mb.For(darkMat), fc, u + k * 0.25f * (k % 2 == 0 ? 1 : -1), y - k * 0.6f, 0.012f, 0.05f, 0.75f, 0.02f, k % 2 == 0 ? 22f : -18f);
                }
            }
            // Dachaufbauten (nur auf intakten Dachflächen)
            int intact = -1;
            for (int i = 0; i < 3; i++) if (slice[i] >= top - 0.5f) intact = i;
            if (intact >= 0 && bx.Hx > 2.5f && bx.Hz > 2.5f)
            {
                float x0 = bx.Cx - bx.Hx + intact * sw + sw * 0.5f;
                var roof = new Vector3(x0, top, bx.Cz);
                if (rng.Chance(0.7f))
                {
                    var p = roof + new Vector3(rng.Range(-sw * 0.25f, sw * 0.25f), 1.1f, rng.Range(-bx.Hz * 0.4f, bx.Hz * 0.4f));
                    mb.For(trim).Box(p, new Vector3(Mathf.Min(2.6f, sw * 0.6f), 2.2f, 2.4f));
                    mb.For(darkMat).Box(p + new Vector3(0, -0.2f, 1.21f), new Vector3(0.9f, 1.8f, 0.04f));
                }
                if (rng.Chance(0.35f)) // Wassertank auf Stelzen
                {
                    var p = roof + new Vector3(rng.Range(-sw * 0.3f, sw * 0.3f), 0, rng.Range(-bx.Hz * 0.5f, bx.Hz * 0.5f));
                    for (int k = 0; k < 4; k++) mb.For(railMat).Box(p + new Vector3(k % 2 == 0 ? -0.6f : 0.6f, 0.7f, k < 2 ? -0.6f : 0.6f), new Vector3(0.1f, 1.4f, 0.1f));
                    mb.For(woodMat).Cylinder(p + Vector3.up * 1.4f, 0.95f, 1.8f, 10, true, 0.9f);
                    mb.For(roofMat).Cylinder(p + Vector3.up * 3.2f, 1.0f, 0.5f, 10, true, 0.1f);
                }
                for (int k = rng.Range(0, 3); k > 0; k--) // Klimaanlagen auf dem Dach
                {
                    var p = roof + new Vector3(rng.Range(-sw * 0.35f, sw * 0.35f), 0.45f, rng.Range(-bx.Hz * 0.6f, bx.Hz * 0.6f));
                    mb.For(acMat).Box(p, new Vector3(1.4f, 0.9f, 1.1f));
                    mb.For(darkMat).Cylinder(p + Vector3.up * 0.45f, 0.4f, 0.03f, 10);
                }
                if (rng.Chance(0.6f)) // Antenne
                {
                    var p = roof + new Vector3(rng.Range(-sw * 0.35f, sw * 0.35f), 0, rng.Range(-bx.Hz * 0.6f, bx.Hz * 0.6f));
                    float ah = rng.Range(2.5f, 5f);
                    mb.For(railMat).Cylinder(p, 0.05f, ah, 5);
                    for (int k = 0; k < 3; k++) mb.For(railMat).Box(p + Vector3.up * (ah - 0.4f - k * 0.5f), new Vector3(1.2f - k * 0.3f, 0.04f, 0.04f));
                }
                if (rng.Chance(0.3f)) // Satellitenschüssel
                {
                    var p = roof + new Vector3(rng.Range(-sw * 0.35f, sw * 0.35f), 0.9f, rng.Range(-bx.Hz * 0.6f, bx.Hz * 0.6f));
                    var o = mb.M;
                    mb.M = Matrix4x4.TRS(p, Quaternion.Euler(-55, rng.Range(0f, 360f), 0), Vector3.one);
                    mb.For(acMat).Lathe(Vector3.zero, new[] { new Vector2(0, 0), new Vector2(0.4f, 0.1f), new Vector2(0.55f, 0.22f) }, 10);
                    mb.M = o;
                    mb.For(railMat).Beam(p + Vector3.down * 0.9f, p, 0.06f);
                }
                if (!mall && rng.Chance(0.4f)) // Schornsteine
                    for (int k = 0; k < 2; k++) mb.For(Mat(new Color(0.55f, 0.32f, 0.26f))).Box(roof + new Vector3(-sw * 0.3f + k * 0.8f, 0.8f, bx.Hz * 0.5f), new Vector3(0.6f, 1.6f, 0.6f));
            }
            // Schutt am Fuß von Ruinen
            if (ruin)
                for (int k = 0; k < 4; k++)
                {
                    int f = rng.Range(0, 4);
                    var fc = FaceOf(bx, f);
                    var p = fc.P(rng.Range(-fc.Len * 0.4f, fc.Len * 0.4f), gy - 0.15f, rng.Range(0.05f, 0.3f));
                    if (Layout.BlockedStatic(p.x, p.z, 0.2f)) continue;
                    mb.For(rubbleMat).Blob(p, rng.Range(0.5f, 0.9f), rng.Range(0.3f, 0.6f), 7, 2, k + (int)bx.Cx, 0.35f);
                }
        }

        void Window(MultiBuilder mb, Facade f, float u, float yb, float w, float h, Rng rng, Material win, Material trim, Material shutter)
        {
            float r = rng.Next();
            var glass = r < 0.1f ? woodMat : r < 0.2f ? glassDark : win;
            FBox(mb.For(glass == woodMat ? glassDark : glass), f, u, yb + h * 0.5f, 0.02f, w, h, 0.06f);
            if (glass == woodMat) // vernagelt
            {
                FBox(mb.For(woodMat), f, u, yb + h * 0.35f, 0.07f, w + 0.2f, 0.18f, 0.04f, 18f);
                FBox(mb.For(woodMat), f, u, yb + h * 0.68f, 0.07f, w + 0.2f, 0.18f, 0.04f, -14f);
            }
            FBox(mb.For(trim), f, u, yb - 0.05f, 0.1f, w + 0.3f, 0.1f, 0.22f);         // Fensterbank
            if (w > 2f) FBox(mb.For(trim), f, u, yb + h + 0.08f, 0.05f, w + 0.24f, 0.14f, 0.1f);   // Sturz (nur große Fenster)
            if (shutter != null)
            {
                FBox(mb.For(shutter), f, u - w * 0.5f - 0.28f, yb + h * 0.5f, 0.05f, 0.5f, h, 0.05f);
                if (rng.Chance(0.8f)) FBox(mb.For(shutter), f, u + w * 0.5f + 0.28f, yb + h * 0.5f, 0.05f, 0.5f, h, 0.05f);
                else FBox(mb.For(shutter), f, u + w * 0.5f + 0.35f, yb + h * 0.4f, 0.08f, 0.5f, h, 0.05f, -16f); // hängt schief
            }
        }

        void Balcony(MultiBuilder mb, Facade f, float u, float y, Rng rng)
        {
            FBox(mb.For(trimMat), f, u, y + 0.08f, 0.55f, 2.0f, 0.15f, 1.1f);
            FBox(mb.For(railMat), f, u, y + 0.62f, 1.08f, 2.0f, 0.9f, 0.05f);
            FBox(mb.For(railMat), f, u - 0.98f, y + 0.62f, 0.55f, 0.05f, 0.9f, 1.1f);
            FBox(mb.For(railMat), f, u + 0.98f, y + 0.62f, 0.55f, 0.05f, 0.9f, 1.1f);
            if (rng.Chance(0.35f)) FBox(mb.For(Mat(SignCols[rng.Range(0, SignCols.Length)] * 0.7f)), f, u + 0.5f, y + 0.4f, 0.5f, 0.5f, 0.5f, 0.5f, 0f); // abgestellte Kiste
        }

        void Door(MultiBuilder mb, Facade f, float u, float gy)
        {
            FBox(mb.For(darkMat), f, u, gy + 1.15f, 0.03f, 1.2f, 2.3f, 0.08f);
            FBox(mb.For(trimMat), f, u, gy + 2.4f, 0.06f, 1.5f, 0.16f, 0.14f);
            FBox(mb.For(trimMat), f, u, gy + 2.7f, 0.45f, 1.8f, 0.1f, 0.9f);           // Vordach
            FBox(mb.For(plinthMat), f, u, gy + 0.1f, 0.35f, 1.6f, 0.2f, 0.7f);         // Stufe
        }

        void ShopFront(MultiBuilder mb, Facade f, float gy, float len, Rng rng, bool mall, int area)
        {
            float w = len * (mall ? 0.85f : 0.72f);
            var sign = Mat(SignCols[rng.Range(0, SignCols.Length)]);
            bool shutter = !mall && rng.Chance(0.45f);
            if (shutter)
            {
                FBox(mb.For(acMat), f, 0, gy + 1.35f, 0.04f, w, 2.5f, 0.08f);
                for (int k = 0; k < 8; k++) FBox(mb.For(railMat), f, 0, gy + 0.3f + k * 0.3f, 0.09f, w, 0.04f, 0.03f);
            }
            else
            {
                FBox(mb.For(WindowMat(area)), f, 0, gy + 1.45f, 0.03f, w, 2.3f, 0.06f);
                for (int k = -1; k <= 1; k++) FBox(mb.For(darkMat), f, k * w / 3f, gy + 1.45f, 0.07f, 0.1f, 2.3f, 0.06f);
                if (rng.Chance(0.5f)) FBox(mb.For(glassDark), f, rng.Range(-w * 0.3f, w * 0.3f), gy + 1.2f, 0.08f, 0.9f, 1.4f, 0.02f, rng.Range(-10f, 10f)); // Scherbe fehlt
            }
            FBox(mb.For(plinthMat), f, 0, gy + 0.18f, 0.08f, w + 0.2f, 0.36f, 0.14f);
            FBox(mb.For(sign), f, 0, gy + 3.0f, 0.12f, w * 0.9f, 0.8f, 0.2f);
            int letters = Mathf.Clamp((int)(w * 0.8f), 3, 9);
            for (int k = 0; k < letters; k++)
                if (!rng.Chance(0.15f)) FBox(mb.For(signLight), f, -w * 0.4f + (k + 0.5f) * w * 0.8f / letters, gy + 3.0f + (rng.Chance(0.1f) ? -0.15f : 0f), 0.23f, w * 0.5f / letters, 0.45f, 0.03f, rng.Chance(0.1f) ? 15f : 0f);
            if (!mall && rng.Chance(0.7f)) // Markise mit Streifen
            {
                var aw = Mat(SignCols[rng.Range(0, SignCols.Length)] * 0.9f);
                bool torn = rng.Chance(0.3f);
                FBox(mb.For(aw), f, 0, gy + 2.45f, 0.65f, w * (torn ? 0.6f : 0.95f), 0.06f, 1.3f, 0f, -18f);
                for (int k = 0; k < 5; k++) FBox(mb.For(trimMat), f, -w * 0.38f + k * w * 0.19f, gy + 2.46f, 0.66f, w * 0.06f, 0.07f, 1.32f, 0f, -18f);
            }
        }

        // ------------------------------------------------------------------ Moderne Serverhalle (NIVALIS)
        void ServerHall(MultiBuilder mb, Box bx, Rng rng, int area, Color col)
        {
            float gy = Terrain.HeightAt(Planet, bx.Cx, bx.Cz);
            float top = bx.Y0 + bx.H;
            var wall = Mat(col);
            var fin = Mat(new Color(0.85f, 0.88f, 0.92f));
            var win = WindowMat(area);
            var snowM = Mat(new Color(0.94f, 0.97f, 1f), 0.3f);
            mb.For(wall).Box(new Vector3(bx.Cx, (bx.Y0 + top) * 0.5f, bx.Cz), new Vector3(bx.Hx * 2, top - bx.Y0, bx.Hz * 2));
            mb.For(plinthMat).Box(new Vector3(bx.Cx, (bx.Y0 + gy + 0.6f) * 0.5f, bx.Cz), new Vector3(bx.Hx * 2 + 0.16f, gy + 0.6f - bx.Y0, bx.Hz * 2 + 0.16f));
            for (int f = 0; f < 4; f++)
            {
                var fc = FaceOf(bx, f);
                for (float y = gy + 1.2f; y + 1.2f < top - 0.6f; y += 3f)
                {
                    FBox(mb.For(win), fc, 0, y + 0.5f, 0.02f, fc.Len - 1.2f, 0.9f, 0.06f);
                    FBox(mb.For(fin), fc, 0, y + 1.05f, 0.1f, fc.Len - 1f, 0.1f, 0.2f);
                }
                for (float u = -fc.Len * 0.5f + 1.5f; u < fc.Len * 0.5f - 1f; u += 2.2f)
                    FBox(mb.For(fin), fc, u, (gy + top) * 0.5f, 0.15f, 0.12f, top - gy, 0.3f);
                if (f == 0) { FBox(mb.For(darkMat), fc, 0, gy + 1.3f, 0.05f, 2.4f, 2.6f, 0.1f); FBox(mb.For(Mats.Get(Mats.Emissive, new Color(0.3f, 0.9f, 1f), new Color(0.3f, 1.2f, 1.5f))), fc, 0, gy + 2.9f, 0.1f, 2.4f, 0.12f, 0.06f); }
                // Eiszapfen an der Dachkante
                for (int k = 0; k < 6; k++)
                {
                    var p = fc.P(rng.Range(-fc.Len * 0.45f, fc.Len * 0.45f), top - 0.05f, 0.1f);
                    mb.For(snowM).Tube(p, p + Vector3.down * rng.Range(0.3f, 1.1f), rng.Range(0.05f, 0.1f), 5, true, 0f);
                }
            }
            mb.For(snowM).Box(new Vector3(bx.Cx, top + 0.12f, bx.Cz), new Vector3(bx.Hx * 2 + 0.1f, 0.25f, bx.Hz * 2 + 0.1f));
            // Reihen von Rückkühlern auf dem Dach
            int nx = Mathf.Max(1, (int)(bx.Hx * 2 / 3.2f)), nz = Mathf.Max(1, (int)(bx.Hz * 2 / 4f));
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    if (rng.Chance(0.3f)) continue;
                    var p = new Vector3(bx.Cx - bx.Hx + (i + 0.5f) * bx.Hx * 2 / nx, top + 0.7f, bx.Cz - bx.Hz + (j + 0.5f) * bx.Hz * 2 / nz);
                    mb.For(acMat).Box(p, new Vector3(2.4f, 1.1f, 1.6f));
                    mb.For(darkMat).Cylinder(p + new Vector3(-0.55f, 0.55f, 0), 0.5f, 0.04f, 10);
                    mb.For(darkMat).Cylinder(p + new Vector3(0.55f, 0.55f, 0), 0.5f, 0.04f, 10);
                }
        }

        void ControlTower(MultiBuilder mb, Box bx, Rng rng, int area, Color col)
        {
            float gy = Terrain.HeightAt(Planet, bx.Cx, bx.Cz);
            float top = bx.Y0 + bx.H;
            var wall = Mat(col);
            var c = new Vector3(bx.Cx, 0, bx.Cz);
            float r = Mathf.Min(bx.Hx, bx.Hz);
            if (top - gy < 8f) { ServerHall(mb, bx, rng, area, col); return; }
            mb.For(wall).Box(new Vector3(bx.Cx, (bx.Y0 + top - 3f) * 0.5f, bx.Cz), new Vector3(bx.Hx * 2, top - 3f - bx.Y0, bx.Hz * 2));
            mb.For(WindowMat(area)).Cylinder(c + Vector3.up * (top - 3f), r * 1.05f, 2.6f, 8, false, r * 1.2f);
            mb.For(darkMat).Cylinder(c + Vector3.up * (top - 3.2f), r * 1.1f, 0.3f, 8);
            mb.For(FinMat(Planet)).Cylinder(c + Vector3.up * (top - 0.4f), r * 1.3f, 0.5f, 8, true, r * 0.9f);
            mb.For(railMat).Cylinder(c + Vector3.up * top, 0.1f, 6f, 5);
            mb.For(Mats.Get(Mats.Emissive, new Color(1f, 0.2f, 0.1f), new Color(3f, 0.5f, 0.3f))).Sphere(c + Vector3.up * (top + 6f), 0.3f, 6, 4);
            for (float y = gy + 3f; y < top - 5f; y += 4f)
                for (int f = 0; f < 4; f++) { var fc = FaceOf(bx, f); FBox(mb.For(WindowMat(area)), fc, 0, y, 0.02f, 1.0f, 1.6f, 0.06f); }
        }

        static Material FinMat(string planet) { return Mat(planet == "nivalis" ? new Color(0.88f, 0.92f, 0.96f) : new Color(0.7f, 0.7f, 0.7f)); }

        /// <summary>Stadtrand (TERRA): die lange Grenzbox wird zu einer Reihe einzelner Häuser unterschiedlicher Höhe.</summary>
        void CityEdge(ChunkBuilder cb, Box bx, Rng rng, Color col)
        {
            bool alongX = bx.Hx > bx.Hz;
            float len = alongX ? bx.Hx * 2 : bx.Hz * 2;
            float start = alongX ? bx.Cx - bx.Hx : bx.Cz - bx.Hz;
            uint[] cols = { 0x9C8E7A, 0xB5A48A, 0x8F8374, 0xA89880, 0xC2A68A };
            float t = 0;
            while (t < len - 2f)
            {
                float w = Mathf.Min(rng.Range(12f, 24f), len - t);
                float mid = start + t + w * 0.5f;
                float gy = alongX ? Terrain.HeightAt(Planet, Mathf.Clamp(mid, -150, 150), Mathf.Clamp(bx.Cz, -150, 150)) : Terrain.HeightAt(Planet, Mathf.Clamp(bx.Cx, -150, 150), Mathf.Clamp(mid, -150, 150));
                var seg = new Box
                {
                    Cx = alongX ? mid : bx.Cx, Cz = alongX ? bx.Cz : mid,
                    Hx = alongX ? w * 0.5f - 0.3f : bx.Hx, Hz = alongX ? bx.Hz : w * 0.5f - 0.3f,
                    Y0 = bx.Y0, H = gy + rng.Range(12f, 32f) - bx.Y0, Kind = "house", Area = PlanetLayout.AreaOf(alongX ? bx.Cz : mid),
                };
                var mb = cb.At(seg.Cx, seg.Cz);
                // nur die zur Spielfläche zeigende Fassade bekommt Fenster (die anderen sieht man nicht)
                int inward = alongX ? (bx.Cz > 0 ? 2 : 1) : (bx.Cx > 0 ? 8 : 4);
                House(mb, seg, rng, Mathf.Clamp(seg.Area, 0, 2), Mats.C(cols[rng.Range(0, cols.Length)]), 3f, false, inward);
                t += w;
            }
        }

        void Pavilion(MultiBuilder mb, Box bx, Rng rng, Color col)
        {
            float y0 = bx.Y0, h = bx.H;
            var c = new Vector3(bx.Cx, y0 + h * 0.5f, bx.Cz);
            var post = Mat(new Color(0.9f, 0.9f, 0.88f));
            for (int i = 0; i < 4; i++) mb.For(post).Cylinder(new Vector3(c.x + (i % 2 == 0 ? -1 : 1) * (bx.Hx - 0.25f), y0, c.z + (i < 2 ? -1 : 1) * (bx.Hz - 0.25f)), 0.16f, h, 8);
            mb.For(post).Box(new Vector3(c.x, y0 + h, c.z), new Vector3(bx.Hx * 2 + 0.3f, 0.25f, bx.Hz * 2 + 0.3f));
            var o = mb.M;
            mb.M = Matrix4x4.TRS(new Vector3(c.x, y0 + h + 0.12f, c.z), Quaternion.identity, Vector3.one);
            mb.For(Mat(new Color(0.35f, 0.55f, 0.5f))).Cylinder(Vector3.zero, Mathf.Max(bx.Hx, bx.Hz) * 1.45f, 1.8f, 8, true, 0.15f);
            mb.M = o;
            mb.For(woodMat).Box(new Vector3(c.x, y0 + 0.9f, c.z - bx.Hz + 0.5f), new Vector3(bx.Hx * 1.4f, 0.1f, 0.5f));
        }

        // ================================================================== Industrie
        /// <summary>Halle/Lagerhaus: Wellblech-Rippen, Satteldach mit Lüftern, Rolltor mit Rahmen, Oberlichter, Rohre, Leiter, Rostspuren, Firmenschild.</summary>
        void IndustrialHall(MultiBuilder mb, Box bx, Rng rng, int area, Color col)
        {
            float gy = Terrain.HeightAt(Planet, bx.Cx, bx.Cz);
            float top = bx.Y0 + bx.H;
            var wall = Mat(col);
            var rib = Mat(col * 0.82f);
            var roofC = Planet == "pelagia" ? SignCols[rng.Range(0, SignCols.Length)] * 0.65f : Planet == "pyra" ? new Color(0.36f, 0.22f, 0.16f) : new Color(0.32f, 0.33f, 0.34f);
            var roof = Mat(roofC);
            var rust = Mat(new Color(0.4f, 0.22f, 0.14f));
            var win = WindowMat(area);
            bool alongX = bx.Hx >= bx.Hz;
            mb.For(wall).Box(new Vector3(bx.Cx, (bx.Y0 + top) * 0.5f, bx.Cz), new Vector3(bx.Hx * 2, top - bx.Y0, bx.Hz * 2));
            mb.For(plinthMat).Box(new Vector3(bx.Cx, (bx.Y0 + gy + 0.8f) * 0.5f, bx.Cz), new Vector3(bx.Hx * 2 + 0.14f, gy + 0.8f - bx.Y0, bx.Hz * 2 + 0.14f));
            // Satteldach (First entlang der langen Seite) mit Überstand
            float span = alongX ? bx.Hz * 2 : bx.Hx * 2, length = alongX ? bx.Hx * 2 : bx.Hz * 2;
            float roofH = Mathf.Clamp(span * 0.22f, 1f, 4f);
            var o = mb.M;
            mb.M = Matrix4x4.TRS(new Vector3(bx.Cx, top, bx.Cz), Quaternion.Euler(0, alongX ? 0 : 90, 0), Vector3.one);
            mb.For(roof).Prism(Vector3.zero, new Vector3(length + 0.5f, roofH, span + 0.9f));
            mb.For(wall).Prism(new Vector3(0, -0.01f, 0), new Vector3(length - 0.02f, roofH - 0.05f, span - 0.02f));
            // Dachrippen, Oberlichtband, Lüfter
            for (float x = -length * 0.5f + 0.8f; x < length * 0.5f; x += 1.6f)
            {
                mb.For(rib).BoxRot(new Vector3(x, roofH * 0.5f + 0.05f, span * 0.25f), new Vector3(0.1f, 0.08f, span * 0.55f), new Vector3(Mathf.Atan2(roofH, span * 0.5f) * Mathf.Rad2Deg, 0, 0));
            }
            if (bx.Kind == "hall" || bx.Kind == "foundry" || rng.Chance(0.4f))
                mb.For(Mats.Get(Mats.Fade, new Color(0.7f, 0.85f, 0.9f, 0.55f), null, 0.9f)).BoxRot(new Vector3(0, roofH * 0.62f, -span * 0.19f), new Vector3(length * 0.7f, 0.06f, span * 0.2f), new Vector3(-Mathf.Atan2(roofH, span * 0.5f) * Mathf.Rad2Deg, 0, 0));
            for (int k = rng.Range(1, 4); k > 0; k--)
            {
                var p = new Vector3(rng.Range(-length * 0.35f, length * 0.35f), roofH * 0.95f, 0);
                mb.For(acMat).Cylinder(p, 0.35f, 0.7f, 8);
                mb.For(acMat).Cylinder(p + Vector3.up * 0.7f, 0.55f, 0.2f, 8, true, 0.1f);
            }
            mb.M = o;
            // Wände: Rippen, Tor, Fenster, Rostspuren, Rohre, Leiter
            int doorFace = alongX ? (rng.Chance(0.5f) ? 0 : 1) : (rng.Chance(0.5f) ? 2 : 3);
            for (int f = 0; f < 4; f++)
            {
                var fc = FaceOf(bx, f);
                float wh = top - gy;
                for (float u = -fc.Len * 0.5f + 0.6f; u < fc.Len * 0.5f - 0.3f; u += 1.2f)
                    FBox(mb.For(rib), fc, u, (gy + 0.8f + top) * 0.5f, 0.04f, 0.14f, top - gy - 0.8f, 0.08f);
                FBox(mb.For(rib), fc, 0, top - 0.1f, 0.12f, fc.Len + 0.2f, 0.2f, 0.25f); // Traufe/Rinne
                if (f == doorFace)
                {
                    float dw = Mathf.Min(fc.Len * 0.5f, 5f), dh = Mathf.Min(wh - 1.2f, 4.2f);
                    FBox(mb.For(darkMat), fc, 0, gy + dh * 0.5f, 0.05f, dw, dh, 0.08f);
                    bool open = rng.Chance(0.5f);
                    float doorOff = open ? dw * 0.55f : 0f;
                    FBox(mb.For(acMat), fc, doorOff, gy + dh * 0.5f, 0.12f, dw, dh, 0.08f);
                    for (int k = 1; k < 8; k++) FBox(mb.For(railMat), fc, doorOff, gy + k * dh / 8f, 0.17f, dw, 0.05f, 0.03f);
                    FBox(mb.For(Mat(new Color(0.9f, 0.7f, 0.15f))), fc, 0, gy + dh + 0.2f, 0.1f, dw + 0.6f, 0.4f, 0.12f);
                    for (int k = 0; k < 4; k++) FBox(mb.For(darkMat), fc, -dw * 0.5f - 0.2f + k * (dw + 0.4f) / 3f, gy + dh + 0.2f, 0.17f, 0.25f, 0.4f, 0.02f, 45f);
                    // Firmenschild
                    if (wh > 5.5f)
                    {
                        var sign = Mat(SignCols[rng.Range(0, SignCols.Length)]);
                        FBox(mb.For(sign), fc, 0, gy + dh + 1.3f, 0.08f, Mathf.Min(fc.Len * 0.7f, 8f), 1.1f, 0.12f);
                        int n = rng.Range(4, 8);
                        for (int k = 0; k < n; k++) FBox(mb.For(signLight), fc, -Mathf.Min(fc.Len * 0.7f, 8f) * 0.4f + (k + 0.5f) * Mathf.Min(fc.Len * 0.7f, 8f) * 0.8f / n, gy + dh + 1.3f, 0.15f, 0.5f, 0.6f, 0.03f);
                    }
                }
                else if (wh > 4.5f && fc.Len > 5f)
                {
                    int n = Mathf.Max(1, (int)(fc.Len / 3.5f));
                    for (int i = 0; i < n; i++)
                    {
                        float u = -fc.Len * 0.5f + (i + 0.5f) * fc.Len / n;
                        FBox(mb.For(rng.Chance(0.25f) ? glassDark : win), fc, u, top - 1.3f, 0.06f, 1.8f, 0.9f, 0.06f);
                        FBox(mb.For(trimMat), fc, u, top - 1.8f, 0.1f, 2.0f, 0.08f, 0.16f);
                    }
                }
                for (int k = rng.Range(1, 4); k > 0; k--)
                {
                    float u = rng.Range(-fc.Len * 0.45f, fc.Len * 0.45f), h = rng.Range(1.2f, wh * 0.6f);
                    FBox(mb.For(rust), fc, u, top - 0.2f - h * 0.5f, 0.1f, rng.Range(0.15f, 0.4f), h, 0.02f);
                }
                if (f != doorFace && rng.Chance(0.35f)) // Rohrleitung
                {
                    float y = rng.Range(gy + 2f, top - 1.5f);
                    var a = fc.P(-fc.Len * 0.45f, y, 0.35f); var b = fc.P(fc.Len * 0.45f, y, 0.35f);
                    mb.For(rust).Tube(a, b, 0.18f, 8);
                    mb.For(rust).Tube(b, b + Vector3.down * (y - gy), 0.18f, 8);
                    for (float u = -fc.Len * 0.4f; u < fc.Len * 0.45f; u += 3f) FBox(mb.For(railMat), fc, u, y, 0.2f, 0.1f, 0.5f, 0.3f);
                }
                if (f != doorFace && wh > 5f && rng.Chance(0.3f)) // Leiter aufs Dach
                {
                    float u = rng.Range(-fc.Len * 0.35f, fc.Len * 0.35f);
                    FBox(mb.For(railMat), fc, u - 0.25f, (gy + top) * 0.5f + 0.4f, 0.25f, 0.06f, wh + 0.8f, 0.06f);
                    FBox(mb.For(railMat), fc, u + 0.25f, (gy + top) * 0.5f + 0.4f, 0.25f, 0.06f, wh + 0.8f, 0.06f);
                    for (float y = gy + 0.4f; y < top; y += 0.4f) FBox(mb.For(railMat), fc, u, y, 0.25f, 0.5f, 0.04f, 0.04f);
                }
            }
            if (bx.Kind == "foundry")
                for (int i = 0; i < 3; i++)
                {
                    var p = new Vector3(bx.Cx - bx.Hx * 0.6f + i * bx.Hx * 0.6f, top + roofH * 0.3f, bx.Cz);
                    mb.For(Mat(new Color(0.35f, 0.24f, 0.2f))).Cylinder(p, 1.3f, 14f, 12, true, 1.0f);
                    mb.For(darkMat).Cylinder(p + Vector3.up * 11f, 1.12f, 1.2f, 12);
                    mb.For(Mat(new Color(0.85f, 0.85f, 0.8f))).Cylinder(p + Vector3.up * 8f, 1.16f, 1f, 12);
                }
        }

        /// <summary>Silo/Hochofen: Ringe, Kegeldach, Käfigleiter, Rohr, Laufsteg.</summary>
        void SiloFurnace(MultiBuilder mb, Box bx, Rng rng, Color col)
        {
            float gy = Terrain.HeightAt(Planet, bx.Cx, bx.Cz);
            float h = bx.Y0 + bx.H - gy, y0 = bx.Y0;
            float r = Mathf.Min(bx.Hx, bx.Hz);
            var c = new Vector3(bx.Cx, 0, bx.Cz);
            var wall = Mat(col);
            var rust = Mat(new Color(0.42f, 0.24f, 0.16f));
            bool furnace = bx.Kind == "furnace";
            if (furnace)
            {
                mb.For(wall).Cylinder(c + Vector3.up * y0, r, gy - y0 + h * 0.55f, 16, true, r * 0.85f);
                mb.For(wall).Cylinder(c + Vector3.up * (gy + h * 0.55f), r * 0.85f, h * 0.45f, 16, true, r * 0.5f);
                mb.For(darkMat).Cylinder(c + Vector3.up * (gy + h), r * 0.35f, h * 0.45f, 12);
                mb.For(Mats.Get(Mats.Emissive, new Color(0.5f, 0.2f, 0.1f), new Color(0.9f, 0.3f, 0.1f))).Box(c + new Vector3(0, gy + 1.2f, r * 0.98f), new Vector3(r * 0.7f, 1.6f, 0.2f));
                mb.For(rust).Tube(c + new Vector3(r * 0.6f, gy + h * 0.9f, 0), c + new Vector3(r + 3f, gy + h * 0.7f, 0), 0.6f, 8);
                mb.For(rust).Tube(c + new Vector3(r + 3f, gy + h * 0.7f, 0), c + new Vector3(r + 3f, gy, 0), 0.6f, 8);
            }
            else
            {
                mb.For(wall).Cylinder(c + Vector3.up * y0, r, gy - y0 + h * 0.85f, 18, false);
                mb.For(roofMat).Cylinder(c + Vector3.up * (gy + h * 0.85f), r * 1.02f, h * 0.15f, 18, true, r * 0.2f);
                // Stützen und Trichter
                mb.For(acMat).Cylinder(c + Vector3.up * (gy + h * 0.85f + h * 0.15f), r * 0.2f, 0.8f, 8);
            }
            for (float y = gy + 2f; y < gy + h * 0.8f; y += 3f) mb.For(darkMat).Torus(c + Vector3.up * y, r * (furnace ? Mathf.Lerp(1f, 0.85f, (y - gy) / (h * 0.55f)) : 1f) + 0.02f, 0.12f, 18, 4);
            // Käfigleiter
            var lp = c + new Vector3(0, 0, -r - 0.35f);
            float lh = h * (furnace ? 0.55f : 0.85f);
            mb.For(railMat).Box(lp + new Vector3(-0.25f, gy + lh * 0.5f, 0), new Vector3(0.06f, lh, 0.06f));
            mb.For(railMat).Box(lp + new Vector3(0.25f, gy + lh * 0.5f, 0), new Vector3(0.06f, lh, 0.06f));
            for (float y = gy + 0.4f; y < gy + lh; y += 0.4f) mb.For(railMat).Box(lp + new Vector3(0, y, 0), new Vector3(0.5f, 0.04f, 0.04f));
            for (float y = gy + 2.5f; y < gy + lh; y += 1.2f) mb.For(railMat).TorusRot(lp + new Vector3(0, y, -0.3f), Vector3.zero, 0.42f, 0.025f, 8, 3);
            // Laufsteg oben
            float wy = gy + lh;
            mb.For(railMat).Torus(c + Vector3.up * wy, r + 0.5f, 0.06f, 20, 3);
            mb.For(railMat).Torus(c + Vector3.up * (wy + 1f), r + 0.5f, 0.04f, 20, 3);
            // Rostspuren
            for (int k = 0; k < 4; k++)
            {
                float a = rng.Range(0f, 6.28f);
                mb.For(rust).BoxRot(c + new Vector3(Mathf.Cos(a) * r, gy + h * 0.5f, Mathf.Sin(a) * r), new Vector3(0.3f, h * rng.Range(0.3f, 0.6f), 0.04f), new Vector3(0, -a * Mathf.Rad2Deg + 90, 0));
            }
        }

        // ================================================================== Hafen (PELAGIA)
        /// <summary>
        /// Kaimauer/Wellenbrecher: Blocksteine in zwei Tönen, Algenband an der Wasserlinie, Abdeckplatte, Geländer, Poller,
        /// Reifenfender, Leitern, Laternen, Tetrapoden und (auf breiten Mauerabschnitten) Container; Seezeichen am Torende.
        /// </summary>
        void Quay(MultiBuilder mb, Box bx, Rng rng, int area, bool outer)
        {
            float top = bx.Y0 + bx.H;
            var coreM = Mat(new Color(0.46f, 0.48f, 0.47f));
            var blockA = Mat(new Color(0.62f, 0.63f, 0.6f));
            var blockB = Mat(new Color(0.53f, 0.55f, 0.53f));
            var cap = Mat(new Color(0.74f, 0.73f, 0.68f));
            var algae = Mat(new Color(0.22f, 0.3f, 0.22f));
            var rust = Mat(new Color(0.45f, 0.25f, 0.16f));
            var rail = MetalMat(new Color(0.85f, 0.75f, 0.3f));
            var tire = Mat(new Color(0.11f, 0.11f, 0.12f));
            mb.For(coreM).Box(new Vector3(bx.Cx, (bx.Y0 + top) * 0.5f, bx.Cz), new Vector3(bx.Hx * 2 - 0.2f, top - bx.Y0, bx.Hz * 2 - 0.2f));
            bool alongX = bx.Hx > bx.Hz;
            float water = Terrain.WaterLevel(Planet);
            float blockH = outer ? 2.6f : 1.5f, blockL = outer ? 5f : 3f;
            float capH = outer ? 0.8f : 0.45f;
            // Blocksteine auf beiden Längsseiten (bei Außenmauern nur die Innenseite)
            for (int f = 0; f < 4; f++)
            {
                bool longFace = alongX ? f < 2 : f >= 2;
                if (!longFace) continue;
                var fc = FaceOf(bx, f);
                bool inner = !outer || Vector3.Dot(fc.N, new Vector3(-bx.Cx, 0, -bx.Cz)) > 0;
                if (!inner) continue;
                float ground = Mathf.Max(water, Terrain.HeightAt(Planet, Mathf.Clamp(fc.P(0, 0, 1f).x, -150, 150), Mathf.Clamp(fc.P(0, 0, 1f).z, -150, 150)));
                float yStart = Mathf.Min(water - 1.5f, ground - 0.5f);
                int row = 0;
                for (float y = yStart; y < top - capH - 0.1f; y += blockH, row++)
                {
                    float hh = Mathf.Min(blockH, top - capH - y);
                    float u = -fc.Len * 0.5f + (row % 2 == 0 ? 0 : blockL * 0.5f);
                    if (u > -fc.Len * 0.5f) { FBox(mb.For(row % 2 == 0 ? blockB : blockA), fc, (-fc.Len * 0.5f + u) * 0.5f, y + hh * 0.5f, -0.05f, u + fc.Len * 0.5f - 0.06f, hh - 0.06f, 0.3f); }
                    int k = 0;
                    while (u < fc.Len * 0.5f - 0.05f)
                    {
                        float l = Mathf.Min(blockL * rng.Range(0.8f, 1.15f), fc.Len * 0.5f - u);
                        FBox(mb.For((k + row) % 3 == 0 ? blockB : blockA), fc, u + l * 0.5f, y + hh * 0.5f, -0.05f + rng.Range(-0.03f, 0.03f), l - 0.06f, hh - 0.06f, 0.3f);
                        u += l; k++;
                    }
                }
                // Algen- und Nässeband an der Wasserlinie
                FBox(mb.For(algae), fc, 0, water + 0.2f, 0.14f, fc.Len, 1.0f, 0.06f);
                // Reifenfender mit Tau
                for (float u = -fc.Len * 0.5f + rng.Range(1.5f, 3f); u < fc.Len * 0.5f - 1f; u += outer ? 9f : rng.Range(3.5f, 5.5f))
                {
                    var p = fc.P(u, water + (outer ? 2.2f : 1.4f), 0.28f);
                    mb.For(tire).TorusRot(p, new Vector3(90, fc.Yaw, 0), 0.42f, 0.16f, 10, 5);
                    mb.For(woodMat).Beam(p + Vector3.up * 0.55f, fc.P(u, top - capH, 0.06f), 0.04f);
                }
                // Leitern und Rostspuren
                for (float u = -fc.Len * 0.5f + rng.Range(3f, 8f); u < fc.Len * 0.5f - 2f; u += rng.Range(12f, 20f))
                {
                    float y0 = water - 0.8f, y1 = top;
                    FBox(mb.For(rust), fc, u - 0.25f, (y0 + y1) * 0.5f, 0.12f, 0.06f, y1 - y0, 0.06f);
                    FBox(mb.For(rust), fc, u + 0.25f, (y0 + y1) * 0.5f, 0.12f, 0.06f, y1 - y0, 0.06f);
                    for (float y = y0 + 0.3f; y < y1; y += 0.35f) FBox(mb.For(rust), fc, u, y, 0.12f, 0.5f, 0.04f, 0.04f);
                }
                for (int k = rng.Range(2, 6); k > 0; k--)
                {
                    float u = rng.Range(-fc.Len * 0.45f, fc.Len * 0.45f), h = rng.Range(0.8f, 2.4f);
                    FBox(mb.For(rust), fc, u, top - capH - h * 0.5f, 0.12f, rng.Range(0.1f, 0.3f), h, 0.02f);
                }
                if (outer) // Entwässerungsauslässe
                    for (float u = -fc.Len * 0.5f + 20f; u < fc.Len * 0.5f; u += 40f)
                    {
                        var p = fc.P(u, water + 3.5f, 0.2f);
                        mb.For(darkMat).TorusRot(p, new Vector3(90, fc.Yaw, 0), 0.6f, 0.15f, 12, 4);
                        mb.For(darkMat).Box(p, new Vector3(0.9f, 0.9f, 0.9f));
                    }
                // Tetrapoden vor der Mauer
                for (float u = -fc.Len * 0.5f + rng.Range(2f, 5f); u < fc.Len * 0.5f; u += rng.Range(4f, 8f))
                {
                    var p = fc.P(u, water - 0.9f + rng.Range(-0.3f, 0.3f), rng.Range(1.3f, 2.4f));
                    if (Terrain.HeightAt(Planet, Mathf.Clamp(p.x, -150, 150), Mathf.Clamp(p.z, -150, 150)) > water + 0.2f) continue;
                    Tetrapod(mb.For(Mat(new Color(0.66f, 0.66f, 0.62f))), p, rng.Range(0.9f, 1.3f), rng.Range(0f, 360f));
                }
            }
            // Abdeckplatte
            mb.For(cap).Box(new Vector3(bx.Cx, top - capH * 0.5f, bx.Cz), new Vector3(bx.Hx * 2 + 0.4f, capH, bx.Hz * 2 + 0.4f));
            if (outer)
            {
                // Container-Stapel und Kranschienen auf der hohen Außenmauer (Silhouette)
                float len = alongX ? bx.Hx * 2 : bx.Hz * 2;
                for (float t = -len * 0.5f + rng.Range(5f, 15f); t < len * 0.5f - 8f; t += rng.Range(14f, 30f))
                {
                    int stack = rng.Range(1, 4);
                    for (int s = 0; s < stack; s++)
                    {
                        var p = alongX ? new Vector3(bx.Cx + t, top + 1.3f + s * 2.6f, bx.Cz) : new Vector3(bx.Cx, top + 1.3f + s * 2.6f, bx.Cz + t);
                        Container(mb, p, alongX ? 90f : 0f + rng.Range(-4f, 4f), rng);
                    }
                }
                return;
            }
            // Geländer, Poller, Laternen auf der Kaimauer
            for (int f = 0; f < 4; f++)
            {
                bool longFace = alongX ? f < 2 : f >= 2;
                if (!longFace) continue;
                var fc = FaceOf(bx, f);
                for (float u = -fc.Len * 0.5f + 0.5f; u <= fc.Len * 0.5f - 0.4f; u += 2f)
                    FBox(mb.For(rail), fc, u, top + 0.5f, -0.2f, 0.07f, 1.0f, 0.07f);
                FBox(mb.For(rail), fc, 0, top + 1.0f, -0.2f, fc.Len - 0.6f, 0.07f, 0.07f);
                FBox(mb.For(rail), fc, 0, top + 0.55f, -0.2f, fc.Len - 0.6f, 0.05f, 0.05f);
                for (float u = -fc.Len * 0.5f + rng.Range(2f, 4f); u < fc.Len * 0.5f - 1f; u += rng.Range(6f, 9f))
                {
                    var p = fc.P(u, top, -0.75f);
                    mb.For(darkMat).Cylinder(p, 0.22f, 0.5f, 10, true, 0.18f);
                    mb.For(darkMat).Cylinder(p + Vector3.up * 0.5f, 0.3f, 0.1f, 10);
                }
            }
            var mid = new Vector3(bx.Cx, top, bx.Cz);
            float half = alongX ? bx.Hx : bx.Hz;
            var axis = alongX ? Vector3.right : Vector3.forward;
            if (half > 7f && rng.Chance(0.6f))
            {
                var p = mid + axis * rng.Range(-half * 0.3f, half * 0.3f);
                mb.For(railMat).Cylinder(p, 0.09f, 4.5f, 6);
                mb.For(railMat).Box(p + Vector3.up * 4.5f + axis * 0.4f, new Vector3(alongX ? 0.9f : 0.12f, 0.1f, alongX ? 0.12f : 0.9f));
                mb.For(LampMat(area)).Box(p + Vector3.up * 4.35f + axis * 0.75f, new Vector3(0.45f, 0.18f, 0.45f));
                lampPositions[area].Add(p + Vector3.up * 4.1f + axis * 0.75f);
            }
            if (half > 6f && rng.Chance(0.35f)) // abgestellte Container
            {
                int stack = rng.Range(1, 3);
                var p = mid + axis * rng.Range(-half * 0.4f, half * 0.4f);
                for (int s = 0; s < stack; s++) Container(mb, p + Vector3.up * (1.3f + s * 2.6f), alongX ? 90f + rng.Range(-3f, 3f) : rng.Range(-3f, 3f), rng);
            }
            // Seezeichen am Ende zum Tor hin
            float endX = alongX ? bx.Cx + (bx.Cx > 0 ? -bx.Hx : bx.Hx) : bx.Cx;
            if (alongX && Mathf.Abs(endX) < 11f)
            {
                var p = new Vector3(endX + (bx.Cx > 0 ? 1.2f : -1.2f), top, bx.Cz);
                bool green = bx.Cx < 0;
                var beaconCol = green ? new Color(0.2f, 0.7f, 0.35f) : new Color(0.85f, 0.2f, 0.18f);
                mb.For(Mat(beaconCol)).Cylinder(p, 0.6f, 3.2f, 10, true, 0.45f);
                mb.For(trimMat).Cylinder(p + Vector3.up * 1.2f, 0.56f, 0.6f, 10);
                mb.For(Mats.Get(Mats.Emissive, beaconCol, beaconCol * 2.5f)).Cylinder(p + Vector3.up * 3.2f, 0.3f, 0.5f, 8);
                mb.For(darkMat).Cylinder(p + Vector3.up * 3.7f, 0.38f, 0.15f, 8);
            }
        }

        static void Tetrapod(MeshBuilder b, Vector3 c, float s, float yaw)
        {
            var dirs = new[] { new Vector3(0, 1, 0), new Vector3(0.943f, -0.333f, 0), new Vector3(-0.471f, -0.333f, 0.816f), new Vector3(-0.471f, -0.333f, -0.816f) };
            var q = Quaternion.Euler(20, yaw, 10);
            foreach (var d in dirs) b.Tube(c, c + q * d * 1.4f * s, 0.45f * s, 6, true, 0.25f * s);
        }

        void Container(MultiBuilder mb, Vector3 c, float yaw, Rng rng)
        {
            var colM = Mat(ContainerCols[rng.Range(0, ContainerCols.Length)]);
            var o = mb.M;
            mb.M = Matrix4x4.TRS(c, Quaternion.Euler(0, yaw, 0), Vector3.one);
            mb.For(colM).Box(Vector3.zero, new Vector3(2.4f, 2.55f, 6f));
            var ribM = Mat(ContainerCols[rng.Range(0, ContainerCols.Length)] * 0.8f);
            for (float z = -2.6f; z <= 2.61f; z += 0.65f)
            {
                mb.For(ribM).Box(new Vector3(1.22f, 0, z), new Vector3(0.05f, 2.35f, 0.12f));
                mb.For(ribM).Box(new Vector3(-1.22f, 0, z), new Vector3(0.05f, 2.35f, 0.12f));
            }
            mb.For(railMat).Box(new Vector3(-0.4f, 0, 3.02f), new Vector3(0.06f, 2.3f, 0.06f));
            mb.For(railMat).Box(new Vector3(0.4f, 0, 3.02f), new Vector3(0.06f, 2.3f, 0.06f));
            mb.For(darkMat).Box(new Vector3(0, 0, 3.005f), new Vector3(0.03f, 2.4f, 0.02f));
            mb.M = o;
        }

        /// <summary>Stelzenhaus: Pfähle mit Kreuzstreben, Plankendeck, Stülpschalung, Fenster mit Läden, Tür mit Leiter, Satteldach, Wäscheleine, Laterne.</summary>
        void StiltHouse(MultiBuilder mb, Box bx, Rng rng, int area, Color col)
        {
            float g = Mathf.Max(Terrain.HeightAt(Planet, bx.Cx, bx.Cz), Terrain.WaterLevel(Planet));
            float y0 = bx.Y0, h = bx.H;
            var pole = Mat(new Color(0.4f, 0.3f, 0.2f));
            var plank = Mat(new Color(0.55f, 0.42f, 0.28f));
            var wall = Mat(col);
            var siding = Mat(col * 0.82f);
            var roof = Mat(SignCols[rng.Range(0, SignCols.Length)] * 0.75f);
            float deck = g + 1.4f;
            float wallTop = g + h - 0.6f;
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(bx.Cx + (i % 2 == 0 ? -1 : 1) * (bx.Hx - 0.3f), 0, bx.Cz + (i < 2 ? -1 : 1) * (bx.Hz - 0.3f));
                mb.For(pole).Cylinder(new Vector3(p.x, y0 - 0.5f, p.z), 0.16f, deck - y0 + 0.5f, 6);
            }
            mb.For(pole).Beam(new Vector3(bx.Cx - bx.Hx + 0.3f, y0 + 0.5f, bx.Cz - bx.Hz + 0.3f), new Vector3(bx.Cx + bx.Hx - 0.3f, deck - 0.2f, bx.Cz - bx.Hz + 0.3f), 0.1f);
            mb.For(pole).Beam(new Vector3(bx.Cx + bx.Hx - 0.3f, y0 + 0.5f, bx.Cz + bx.Hz - 0.3f), new Vector3(bx.Cx - bx.Hx + 0.3f, deck - 0.2f, bx.Cz + bx.Hz - 0.3f), 0.1f);
            // Plankendeck
            int planks = Mathf.Max(3, (int)(bx.Hz * 2 / 0.45f));
            for (int i = 0; i < planks; i++)
                mb.For(i % 3 == 0 ? pole : plank).Box(new Vector3(bx.Cx, deck, bx.Cz - bx.Hz + (i + 0.5f) * bx.Hz * 2 / planks), new Vector3(bx.Hx * 2 + 0.3f, 0.1f, bx.Hz * 2 / planks - 0.05f));
            // Wände etwas eingerückt (umlaufender Steg)
            var inner = new Box { Cx = bx.Cx, Cz = bx.Cz, Hx = bx.Hx - 0.6f, Hz = bx.Hz - 0.6f, Y0 = deck, H = wallTop - deck };
            mb.For(wall).Box(new Vector3(bx.Cx, (deck + wallTop) * 0.5f, bx.Cz), new Vector3(inner.Hx * 2, wallTop - deck, inner.Hz * 2));
            int door = rng.Range(0, 4);
            for (int f = 0; f < 4; f++)
            {
                var fc = FaceOf(inner, f);
                for (float y = deck + 0.3f; y < wallTop - 0.1f; y += 0.4f) FBox(mb.For(siding), fc, 0, y, 0.02f, fc.Len + 0.04f, 0.05f, 0.05f);
                if (f == door)
                {
                    FBox(mb.For(darkMat), fc, fc.Len * 0.2f, deck + 1.05f, 0.04f, 0.9f, 2.0f, 0.06f);
                    // Leiter zum Wasser/Boden
                    var lp = FaceOf(bx, f);
                    FBox(mb.For(pole), lp, fc.Len * 0.2f - 0.3f, (g + deck) * 0.5f - 0.3f, 0.2f, 0.06f, deck - g + 0.6f, 0.06f, 0f, 12f);
                    FBox(mb.For(pole), lp, fc.Len * 0.2f + 0.3f, (g + deck) * 0.5f - 0.3f, 0.2f, 0.06f, deck - g + 0.6f, 0.06f, 0f, 12f);
                }
                else if (fc.Len > 2f)
                {
                    FBox(mb.For(rng.Chance(0.3f) ? glassDark : WindowMat(area)), fc, 0, deck + 1.4f, 0.04f, 0.9f, 0.8f, 0.06f);
                    var sh = Mat(SignCols[rng.Range(0, SignCols.Length)] * 0.7f);
                    FBox(mb.For(sh), fc, -0.7f, deck + 1.4f, 0.06f, 0.4f, 0.9f, 0.04f);
                    FBox(mb.For(sh), fc, 0.7f, deck + 1.4f, 0.06f, 0.4f, 0.9f, 0.04f, rng.Chance(0.4f) ? -15f : 0f);
                }
            }
            // Geländer am Steg
            for (int f = 0; f < 4; f++)
            {
                var fc = FaceOf(bx, f);
                if (f == door) continue;
                FBox(mb.For(pole), fc, 0, deck + 0.9f, -0.12f, fc.Len, 0.06f, 0.06f);
                for (float u = -fc.Len * 0.5f + 0.2f; u < fc.Len * 0.5f; u += 1.2f) FBox(mb.For(pole), fc, u, deck + 0.45f, -0.12f, 0.06f, 0.9f, 0.06f);
            }
            // Satteldach mit Überstand
            bool alongX = bx.Hx >= bx.Hz;
            var o = mb.M;
            mb.M = Matrix4x4.TRS(new Vector3(bx.Cx, wallTop, bx.Cz), Quaternion.Euler(0, alongX ? 0 : 90, 0), Vector3.one);
            float span = alongX ? inner.Hz * 2 : inner.Hx * 2, length = alongX ? inner.Hx * 2 : inner.Hz * 2;
            mb.For(roof).Prism(Vector3.zero, new Vector3(length + 1.2f, span * 0.45f + 0.4f, span + 1.4f));
            mb.For(wall).Prism(new Vector3(0, -0.01f, 0), new Vector3(length, span * 0.45f + 0.3f, span));
            mb.M = o;
            // Laterne und Wäscheleine
            var lamp = new Vector3(bx.Cx + bx.Hx - 0.3f, deck, bx.Cz + bx.Hz - 0.3f);
            mb.For(pole).Cylinder(lamp, 0.06f, 2.2f, 5);
            mb.For(LampMat(area)).Box(lamp + Vector3.up * 2.2f, new Vector3(0.3f, 0.35f, 0.3f));
            if (rng.Chance(0.5f))
            {
                var a = new Vector3(bx.Cx - bx.Hx + 0.3f, deck + 1.9f, bx.Cz + bx.Hz - 0.3f);
                mb.For(pole).Cylinder(new Vector3(a.x, deck, a.z), 0.05f, 2f, 5);
                mb.For(darkMat).Beam(a, lamp + Vector3.up * 1.9f, 0.02f);
                for (int k = 1; k < 5; k++)
                {
                    var p = Vector3.Lerp(a, lamp + Vector3.up * 1.9f, k / 5f) + Vector3.down * 0.3f;
                    mb.For(Mat(SignCols[rng.Range(0, SignCols.Length)] * 0.95f)).Box(p, new Vector3(0.35f, 0.5f, 0.02f));
                }
            }
        }

        void SunkenRuin(MultiBuilder mb, Box bx, Rng rng)
        {
            var c = new Vector3(bx.Cx, bx.Y0 + bx.H * 0.5f, bx.Cz);
            var s = new Vector3(bx.Hx * 2, bx.H, bx.Hz * 2);
            var e = new Vector3(rng.Range(-8f, 8f), rng.Range(0f, 90f), rng.Range(-8f, 8f));
            mb.For(Mat(new Color(0.45f, 0.55f, 0.5f))).BoxJ(c, s, e, 0.25f, (int)(bx.Cx * 3));
            mb.For(Mat(new Color(0.28f, 0.4f, 0.3f))).BoxRot(c + Vector3.up * (bx.H * 0.5f + 0.05f), new Vector3(s.x * 0.9f, 0.1f, s.z * 0.9f), e);
            for (int k = 0; k < 4; k++)
                mb.For(Mat(new Color(0.25f, 0.5f, 0.35f))).Tube(c + new Vector3(rng.Range(-s.x * 0.4f, s.x * 0.4f), bx.H * 0.5f, rng.Range(-s.z * 0.4f, s.z * 0.4f)), c + new Vector3(rng.Range(-s.x * 0.5f, s.x * 0.5f), bx.H * 0.5f + rng.Range(1f, 2.5f), rng.Range(-s.z * 0.5f, s.z * 0.5f)), 0.06f, 4, false, 0.02f);
        }

        // ================================================================== Eis- und Felswände
        /// <summary>Eiswand aus mehreren gekippten Eisblöcken mit Schneekappen, Eiszapfen und eingefrorenem Schrott.</summary>
        void IceWall(ChunkBuilder cb, Box bx, Rng rng)
        {
            var iceA = Mats.Get(Mats.Opaque, new Color(0.72f, 0.86f, 0.96f), null, 0.85f);
            var iceB = Mats.Get(Mats.Opaque, new Color(0.62f, 0.78f, 0.92f), null, 0.9f);
            var snowM = Mat(new Color(0.94f, 0.97f, 1f), 0.3f);
            var scrap = Mat(new Color(0.3f, 0.32f, 0.36f));
            bool alongX = bx.Hx > bx.Hz;
            float len = alongX ? bx.Hx * 2 : bx.Hz * 2, depth = alongX ? bx.Hz * 2 : bx.Hx * 2;
            float top = bx.Y0 + bx.H;
            float t = 0;
            while (t < len - 0.5f)
            {
                float w = Mathf.Min(len - t, rng.Range(4f, 9f) * (depth > 6f ? 2f : 1f));
                float mid = (alongX ? bx.Cx - bx.Hx : bx.Cz - bx.Hz) + t + w * 0.5f;
                float hh = top + rng.Range(-1.5f, 2.5f);
                var c = alongX ? new Vector3(mid, (bx.Y0 + hh) * 0.5f, bx.Cz) : new Vector3(bx.Cx, (bx.Y0 + hh) * 0.5f, mid);
                var mb = cb.At(c.x, c.z);
                var e = new Vector3(rng.Range(-4f, 4f), rng.Range(-6f, 6f), rng.Range(-4f, 4f));
                var size = alongX ? new Vector3(w + 0.8f, hh - bx.Y0, depth + rng.Range(0f, 1.2f)) : new Vector3(depth + rng.Range(0f, 1.2f), hh - bx.Y0, w + 0.8f);
                mb.For(rng.Chance(0.5f) ? iceA : iceB).BoxJ(c, size, e, Mathf.Min(0.6f, w * 0.08f), rng.Range(0, 9999));
                mb.For(snowM).BoxJ(new Vector3(c.x, hh + 0.15f, c.z), new Vector3(size.x * 0.95f, 0.5f, size.z * 0.95f), e, 0.15f, rng.Range(0, 9999));
                for (int k = 0; k < 3; k++)
                {
                    var side = (rng.Chance(0.5f) ? 1 : -1) * (depth * 0.5f + 0.2f);
                    var p = alongX ? new Vector3(mid + rng.Range(-w * 0.4f, w * 0.4f), hh - 0.2f, bx.Cz + side) : new Vector3(bx.Cx + side, hh - 0.2f, mid + rng.Range(-w * 0.4f, w * 0.4f));
                    mb.For(iceA).Tube(p, p + Vector3.down * rng.Range(0.6f, 2.2f), rng.Range(0.12f, 0.3f), 5, true, 0f);
                }
                if (rng.Chance(0.4f))
                {
                    var side = (rng.Chance(0.5f) ? 1 : -1) * depth * 0.45f;
                    var p = alongX ? new Vector3(mid, rng.Range(bx.Y0 + 16f, hh - 2f), bx.Cz + side) : new Vector3(bx.Cx + side, rng.Range(bx.Y0 + 16f, hh - 2f), mid);
                    mb.For(scrap).BoxJ(p, new Vector3(rng.Range(1f, 2.5f), rng.Range(0.8f, 1.8f), rng.Range(1f, 2f)), new Vector3(rng.Range(-30f, 30f), rng.Range(0f, 90f), rng.Range(-30f, 30f)), 0.15f, rng.Range(0, 999));
                }
                t += w;
            }
        }

        /// <summary>Felswand mit Gesteinsschichten, eingeklemmtem Schrott und Geröll (PYRA).</summary>
        void RockWall(ChunkBuilder cb, Box bx, Rng rng)
        {
            var rocks = new[] { Mat(new Color(0.56f, 0.25f, 0.17f)), Mat(new Color(0.64f, 0.3f, 0.2f)), Mat(new Color(0.5f, 0.22f, 0.15f)) };
            var strata = Mat(new Color(0.74f, 0.42f, 0.28f));
            var scrap = Mat(new Color(0.4f, 0.36f, 0.33f));
            bool alongX = bx.Hx > bx.Hz;
            float len = alongX ? bx.Hx * 2 : bx.Hz * 2, depth = alongX ? bx.Hz * 2 : bx.Hx * 2;
            float top = bx.Y0 + bx.H;
            int k = Mathf.Max(2, (int)(len / 7f));
            for (int i = 0; i < k; i++)
            {
                float f = (i + 0.5f) / k;
                var p = alongX ? new Vector3(bx.Cx - bx.Hx + f * len, 0, bx.Cz) : new Vector3(bx.Cx, 0, bx.Cz - bx.Hz + f * len);
                var mb = cb.At(p.x, p.z);
                float hh = (top - bx.Y0) * (0.72f + rng.Next() * 0.4f);
                var size = alongX ? new Vector3(len / k * 1.35f, hh, depth * 1.3f) : new Vector3(depth * 1.3f, hh, len / k * 1.35f);
                var e = new Vector3(rng.Range(-6f, 6f), rng.Range(-20f, 20f), rng.Range(-6f, 6f));
                mb.For(rocks[i % 3]).BoxJ(new Vector3(p.x, bx.Y0 + hh * 0.5f, p.z), size, e, Mathf.Min(1.2f, size.y * 0.06f), rng.Range(0, 9999));
                for (float y = bx.Y0 + hh * 0.35f; y < bx.Y0 + hh - 1f; y += rng.Range(2.5f, 4.5f))
                    mb.For(strata).BoxRot(new Vector3(p.x, y, p.z), new Vector3(size.x * 1.02f, rng.Range(0.3f, 0.7f), size.z * 1.02f), e);
                if (rng.Chance(0.5f))
                {
                    var q = new Vector3(p.x + rng.Range(-2f, 2f), bx.Y0 + hh * rng.Range(0.5f, 0.9f), p.z + rng.Range(-2f, 2f));
                    mb.For(scrap).Beam(q, q + new Vector3(rng.Range(-4f, 4f), rng.Range(1f, 4f), rng.Range(-4f, 4f)), 0.25f);
                }
            }
        }

        // ================================================================== Forschungsstation (NIVALIS)
        void Dome(MultiBuilder mb, Box bx, Rng rng, int area)
        {
            float r = Mathf.Min(bx.Hx, bx.Hz);
            float gy = Terrain.HeightAt(Planet, bx.Cx, bx.Cz);
            var c = new Vector3(bx.Cx, bx.Y0 + 0.5f, bx.Cz);
            var shell = Mats.Get(Mats.Opaque, new Color(0.8f, 0.87f, 0.93f), null, 0.9f);
            var snowM = Mat(new Color(0.95f, 0.97f, 1f), 0.3f);
            mb.For(shell).Sphere(c, r, 20, 12, 0.75f);
            mb.For(darkMat).Cylinder(new Vector3(c.x, bx.Y0, c.z), r * 1.02f, 1.2f, 20);
            // Rippen
            for (int k = 1; k < 3; k++) mb.For(railMat).Torus(c + Vector3.up * r * 0.75f * Mathf.Sin(k * 0.5f), r * Mathf.Cos(k * 0.5f) + 0.03f, 0.1f, 24, 4);
            for (int k = 0; k < 4; k++)
            {
                var o = mb.M;
                mb.M = Matrix4x4.TRS(c, Quaternion.Euler(0, k * 45, 0), new Vector3(1f, 0.75f, 1f));
                mb.For(railMat).TorusRot(Vector3.zero, new Vector3(90, 0, 0), r + 0.03f, 0.09f, 24, 4);
                mb.M = o;
            }
            mb.For(snowM).Crumple(c + Vector3.up * r * 0.72f, r * 0.55f, 0.25f, (int)bx.Cx, 0.2f, 10, 4);
            // Schleuse mit Tür und Licht
            var fwd = Vector3.forward;
            var lp = c + fwd * r * 0.95f;
            mb.For(shell).Box(new Vector3(lp.x, gy + 1.4f, lp.z + 0.2f), new Vector3(2.6f, 2.8f, 1.2f));
            mb.For(darkMat).Box(new Vector3(lp.x, gy + 1.2f, lp.z + 0.82f), new Vector3(1.3f, 2.2f, 0.06f));
            mb.For(Mats.Get(Mats.Emissive, new Color(1f, 0.6f, 0.2f), new Color(2f, 1f, 0.3f))).Box(new Vector3(lp.x, gy + 2.55f, lp.z + 0.83f), new Vector3(1.5f, 0.1f, 0.06f));
            // Fensterband und Antenne
            for (int k = 0; k < 8; k++)
            {
                float a = k / 8f * Mathf.PI * 2f + 0.2f;
                if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 90f)) < 25f) continue;
                var p = c + new Vector3(Mathf.Cos(a) * r * 0.93f, r * 0.3f, Mathf.Sin(a) * r * 0.93f);
                mb.For(WindowMat(area)).BoxRot(p, new Vector3(1.1f, 0.7f, 0.2f), new Vector3(0, 90 - a * Mathf.Rad2Deg, 0));
            }
            var top = c + Vector3.up * r * 0.75f;
            mb.For(railMat).Cylinder(top, 0.08f, 4f, 5);
            var od = mb.M;
            mb.M = Matrix4x4.TRS(top + Vector3.up * 3f, Quaternion.Euler(-50, rng.Range(0f, 360f), 0), Vector3.one);
            mb.For(acMat).Lathe(Vector3.zero, new[] { new Vector2(0, 0), new Vector2(0.7f, 0.2f), new Vector2(1f, 0.45f) }, 12);
            mb.M = od;
        }

        void Hangar(MultiBuilder mb, Box bx, Rng rng, int area, Color col)
        {
            float gy = Terrain.HeightAt(Planet, bx.Cx, bx.Cz);
            float y0 = bx.Y0, h = bx.H;
            var wall = Mat(col);
            var roof = Mat(new Color(0.55f, 0.6f, 0.66f));
            var snowM = Mat(new Color(0.95f, 0.97f, 1f), 0.3f);
            var c = new Vector3(bx.Cx, 0, bx.Cz);
            float s = h * 0.6f;
            mb.For(wall).Box(new Vector3(c.x, y0 + h * 0.3f, c.z), new Vector3(bx.Hx * 2, h * 0.6f, bx.Hz * 2));
            float rr = Mathf.Min(bx.Hz, h * 0.5f);
            mb.For(roof).CylinderX(new Vector3(c.x, y0 + h * 0.6f, c.z), rr, bx.Hx * 2, 18);
            for (float x = -bx.Hx + 1f; x < bx.Hx; x += 2.5f)
                mb.For(railMat).TorusRot(new Vector3(c.x + x, y0 + h * 0.6f, c.z), new Vector3(0, 0, 90), rr + 0.04f, 0.1f, 18, 4);
            mb.For(snowM).BoxJ(new Vector3(c.x, y0 + h * 0.6f + rr * 0.92f, c.z), new Vector3(bx.Hx * 2 * 0.95f, 0.35f, rr * 1.1f), Vector3.zero, 0.1f, (int)c.x);
            // großes Tor auf einer Stirnseite
            var fc = FaceOf(bx, 2);
            float dh = Mathf.Min(s + rr * 0.7f, h * 0.9f);
            FBox(mb.For(acMat), fc, 0, gy + dh * 0.5f, 0.05f, bx.Hz * 1.6f, dh, 0.1f);
            for (float u = -bx.Hz * 0.8f + 0.8f; u < bx.Hz * 0.8f; u += 1.6f) FBox(mb.For(railMat), fc, u, gy + dh * 0.5f, 0.12f, 0.08f, dh, 0.05f);
            FBox(mb.For(Mat(new Color(0.95f, 0.75f, 0.15f))), fc, 0, gy + 0.25f, 0.12f, bx.Hz * 1.7f, 0.3f, 0.06f);
            for (int f = 0; f < 2; f++)
            {
                var side = FaceOf(bx, f);
                for (float u = -side.Len * 0.4f; u < side.Len * 0.45f; u += 3f) FBox(mb.For(WindowMat(area)), side, u, y0 + h * 0.45f, 0.03f, 1.6f, 0.8f, 0.06f);
            }
        }

        void LaunchTower(MultiBuilder mb, Box bx)
        {
            float y0 = bx.Y0, h = bx.H;
            var c = new Vector3(bx.Cx, 0, bx.Cz);
            var steelM = Mats.Get(Mats.Metal, new Color(0.7f, 0.72f, 0.75f));
            var red = Mat(new Color(0.85f, 0.3f, 0.22f));
            for (int i = 0; i < 4; i++) mb.For(steelM).Box(new Vector3(c.x + (i % 2 == 0 ? -3 : 3), y0 + h * 0.5f, c.z + (i < 2 ? -3 : 3)), new Vector3(0.8f, h, 0.8f));
            int levels = 10;
            for (int k = 1; k <= levels; k++)
            {
                float y = y0 + k * h / (levels + 1);
                mb.For(k % 3 == 0 ? red : steelM).Box(new Vector3(c.x, y, c.z), new Vector3(6.8f, 0.4f, 6.8f));
                for (int s = 0; s < 4; s++)
                {
                    var a = new Vector3(c.x + (s % 2 == 0 ? -3 : 3), y, c.z + (s < 2 ? -3 : 3));
                    var b2 = new Vector3(c.x + (s % 2 == 0 ? 3 : -3), y + h / (levels + 1), c.z + (s < 2 ? -3 : 3));
                    if (s < 2) mb.For(steelM).Beam(a, b2, 0.15f);
                }
            }
            // Rakete mit Stufen, Flossen und Spitze
            var rp = new Vector3(c.x, y0, c.z + 7);
            mb.For(Mat(new Color(0.92f, 0.94f, 0.96f))).Cylinder(rp, 2.2f, h * 0.65f, 16);
            mb.For(darkMat).Cylinder(rp + Vector3.up * h * 0.3f, 2.23f, 1.2f, 16);
            mb.For(Mat(new Color(0.92f, 0.94f, 0.96f))).Cylinder(rp + Vector3.up * h * 0.65f, 2.2f, h * 0.2f, 16, true, 0.2f);
            mb.For(red).Cylinder(rp + Vector3.up * h * 0.55f, 2.23f, 1.2f, 16);
            for (int i = 0; i < 4; i++) mb.For(red).BoxRot(rp + new Vector3(Mathf.Cos(i * 1.571f) * 2.5f, 2.5f, Mathf.Sin(i * 1.571f) * 2.5f), new Vector3(0.2f, 5f, 2.2f), new Vector3(0, -i * 90, 0));
            for (int k = 0; k < 3; k++) mb.For(steelM).Beam(new Vector3(c.x, y0 + h * (0.3f + k * 0.2f), c.z + 3f), rp + new Vector3(0, h * (0.3f + k * 0.2f), -2.2f), 0.5f);
        }
    }
}
