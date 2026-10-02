// Laufzeitprüfung TNT und Schätze je Planet: Müllberg mit einem Wurf sprengen (Ladung im Flug/zischend sichtbar, Explosion,
// fliegende Stücke, Stufe beim Client), einen zweiten Spieler mit TNT treffen (Flugbahn/Überschlag beim Client) und einen
// Schatz finden (Funkeln aus der Nähe, Fund-Anzeige).
using System;
using System.Linq;
using RePlanet;
using RePlanet.Core;
using UnityEngine;

public static partial class Checks
{
    static V3 FunFreeSpot(PlanetState ps, V3 near, float minR, float maxR, int seed)
    {
        var l = WorldGen.Get(ps.Id);
        var r = new Rng(seed);
        float water = Terrain.WaterLevel(ps.Id);
        for (int i = 0; i < 3000; i++)
        {
            float a = r.Range(0f, 6.283f), d = r.Range(minR, maxR);
            float x = near.x + RePlanet.Core.M.Cos(a) * d, z = near.z + RePlanet.Core.M.Sin(a) * d;
            if (Math.Abs(x) > 140 || Math.Abs(z) > 140) continue;
            if (x >= l.Base.MinX - 8 && x <= l.Base.MaxX + 8 && z >= l.Base.MinZ - 8 && z <= l.Base.MaxZ + 8) continue;
            if (l.BlockedStatic(x, z, 1.2f) || Rules.HeapAt(ps, new V3(x, 0, z), 1.5f) >= 0) continue;
            float y = l.GroundAt(x, z);
            if (y < water + 0.3f) continue;
            if (Rules.ShelterKind(new WorldState(), ps, new V3(x, y, z)) > 0) continue;
            return new V3(x, y, z);
        }
        return new V3(float.NaN, 0, 0);
    }

    static void FunChecks(GameApp app, Game g, string pid, string planet)
    {
        Info("TNT und Schätze …");
        var ps = g.S.Cur; var l = WorldGen.Get(planet);
        ps.StormActive = false; ps.StormWarn = false; ps.StormTimer = 0;
        SetPhase(g, planet, 0.45f);
        var me = g.S.Players[pid];
        if (me.Vehicle != null) g.Apply(pid, new JObj().Set("a", "vexit").Set("rid", "fx" + planet), true);
        me.Tnt = GameData.TntMaxCarry;
        Run(0.5f);

        // ---------------------------------------------------------------- Müllberg sprengen
        int heap = -1; V3 from = default(V3), dir = default(V3); float charge = 0;
        for (int i = 0; i < l.Mounds.Count && heap < 0; i++)
        {
            if (Rules.MoundBlastCheck(g.S, ps, i) != null) continue;
            var m = l.Mounds[i];
            for (int k = 0; k < 6 && heap < 0; k++)
            {
                var spot = FunFreeSpot(ps, m.Pos, m.Radius + 9f, m.Radius + 13f, 10 + k);
                if (!spot.IsFinite) continue;
                TntFlight f;
                if (Rules.TntAim(ps, spot, m.Pos, m.Radius * 0.6f, out dir, out charge, out f) && (f.Mound == i || Rules.HeapAt(ps, f.Pos, GameData.TntHeapReach) == i)) { heap = i; from = spot; }
            }
        }
        if (heap < 0) Fail("Kein sprengbarer Müllberg mit Wurfposition gefunden");
        else
        {
            Teleport(g, pid, from);
            Run(0.5f);
            int stage0 = ps.Blasts(heap);
            var r = At(g, pid, from, new JObj().Set("a", "tnt").Set("dir", Json.Arr(dir.x, dir.y, dir.z)).Set("s", charge));
            if (!r.Ok) Fail("Wurf abgelehnt: " + r.Err);
            int maxCharges = 0, maxFlyers = 0, maxBooms = 0;
            Run(Math.Max(GameData.TntFuse, (r.Data != null ? r.Data.Float("fl") : 0f) + GameData.TntMinLyingFuse) + 2.5f, 1f / 30f, t =>
            {
                if (TntView.I == null) return;
                maxCharges = Math.Max(maxCharges, TntView.I.Charges); maxFlyers = Math.Max(maxFlyers, TntView.I.Flyers); maxBooms = Math.Max(maxBooms, TntView.I.Booms);
            });
            int pieces = app.W.Cur.Dyn.Values.Count(d => d.Heap == heap + 1);
            Info($"  Müllberg {heap} gesprengt: Stufe {ps.Blasts(heap)} (Client {app.W.Cur.Blasts(heap)}), Stücke beim Client {pieces}, Darstellung: Ladungen {maxCharges}, Explosionen {maxBooms}, fliegende Teile {maxFlyers}, Größe jetzt {Rules.MoundScale(app.W.Cur, heap):0.00}");
            if (ps.Blasts(heap) != stage0 + 1) Fail("Müllberg wurde nicht gesprengt");
            if (app.W.Cur.Blasts(heap) != ps.Blasts(heap)) Fail("Sprengstufe kommt beim Client nicht an");
            if (pieces < 5) Fail("Zu wenige Stücke beim Client (" + pieces + ")");
            if (maxCharges < 1 || maxBooms < 1 || maxFlyers < 5) Fail("TNT-Darstellung unvollständig (Ladung/Explosion/fliegende Teile)");
            if (TrashRenderer.HiddenUntil.Count > 0 && TrashRenderer.HiddenUntil.Values.Any(t => t > Time.time + 0.1f)) Fail("Gelandete Stücke bleiben ausgeblendet");
        }

        // ---------------------------------------------------------------- Mitspieler treffen
        var guest = g.Join("gast_" + planet, "Gast");
        Run(1f);
        var pb = FunFreeSpot(ps, new V3(0, 0, -60), 0, 40, 3);
        V3 pa = new V3(float.NaN, 0, 0);
        for (int k = 0; k < 40 && pb.IsFinite; k++)
        {
            var cand = FunFreeSpot(ps, pb, 9f, 13f, 50 + k);
            TntFlight f;
            if (cand.IsFinite && Rules.TntAim(ps, cand, pb, 1.2f, out dir, out charge, out f) && f.Land == 0 && Rules.HeapAt(ps, f.Pos, GameData.TntHeapReach) < 0) { pa = cand; break; }
        }
        if (!pa.IsFinite || !pb.IsFinite) Fail("Keine Stelle für den Wurf auf den Mitspieler");
        else
        {
            g.S.Players[guest.Id].Pos = pb; g.AllowTeleport(guest.Id);
            Teleport(g, pid, pa);
            Run(1.5f); // Wurf-Abklingzeit, Gast-Roboter erscheint
            var r = At(g, pid, pa, new JObj().Set("a", "tnt").Set("dir", Json.Arr(dir.x, dir.y, dir.z)).Set("s", charge));
            if (!r.Ok) Fail("Wurf auf Mitspieler abgelehnt: " + r.Err);
            float waitHit = Math.Max(GameData.TntFuse, (r.Data != null ? r.Data.Float("fl") : 0f) + GameData.TntMinLyingFuse) + 1.5f;
            int maxKnocks = 0;
            JObj lastBoom = null;
            Action<JObj> hook = f => { if (f.Str("k") == "tntboom") lastBoom = f; };
            app.OnFx += hook;
            Run(waitHit, 1f / 30f, t => { if (TntView.I != null) maxKnocks = Math.Max(maxKnocks, TntView.I.Knocks); });
            app.OnFx -= hook;
            if (lastBoom == null || lastBoom.Arr("hits") == null) Info("  Explosion: " + (lastBoom != null ? Json.Write(lastBoom) : "–") + ", Wurf " + Json.Write(r.Data ?? new JObj()) + ", Gast bei " + g.S.Players[guest.Id].Pos + " (Ziel " + pb + "), Schutz " + Rules.ShelterKind(g.S, ps, g.S.Players[guest.Id].Pos) + ", online " + g.S.Players[guest.Id].Online + ", Fahrzeug " + g.S.Players[guest.Id].Vehicle + ", Abschlepp " + g.S.Players[guest.Id].TowTimer);
            float moved = V3.DistXZ(g.S.Players[guest.Id].Pos, pb);
            Info($"  Mitspieler getroffen: {moved:0.0} m geflogen, benommen {g.Stunned(guest.Id)}, Darstellung Treffer {maxKnocks}, Treffer-Statistik {g.S.Stat("tntHits")}");
            if (moved < 1f) Fail("Mitspieler wurde nicht weggeschleudert");
            if (maxKnocks < 1) Fail("Treffer beim Client nicht dargestellt");
            Run(GameData.TntSootTime + 0.5f);
        }
        g.Leave(guest.Id);
        Run(0.5f);

        // ---------------------------------------------------------------- Schatz finden
        g.S.Tech["grab"] = Math.Max(g.S.TechLevel("grab"), 2); g.S.Tech["hazard"] = Math.Max(g.S.TechLevel("hazard"), 2);
        me.Bin.Clear();
        int glintMax = 0;
        string found = null;
        foreach (var kv in Treasures.Carriers(g.S, planet))
        {
            if (g.S.TreasureFound.Contains(kv.Value)) continue;
            var o = Rules.Obj(ps, "s" + kv.Key);
            if (o == null || o.Underwater || Rules.CollectCheck(g.S, o, "grab", o.Pos, 0) != null) continue;
            var stand = new V3(o.Pos.x + 0.6f, l.GroundAt(o.Pos.x + 0.6f, o.Pos.z), o.Pos.z);
            Teleport(g, pid, stand);
            Run(1f, 1f / 30f, t => { if (TreasureView.I != null) glintMax = Math.Max(glintMax, TreasureView.I.Glints); });
            var r = At(g, pid, stand, new JObj().Set("a", "grab").Set("o", o.Key));
            if (!r.Ok) continue;
            found = kv.Value;
            break;
        }
        int pops = 0;
        Run(1.5f, 1f / 30f, t => { if (TreasureView.I != null) pops = Math.Max(pops, TreasureView.I.Pops); });
        Info($"  Schatz: {(found != null ? GameData.TreasureById[found].Name : "–")}, beim Client gefunden {found != null && app.W.TreasureFound.Contains(found)}, Funkeln {glintMax}, Fund-Anzeige {pops}");
        if (found == null) Fail("Kein Schatz auffindbar");
        else
        {
            if (!app.W.TreasureFound.Contains(found)) Fail("Fund kommt beim Client nicht an");
            if (glintMax < 1) Fail("Schatz-Träger funkelt nicht");
            if (pops < 1) Fail("Fund-Anzeige fehlt");
        }
        Teleport(g, pid, l.Base.Spawn);
        Run(1f);
    }
}
