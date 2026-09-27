using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using RePlanet.Core;

/// <summary>
/// Prüf- und Exportwerkzeug für die prozedurale Audioerzeugung:
/// dotnet RePlanet.Tests.dll audio &lt;ordner&gt; [sfx|music|intro|all] [id …]
/// Schreibt WAVs (SFX, jeder Musik-Stem einzeln und als Mix, Intro), misst Erzeugungszeit, Spitzen, NaN/Inf,
/// RMS-Verlauf je 4 s und die Nahtstelle der Loops.
/// </summary>
public static class AudioProbe
{
    public static int Failures;

    public static void Run(string outDir, string what = "all", string[] only = null)
    {
        // Auswahl ohne Änderung an Program.cs: Umgebungsvariable AUDIOPROBE="music terra pyra" (Art, dann IDs)
        var env = Environment.GetEnvironmentVariable("AUDIOPROBE");
        if (what == "all" && only == null && !string.IsNullOrWhiteSpace(env))
        {
            var parts = env.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            what = parts[0];
            only = new string[parts.Length - 1];
            Array.Copy(parts, 1, only, 0, only.Length);
        }
        Directory.CreateDirectory(outDir);
        var report = new StringBuilder();
        var sw = Stopwatch.StartNew();
        if (what == "all" || what == "sfx")
        {
            foreach (var id in Synth.SfxIds)
            {
                if (only != null && only.Length > 0 && Array.IndexOf(only, id) < 0) continue;
                sw.Restart();
                var d = Synth.Sfx(id);
                long ms = sw.ElapsedMilliseconds;
                float peak; bool bad; Check(d, out peak, out bad);
                double rms = Rms(d, 0, d.Length);
                string seam = id.Contains("loop") || Synth.IsAmbience(id) ? " naht=" + Seam(d).ToString("0.00") : "";
                Line(report, $"sfx {id,-15} {d.Length / (float)Synth.SfxRate,5:0.00}s {ms,5} ms peak={peak:0.00} rms={rms:0.000}{seam}{(bad ? " NAN/INF!" : "")}");
                if (bad) Failures++;
                Synth.WriteWav(Path.Combine(outDir, "sfx_" + id + ".wav"), d, Synth.SfxRate);
            }
        }
        if (what == "all" || what == "music")
        {
            foreach (var id in Synth.MusicIds)
            {
                if (only != null && only.Length > 0 && Array.IndexOf(only, id) < 0) continue;
                sw.Restart();
                var m = Synth.Music(id);
                long ms = sw.ElapsedMilliseconds;
                int n = m.Stems["pad"].Length;
                var mix = new float[n];
                var sb = new StringBuilder();
                foreach (var name in Synth.StemNames)
                {
                    var s = m.Stems[name];
                    float peak; bool bad; Check(s, out peak, out bad);
                    if (bad) { Failures++; sb.Append(" NAN in " + name); }
                    for (int i = 0; i < n; i++) mix[i] += s[i];
                    sb.Append($" {name}={m.Rms[name]:0.000}/{peak:0.00}");
                    Synth.WriteWav(Path.Combine(outDir, "music_" + id + "_" + name + ".wav"), s, m.Rate);
                }
                float mp; bool mb; Check(mix, out mp, out mb);
                Line(report, $"music {id,-8} {m.Length:0.0}s {m.Bpm:0} BPM {ms} ms  mixPeak={mp:0.00} naht={Seam(mix):0.00}  rms/peak:{sb}");
                Line(report, "  RMS je 4 s (dBFS): " + Profile(mix, m.Rate, 4f));
                // zwei Durchläufe hintereinander → Naht hörbar prüfbar
                var twice = new float[n * 2];
                Array.Copy(mix, twice, n); Array.Copy(mix, 0, twice, n, n);
                Synth.WriteWav(Path.Combine(outDir, "music_" + id + ".wav"), twice, m.Rate);
                if (mp > 1f) Failures++;
            }
        }
        if (what == "all" || what == "intro")
        {
            sw.Restart();
            int rate; float len;
            var intro = Synth.IntroScore(out rate, out len);
            long ms = sw.ElapsedMilliseconds;
            float peak; bool bad; Check(intro, out peak, out bad);
            if (bad) Failures++;
            Line(report, $"intro {ms} ms {len}s ({intro.Length / (float)rate:0.0}s Puffer) peak={peak:0.00}{(bad ? " NAN/INF!" : "")}");
            Line(report, "  RMS je 4 s (dBFS): " + Profile(intro, rate, 4f));
            Synth.WriteWav(Path.Combine(outDir, "intro.wav"), intro, rate);
        }
        File.WriteAllText(Path.Combine(outDir, "report.txt"), report.ToString());
        Console.WriteLine(Failures == 0 ? "Audio OK" : "Audio: " + Failures + " Fehler");
    }

    static void Line(StringBuilder sb, string s) { Console.WriteLine(s); sb.AppendLine(s); }

    static void Check(float[] d, out float peak, out bool bad)
    {
        peak = 0; bad = false;
        foreach (var v in d) { if (float.IsNaN(v) || float.IsInfinity(v)) bad = true; else peak = Math.Max(peak, Math.Abs(v)); }
    }

    static double Rms(float[] d, int a, int b)
    {
        double s = 0; int n = 0;
        for (int i = Math.Max(0, a); i < Math.Min(d.Length, b); i++) { s += d[i] * (double)d[i]; n++; }
        return n > 0 ? Math.Sqrt(s / n) : 0;
    }

    static string Profile(float[] d, int rate, float win)
    {
        var sb = new StringBuilder();
        int w = (int)(win * rate);
        for (int a = 0; a < d.Length; a += w)
        {
            double r = Rms(d, a, a + w);
            sb.Append(r > 1e-6 ? (20 * Math.Log10(r)).ToString("0") : "-inf").Append(' ');
        }
        return sb.ToString();
    }

    /// <summary>Sprung an der Loop-Naht im Verhältnis zur typischen Sampledifferenz (≈1 = unauffällig).</summary>
    static double Seam(float[] d)
    {
        double s = 0; int n = 0;
        for (int i = 1; i < d.Length; i += 7) { s += Math.Abs(d[i] - d[i - 1]); n++; }
        double typ = s / Math.Max(1, n);
        return typ > 1e-9 ? Math.Abs(d[0] - d[d.Length - 1]) / typ : 0;
    }
}
