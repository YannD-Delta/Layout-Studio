// Minimal test harness: runs every public static void method with no parameters of every public static class
// whose name ends with "Tests". A test fails when it throws. Exit code is the number of failures (capped at 1).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public static class Assert
{
    public static void Equal<T>(T expected, T actual, string message = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception((message == null ? "" : message + ": ") + "expected <" + expected + "> but was <" + actual + ">");
    }

    public static void True(bool condition, string message = null)
    {
        if (!condition) throw new Exception(message ?? "expected true");
    }

    public static void False(bool condition, string message = null)
    {
        if (condition) throw new Exception(message ?? "expected false");
    }

    public static void Throws<TException>(Action action) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new Exception("expected " + typeof(TException).Name + " but nothing was thrown");
    }
}

public static class Harness
{
    public static int Main(string[] args)
    {
        string filter = args.Length > 0 ? args[0] : null;
        int passed = 0, failed = 0;
        var suites = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsClass && t.IsAbstract && t.IsSealed && t.IsPublic && t.Name.EndsWith("Tests"));
        foreach (var suite in suites)
        {
            foreach (var m in suite.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(x => x.GetParameters().Length == 0 && x.ReturnType == typeof(void) && x.DeclaringType == suite))
            {
                string name = suite.Name + "." + m.Name;
                if (filter != null && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                try
                {
                    m.Invoke(null, null);
                    passed++;
                    Console.WriteLine("PASS " + name);
                }
                catch (TargetInvocationException ex)
                {
                    // the exported Layout Studio repository has no theme folder: a test that reads a theme file is skipped there
                    var missing = ex.InnerException as System.IO.IOException;
                    if (missing != null && (missing is System.IO.FileNotFoundException || missing is System.IO.DirectoryNotFoundException)
                        && missing.Message.Replace('/', '\\').IndexOf("\\theme\\", StringComparison.OrdinalIgnoreCase) >= 0
                        && !System.IO.Directory.Exists(System.IO.Path.Combine(TestPaths.RepoRoot(), "theme")))
                    {
                        Console.WriteLine("SKIP " + name + " (needs the theme folder)");
                        continue;
                    }
                    failed++;
                    Console.WriteLine("FAIL " + name + " - " + ex.InnerException.Message);
                }
            }
        }
        Console.WriteLine(passed + " passed, " + failed + " failed");
        return failed == 0 ? 0 : 1;
    }
}
