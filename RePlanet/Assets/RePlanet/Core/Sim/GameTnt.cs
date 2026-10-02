using System;
using System.Collections.Generic;

namespace RePlanet.Core
{
    /// <summary>
    /// TNT auf dem Server: Ladungen kaufen (Werkstatt), werfen (Wurfbahn wird hier berechnet), Zündschnur, Explosion.
    /// Eine Explosion am Rand eines Müllbergs zerlegt ihn stufenweise in viele sammelbare Stücke (ein Teil geht als Staub
    /// verloren); Roboter im Sprengradius fliegen harmlos ein Stück durch die Luft und sind kurz benommen (Mitspieler nur,
    /// wenn der Host „TNT trifft Mitspieler“ erlaubt). Gebäude, Projekte, Helfer und Tiere nehmen nie Schaden – Helfer
    /// laufen weg, Tiere fliehen (Darstellung).
    /// </summary>
    public partial class Game
    {
        /// <summary>Spielzeit, bis zu der ein getroffener Roboter benommen ist (keine Werkzeuge, keine Würfe).</summary>
        readonly Dictionary<string, double> stunUntil = new Dictionary<string, double>();
        int tntSeq;

        public bool Stunned(string pid) { double t; return stunUntil.TryGetValue(pid, out t) && t > Now; }

        /// <summary>Aktionen, die benommene Roboter nicht ausführen können.</summary>
        static readonly HashSet<string> StunBlocked = new HashSet<string> { "grab", "vacuum", "magnet", "cut", "thaw", "filter", "plant", "tnt", "botfix", "repair", "venter" };

        ActResult ActBuyTnt(PlayerData p, JObj a)
        {
            if (!InBase(p)) return ActResult.Fail("TNT gibt es in der Werkstatt am Stützpunkt.");
            int n = M.Clamp(a.Int("n", 1), 1, GameData.TntMaxCarry);
            n = Math.Min(n, GameData.TntMaxCarry - p.Tnt);
            if (n <= 0) return ActResult.Fail("Mehr als " + GameData.TntMaxCarry + " Ladungen kann MIKO nicht tragen.");
            long cost = (long)GameData.TntPrice(S.CurrentPlanet) * n;
            if (S.Credits < cost) return ActResult.Fail("Es fehlen " + (cost - S.Credits) + " Credits für TNT.");
            Spend(cost);
            p.Tnt += n;
            DPl(p.Id);
            S.AddStat("tntBought", n); DW("stats");
            Fx(new JObj().Set("k", "tntbuy").Set("pid", p.Id).Set("n", n).Set("c", cost));
            return ActResult.OK(new JObj().Set("n", n).Set("tnt", p.Tnt));
        }

        ActResult ActTntHits(JObj a, bool isHost)
        {
            if (!isHost) return ActResult.Fail("Nur der Host kann das ändern.");
            S.TntHitsPlayers = a.Bool("v", true);
            DW("tnt");
            return ActResult.OK();
        }

        /// <summary>Wurf: „dir“ = Wurfrichtung (x, y, z), „s“ = Wurfkraft 0…1. Die Bahn rechnet der Server.</summary>
        ActResult ActThrowTnt(PlayerData p, JObj a)
        {
            var why = Rules.TntThrowCheck(S, S.Cur, p);
            if (why != null) return ActResult.Fail(why);
            if (Since(p.Id, "tnt") < GameData.TntThrowCooldown) return ActResult.Fail("Die nächste Ladung ist gleich bereit …");
            var d = a.Floats("dir");
            var dir = d != null && d.Length >= 3 ? new V3(d[0], d[1], d[2]) : new V3(M.Sin(p.Yaw) * 0.7f, 0.7f, M.Cos(p.Yaw) * 0.7f);
            if (!dir.IsFinite) return ActResult.Fail("Ungültige Wurfrichtung.");
            float charge = M.Clamp01(a.Float("s", 0.5f));
            var vel = Rules.TntVelocity(dir, charge);
            var from = Rules.TntStart(p.Pos, vel);
            var fl = Rules.TntSimulate(S.Cur, from, vel);
            Mark(p.Id, "tnt");
            p.Tnt--;
            p.Sleeping = false; p.Waiting = false;
            DPl(p.Id);
            var c = new TntCharge
            {
                Id = "t" + (++tntSeq) + "_" + ((long)(Now * 10) % 100000), By = p.Id, From = from, Vel = vel, Pos = fl.Pos, Flight = fl.Time,
                Fuse = Math.Max(GameData.TntFuse, fl.Time + GameData.TntMinLyingFuse), Mound = fl.Mound, Land = fl.Land
            };
            S.Cur.Tnt.Add(c);
            DP("tnt");
            S.AddStat("tntThrown", 1); DW("stats");
            Fx(new JObj().Set("k", "tntthrow").Set("id", c.Id).Set("pid", p.Id).Set("from", from.ToJson(2)).Set("v", vel.ToJson(3))
                .Set("pos", c.Pos.ToJson(2)).Set("fl", Json.R(c.Flight, 3)).Set("fu", Json.R(c.Fuse, 3)).Set("l", c.Land).Set("m", c.Mound));
            return ActResult.OK(new JObj().Set("id", c.Id).Set("pos", c.Pos.ToJson(2)).Set("fl", Json.R(c.Flight, 3)).Set("land", c.Land).Set("m", c.Mound).Set("tnt", p.Tnt));
        }

        void TickTnt(float dt)
        {
            var ps = S.Cur;
            if (ps.Tnt.Count == 0) return;
            for (int i = 0; i < ps.Tnt.Count; i++)
            {
                var c = ps.Tnt[i];
                c.Age += dt;
                if (c.Age >= c.Flight && c.Land != 1 && c.Land != 3) ShooHelpers(ps, c.Pos, dt);
                if (c.Age < c.Fuse) continue;
                ps.Tnt.RemoveAt(i); i--;
                Explode(ps, c);
            }
        }

        /// <summary>Helfer in der Nähe einer zischenden Ladung laufen davon (sie nehmen nie Schaden).</summary>
        void ShooHelpers(PlanetState ps, V3 at, float dt)
        {
            float safe = GameData.TntKnockRadius + 1.5f;
            foreach (var b in ps.Bots.Values)
            {
                float d = V3.DistXZ(b.Pos, at);
                if (d >= safe) continue;
                float dx = d > 0.1f ? (b.Pos.x - at.x) / d : 1f, dz = d > 0.1f ? (b.Pos.z - at.z) / d : 0f;
                MoveBot(b, new V3(at.x + dx * (safe + 1f), b.Pos.y, at.z + dz * (safe + 1f)), GameData.HelperFollowSpeed, 0.2f, dt);
                b.Target = null;
                if (b.State == 1) b.State = 0;
                DP("bots");
            }
        }

        void Explode(PlanetState ps, TntCharge c)
        {
            DP("tnt");
            var f = new JObj().Set("k", "tntboom").Set("id", c.Id).Set("pid", c.By).Set("pos", c.Pos.ToJson(2));
            // Erloschen: im Wasser versunken bzw. im Stützpunkt (dort wird nicht gesprengt – die Ladung kommt zurück)
            if (c.Land == 1 || c.Land == 3 || WorldGen.Get(ps.Id).Base.InBase(c.Pos.x, c.Pos.z))
            {
                f["fizzle"] = c.Land == 1 ? "water" : "base";
                PlayerData owner;
                if (c.Land != 1 && c.By != null && S.Players.TryGetValue(c.By, out owner) && owner.Tnt < GameData.TntMaxCarry) { owner.Tnt++; DPl(owner.Id); f["refund"] = true; }
                Fx(f);
                return;
            }
            S.AddStat("tntBooms", 1); DW("stats");
            // Müllberg
            int mound = c.Mound >= 0 ? c.Mound : Rules.HeapAt(ps, c.Pos, GameData.TntHeapReach);
            if (mound >= 0 && V3.DistXZ(c.Pos, WorldGen.Get(ps.Id).Mounds[mound].Pos) - WorldGen.Get(ps.Id).Mounds[mound].Radius * Rules.MoundScale(ps, mound) > GameData.TntHeapReach + 0.01f) mound = -1;
            if (mound >= 0)
            {
                f["m"] = mound;
                var why = Rules.MoundBlastCheck(S, ps, mound);
                if (why != null) f["why"] = why;
                else BlastMound(ps, mound, c, f);
            }
            // Roboter im Sprengradius: fliegen harmlos durch die Luft, kurz benommen
            var hits = new List<object>();
            PlayerData thrower;
            S.Players.TryGetValue(c.By ?? "", out thrower);
            foreach (var p in S.Players.Values)
            {
                if (!p.Online || p.Vehicle != null || p.TowTimer > 0) continue;
                if (V3.DistXZ(p.Pos, c.Pos) >= GameData.TntKnockRadius || Math.Abs(p.Pos.y - c.Pos.y) > 4f) continue;
                bool self = p.Id == c.By;
                if (!self && !S.TntHitsPlayers) continue;
                if (Rules.ShelterKind(S, ps, p.Pos) > 0) continue; // im Unterschlupf geschützt
                var from = p.Pos;
                var to = Rules.TntKnockTarget(ps, c.Pos, p.Pos, thrower != null ? thrower.Yaw : 0f);
                p.Pos = to;
                p.Sleeping = false; p.Waiting = false;
                stunUntil[p.Id] = Now + GameData.TntKnockAir + GameData.TntStun;
                moveBudget.Remove(p.Id);
                DPl(p.Id);
                hits.Add(Json.Arr(p.Id, Json.R(from.x), Json.R(from.y), Json.R(from.z), Json.R(to.x), Json.R(to.y), Json.R(to.z), self ? 1 : 0));
                if (!self) { S.AddStat("tntHits", 1); DW("stats"); }
            }
            if (hits.Count > 0) f["hits"] = hits;
            // Helfer, die es nicht rechtzeitig weggeschafft haben, stehen danach am Rand (unversehrt)
            float safe = GameData.TntKnockRadius + 1f;
            foreach (var b in ps.Bots.Values)
            {
                float d = V3.DistXZ(b.Pos, c.Pos);
                if (d >= safe) continue;
                float dx = d > 0.1f ? (b.Pos.x - c.Pos.x) / d : 1f, dz = d > 0.1f ? (b.Pos.z - c.Pos.z) / d : 0f;
                float nx = M.Clamp(c.Pos.x + dx * safe, -146f, 146f), nz = M.Clamp(c.Pos.z + dz * safe, -146f, 146f);
                b.Pos = new V3(nx, Math.Max(WorldGen.Get(ps.Id).GroundAt(nx, nz), Terrain.WaterLevel(ps.Id)), nz);
                b.Target = null; if (b.State == 1) b.State = 0;
                DP("bots");
            }
            Fx(f);
        }

        /// <summary>Eine Sprengstufe: Müllberg schrumpft, Stücke aus der Müllliste seines Bereichs fliegen ringsum auf trockenen Boden.</summary>
        void BlastMound(PlanetState ps, int mound, TntCharge c, JObj f)
        {
            var l = WorldGen.Get(ps.Id);
            var m = l.Mounds[mound];
            float scaleBefore = Rules.MoundScale(ps, mound);
            int stage = ps.Blasts(mound) + 1;
            ps.MoundBlasts[mound] = stage;
            ps.MoundCool[mound] = Now + GameData.TntHeapCooldown;
            DP("tnt");
            S.AddStat("tntBlasts", 1); DW("stats");
            // Müllarten des Bereichs – nur kleine Stücke (greifbar oder magnetisch, ≤ 6 kg; ohne Wracks, Öl, Treibgut, schwere Gefahrstoffe)
            var types = new List<string>();
            foreach (var s in GameData.Planets[ps.Id].Spawns[m.Area])
            {
                var t = GameData.Trash[s.Type];
                if (t.Crane || t.Oil || t.Floating || t.Hazard > 1 || t.Mass > 6f) continue;
                if (t.Grab || t.Magnet) types.Add(t.Id);
            }
            if (types.Count == 0) types.Add(ps.Id == "pyra" ? "schrauben" : "dose");
            int count = GameData.TntPiecesPerStage + (m.Radius >= 7f ? 2 : 0);
            int keep = (int)Math.Round(count * (1f - GameData.TntDustShare));
            var r = new Rng(GameData.Planets[ps.Id].Seed * 131 + mound * 977 + stage * 31 + (int)(S.NextDyn % 100000));
            float scaleAfter = Rules.MoundScale(ps, mound);
            float inner = m.Radius * Math.Max(scaleAfter, 0.2f) + 0.8f, outer = m.Radius * Math.Max(scaleBefore, 0.4f) + 4.5f;
            float water = Terrain.WaterLevel(ps.Id);
            var pieces = new List<object>();
            // Richtung zum Werfer hin etwas bevorzugen (Stücke fliegen „auf die Seite der Explosion“)
            float baseAng = M.Atan2(c.Pos.z - m.Pos.z, c.Pos.x - m.Pos.x);
            for (int i = 0, tries = 0; i < keep && tries < keep * 12; tries++)
            {
                float ang = baseAng + (r.Next() - 0.5f) * 5.2f;
                float rad = M.Lerp(inner, outer, M.Sqrt(r.Next()));
                float x = m.Pos.x + M.Cos(ang) * rad, z = m.Pos.z + M.Sin(ang) * rad;
                if (Math.Abs(x) > 145f || Math.Abs(z) > 145f) continue;
                if (l.Base.InBase(x, z) || l.BlockedStatic(x, z, 0.6f)) continue;
                float y = l.GroundAt(x, z);
                if (water > -50f && y < water + 0.15f) continue;
                string type = types[r.Range(0, types.Count)];
                var d = SpawnDyn(type, new V3(x, y, z), m.Area, false, false);
                d.Heap = mound + 1;
                UpdateDyn(d);
                pieces.Add(Json.Arr(d.Id, type, Json.R(x), Json.R(y), Json.R(z)));
                i++;
            }
            f["stage"] = stage;
            f["stages"] = GameData.MoundStages(m);
            f["pieces"] = pieces;
            f["dust"] = count - pieces.Count;
            f["mpos"] = m.Pos.ToJson(1);
            f["area"] = m.Area;
        }

        /// <summary>Stück eines gesprengten Müllbergs eingesammelt: zählt (gedeckelt) zum Hauptmüll seines Bereichs.</summary>
        void OnHeapPieceRemoved(PlanetState ps, DynObj d)
        {
            if (d == null || d.Heap <= 0 || d.Area < 0 || d.Area > 2) return;
            ps.HeapWeight[d.Area] += WorldGen.Weight(d.Type);
            DP("tnt");
            CheckAreaClean(ps, d.Area);
        }
    }
}
