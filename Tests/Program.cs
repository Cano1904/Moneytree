using RePlanet.Core;
using System;
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "probe") { Probe.Run(); return 0; }
        if (args.Length > 0 && args[0] == "balance") return BalanceRun.Run(args.Length > 1 && args[1] == "mensch");
        if (args.Length > 1 && args[0] == "audio") { AudioProbe.Run(args[1]); return 0; }
        if (args.Length > 1 && args[0] == "loc") { Loc.Lang = "en"; for (int i = 1; i < args.Length; i++) Console.WriteLine(args[i] + "  →  " + Loc.T(args[i])); return 0; }
        if (args.Length > 0 && args[0] == "locmissing") { foreach (var m in LocTests.Missing()) Console.WriteLine(m.Replace("\n", "\\n")); return 0; }
        return TestRunner.RunAll(args);
    }
}
