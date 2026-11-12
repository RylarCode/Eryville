using System;
using System.Linq;

// dotnet run -- [part of a suite name]   runs the suites (all of them, or those whose name contains the text); -v prints sections
public static class Program
{
    public static int Main(string[] args)
    {
        T.Verbose = args.Contains("-v");
        string filter = args.FirstOrDefault(a => !a.StartsWith("-")) ?? "";
        foreach (var (name, body) in Suites.All)
        {
            if (filter.Length > 0 && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
            T.Suite(name, body);
        }
        Console.WriteLine();
        Console.WriteLine(T.Failed == 0 ? $"{T.Passed}/{T.Passed} checks passed" : $"{T.Passed}/{T.Passed + T.Failed} checks passed - {T.Failed} FAILED");
        return T.Failed == 0 ? 0 : 1;
    }
}
