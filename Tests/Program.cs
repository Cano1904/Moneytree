using System;
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "probe") { Probe.Run(); return 0; }
        if (args.Length > 0 && args[0] == "balance") return BalanceRun.Run();
        if (args.Length > 1 && args[0] == "audio") { AudioProbe.Run(args[1]); return 0; }
        return TestRunner.RunAll(args);
    }
}
