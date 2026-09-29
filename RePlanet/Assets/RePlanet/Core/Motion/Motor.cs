using System;
using System.Collections.Generic;

namespace RePlanet.Core
{
    /// <summary>Kollisionsumgebung für die Bewegung (statische Welt + aktueller Zustand).</summary>
    public class MotorEnv
    {
        public string Planet;
        public PlanetLayout L;
        public float Water = -100f;
        public bool[] GateOpen = new bool[2];
        public int ActiveDuneSet = -1;
        public readonly List<Box> Extra = new List<Box>();
        public readonly List<float[]> Circles = new List<float[]>(); // x, z, r
        readonly List<Box> tmp = new List<Box>();

        public MotorEnv(string planet)
        {
            Planet = planet;
            L = WorldGen.Get(planet);
            Water = Terrain.WaterLevel(planet);
        }

        /// <summary>Übernimmt Tore, Dünen, Gebäude und Müllberge aus dem Weltzustand.</summary>
        public void Sync(WorldState w, float[] moundScale)
        {
            var ps = w.Planet(Planet);
            GateOpen[0] = Rules.GateOpen(ps, 0);
            GateOpen[1] = Rules.GateOpen(ps, 1);
            ActiveDuneSet = GameData.Planets[Planet].Storms ? ps.StormCount % 2 : -1;
            Extra.Clear();
            var bl = L.Base;
            foreach (var b in ps.Buildings)
            {
                var c = bl.CellCenter(b.Gx, b.Gz, b.W, b.H);
                if (b.Type == "foerderband") continue; // befahrbar
                Extra.Add(new Box { Cx = c.x, Cz = c.z, Hx = b.W * bl.Cell * 0.5f - 0.1f, Hz = b.H * bl.Cell * 0.5f - 0.1f, Y0 = bl.Center.y - 1, H = 6, Kind = "building" });
            }
            Circles.Clear();
            for (int i = 0; i < L.Mounds.Count; i++)
            {
                float s = moundScale != null && i < moundScale.Length ? moundScale[i] : 1f;
                if (s < 0.15f) continue;
                var m = L.Mounds[i];
                Circles.Add(new[] { m.Pos.x, m.Pos.z, m.Radius * s * 0.85f });
            }
        }

        public bool Solid(Box b)
        {
            if (!b.Solid) return false;
            if (b.Gate >= 0 && GateOpen[b.Gate]) return false;
            if (b.DuneSet >= 0 && b.DuneSet != ActiveDuneSet) return false;
            return true;
        }

        public void Query(float x, float z, float r, List<Box> result)
        {
            L.Query(x, z, r, result);
            foreach (var b in Extra) if (b.Contains(x, z, r)) result.Add(b);
        }

        /// <summary>Bodenhöhe: Gelände oder begehbare Fläche darüber (Schiffsrampe, Laderaum).</summary>
        public float Ground(float x, float z) { return L.GroundAt(x, z); }

        public List<Box> Tmp { get { return tmp; } }
    }

    public struct MoverState
    {
        public V3 Pos, Vel;
        public float Yaw, Speed;
        public bool Swimming, Diving, Blocked;
    }

    public static class Motor
    {
        public const float RobotRadius = 0.8f;
        public const float WalkSpeed = 6.5f;
        public const float SprintSpeed = 9.5f;

        /// <summary>
        /// Bewegt MIKO. (mx, mz) = gewünschte Richtung in Weltkoordinaten (Länge 0..1), vertical = Tauchen (−1 runter, +1 hoch).
        /// </summary>
        public static void StepRobot(ref MoverState s, MotorEnv env, float mx, float mz, bool sprint, float vertical, float speedMul, bool canDive, float dt, float windX = 0f, float windZ = 0f)
        {
            if (dt <= 0) return;
            dt = Math.Min(dt, 0.1f);
            float len = M.Sqrt(mx * mx + mz * mz);
            if (len > 1) { mx /= len; mz /= len; len = 1; }
            float ground = env.Ground(s.Pos.x, s.Pos.z);
            bool water = ground < env.Water - 0.6f;
            float max = (sprint ? SprintSpeed : WalkSpeed) * speedMul * (water ? 0.62f : 1f);
            float tx = mx * max, tz = mz * max;
            float accel = len > 0.05f ? 22f : 16f;
            s.Vel.x = Approach(s.Vel.x, tx, accel * dt);
            s.Vel.z = Approach(s.Vel.z, tz, accel * dt);
            var np = new V3(s.Pos.x + (s.Vel.x + windX) * dt, s.Pos.y, s.Pos.z + (s.Vel.z + windZ) * dt);
            Collide(ref np, RobotRadius, env, s.Pos.y);
            np.x = M.Clamp(np.x, -149f, 149f);
            np.z = M.Clamp(np.z, -149f, 149f);
            float g = env.Ground(np.x, np.z);
            bool inWater = g < env.Water - 0.6f;
            s.Swimming = inWater;
            if (inWater)
            {
                float surface = env.Water - 0.35f;
                float bottom = g + 0.45f;
                if (canDive && (vertical < -0.1f || s.Diving))
                {
                    float y = s.Pos.y + vertical * 3.2f * dt;
                    if (!s.Diving) y = Math.Min(y, surface - 0.05f);
                    np.y = M.Clamp(y, bottom, surface);
                    s.Diving = np.y < env.Water - 1.1f;
                    if (np.y >= surface - 0.01f) s.Diving = false;
                }
                else
                {
                    np.y = M.Lerp(s.Pos.y, surface, Math.Min(1f, dt * 6f));
                    s.Diving = false;
                }
            }
            else
            {
                s.Diving = false;
                // Gelände folgen (weich)
                np.y = M.Lerp(s.Pos.y, g, Math.Min(1f, dt * 14f));
                if (np.y < g) np.y = g;
            }
            float moved = V3.DistXZ(np, s.Pos);
            s.Blocked = len > 0.2f && moved < max * dt * 0.2f;
            s.Pos = np;
            float sp = M.Sqrt(s.Vel.x * s.Vel.x + s.Vel.z * s.Vel.z);
            s.Speed = sp;
            if (sp > 0.3f)
            {
                float targetYaw = M.Atan2(s.Vel.x, s.Vel.z);
                s.Yaw = M.MoveTowardsAngle(s.Yaw, targetYaw, 9f * dt);
            }
        }

        /// <summary>Fahrzeugsteuerung: throttle −1..1, steer −1..1. Boote fahren nur auf Wasser, Landfahrzeuge nicht im tiefen Wasser.</summary>
        public static void StepVehicle(ref MoverState s, MotorEnv env, VehicleDef def, float throttle, float steer, float dt)
        {
            if (dt <= 0) return;
            dt = Math.Min(dt, 0.1f);
            float max = def.Speed;
            float target = throttle * (throttle >= 0 ? max : max * 0.45f);
            float accel = Math.Abs(target) > Math.Abs(s.Speed) ? 7f : 11f;
            s.Speed = Approach(s.Speed, target, accel * dt);
            float steerRate = 1.9f * M.Clamp(Math.Abs(s.Speed) / 3f, 0f, 1f) * (def.Id == "crane" ? 0.8f : 1f);
            s.Yaw = M.WrapAngle(s.Yaw + steer * steerRate * dt * Math.Sign(s.Speed == 0 ? 1 : s.Speed));
            float fx = M.Sin(s.Yaw), fz = M.Cos(s.Yaw);
            var np = new V3(s.Pos.x + fx * s.Speed * dt, s.Pos.y, s.Pos.z + fz * s.Speed * dt);
            Collide(ref np, def.Radius, env, s.Pos.y);
            np.x = M.Clamp(np.x, -148f, 148f);
            np.z = M.Clamp(np.z, -148f, 148f);
            float g = env.Ground(np.x, np.z);
            bool ok;
            if (def.Water) ok = g < env.Water - 0.5f;
            else ok = g > env.Water - 0.9f;
            if (!ok)
            {
                np = new V3(s.Pos.x, s.Pos.y, s.Pos.z);
                s.Speed = 0;
                g = env.Ground(np.x, np.z);
            }
            float moved = V3.DistXZ(np, s.Pos);
            s.Blocked = Math.Abs(throttle) > 0.3f && moved < Math.Abs(target) * dt * 0.15f;
            if (s.Blocked && moved < 0.001f) s.Speed *= 0.5f;
            np.y = def.Water ? env.Water : M.Lerp(s.Pos.y, Math.Max(g, env.Water > -50 ? env.Water - 0.9f : g), Math.Min(1f, dt * 10f));
            if (!def.Water && np.y < g) np.y = g;
            s.Pos = np;
            s.Vel = new V3(fx * s.Speed, 0, fz * s.Speed);
        }

        static float Approach(float v, float target, float step)
        {
            if (v < target) return Math.Min(v + step, target);
            return Math.Max(v - step, target);
        }

        /// <summary>Kreis gegen Quader/Kreise, mit Höhenprüfung (Unterwasser-Ruinen blockieren nur Taucher).</summary>
        public static void Collide(ref V3 p, float r, MotorEnv env, float y)
        {
            var list = env.Tmp;
            for (int iter = 0; iter < 3; iter++)
            {
                list.Clear();
                env.Query(p.x, p.z, r + 0.5f, list);
                bool any = false;
                foreach (var b in list)
                {
                    if (!env.Solid(b)) continue;
                    if (y + 1.6f < b.Y0 || y + 0.2f > b.Y0 + b.H) continue;
                    float cx = M.Clamp(p.x, b.Cx - b.Hx, b.Cx + b.Hx);
                    float cz = M.Clamp(p.z, b.Cz - b.Hz, b.Cz + b.Hz);
                    float dx = p.x - cx, dz = p.z - cz;
                    float d2 = dx * dx + dz * dz;
                    if (d2 >= r * r) continue;
                    any = true;
                    if (d2 > 1e-6f)
                    {
                        float d = M.Sqrt(d2);
                        float push = r - d;
                        p.x += dx / d * push; p.z += dz / d * push;
                    }
                    else
                    {
                        // Mittelpunkt im Quader: zur nächsten Kante schieben
                        float ex = b.Hx - Math.Abs(p.x - b.Cx), ez = b.Hz - Math.Abs(p.z - b.Cz);
                        if (ex < ez) p.x += Math.Sign(p.x - b.Cx == 0 ? 1 : p.x - b.Cx) * (ex + r);
                        else p.z += Math.Sign(p.z - b.Cz == 0 ? 1 : p.z - b.Cz) * (ez + r);
                    }
                }
                foreach (var c in env.Circles)
                {
                    float dx = p.x - c[0], dz = p.z - c[1];
                    float rr = r + c[2];
                    float d2 = dx * dx + dz * dz;
                    if (d2 >= rr * rr || d2 < 1e-6f) continue;
                    float d = M.Sqrt(d2);
                    p.x += dx / d * (rr - d); p.z += dz / d * (rr - d);
                    any = true;
                }
                if (!any) break;
            }
        }
    }
}
