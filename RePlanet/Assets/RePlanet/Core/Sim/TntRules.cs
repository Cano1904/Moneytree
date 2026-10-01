using System;
using System.Collections.Generic;

namespace RePlanet.Core
{
    /// <summary>Ergebnis einer Wurfbahn: Ruhelage, Flugzeit, Landung (siehe <see cref="TntCharge.Land"/>), getroffener Müllberg.</summary>
    public struct TntFlight
    {
        public V3 Pos;
        public float Time;
        public int Land, Mound;
    }

    /// <summary>
    /// Regeln für TNT (Server und Anzeige rechnen gleich): Wurf erlaubt?, deterministische Wurfbahn mit Abprallen und Rollen,
    /// Müllberge (Größe nach Sprengungen, sprengbar?), Flugbahn getroffener Roboter und der Anteil gesprengter Berge am Hauptmüll.
    /// </summary>
    public static partial class Rules
    {
        // ------------------------------------------------------------ Müllberge
        public static int MoundIndex(PlanetState ps, Mound m) { return m == null ? -1 : WorldGen.Get(ps.Id).Mounds.IndexOf(m); }

        /// <summary>Verbleibender Anteil eines Müllbergs nach Sprengungen (1 = unberührt, 0 = ganz zerlegt).</summary>
        public static float MoundRemaining(PlanetState ps, int mound)
        {
            var l = WorldGen.Get(ps.Id);
            if (mound < 0 || mound >= l.Mounds.Count) return 0f;
            int st = GameData.MoundStages(l.Mounds[mound]);
            return M.Clamp01(1f - ps.Blasts(mound) / (float)st);
        }

        /// <summary>Darstellungs- und Kollisionsgröße eines Müllbergs: schrumpft mit der Sauberkeit seines Bereichs und mit jeder Sprengung.</summary>
        public static float MoundScale(PlanetState ps, int mound)
        {
            var l = WorldGen.Get(ps.Id);
            if (mound < 0 || mound >= l.Mounds.Count) return 0f;
            float clean = M.Clamp01(1f - Cleanliness(ps, l.Mounds[mound].Area) * 1.05f);
            return Math.Min(clean, MoundRemaining(ps, mound));
        }

        public static float MoundScale(PlanetState ps, Mound m) { return MoundScale(ps, MoundIndex(ps, m)); }

        /// <summary>Steht der Müllberg im Wasser (PELAGIA: treibende Müllinseln)? Dort zündet keine Ladung.</summary>
        public static bool MoundInWater(string planet, Mound m)
        {
            return Terrain.HeightAt(planet, m.Pos.x, m.Pos.z) < Terrain.WaterLevel(planet) - 0.5f;
        }

        /// <summary>null = der Müllberg lässt sich jetzt sprengen, sonst der Grund.</summary>
        public static string MoundBlastCheck(WorldState s, PlanetState ps, int mound)
        {
            var l = WorldGen.Get(ps.Id);
            if (mound < 0 || mound >= l.Mounds.Count) return "Hier ist kein Müllberg.";
            var m = l.Mounds[mound];
            if (MoundScale(ps, mound) < 0.06f) return "Von diesem Müllberg ist nichts mehr übrig.";
            if (MoundInWater(ps.Id, m)) return "Dieser Müllberg treibt im Wasser – dort zündet keine Ladung.";
            for (int g = 0; g < m.Area; g++) if (!GateOpen(ps, g)) return "Dieser Bereich ist noch versperrt.";
            double until;
            if (ps.MoundCool.TryGetValue(mound, out until) && until > s.PlayTime + 0.01)
                return "Der Staub legt sich noch – in " + Math.Ceiling(until - s.PlayTime) + " s lässt sich der Müllberg wieder sprengen.";
            return null;
        }

        /// <summary>Müllberg, dessen (geschrumpfter) Rand höchstens <paramref name="reach"/> m von der Stelle entfernt ist (nächster), sonst -1.</summary>
        public static int HeapAt(PlanetState ps, V3 p, float reach)
        {
            var l = WorldGen.Get(ps.Id);
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < l.Mounds.Count; i++)
            {
                float s = MoundScale(ps, i);
                if (s < 0.06f) continue;
                var m = l.Mounds[i];
                float d = V3.DistXZ(p, m.Pos) - m.Radius * s;
                if (d <= reach && d < bd) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>Anrechenbarer Anteil eingesammelter Müllberg-Stücke am Hauptmüll eines Bereichs (Materialeinheiten, gedeckelt).</summary>
        public static float HeapCredit(PlanetState ps, int area)
        {
            if (area < 0 || area > 2 || ps.HeapWeight == null) return 0f;
            var l = WorldGen.Get(ps.Id);
            return Math.Min(ps.HeapWeight[area], l.AreaWeight[area] * GameData.TntCleanShare);
        }

        // ------------------------------------------------------------ Werfen
        /// <summary>Darf dieser Spieler jetzt eine Ladung werfen? null = ja, sonst ein verständlicher Grund.</summary>
        public static string TntThrowCheck(WorldState s, PlanetState ps, PlayerData p)
        {
            if (p.TowTimer > 0) return "MIKO ist abgeschaltet.";
            if (p.Tnt <= 0) return "Keine TNT-Ladung dabei – in der Werkstatt am Stützpunkt kaufen.";
            if (p.Vehicle != null) return "Aus dem Fahrzeug wird nicht geworfen – erst aussteigen.";
            var l = WorldGen.Get(ps.Id);
            if (Indoors(ShelterKind(s, ps, p.Pos)) || l.Base.InBase(p.Pos.x, p.Pos.z)) return "Im Stützpunkt wird nicht gesprengt – geh ein Stück hinaus.";
            float water = Terrain.WaterLevel(ps.Id);
            if (water > -50f && p.Pos.y < water - 0.2f && Terrain.HeightAt(ps.Id, p.Pos.x, p.Pos.z) < water - 0.4f) return "Im Wasser zündet keine Zündschnur.";
            if (ps.StormActive) return "Im Sturm bläst der Wind jede Zündschnur aus – erst den Sturm abwarten.";
            return null;
        }

        /// <summary>Wurfrichtung aus Blickrichtung (Gier, Bogenmaß) und Abwurfwinkel (Bogenmaß über der Waagerechten).</summary>
        public static V3 TntDirection(float yaw, float elevation)
        {
            elevation = M.Clamp(elevation, -0.3f, 1.3f);
            float c = M.Cos(elevation);
            return new V3(M.Sin(yaw) * c, M.Sin(elevation), M.Cos(yaw) * c);
        }

        /// <summary>Anfangsgeschwindigkeit: Richtung (wird normiert, Abwurfwinkel begrenzt) × Wurfkraft 0…1.</summary>
        public static V3 TntVelocity(V3 dir, float charge)
        {
            float len = dir.Length;
            if (!dir.IsFinite || len < 1e-4f) dir = new V3(0, 0.7f, 0.7f); else dir = dir * (1f / len);
            float horiz = dir.LengthXZ;
            float elev = M.Clamp(M.Atan2(dir.y, horiz), -0.3f, 1.3f);
            float yaw = horiz > 1e-4f ? M.Atan2(dir.x, dir.z) : 0f;
            var d = TntDirection(yaw, elev);
            return d * M.Lerp(GameData.TntSpeedMin, GameData.TntSpeedMax, M.Clamp01(charge));
        }

        /// <summary>Abwurfpunkt: über MIKOs Kopf, ein Stück in Wurfrichtung.</summary>
        public static V3 TntStart(V3 robot, V3 vel)
        {
            float h = vel.LengthXZ;
            float fx = h > 1e-4f ? vel.x / h : 0f, fz = h > 1e-4f ? vel.z / h : 0f;
            return new V3(robot.x + fx * 0.45f, robot.y + GameData.TntThrowHeight, robot.z + fz * 0.45f);
        }

        [ThreadStatic] static List<Box> tntBoxes;

        static bool BoxSolidNow(PlanetState ps, Box b)
        {
            if (!b.Solid) return false;
            if (b.Gate >= 0 && GateOpen(ps, b.Gate)) return false;
            if (b.DuneSet >= 0 && (!GameData.Planets[ps.Id].Storms || b.DuneSet != ps.StormCount % 2)) return false;
            return true;
        }

        /// <summary>
        /// Deterministische Wurfbahn (fester Zeitschritt): Schwerkraft, Abprallen an Wänden und am Boden, Ausrollen,
        /// Landung auf einem Müllberg (bleibt liegen) oder im Wasser (versinkt). <paramref name="path"/> (optional) erhält alle Punkte.
        /// </summary>
        public static TntFlight TntSimulate(PlanetState ps, V3 start, V3 vel, List<V3> path = null)
        {
            var l = WorldGen.Get(ps.Id);
            float water = Terrain.WaterLevel(ps.Id);
            var boxes = tntBoxes ?? (tntBoxes = new List<Box>());
            var res = new TntFlight { Mound = -1 };
            V3 p = start, v = vel;
            if (path != null) { path.Clear(); path.Add(p); }
            float dt = GameData.TntStep, t = 0f;
            int bounces = 0;
            bool rolling = false, done = false;
            int mcount = l.Mounds.Count;
            var scales = new float[mcount];
            for (int i = 0; i < mcount; i++) scales[i] = MoundScale(ps, i);
            while (!done && t < GameData.TntMaxFlight)
            {
                if (!rolling) v.y -= GameData.TntGravity * dt;
                var np = new V3(p.x + v.x * dt, p.y + v.y * dt, p.z + v.z * dt);
                // Wände und Dächer (feste Kollisionsboxen)
                l.Query(np.x, np.z, 0.4f, boxes);
                float roof = float.MinValue;
                foreach (var b in boxes)
                {
                    if (!BoxSolidNow(ps, b) || !b.Contains(np.x, np.z, 0.12f)) continue;
                    float top = b.Y0 + b.H;
                    if (np.y > top + 0.05f || np.y < b.Y0 - 0.6f) continue;
                    if (p.y >= top - 0.05f) { roof = Math.Max(roof, top); continue; } // von oben: landet auf dem Dach
                    bool inX = b.Contains(np.x, p.z, 0.12f), inZ = b.Contains(p.x, np.z, 0.12f);
                    if (inX || !inZ) v.x = -v.x * 0.35f;
                    if (inZ || !inX) v.z = -v.z * 0.35f;
                    np = new V3(p.x, np.y, p.z);
                    bounces++;
                }
                // Müllberge: auf der (geschrumpften) Halbkugel bleibt die Ladung liegen
                for (int i = 0; i < mcount && !done; i++)
                {
                    float s = scales[i];
                    if (s < 0.06f) continue;
                    var m = l.Mounds[i];
                    float r = m.Radius * s, d = V3.DistXZ(np, m.Pos);
                    if (d >= r) continue;
                    float k = d / r;
                    float surf = m.Pos.y + m.Height * s * M.Sqrt(1f - k * k);
                    if (np.y > surf) continue;
                    np.y = surf;
                    res.Land = 2; res.Mound = i; done = true;
                }
                if (!done)
                {
                    float gnd = Math.Max(l.GroundAt(np.x, np.z), roof);
                    if (water > -50f && np.y <= water && gnd < water - 0.05f) { np.y = water; res.Land = 1; done = true; }
                    else if (np.y <= gnd)
                    {
                        np.y = gnd;
                        if (!rolling && v.y < -2.5f && bounces < 4) { v.y = -v.y * 0.3f; v.x *= 0.55f; v.z *= 0.55f; bounces++; }
                        else
                        {
                            rolling = true; v.y = 0f;
                            v.x *= 0.82f; v.z *= 0.82f;
                            if (M.Sqrt(v.x * v.x + v.z * v.z) < 0.35f) done = true;
                        }
                    }
                    else if (rolling)
                    {
                        // abwärts rollend: am Boden bleiben, sonst wieder fallen
                        if (np.y - gnd < 0.25f) np.y = gnd; else rolling = false;
                    }
                }
                if (Math.Abs(np.x) > 147f || Math.Abs(np.z) > 147f)
                {
                    np = new V3(M.Clamp(np.x, -147f, 147f), np.y, M.Clamp(np.z, -147f, 147f));
                    v.x = 0; v.z = 0;
                }
                p = np;
                t += dt;
                if (path != null) path.Add(p);
            }
            if (!done && res.Land == 0) p.y = Math.Max(p.y, l.GroundAt(p.x, p.z));
            if (res.Land == 0 && l.Base.InBase(p.x, p.z)) res.Land = 3;
            res.Pos = p;
            res.Time = t;
            return res;
        }

        /// <summary>
        /// Zielhilfe (Bot, Prüfungen): sucht Abwurfwinkel und Wurfkraft, deren Bahn möglichst nah am Ziel zur Ruhe kommt.
        /// true, wenn die Landestelle höchstens <paramref name="tolerance"/> m vom Ziel entfernt ist.
        /// </summary>
        public static bool TntAim(PlanetState ps, V3 robot, V3 target, float tolerance, out V3 dir, out float charge, out TntFlight flight)
        {
            float yaw = M.Atan2(target.x - robot.x, target.z - robot.z);
            float[] elevs = { 0.78f, 0.62f, 0.95f, 0.45f, 1.1f };
            dir = TntDirection(yaw, elevs[0]); charge = 0.5f; flight = default(TntFlight);
            float best = float.MaxValue;
            foreach (var e in elevs)
            {
                var d = TntDirection(yaw, e);
                for (int k = 0; k <= 50; k++)
                {
                    float c = k / 50f;
                    var vel = TntVelocity(d, c);
                    var f = TntSimulate(ps, TntStart(robot, vel), vel);
                    float err = V3.DistXZ(f.Pos, target);
                    if (err < best) { best = err; dir = d; charge = c; flight = f; }
                }
                if (best <= tolerance * 0.5f) break;
            }
            return best <= tolerance;
        }

        /// <summary>
        /// Wohin ein getroffener Roboter fliegt: von der Ladung weg, nah dran weiter, am Rand kürzer. Nicht in Wände, nicht ins Wasser,
        /// nicht aus dem Spielfeld – notfalls kürzer bzw. an Ort und Stelle.
        /// </summary>
        public static V3 TntKnockTarget(PlanetState ps, V3 center, V3 robot, float fallbackYaw)
        {
            var l = WorldGen.Get(ps.Id);
            float dx = robot.x - center.x, dz = robot.z - center.z;
            float d = M.Sqrt(dx * dx + dz * dz);
            if (d < 0.3f) { dx = M.Sin(fallbackYaw); dz = M.Cos(fallbackYaw); d = 1f; }
            else { dx /= d; dz /= d; }
            float dist = M.Lerp(GameData.TntKnockMax, GameData.TntKnockMin, M.Clamp01(V3.DistXZ(robot, center) / GameData.TntKnockRadius));
            float water = Terrain.WaterLevel(ps.Id);
            for (int k = 0; k < 6; k++)
            {
                float f = dist * (1f - k * 0.18f);
                float x = M.Clamp(robot.x + dx * f, -146f, 146f), z = M.Clamp(robot.z + dz * f, -146f, 146f);
                if (l.BlockedStatic(x, z, 0.5f)) continue;
                float y = l.GroundAt(x, z);
                if (water > -50f && y < water - 0.3f && robot.y >= water - 0.3f) continue;
                if (water > -50f && y < water) y = water - 0.35f; // flaches Wasser: schwimmend
                return new V3(x, y, z);
            }
            return robot;
        }
    }
}
