using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

// The test runner: suites of checks, each started on a clean slate. A failed check is reported and the run goes on;
// an exception ends that suite (and is reported as a failure).
public static class T
{
    public static int Passed, Failed;
    public static readonly List<string> Failures = new List<string>();
    private static string suite = "", section = "";

    public static void Section(string name)
    {
        section = name;
        if (Verbose) Console.WriteLine($"  -- {name}");
    }

    public static bool Verbose;

    public static bool Check(bool condition, string what)
    {
        if (condition) { Passed++; return true; }
        Failed++;
        string line = $"{suite} / {section}: {what}";
        Failures.Add(line);
        Console.WriteLine("  FAIL: " + line);
        return false;
    }

    public static bool Near(float a, float b, float tolerance = 1e-3f) => Math.Abs(a - b) <= tolerance;

    // Runs one suite on a clean world
    public static void Suite(string name, Action body)
    {
        suite = name; section = "";
        Fresh();
        Stopwatch sw = Stopwatch.StartNew();
        int before = Passed + Failed;
        try { body(); }
        catch (Exception e)
        {
            Failed++;
            string line = $"{suite} / {section}: threw {e.GetType().Name}: {e.Message}";
            Failures.Add(line);
            Console.WriteLine("  FAIL: " + line);
            Console.WriteLine(e.StackTrace?.Split('\n').Take(6).Aggregate((a, b) => a + "\n" + b));
        }
        Console.WriteLine($"{name}: {Passed + Failed - before} checks ({sw.ElapsedMilliseconds} ms)");
        Fresh();
    }

    // Back to nothing: no objects, no saved preferences, no registered assets, every static the game keeps reset
    public static void Fresh()
    {
        World.ThrowExceptions = true;
        World.Reset();
        PlayerPrefs.DeleteAll();
        Resources.Clear();
        UnityEngine.Debug.ClearAll();
        Physics2D.NextHits = null;
        UnityEngine.EventSystems.EventSystem.OverUI = false;
        UnityEngine.EventSystems.EventSystem.OverAt = null;
        UnityEngine.InputSystem.Keyboard.current = null;
        UnityEngine.InputSystem.Mouse.current = null;
        UnityEngine.InputSystem.Touchscreen.current = null;
        Screen.width = 1920; Screen.height = 1080; Screen.SafeAreaOverride = null;
        Camera.main = null;
        Notify.Center.Reset();
        Notify.Sound = null;
        ItemCatalog.Reset();
        FoodSettings.Current = null;
        LoyaltySettings.Current = null;
        Backend.Current = new LocalBackend();
        PlayerInputActions.Made.Clear();
        string dir = Application.persistentDataPath;
        if (System.IO.Directory.Exists(dir)) foreach (string f in System.IO.Directory.GetFiles(dir)) System.IO.File.Delete(f);
        System.IO.Directory.CreateDirectory(dir);
    }
}
