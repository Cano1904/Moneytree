using System;
using System.Collections.Generic;

namespace RePlanet.Core
{
    /// <summary>
    /// Erzeugt die Planeten deterministisch aus dem Seed. Server und alle Clients erzeugen exakt dieselbe Welt;
    /// im Spielstand werden nur Abweichungen (entfernte Objekte, neue Objekte, Bauwerke …) gespeichert.
    /// </summary>
    public static class WorldGen
    {
        static readonly Dictionary<string, PlanetLayout> cache = new Dictionary<string, PlanetLayout>();
        static readonly object lk = new object();

        public static PlanetLayout Get(string planet)
        {
            lock (lk)
            {
                PlanetLayout l;
                if (!cache.TryGetValue(planet, out l))
                {
                    l = Generate(GameData.Planets[planet]);
                    cache[planet] = l;
                }
                return l;
            }
        }

        public static readonly string[][] ZoneNames =
        {
            new[] { "Spielplatz", "Haltestelle Linie 7", "Vorgarten", "Parkdeck", "Brunnenplatz", "Alte Werkstatt", "Rosengarten", "Teichufer", "Gewächshaus-Vorplatz" },
            new[] { "Marktstand", "Schrottwaage", "Schrottgasse", "Kantine", "Montagehalle", "Lokschuppen", "Schlackenfeld", "Kühlturm", "Werkstor" },
            new[] { "Fischmarkt", "Inneres Hafenbecken", "Bootsschuppen", "Stelzendorf", "Inselschule", "Muschelbucht", "Riffkante", "Leuchtturm", "Wrackbucht" },
            new[] { "Laborhof", "Kuppel B", "Messstation", "Kühlhalle", "Serverhof", "Notstromraum", "Startrampe 3", "Hangar", "Leitstand" },
        };

        static readonly float[,] ZonePos =
        {
            { -66, -120, 8 }, { 22, -60, 7 }, { 70, -112, 7 },
            { -75, -40, 7 }, { 26, 0, 8 }, { -75, 2, 8 },
            { -75, 100, 8 }, { 75, 60, 7 }, { 26, 98, 7 },
        };

        class Ctx
        {
            public PlanetLayout L;
            public Rng R;
            public string P;
            public List<float[]> KeepOut = new List<float[]>(); // x,z,r
            public bool KeptOut(float x, float z, float r)
            {
                foreach (var k in KeepOut)
                {
                    float dx = x - k[0], dz = z - k[1], rr = r + k[2];
                    if (dx * dx + dz * dz < rr * rr) return true;
                }
                return false;
            }
            public bool BoxKeptOut(float cx, float cz, float hx, float hz)
            {
                foreach (var k in KeepOut)
                {
                    float dx = Math.Max(0, Math.Abs(k[0] - cx) - hx), dz = Math.Max(0, Math.Abs(k[1] - cz) - hz);
                    if (dx * dx + dz * dz < k[2] * k[2]) return true;
                }
                return false;
            }
        }

        static bool InBaseRect(float x, float z, float margin)
        {
            return Math.Abs(x) < 36f + margin && z < -92f + margin;
        }

        public static PlanetLayout Generate(PlanetDef def)
        {
            var c = new Ctx { L = new PlanetLayout { Def = def }, R = new Rng(def.Seed), P = def.Id };
            BuildBase(c);
            BuildZonesAndSites(c);
            BuildSpots(c);
            BuildBoundaries(c);
            switch (def.Id)
            {
                case "terra": SceneryTerra(c); break;
                case "pyra": SceneryPyra(c); break;
                case "pelagia": SceneryPelagia(c); break;
                case "nivalis": SceneryNivalis(c); break;
            }
            c.L.BuildGrid();
            PlaceGates(c);
            PlaceTrash(c);
            PlaceMounds(c);
            PlaceSkyline(c);
            foreach (var t in c.L.Trash)
            {
                if (t.Gate >= 0) continue;
                c.L.AreaWeight[t.Area] += Weight(t.Type);
                c.L.AreaCount[t.Area]++;
            }
            return c.L;
        }

        public static float Weight(string type) { return Math.Max(1, GameData.Trash[type].TotalUnits); }

        static Box AddBox(Ctx c, float cx, float cz, float hx, float hz, float h, string kind, int style, uint color, bool sinkToGround = true)
        {
            float y0 = 0;
            if (sinkToGround)
            {
                float h1 = Terrain.HeightAt(c.P, cx - hx, cz - hz), h2 = Terrain.HeightAt(c.P, cx + hx, cz + hz);
                float h3 = Terrain.HeightAt(c.P, cx, cz);
                y0 = Math.Min(h3, Math.Min(h1, h2)) - 0.5f;
            }
            var b = new Box { Cx = cx, Cz = cz, Hx = hx, Hz = hz, Y0 = y0, H = h + 0.5f, Kind = kind, Style = style, Color = color, Area = PlanetLayout.AreaOf(cz) };
            c.L.Colliders.Add(b);
            return b;
        }

        static void AddProp(Ctx c, string kind, float x, float z, float rot, float scale, int style = 0, string litBy = null, float yOverride = float.NaN)
        {
            float y = float.IsNaN(yOverride) ? Terrain.HeightAt(c.P, x, z) : yOverride;
            c.L.Props.Add(new Prop { Kind = kind, Pos = new V3(x, y, z), Rot = rot, Scale = scale, Style = style, Area = PlanetLayout.AreaOf(z), LitBy = litBy });
        }

        // ------------------------------------------------------------------ Stützpunkt
        static void BuildBase(Ctx c)
        {
            var b = c.L.Base;
            float gy = Terrain.HeightAt(c.P, 0, -125);
            b.Center = new V3(0, gy, -125);
            b.MinX = -36; b.MaxX = 36; b.MinZ = -150; b.MaxZ = -92;
            b.Spawn = new V3(0, gy, -121);
            b.DropZone = new V3(0, gy, -124);
            b.DropRadius = 7f;
            b.GarageSpot = new V3(-20, gy, -134);
            b.ShipPad = new V3(26, gy, -143);
            b.BoatSpot = c.P == "pelagia" ? new V3(62, 0, -100) : new V3(-20, gy, -134);
            b.GridX0 = -34; b.GridZ0 = -114; b.GridW = 34; b.GridH = 10; b.Cell = 2f;
            b.Stations["storage"] = new V3(0, gy, -139.5f);
            b.Stations["charge"] = new V3(-7, gy, -138.5f);
            b.Stations["sell"] = new V3(-13, gy, -133);
            b.Stations["workshop"] = new V3(13, gy, -133);
            b.Stations["sort"] = new V3(-21, gy, -127);
            b.Stations["trader"] = new V3(21, gy, -127);
            b.Stations["disposal"] = new V3(-30, gy, -128);
            b.Stations["contracts"] = new V3(30, gy, -128);
            b.Stations["ship"] = b.ShipPad;
            b.Stations["garage"] = new V3(-26, gy, -139);
            b.Stations["build"] = new V3(0, gy, -114);

            BuildHangar(c, gy);
            BuildShipHold(c, gy);
            AddBox(c, -26, -146, 5, 3.5f, 5, "garage", 0, 0xBFB6A0);
            foreach (var kv in b.Stations)
            {
                if (kv.Key == "storage" || kv.Key == "ship" || kv.Key == "garage" || kv.Key == "build" || kv.Key == "charge") continue;
                AddBox(c, kv.Value.x, kv.Value.z - 1.2f, 0.7f, 0.5f, 2.0f, "terminal", 0, 0xFF8C2E);
            }
            c.KeepOut.Add(new float[] { 0, -125, 0 }); // Platzhalter (Basisrechteck wird separat geprüft)
        }

        /// <summary>Box aus Kanten (x0..x1, z0..z1) mit fester Unterkante (nicht ans Gelände angepasst).</summary>
        static Box AddWall(Ctx c, float x0, float x1, float z0, float z1, float y0, float h, string kind, int style, uint color)
        {
            var b = new Box { Cx = (x0 + x1) * 0.5f, Cz = (z0 + z1) * 0.5f, Hx = Math.Abs(x1 - x0) * 0.5f, Hz = Math.Abs(z1 - z0) * 0.5f, Y0 = y0, H = h, Kind = kind, Style = style, Color = color };
            b.Area = PlanetLayout.AreaOf(b.Cz);
            c.L.Colliders.Add(b);
            return b;
        }

        static Box Rect(float x0, float x1, float z0, float z1, float y0, float h)
        {
            return new Box { Cx = (x0 + x1) * 0.5f, Cz = (z0 + z1) * 0.5f, Hx = Math.Abs(x1 - x0) * 0.5f, Hz = Math.Abs(z1 - z0) * 0.5f, Y0 = y0, H = h, Kind = "room", Solid = false };
        }

        /// <summary>
        /// Hauptgebäude als befahrbarer Hangar: Halle x −8…3,6 / z −149,5…−141,5 (Silos rechts daneben bis x 8), Wände 0,4 m,
        /// Rolltor-Öffnung x ±2,6 in der Vorderwand (Sturz ab 4,3 m, blockiert nur die Kamera). Innen feste Werkbank (Rückwand),
        /// Regalwand (links) und Ladesäule (rechts hinten). Alle Wandteile heißen „core“ (werden separat gezeichnet).
        /// </summary>
        static void BuildHangar(Ctx c, float gy)
        {
            const uint col = 0xE8E2D0;
            float y0 = gy - 0.5f, h = 7.5f;
            AddWall(c, -8f, 3.6f, -149.5f, -149.1f, y0, h, "core", 0, col);      // Rückwand
            AddWall(c, -8f, -7.6f, -149.1f, -141.5f, y0, h, "core", 0, col);     // linke Wand
            AddWall(c, 2.6f, 8f, -149.5f, -141.5f, y0, h + 3.5f, "core", 0, col); // rechte Wand + Lager-Silos
            AddWall(c, -7.6f, -2.6f, -141.9f, -141.5f, y0, h, "core", 0, col);   // Vorderwand links vom Tor
            AddWall(c, -2.6f, 2.6f, -141.9f, -141.5f, gy + 4.3f, 3.2f, "core", 0, col); // Torsturz
            // Einrichtung (fest): Werkbank, Regalwand, Ladesäule
            AddWall(c, -6.0f, 1.0f, -149.1f, -148.3f, y0, 1.6f, "core", 0, col);
            AddWall(c, -7.6f, -6.9f, -148.3f, -143.0f, y0, 3.1f, "core", 0, col);
            AddWall(c, 2.05f, 2.6f, -147.6f, -146.4f, y0, 2.6f, "core", 0, col);
            var r = new ShelterRoom
            {
                Kind = Rules.ShelterHangar, Id = "hangar", Name = "Hangar",
                Inner = Rect(-7.6f, 2.6f, -149.1f, -141.9f, gy, 6.6f),
                Door = Rect(-2.6f, 2.6f, -141.9f, -141.5f, gy, 4.3f),
                Spot = new V3(-2.5f, gy, -145.2f),
                Outside = new V3(0, gy, -138.8f),
                OutX = 0, OutZ = 1,
            };
            c.L.Base.Hangar = r;
            c.L.Base.Rooms.Add(r);
        }

        /// <summary>
        /// Transportschiff auf dem Landeplatz (Nase nach +X): Laderaum x 20,4…30 / z ±2,7 um die Padmitte, Boden 0,9 m über dem
        /// Stützpunkt, Heckrampe nach −X (x 16,4…20,4, Breite 5 m). Seitenwände, Bug mit Cockpit, Container und Landebeine
        /// sind fest; Flügel und Triebwerke hängen über Kopfhöhe (keine Kollision). Wandteile heißen „core“ (Stil 1).
        /// </summary>
        static void BuildShipHold(Ctx c, float gy)
        {
            const uint col = 0xD8DBE2;
            var b = c.L.Base;
            float px = b.ShipPad.x, pz = b.ShipPad.z;
            float floorY = gy + 0.9f;
            b.ShipYaw = 90f;
            b.ShipFloorY = floorY;
            float y0 = gy - 0.5f;
            float rear = px - 5.6f, front = px + 4.0f, hw = 2.7f, wall = 0.4f;
            AddWall(c, rear, front + wall, pz - hw - wall, pz - hw, y0, 5.6f, "core", 1, col);
            AddWall(c, rear, front + wall, pz + hw, pz + hw + wall, y0, 5.6f, "core", 1, col);
            AddWall(c, front, px + 8.6f, pz - 3.1f, pz + 3.1f, y0, 5.6f, "core", 1, col); // Bugschott, Cockpit, Nase
            // Rahmen über der Heckluke (nur Kamera)
            AddWall(c, rear - 0.3f, rear + 0.1f, pz - hw, pz + hw, floorY + 3.1f, 1.6f, "core", 1, col);
            // Container vorn im Laderaum
            AddWall(c, front - 1.4f, front, pz - hw, pz - 1.5f, floorY, 1.6f, "core", 1, col);
            AddWall(c, front - 1.4f, front, pz + 1.5f, pz + hw, floorY, 1.6f, "core", 1, col);
            // Landebeine
            foreach (float lx in new[] { px + 3.0f, px - 4.4f })
                foreach (float sz in new[] { -1f, 1f })
                    AddWall(c, lx - 0.45f, lx + 0.45f, pz + sz * 3.8f - 0.45f, pz + sz * 3.8f + 0.45f, y0, 3.0f, "core", 1, col);
            // Boden: Laderaum eben, Rampe steigt vom Stützpunktboden zum Laderaum
            c.L.Floors.Add(new FloorPatch { X0 = rear, X1 = front, Z0 = pz - hw, Z1 = pz + hw, Y0 = floorY, Y1 = floorY, Axis = 0 });
            c.L.Floors.Add(new FloorPatch { X0 = rear - 4.0f, X1 = rear, Z0 = pz - 2.5f, Z1 = pz + 2.5f, Y0 = gy + 0.05f, Y1 = floorY, Axis = 0 });
            var r = new ShelterRoom
            {
                Kind = Rules.ShelterShip, Id = "ship", Name = "Transportschiff",
                Inner = Rect(rear, front, pz - hw, pz + hw, floorY, 3.1f),
                Door = Rect(rear - 4.0f, rear, pz - 2.5f, pz + 2.5f, gy, 4f),
                Spot = new V3(px - 1.0f, floorY, pz),
                Outside = new V3(rear - 5.2f, gy, pz),
                OutX = -1, OutZ = 0,
            };
            b.Ship = r;
            b.Rooms.Add(r);
        }

        // ------------------------------------------------------------------ Zonen & Projektplätze
        static void BuildZonesAndSites(Ctx c)
        {
            int pi = GameData.Planets[c.P].Order;
            for (int i = 0; i < 9; i++)
            {
                float x = ZonePos[i, 0], z = ZonePos[i, 1], r = ZonePos[i, 2];
                if (c.P == "pelagia" && i == 1) { x = 76; z = -96; r = 9; }
                if (c.P == "pelagia" && i == 7) { x = 80; z = 70; r = 8; }
                var zd = new ZoneDef { Index = i, Area = PlanetLayout.AreaOf(z), Name = ZoneNames[pi][i], Center = new V3(x, Terrain.HeightAt(c.P, x, z), z), Radius = r };
                c.L.Zones.Add(zd);
                c.KeepOut.Add(new[] { x, z, r + 2f });
                AddProp(c, "zonelamp", x, z, 0, 1, i);
            }
            for (int a = 0; a < 3; a++)
            {
                var p = Terrain.ProjectSite(c.P, a);
                p.y = Terrain.HeightAt(c.P, p.x, p.z);
                c.L.ProjectSites[a] = p;
                c.KeepOut.Add(new[] { p.x, p.z, 16f });
            }
            // Tore freihalten
            c.KeepOut.Add(new[] { 0f, -50f, 14f });
            c.KeepOut.Add(new[] { 0f, 50f, 14f });
        }

        static bool SpotFree(Ctx c, float x, float z, float r, bool wantWater)
        {
            if (Math.Abs(x) > 140 || z < -146 || z > 144) return false;
            if (InBaseRect(x, z, 4)) return false;
            if (Math.Abs(z + 50) < 6 || Math.Abs(z - 50) < 6) return false;
            if (c.KeptOut(x, z, r)) return false;
            if (c.P == "pelagia")
            {
                float h = Terrain.HeightAt(c.P, x, z);
                if (wantWater && h > -1.8f) return false;
                if (!wantWater && h < 0.4f) return false;
            }
            return true;
        }

        static Spot MakeSpot(Ctx c, string kind, string id, string name, int area, bool water)
        {
            float z0 = area == 0 ? -88 : area == 1 ? -44 : 56, z1 = area == 0 ? -56 : area == 1 ? 44 : 140;
            if (area == 0) { z0 = -146; z1 = -56; }
            for (int attempt = 0; attempt < 400; attempt++)
            {
                float x = c.R.Range(-135f, 135f), z = c.R.Range(z0, z1);
                if (!SpotFree(c, x, z, 5f, water)) continue;
                float y = Terrain.HeightAt(c.P, x, z);
                if (c.P == "pelagia" && water) y = 0;
                var s = new Spot { Id = id, Kind = kind, Name = name, Area = area, Pos = new V3(x, y, z), Yaw = c.R.Range(-3.1f, 3.1f) };
                c.KeepOut.Add(new[] { x, z, 5f });
                return s;
            }
            // Notfall: direkt an der Hauptstraße
            float fz = area == 0 ? -70 : area == 1 ? 20 : 90;
            var fs = new Spot { Id = id, Kind = kind, Name = name, Area = area, Pos = new V3(6, Terrain.HeightAt(c.P, 6, fz), fz) };
            return fs;
        }

        static void BuildSpots(Ctx c)
        {
            var def = GameData.Planets[c.P];
            int[] repairsPerArea = { 3, 4, 3 };
            int n = 0;
            for (int a = 0; a < 3; a++)
                for (int i = 0; i < repairsPerArea[a]; i++)
                {
                    var s = MakeSpot(c, "repair", c.P + "_r" + n, def.RepairName, a, c.P == "pelagia");
                    c.L.Repairs.Add(s); n++;
                }
            n = 0;
            for (int a = 0; a < 3; a++)
                for (int i = 0; i < 4; i++)
                {
                    var s = MakeSpot(c, "eco", c.P + "_e" + n, def.EcoName, a, c.P == "pelagia");
                    c.L.Eco.Add(s); n++;
                }
            int[] loreAreas = { 0, 1, 2, 2 };
            var loreIds = new List<string>();
            foreach (var id in GameData.LoreOrder) if (GameData.Lore[id].Planet == c.P) loreIds.Add(id);
            for (int i = 0; i < loreIds.Count; i++)
            {
                var s = MakeSpot(c, "lore", loreIds[i], GameData.Lore[loreIds[i]].Title, loreAreas[Math.Min(i, 3)], false);
                c.L.LoreSpots.Add(s);
            }
            n = 0;
            for (int a = 0; a < 3; a++)
                for (int i = 0; i < 3; i++)
                {
                    var s = MakeSpot(c, "shelter", c.P + "_h" + n, def.ShelterName, a, false);
                    c.L.Shelters.Add(s); n++;
                }
            float gy = Terrain.HeightAt(c.P, 18, -92);
            c.L.Viewpoints.Add(new Spot { Id = c.P + "_vp0", Kind = "view", Name = "Aussichtspunkt Stützpunkt", Area = 0, Pos = new V3(18, gy + 6f, -92), Yaw = 0.25f });
            float vy = Terrain.HeightAt(c.P, -30, 64);
            c.L.Viewpoints.Add(new Spot { Id = c.P + "_vp1", Kind = "view", Name = "Aussichtspunkt " + def.AreaNames[2], Area = 2, Pos = new V3(-30, Math.Max(vy, 0) + 8f, 64), Yaw = 0.5f });
        }

        // ------------------------------------------------------------------ Grenzen & Tore
        static void BuildBoundaries(Ctx c)
        {
            string wallKind = c.P == "terra" ? "cityedge" : c.P == "pyra" ? "cliff" : c.P == "pelagia" ? "seawall" : "icewall";
            uint col = c.P == "terra" ? 0x9C8E7Au : c.P == "pyra" ? 0x8E3F2Au : c.P == "pelagia" ? 0x8FA3A8u : 0xBFE3F5u;
            // Außenränder
            var e1 = AddBox(c, -154, 0, 4, 160, 14, wallKind, 1, col, false); e1.Y0 = -20; e1.H = 40;
            var e2 = AddBox(c, 154, 0, 4, 160, 14, wallKind, 1, col, false); e2.Y0 = -20; e2.H = 40;
            var e3 = AddBox(c, 0, -154, 160, 4, 14, wallKind, 1, col, false); e3.Y0 = -20; e3.H = 40;
            var e4 = AddBox(c, 0, 154, 160, 4, 14, wallKind, 1, col, false); e4.Y0 = -20; e4.H = 40;
            // Bereichsgrenzen mit Torlücke (x ∈ [-10, 10])
            float[] zs = { -50f, 50f };
            for (int g = 0; g < 2; g++)
            {
                float z = zs[g];
                for (int side = -1; side <= 1; side += 2)
                {
                    // In Segmente aufteilen für abwechslungsreiche Silhouette
                    float x = 10f;
                    while (x < 150f)
                    {
                        float w = Math.Min(c.R.Range(12f, 22f), 150f - x);
                        float h = c.P == "terra" ? c.R.Range(9f, 22f) : c.P == "pyra" ? c.R.Range(8f, 16f) : c.P == "pelagia" ? 4.5f : c.R.Range(6f, 12f);
                        float cx = side * (x + w * 0.5f);
                        var bx = AddBox(c, cx, z, w * 0.5f, 2.5f, h, "areawall", c.R.Range(0, 4), col, true);
                        float ground = Math.Max(0f, Terrain.HeightAt(c.P, cx, z));
                        bx.Y0 = Math.Min(bx.Y0, -14f);
                        bx.H = ground + h - bx.Y0;
                        x += w;
                    }
                }
                var gate = new GateLayout { Index = g, FromArea = g, ToArea = g + 1, Hint = GameData.Planets[c.P].Gates[g].Hint };
                gate.Blocker = new Box { Cx = 0, Cz = z, Hx = 10f, Hz = 2.6f, Y0 = -20f, H = 60f, Kind = "gateblock", Gate = g, Solid = true, Area = g };
                c.L.Colliders.Add(gate.Blocker);
                c.L.Gates[g] = gate;
            }
        }

        // ------------------------------------------------------------------ Szenerie TERRA
        static readonly float[][] XBlocks = { new[] { -148f, -106f }, new[] { -94f, -56f }, new[] { -44f, -9f }, new[] { 9f, 44f }, new[] { 56f, 94f }, new[] { 106f, 148f } };
        static readonly float[][] ZBlocks = { new[] { -148f, -82f }, new[] { -68f, -53f }, new[] { -47f, -32f }, new[] { -18f, 18f }, new[] { 32f, 47f }, new[] { 53f, 68f }, new[] { 82f, 118f }, new[] { 132f, 148f } };

        static void AddRoads(Ctx c, bool grid, bool zRoads)
        {
            c.L.Roads.Add(new[] { 0f, -150f, 0f, 150f, 16f });
            if (zRoads) foreach (var z in Terrain.RoadZ) c.L.Roads.Add(new[] { -150f, z, 150f, z, 12f });
            if (grid) foreach (var x in Terrain.RoadX) c.L.Roads.Add(new[] { x, -150f, x, 150f, 10f });
        }

        /// <summary>Füllt einen Block entlang des Randes mit Gebäuden und lässt einen Innenhof frei.</summary>
        static void FillBlockPerimeter(Ctx c, float x0, float x1, float z0, float z1, Func<int, float[]> dims, string kind, uint[] colors, float skip)
        {
            float inset = 1.5f;
            x0 += inset; x1 -= inset; z0 += inset; z1 -= inset;
            if (x1 - x0 < 8 || z1 - z0 < 8) return;
            // vier Kanten
            for (int edge = 0; edge < 4; edge++)
            {
                float t = edge < 2 ? x0 : z0;
                float tEnd = edge < 2 ? x1 : z1;
                while (t < tEnd - 4)
                {
                    int area = PlanetLayout.AreaOf(edge < 2 ? (edge == 0 ? z0 : z1) : t);
                    var d = dims(area); // w, depth, hMin, hMax
                    float w = Math.Min(c.R.Range(d[0] * 0.7f, d[0] * 1.3f), tEnd - t);
                    float depth = Math.Min(d[1], (edge < 2 ? (z1 - z0) : (x1 - x0)) * 0.35f);
                    if (w < 4) break;
                    if (!c.R.Chance(skip))
                    {
                        float cx, cz, hx, hz;
                        if (edge < 2) { cx = t + w * 0.5f; hx = w * 0.5f; hz = depth * 0.5f; cz = edge == 0 ? z0 + hz : z1 - hz; }
                        else { cz = t + w * 0.5f; hz = w * 0.5f; hx = depth * 0.5f; cx = edge == 2 ? x0 + hx : x1 - hx; }
                        if (!InBaseRect(cx, cz, Math.Max(hx, hz) + 2) && !c.BoxKeptOut(cx, cz, hx, hz) && !OverlapsExisting(c, cx, cz, hx, hz))
                        {
                            float h = c.R.Range(d[2], d[3]);
                            AddBox(c, cx, cz, hx - 0.2f, hz - 0.2f, h, kind, c.R.Range(0, 6), colors[c.R.Range(0, colors.Length)]);
                        }
                    }
                    t += w + c.R.Range(0.5f, 3f);
                }
            }
        }

        static bool OverlapsExisting(Ctx c, float cx, float cz, float hx, float hz)
        {
            foreach (var b in c.L.Colliders)
            {
                if (b.Gate >= 0) continue;
                if (Math.Abs(b.Cx - cx) < b.Hx + hx && Math.Abs(b.Cz - cz) < b.Hz + hz) return true;
            }
            return false;
        }

        static void SceneryTerra(Ctx c)
        {
            AddRoads(c, true, true);
            uint[] houseCols = { 0xC9B79C, 0xB5A48A, 0xD4C4A8, 0xA89880, 0xC2A68A, 0xBDAE95 };
            uint[] mallCols = { 0xD8CFC0, 0xC4B8A5, 0xE2D6C0 };
            foreach (var xb in XBlocks)
                foreach (var zb in ZBlocks)
                {
                    float zc = (zb[0] + zb[1]) * 0.5f;
                    int area = PlanetLayout.AreaOf(zc);
                    if (area == 0)
                        FillBlockPerimeter(c, xb[0], xb[1], zb[0], zb[1], a => new[] { 10f, 9f, 6f, 13f }, "house", houseCols, 0.3f);
                    else if (area == 1)
                        FillBlockPerimeter(c, xb[0], xb[1], zb[0], zb[1], a => new[] { 20f, 14f, 9f, 20f }, "mall", mallCols, 0.35f);
                    else
                    {
                        // Parks: vereinzelte Pavillons
                        if (c.R.Chance(0.5f))
                        {
                            float cx = c.R.Range(xb[0] + 6, xb[1] - 6), cz = c.R.Range(zb[0] + 5, zb[1] - 5);
                            if (!c.BoxKeptOut(cx, cz, 3, 3) && !OverlapsExisting(c, cx, cz, 3, 3))
                                AddBox(c, cx, cz, 3, 3, 4, "pavilion", 0, 0xE8E0CF);
                        }
                    }
                }
            // Großes Gewächshaus (Ruine bis zum Großprojekt)
            AddBox(c, 0, 131, 16, 8, 14, "greenhouse", 0, 0xCFE8E0);
            // Laternen entlang der Hauptstraße, Bäume, Werbetafeln, Autos
            for (float z = -88; z < 148; z += 18)
            {
                if (Math.Abs(z + 50) < 6 || Math.Abs(z - 50) < 6) continue;
                int area = PlanetLayout.AreaOf(z);
                string lit = GameData.ProjectId("terra", area);
                AddProp(c, "lamp", -8.5f, z, 0, 1, 0, lit);
                AddProp(c, "lamp", 8.5f, z + 9, (float)Math.PI, 1, 0, lit);
            }
            foreach (var rz in Terrain.RoadZ)
                for (float x = -140; x < 140; x += 24)
                {
                    if (Math.Abs(x) < 12) continue;
                    int area = PlanetLayout.AreaOf(rz);
                    AddProp(c, "lamp", x, rz - 6.5f, 0, 1, 0, GameData.ProjectId("terra", area));
                }
            TreesInOpen(c, 70, "tree");
            AddProp(c, "billboard", -30, -20, 0.3f, 1.3f, 0);
            AddProp(c, "billboard", 60, -95, -0.4f, 1f, 1);
            AddProp(c, "billboard", 35, 40, 3.0f, 1f, 0);
            AddProp(c, "busstop", 13, -60, -1.57f, 1, 0, "terra_p1");
            AddProp(c, "fountain", 0, 22, 0, 1.2f, 0, "terra_p2");
            AddProp(c, "fountain", -40, -5, 0, 0.8f, 0, "terra_p2");
            AddProp(c, "fountain", 40, 12, 0, 0.8f, 0, "terra_p2");
            AddProp(c, "pipe", 0, 120, 0, 1, 0, "terra_p3");
            for (int i = 0; i < 26; i++)
            {
                float x = c.R.Range(-140, 140), z = c.R.Range(-140, 145);
                if (!Placeable(c, x, z, 3f)) continue;
                AddProp(c, "rustcar", x, z, c.R.Range(0, 6.28f), 1, c.R.Range(0, 4));
            }
        }

        static void TreesInOpen(Ctx c, int count, string kind)
        {
            for (int i = 0; i < count; i++)
            {
                float x = c.R.Range(-140, 140), z = c.R.Range(-140, 145);
                if (!Placeable(c, x, z, 2f)) continue;
                if (c.P == "pelagia" && Terrain.HeightAt(c.P, x, z) < 0.5f) continue;
                AddProp(c, kind, x, z, c.R.Range(0, 6.28f), c.R.Range(0.8f, 1.4f), i);
            }
        }

        static bool Placeable(Ctx c, float x, float z, float margin)
        {
            if (InBaseRect(x, z, margin + 2)) return false;
            if (Math.Abs(z + 50) < 5 || Math.Abs(z - 50) < 5) return false;
            if (c.KeptOut(x, z, margin)) return false;
            foreach (var b in c.L.Colliders) if (b.Gate < 0 && b.Contains(x, z, margin)) return false;
            return true;
        }

        // ------------------------------------------------------------------ Szenerie PYRA
        static void SceneryPyra(Ctx c)
        {
            AddRoads(c, false, true);
            uint[] hall = { 0x8A5A44, 0x7A4E3A, 0x9C6A50, 0x6E4636 };
            foreach (var xb in XBlocks)
                foreach (var zb in ZBlocks)
                {
                    float zc = (zb[0] + zb[1]) * 0.5f;
                    int area = PlanetLayout.AreaOf(zc);
                    int n = area == 0 ? 2 : 3;
                    for (int i = 0; i < n; i++)
                    {
                        float w = area == 1 ? c.R.Range(8, 16) : c.R.Range(4, 9);
                        float d = area == 1 ? c.R.Range(6, 10) : c.R.Range(4, 7);
                        float cx = c.R.Range(xb[0] + w + 1, xb[1] - w - 1), cz = c.R.Range(zb[0] + d + 1, zb[1] - d - 1);
                        if (xb[1] - xb[0] < 2 * w + 4 || zb[1] - zb[0] < 2 * d + 4) continue;
                        if (InBaseRect(cx, cz, 10) || c.BoxKeptOut(cx, cz, w, d) || OverlapsExisting(c, cx, cz, w + 3, d + 3)) continue;
                        string kind = area == 0 ? "shed" : area == 1 ? "hall" : (c.R.Chance(0.5f) ? "furnace" : "silo");
                        float h = area == 0 ? c.R.Range(4, 7) : area == 1 ? c.R.Range(10, 16) : c.R.Range(12, 24);
                        if (kind == "furnace" || kind == "silo") { w = d = Math.Min(w, d); }
                        AddBox(c, cx, cz, w, d, h, kind, c.R.Range(0, 4), hall[c.R.Range(0, hall.Length)]);
                        if (kind == "hall" && c.R.Chance(0.6f)) AddProp(c, "chimney", cx + w * 0.6f, cz, 0, c.R.Range(1f, 1.6f), 0, "pyra_p3");
                    }
                }
            // Förderbänder über den Straßen (dekorativ, laufen nach Projekt 2)
            foreach (var rz in Terrain.RoadZ)
                for (float x = -130; x < 130; x += 40)
                    if (Math.Abs(x) > 14) AddProp(c, "gantry", x, rz, 0, 1, 0, "pyra_p2");
            // Windturbinen
            for (int i = 0; i < 12; i++)
            {
                float x = c.R.Range(-140, 140), z = c.R.Range(-40, 140);
                if (!Placeable(c, x, z, 4)) continue;
                AddProp(c, "turbine", x, z, c.R.Range(0, 6.28f), 1, 0, "pyra_p2");
            }
            // Dünenwälle: Set 0 und 1 wechseln mit jedem Sandsturm
            float[] duneX = { -75f, 75f, -125f, 125f, -28f, 28f };
            for (int i = 0; i < duneX.Length; i++)
            {
                foreach (var rz in Terrain.RoadZ)
                {
                    if (rz > 140) continue;
                    var b = AddBox(c, duneX[i], rz, 2.5f, 6.5f, 2.6f, "dune", 0, 0xC96A45);
                    b.DuneSet = i % 2;
                }
            }
            TreesInOpen(c, 40, "deadcactus");
            for (int i = 0; i < 24; i++)
            {
                float x = c.R.Range(-140, 140), z = c.R.Range(-140, 145);
                if (!Placeable(c, x, z, 3f)) continue;
                AddProp(c, "rustcar", x, z, c.R.Range(0, 6.28f), 1.2f, c.R.Range(0, 4));
            }
            AddProp(c, "billboard", -30, -20, 0.3f, 1.2f, 2);
            AddProp(c, "tradepost", -40, -100, 0.0f, 1, 0, "pyra_p1");
            AddProp(c, "recycler", 0, 128, 0, 1.4f, 0, "pyra_p3");
            AddBox(c, 0, 132, 14, 7, 12, "foundry", 0, 0x5A3A2A);
        }

        // ------------------------------------------------------------------ Szenerie PELAGIA
        static void SceneryPelagia(Ctx c)
        {
            c.L.Roads.Add(new[] { 0f, -150f, 0f, -58f, 14f });
            c.L.Roads.Add(new[] { 0f, -42f, 0f, 38f, 12f });
            uint[] cols = { 0xE8E0CF, 0x8FC7D0, 0xF2C14E, 0xE88D6A, 0xBFD8C0 };
            // Lagerhäuser am Hafen
            for (int i = 0; i < 40; i++)
            {
                float x = c.R.Range(-140, 140), z = c.R.Range(-146, -64);
                float w = c.R.Range(4, 9), d = c.R.Range(4, 7);
                if (Terrain.HeightAt(c.P, x - w, z - d) < 0.4f || Terrain.HeightAt(c.P, x + w, z + d) < 0.4f || Terrain.HeightAt(c.P, x, z) < 0.4f) continue;
                if (InBaseRect(x, z, 12) || c.BoxKeptOut(x, z, w, d) || OverlapsExisting(c, x, z, w + 4, d + 4)) continue;
                AddBox(c, x, z, w, d, c.R.Range(5, 9), "warehouse", c.R.Range(0, 3), cols[c.R.Range(0, cols.Length)]);
            }
            // Hafenkräne
            for (int i = 0; i < 4; i++) AddProp(c, "harborcrane", 44 + i * 18, -60, (float)Math.PI, 1, 0, "pelagia_p1");
            // Stelzenhäuser auf Inseln
            for (int i = 0; i < 70; i++)
            {
                float x = c.R.Range(-140, 140), z = c.R.Range(-38, 140);
                float h0 = Terrain.HeightAt(c.P, x, z);
                if (h0 < 0.6f) continue;
                float w = c.R.Range(2.5f, 4.5f);
                if (c.BoxKeptOut(x, z, w, w) || OverlapsExisting(c, x, z, w + 3, w + 3)) continue;
                AddBox(c, x, z, w, w, c.R.Range(4, 7), "stilt", c.R.Range(0, 3), cols[c.R.Range(0, cols.Length)]);
            }
            // Versunkene Ruinen in der Lagune (nur für Taucher im Weg)
            for (int i = 0; i < 22; i++)
            {
                float x = c.R.Range(-120, 120), z = c.R.Range(66, 140);
                float h0 = Terrain.HeightAt(c.P, x, z);
                if (h0 > -7f) continue;
                float w = c.R.Range(2, 5), d = c.R.Range(2, 5);
                if (c.BoxKeptOut(x, z, w, d) || OverlapsExisting(c, x, z, w + 2, d + 2)) continue;
                var b = AddBox(c, x, z, w, d, c.R.Range(2.5f, 4.5f), "ruin", c.R.Range(0, 3), 0x7E8C85);
                b.Y0 = h0 - 0.5f;
            }
            AddProp(c, "lighthouse", -60, -70, 0, 1, 0, "pelagia_p1");
            AddProp(c, "filterstation", 30, 20, 0, 1, 0, "pelagia_p2");
            AddProp(c, "filterstation", -60, 30, 0, 1, 1, "pelagia_p2");
            AddProp(c, "filterstation", 80, -20, 0, 1, 2, "pelagia_p2");
            AddProp(c, "reef", 0, 90, 0, 1.5f, 0, "pelagia_p3", Terrain.HeightAt(c.P, 0, 90));
            for (int i = 0; i < 30; i++)
            {
                float x = c.R.Range(-140, 140), z = c.R.Range(-140, 140);
                if (Terrain.HeightAt(c.P, x, z) > -1.5f || c.KeptOut(x, z, 3)) continue;
                AddProp(c, "buoy", x, z, 0, 1, i % 3, null, 0f);
            }
            TreesInOpen(c, 60, "palm");
        }

        // ------------------------------------------------------------------ Szenerie NIVALIS
        static void SceneryNivalis(Ctx c)
        {
            AddRoads(c, true, true);
            uint[] cols = { 0xE6EEF5, 0xCBD8E6, 0xAFC3D8, 0x8FA3BF };
            foreach (var xb in XBlocks)
                foreach (var zb in ZBlocks)
                {
                    float zc = (zb[0] + zb[1]) * 0.5f;
                    int area = PlanetLayout.AreaOf(zc);
                    if (area == 0)
                    {
                        float r = c.R.Range(5, 9);
                        float cx = (xb[0] + xb[1]) * 0.5f + c.R.Range(-4, 4), cz = zc + c.R.Range(-3, 3);
                        if (xb[1] - xb[0] > 2 * r + 4 && zb[1] - zb[0] > 2 * r + 4 && !InBaseRect(cx, cz, r + 4) && !c.BoxKeptOut(cx, cz, r, r))
                            AddBox(c, cx, cz, r, r, r, "dome", 0, 0xD8ECF8);
                    }
                    else if (area == 1)
                        FillBlockPerimeter(c, xb[0], xb[1], zb[0], zb[1], a => new[] { 14f, 10f, 6f, 12f }, "serverhall", cols, 0.4f);
                    else
                    {
                        float w = c.R.Range(6, 10);
                        float cx = (xb[0] + xb[1]) * 0.5f, cz = zc;
                        if (xb[1] - xb[0] > 2 * w + 4 && zb[1] - zb[0] > 2 * w + 4 && !c.BoxKeptOut(cx, cz, w, w))
                            AddBox(c, cx, cz, w, w, c.R.Range(3, 26), c.R.Chance(0.5f) ? "hangar" : "tower", c.R.Range(0, 3), cols[c.R.Range(0, cols.Length)]);
                    }
                }
            AddBox(c, 0, 132, 12, 8, 30, "launchtower", 0, 0xC8D4E0);
            for (int i = 0; i < 10; i++)
            {
                float x = c.R.Range(-140, 140), z = c.R.Range(-140, 140);
                if (!Placeable(c, x, z, 4)) continue;
                AddProp(c, "radar", x, z, c.R.Range(0, 6.28f), 1, 0, GameData.ProjectId("nivalis", PlanetLayout.AreaOf(z)));
            }
            for (float z = -88; z < 148; z += 22)
            {
                if (Math.Abs(z + 50) < 6 || Math.Abs(z - 50) < 6) continue;
                AddProp(c, "heatlamp", -8.5f, z, 0, 1, 0, GameData.ProjectId("nivalis", PlanetLayout.AreaOf(z)));
                AddProp(c, "heatlamp", 8.5f, z + 11, 0, 1, 0, GameData.ProjectId("nivalis", PlanetLayout.AreaOf(z + 11)));
            }
            TreesInOpen(c, 50, "icespike");
        }

        // ------------------------------------------------------------------ Tore
        static void PlaceGates(Ctx c)
        {
            var def = GameData.Planets[c.P];
            float[] zs = { -50f, 50f };
            for (int g = 0; g < 2; g++)
            {
                var gd = def.Gates[g];
                var gl = c.L.Gates[g];
                for (int k = 0; k < gd.Count; k++)
                {
                    float x = gd.Count == 1 ? 0 : -6.5f + 13f * (k + 0.5f) / gd.Count;
                    float z = zs[g] + (gd.Count == 1 ? 0 : ((k % 2 == 0) ? -0.8f : 0.8f));
                    var t = new TrashObj { Id = c.L.Trash.Count, Type = gd.Type, Area = g, Gate = g, Rot = gd.Count == 1 ? 0.35f : c.R.Range(-0.6f, 0.6f), Scale = 1f };
                    float h = Terrain.HeightAt(c.P, x, z);
                    float y = h;
                    var td = GameData.Trash[gd.Type];
                    if (c.P == "pelagia")
                    {
                        if (gd.Type == "wrackteil") { y = h + 0.3f; t.Underwater = true; }
                        else y = -0.6f;
                    }
                    if (c.P == "nivalis" && gd.Type == "eismaschine") t.Frozen = true;
                    t.Pos = new V3(x, y, z);
                    c.L.Trash.Add(t);
                    gl.Objects.Add(t.Id);
                    if (td.Crane) break;
                }
            }
        }

        // ------------------------------------------------------------------ Müll
        static bool IsUnderwaterType(string type)
        {
            return type == "elektroschrott" || type == "schiffsteil" || type == "anker" || type == "wrackteil";
        }

        static float ObjRadius(TrashType t)
        {
            if (t.Crane) return 5f;
            if (t.Mass >= 20) return 2.5f;
            if (t.Oil) return 2.5f;
            return 0.9f * t.Size;
        }

        /// <summary>Prüft eine Position für ein Müllobjekt und liefert die Höhe und Flags.</summary>
        static bool TryPos(Ctx c, TrashType t, float x, float z, int area, out float y, out bool underwater)
        {
            y = 0; underwater = false;
            float r = ObjRadius(t);
            if (Math.Abs(x) > 146 - r) return false;
            float z0 = area == 0 ? -148 : area == 1 ? -47 : 53;
            float z1 = area == 0 ? -53 : area == 1 ? 47 : 148;
            if (z < z0 + r || z > z1 - r) return false;
            if (InBaseRect(x, z, 3 + r)) return false;
            if (Math.Abs(x) < 14 && (Math.Abs(z + 50) < 8 || Math.Abs(z - 50) < 8)) return false;
            var tmp = new List<Box>();
            c.L.Query(x, z, r + 1, tmp);
            float h = Terrain.HeightAt(c.P, x, z);
            foreach (var b in tmp)
            {
                if (b.Gate >= 0) continue;
                if (!b.Contains(x, z, r)) continue;
                if (b.Kind == "ruin") { if (!t.Floating && h < -2f) return false; continue; }
                return false;
            }
            foreach (var p in c.L.ProjectSites)
            {
                float dx = x - p.x, dz = z - p.z;
                if (dx * dx + dz * dz < 8 * 8) return false;
            }
            if (c.P == "pelagia")
            {
                if (t.Floating)
                {
                    if (h > -0.8f) return false;
                    y = 0f;
                    return true;
                }
                if (IsUnderwaterType(t.Id) && h < -2.5f)
                {
                    y = h + 0.25f; underwater = true;
                    return true;
                }
                if (h < 0.3f) return false;
                // Hanglage vermeiden
                if (Math.Abs(Terrain.HeightAt(c.P, x + 1.5f, z) - h) > 1.2f) return false;
                y = h;
                return true;
            }
            y = h;
            if (Math.Abs(Terrain.HeightAt(c.P, x + r, z) - h) > 1.5f || Math.Abs(Terrain.HeightAt(c.P, x, z + r) - h) > 1.5f) return false;
            return true;
        }

        static void PlaceTrash(Ctx c)
        {
            var def = GameData.Planets[c.P];
            var zoneRng = new Rng(def.Seed + 7);
            for (int area = 0; area < 3; area++)
            {
                var items = new List<string>();
                foreach (var s in def.Spawns[area]) for (int i = 0; i < s.Count; i++) items.Add(s.Type);
                // deterministisch mischen
                for (int i = items.Count - 1; i > 0; i--) { int j = c.R.Range(0, i + 1); var tmp = items[i]; items[i] = items[j]; items[j] = tmp; }

                // 1) Lichtpunkte (Mikrozonen) bestücken
                foreach (var zd in c.L.Zones)
                {
                    if (zd.Area != area) continue;
                    int want = 9 + zoneRng.Range(0, 4);
                    if (c.P == "terra" && zd.Index == 0) want = 8; // Tutorial-Spielplatz
                    int placed = 0;
                    for (int i = 0; i < items.Count && placed < want; i++)
                    {
                        var t = GameData.Trash[items[i]];
                        if (t.Mass > 3f || t.Crane || t.Hazard > 0 || t.Oil) continue;
                        if (c.P == "terra" && zd.Index == 0 && (t.Mass > 1f || t.Magnet && !t.Grab)) continue;
                        bool ok = false; float y = 0; bool uw = false; float px = 0, pz = 0;
                        for (int a = 0; a < 30 && !ok; a++)
                        {
                            float ang = zoneRng.Range(0, 6.283f), rad = M.Sqrt(zoneRng.Next()) * (zd.Radius - 1f);
                            px = zd.Center.x + M.Cos(ang) * rad; pz = zd.Center.z + M.Sin(ang) * rad;
                            ok = TryPos(c, t, px, pz, area, out y, out uw);
                            if (ok && c.P == "terra" && zd.Index == 0 && uw) ok = false;
                        }
                        if (!ok) continue;
                        var o = NewObj(c, items[i], px, y, pz, area, uw);
                        o.Zone = zd.Index;
                        zd.Objects.Add(o.Id);
                        items.RemoveAt(i); i--;
                        placed++;
                    }
                }

                // 2) Haufen und 3) Streuung
                int clusters = Math.Max(3, items.Count / 12);
                var centers = new List<V3>();
                for (int k = 0; k < clusters; k++)
                {
                    for (int a = 0; a < 60; a++)
                    {
                        float x = c.R.Range(-140, 140);
                        float z = area == 0 ? c.R.Range(-146, -55) : area == 1 ? c.R.Range(-45, 45) : c.R.Range(55, 146);
                        float yy; bool uw;
                        if (TryPos(c, GameData.Trash["glasflasche"], x, z, area, out yy, out uw) || (c.P == "pelagia" && TryPos(c, GameData.Trash["treibgut"], x, z, area, out yy, out uw)))
                        {
                            centers.Add(new V3(x, 0, z));
                            break;
                        }
                    }
                }
                for (int i = 0; i < items.Count; i++)
                {
                    var t = GameData.Trash[items[i]];
                    bool clustered = centers.Count > 0 && !t.Crane && c.R.Chance(0.55f);
                    bool ok = false; float y = 0, px = 0, pz = 0; bool uw = false;
                    for (int a = 0; a < 60 && !ok; a++)
                    {
                        if (clustered && a < 25)
                        {
                            var cc = centers[c.R.Range(0, centers.Count)];
                            float ang = c.R.Range(0, 6.283f), rad = c.R.Range(0.5f, 7f);
                            px = cc.x + M.Cos(ang) * rad; pz = cc.z + M.Sin(ang) * rad;
                        }
                        else
                        {
                            px = c.R.Range(-144, 144);
                            pz = area == 0 ? c.R.Range(-147, -54) : area == 1 ? c.R.Range(-46, 46) : c.R.Range(54, 147);
                        }
                        ok = TryPos(c, t, px, pz, area, out y, out uw);
                    }
                    if (!ok) continue;
                    var o = NewObj(c, items[i], px, y, pz, area, uw);
                    // Mikrozonen-Zugehörigkeit auch für zufällig hineingefallene Objekte
                    foreach (var zd in c.L.Zones)
                    {
                        if (zd.Area != area) continue;
                        if (V3.DistXZ(zd.Center, o.Pos) < zd.Radius && !t.Crane) { o.Zone = zd.Index; zd.Objects.Add(o.Id); break; }
                    }
                }
            }
        }

        static TrashObj NewObj(Ctx c, string type, float x, float y, float z, int area, bool underwater)
        {
            var t = GameData.Trash[type];
            var o = new TrashObj { Id = c.L.Trash.Count, Type = type, Pos = new V3(x, y, z), Rot = c.R.Range(0, 6.283f), Scale = c.R.Range(0.9f, 1.12f), Area = area, Underwater = underwater };
            if (t.Crane || t.Oil) o.Scale = 1f;
            if (c.P == "nivalis")
            {
                float p = type == "eismaschine" ? 1f : type == "akku" ? 0.6f : 0.25f;
                if (!t.Crane && c.R.Chance(p)) o.Frozen = true;
            }
            c.L.Trash.Add(o);
            return o;
        }

        // ------------------------------------------------------------------ Dekorative Müllberge & Skyline
        static void PlaceMounds(Ctx c)
        {
            for (int area = 0; area < 3; area++)
            {
                int placed = 0;
                for (int a = 0; a < 300 && placed < 7; a++)
                {
                    float x = (c.R.Chance(0.5f) ? -1 : 1) * c.R.Range(112, 140);
                    float z = area == 0 ? c.R.Range(-140, -60) : area == 1 ? c.R.Range(-40, 40) : c.R.Range(62, 140);
                    float r = c.R.Range(5, 9);
                    if (c.KeptOut(x, z, r)) continue;
                    bool hit = false;
                    foreach (var b in c.L.Colliders) if (b.Gate < 0 && b.Contains(x, z, r)) { hit = true; break; }
                    if (hit) continue;
                    foreach (var m in c.L.Mounds) if (V3.DistXZ(m.Pos, new V3(x, 0, z)) < m.Radius + r + 2) { hit = true; break; }
                    if (hit) continue;
                    float y = Terrain.HeightAt(c.P, x, z);
                    if (c.P == "pelagia") y = Math.Max(y, -0.4f);
                    c.L.Mounds.Add(new Mound { Pos = new V3(x, y, z), Radius = r, Height = c.R.Range(4, 8), Area = area });
                    placed++;
                }
            }
        }

        static void PlaceSkyline(Ctx c)
        {
            var r = new Rng(GameData.Planets[c.P].Seed + 99);
            string kind = c.P == "terra" ? "skyline" : c.P == "pyra" ? "mesa" : c.P == "pelagia" ? "farisland" : "icepeak";
            for (int i = 0; i < 90; i++)
            {
                float ang = r.Range(0, 6.283f);
                float dist = r.Range(175, 300);
                float x = M.Cos(ang) * dist, z = M.Sin(ang) * dist;
                int style = r.Range(0, 4);
                // TERRA: Türme aus gepressten Müllwürfeln neben verlassenen Hochhäusern (Intro-Motiv)
                if (c.P == "terra" && r.Chance(0.45f)) style = 9;
                c.L.Props.Add(new Prop { Kind = kind, Pos = new V3(x, c.P == "pelagia" ? -2f : -1f, z), Rot = r.Range(0, 6.28f), Scale = r.Range(0.7f, 1.8f), Style = style, Area = -1 });
            }
        }
    }
}
