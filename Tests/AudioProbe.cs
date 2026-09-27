using System;
using System.Diagnostics;
using RePlanet.Core;
public static class AudioProbe
{
    public static void Run(string outDir)
    {
        System.IO.Directory.CreateDirectory(outDir);
        var sw = Stopwatch.StartNew();
        foreach (var id in Synth.SfxIds)
        {
            var d = Synth.Sfx(id);
            float peak = 0; bool nan = false;
            foreach (var v in d) { if (float.IsNaN(v) || float.IsInfinity(v)) nan = true; peak = Math.Max(peak, Math.Abs(v)); }
            Console.WriteLine($"sfx {id,-14} len={d.Length / 44100f:0.00}s peak={peak:0.00} nan={nan}");
            Synth.WriteWav(System.IO.Path.Combine(outDir, "sfx_" + id + ".wav"), d, Synth.SfxRate);
        }
        Console.WriteLine("SFX gesamt " + sw.ElapsedMilliseconds + " ms");
        foreach (var id in new[] { "menu", "terra", "pyra", "pelagia", "nivalis" })
        {
            sw.Restart();
            var m = Synth.Music(id);
            var mix = new float[m.Stems["pad"].Length];
            foreach (var kv in m.Stems) { bool nan = false; foreach (var v in kv.Value) if (float.IsNaN(v)) nan = true; if (nan) Console.WriteLine("NaN in " + id + "/" + kv.Key); for (int i = 0; i < mix.Length; i++) mix[i] += kv.Value[i] * 0.4f; }
            Console.WriteLine($"music {id} {sw.ElapsedMilliseconds} ms, {m.Length}s");
            Synth.WriteWav(System.IO.Path.Combine(outDir, "music_" + id + ".wav"), mix, m.Rate);
        }
        sw.Restart();
        int rate; float len;
        var intro = Synth.IntroScore(out rate, out len);
        Console.WriteLine($"intro {sw.ElapsedMilliseconds} ms {len}s");
        Synth.WriteWav(System.IO.Path.Combine(outDir, "intro.wav"), intro, rate);
    }
}
