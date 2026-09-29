using System;
using System.Collections.Generic;
using System.IO;
using RePlanet.Core;

/// <summary>Gemeinsame Hilfen für Speicher-, Netz- und Wettertests.</summary>
public static class TestKit
{
    public static JObj A(string kind) { return new JObj().Set("a", kind); }

    /// <summary>Läuft in kleinen, gültigen Schritten zum Ziel (wie ein echter Spieler, ohne Teleport).</summary>
    public static void WalkTo(Game g, string pid, V3 target, float stop = 0.3f)
    {
        var p = g.S.Players[pid];
        const float dt = 0.1f;
        for (int i = 0; i < 20000; i++)
        {
            float d = V3.DistXZ(p.Pos, target);
            if (d <= stop) return;
            float step = Math.Min(Motor.WalkSpeed * dt, d - stop + 0.01f);
            var np = new V3(p.Pos.x + (target.x - p.Pos.x) / d * step, 0, p.Pos.z + (target.z - p.Pos.z) / d * step);
            np.y = Terrain.HeightAt(g.S.CurrentPlanet, np.x, np.z);
            if (!g.Move(pid, np, 0, false, 0, "grab", dt)) throw new Exception("Bewegung abgelehnt bei " + np);
            g.Tick(dt);
        }
        throw new Exception("WalkTo hängt");
    }

    public static void Ticks(Game g, float seconds, float dt = 0.25f)
    {
        for (float t = 0; t < seconds; t += dt) g.Tick(dt);
    }

    /// <summary>Statische, mit dem Greifarm sammelbare Objekte in Bereich 0 (ohne Tore).</summary>
    public static List<ObjView> Grabbables(Game g, int n, Func<ObjView, bool> extra = null)
    {
        var l = new List<ObjView>();
        foreach (var o in Rules.All(g.S.Cur))
        {
            if (o.Gate >= 0 || !o.IsStatic || o.Area != 0) continue;
            if (extra != null && !extra(o)) continue;
            if (Rules.CollectCheck(g.S, o, "grab", o.Pos, 0) != null) continue;
            l.Add(o);
            if (l.Count >= n) break;
        }
        return l;
    }

    /// <summary>Wurzel des Repositorys (enthält RePlanet/ und docs/), gesucht ab dem Arbeitsverzeichnis.</summary>
    public static string RepoRoot()
    {
        var d = new System.IO.DirectoryInfo(System.IO.Directory.GetCurrentDirectory());
        while (d != null && !(System.IO.Directory.Exists(System.IO.Path.Combine(d.FullName, "RePlanet")) && System.IO.Directory.Exists(System.IO.Path.Combine(d.FullName, "docs")))) d = d.Parent;
        if (d == null) throw new System.Exception("Repository-Wurzel nicht gefunden");
        return d.FullName;
    }

    public static string TempDir(string name)
    {
        var d = Path.Combine(Path.GetTempPath(), "replanet-test-" + name + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(d);
        return d;
    }

    public static void Near(V3 expected, V3 actual, float tol, string msg)
    {
        if (V3.Dist(expected, actual) > tol) throw new AssertException(msg + " (erwartet " + expected + ", erhalten " + actual + ")");
    }

    /// <summary>Spielstand mit gültiger Prüfsumme aus einem beliebigen Nutzdaten-Objekt bauen (z. B. altes Format).</summary>
    public static string Envelope(JObj payloadObj, int version)
    {
        string payload = Json.Write(payloadObj);
        return Json.Write(new JObj().Set("format", SaveCodec.Format).Set("version", version).Set("saved", "2020-01-01 00:00:00")
            .Set("checksum", Hash.Fnv1a(payload).ToString("x8")).Set("meta", new JObj()).Set("payload", payload));
    }
}
