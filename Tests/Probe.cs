using System;
using System.Collections.Generic;
using RePlanet.Core;
public static class Probe
{
    public static void Run()
    {
        foreach (var pl in GameData.PlanetOrder)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var l = WorldGen.Get(pl);
            int want = 0; for (int a = 0; a < 3; a++) foreach (var s in l.Def.Spawns[a]) want += s.Count;
            var byArea = new int[3]; int uw = 0, fr = 0, gate = 0;
            foreach (var t in l.Trash) { if (t.Gate >= 0) gate++; else byArea[t.Area]++; if (t.Underwater) uw++; if (t.Frozen) fr++; }
            Console.WriteLine($"{pl}: {sw.ElapsedMilliseconds}ms trash={l.Trash.Count} (soll {want}+gates) areas={byArea[0]}/{byArea[1]}/{byArea[2]} gate={gate} uw={uw} frozen={fr} colliders={l.Colliders.Count} props={l.Props.Count} zones:{string.Join(",", l.Zones.ConvertAll(z => z.Objects.Count))} weight={l.AreaWeight[0]}/{l.AreaWeight[1]}/{l.AreaWeight[2]} repairs={l.Repairs.Count} eco={l.Eco.Count} lore={l.LoreSpots.Count} mounds={l.Mounds.Count}");
            var sp = l.Base.Spawn;
            foreach (var b in l.Bots) Console.WriteLine($"   Helfer {b.Id} {b.Pos} Boden {Terrain.HeightAt(pl, b.Pos.x, b.Pos.z):0.00} Wasser {Terrain.WaterLevel(pl)} imStützpunkt {l.Base.InBase(b.Pos.x, b.Pos.z)}");
            Console.WriteLine($"   spawn blocked={l.BlockedStatic(sp.x, sp.z, 0.8f)} h={Terrain.HeightAt(pl, sp.x, sp.z)}");
        }
    }
}
