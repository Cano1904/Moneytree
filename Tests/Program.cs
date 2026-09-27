using System;
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "probe") { Probe.Run(); return 0; }
        return TestRunner.RunAll(args);
    }
}
