using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

[AttributeUsage(AttributeTargets.Method)]
public class TestAttribute : Attribute { }

public class AssertException : Exception { public AssertException(string m) : base(m) { } }

public static class Assert
{
    public static void True(bool c, string msg) { if (!c) throw new AssertException(msg); }
    public static void False(bool c, string msg) { if (c) throw new AssertException(msg); }
    public static void Equal<T>(T expected, T actual, string msg)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new AssertException(msg + " (erwartet " + expected + ", erhalten " + actual + ")");
    }
}

/// <summary>Minimaler Testläufer ohne externe Pakete. Ergebnisse gehen zusätzlich nach test-results.txt.</summary>
public static class TestRunner
{
    public static int RunAll(string[] filter)
    {
        var tests = Assembly.GetExecutingAssembly().GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.GetCustomAttribute<TestAttribute>() != null)
            .Where(m => filter.Length == 0 || filter.Any(f => (m.DeclaringType.Name + "." + m.Name).Contains(f)))
            .OrderBy(m => m.DeclaringType.Name).ThenBy(m => m.Name).ToList();
        int pass = 0, fail = 0;
        var lines = new List<string>();
        foreach (var m in tests)
        {
            var sw = Stopwatch.StartNew();
            string name = m.DeclaringType.Name + "." + m.Name;
            try
            {
                m.Invoke(null, null);
                pass++;
                lines.Add("BESTANDEN  " + name + " (" + sw.ElapsedMilliseconds + " ms)");
            }
            catch (TargetInvocationException e)
            {
                fail++;
                lines.Add("FEHLER     " + name + ": " + e.InnerException.Message);
                lines.Add(e.InnerException.StackTrace?.Split('\n').FirstOrDefault()?.Trim() ?? "");
            }
            Console.WriteLine(lines[lines.Count - 1 - (lines[lines.Count - 1].StartsWith("BEST") || lines[lines.Count - 1].StartsWith("FEHL") ? 0 : 1)]);
        }
        lines.Add("");
        lines.Add("Gesamt: " + pass + " bestanden, " + fail + " fehlgeschlagen");
        Console.WriteLine(lines[lines.Count - 1]);
        System.IO.File.WriteAllLines("test-results.txt", lines);
        return fail == 0 ? 0 : 1;
    }
}
