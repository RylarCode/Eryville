using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// The project's own files, read as text: the story scripts in the samples read cleanly, and the server knows every quest that pays letters
public static class ProjectFileTests
{
    private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.."));

    private static string Unescape(string yaml) => Regex.Replace(yaml, @"\\(.)", m => m.Groups[1].Value == "n" ? "\n" : m.Groups[1].Value);

    public static void Run()
    {
        string story = Path.Combine(Root, "Assets/_Project/Resources/Story");
        T.Section("story chapters");
        T.Check(Directory.Exists(story), "found the story folder: " + story);
        foreach (string file in Directory.GetFiles(story, "*.asset"))
        {
            Match m = Regex.Match(File.ReadAllText(file), "(?m)^  script: \"(.*)\"$");
            List<string> problems = new List<string>();
            var beats = StoryScript.Parse(m.Success ? Unescape(m.Groups[1].Value) : "", problems);
            T.Check(m.Success && beats.Count >= 3 && problems.Count == 0, Path.GetFileName(file) + " reads cleanly (" + beats.Count + " lines" + (problems.Count > 0 ? ": " + string.Join("; ", problems) : "") + ")");
        }

        T.Section("Stone");
        string stoneAsset = File.ReadAllText(Path.Combine(Root, "Assets/_Project/Resources/Items/Stone.asset"));
        string stoneGuid = Regex.Match(File.ReadAllText(Path.Combine(Root, "Assets/_Project/Resources/Items/Stone.asset.meta")), @"guid: ([0-9a-f]{32})").Groups[1].Value;
        float stoneCapacity = float.Parse(Regex.Match(stoneAsset, @"(?m)^  capacity: (\d+)").Groups[1].Value);
        T.Check(Regex.IsMatch(stoneAsset, @"(?m)^  itemName: Stone$") && Regex.IsMatch(stoneAsset, @"(?m)^  category: 1$") && stoneGuid.Length == 32, "Stone is a Material item with its own id");
        var allGuids = Directory.GetFiles(Path.Combine(Root, "Assets/_Project/Resources/Items"), "*.meta").Select(f => Regex.Match(File.ReadAllText(f), @"guid: ([0-9a-f]{32})").Groups[1].Value).ToList();
        T.Check(allGuids.Count(g => g == stoneGuid) == 1, "no other item shares its id");
        int biggest = 0;
        foreach (string file in Directory.GetFiles(Path.Combine(Root, "Assets/_Project/Resources/Regions"), "*.asset"))
        {
            Match y = Regex.Match(File.ReadAllText(file), @"- item: Stone\r?\n    amount: (\d+)");
            if (y.Success) biggest = Math.Max(biggest, int.Parse(y.Groups[1].Value));
        }
        T.Check(biggest > 0 && biggest <= stoneCapacity, $"the most a region gives ({biggest}) fits in the stock limit ({stoneCapacity})");
        string granary = File.ReadAllText(Path.Combine(Root, "Assets/_Project/ScriptableObjects/Buildings/Granary.asset"));
        T.Check(Regex.IsMatch(granary, @"extraCosts:\r?\n  - item: \{fileID: 11400000, guid: " + stoneGuid + @", type: 2\}\r?\n    amount: \d+"), "the Granary costs Stone (its reference points at the Stone asset)");

        T.Section("quests that pay letters are listed on the server");
        string config = File.ReadAllText(Path.Combine(Root, "firebase/functions/src/config.ts"));
        foreach (string file in Directory.GetFiles(Path.Combine(Root, "Assets/_Project/Resources/Quests"), "*.asset"))
        {
            string text = File.ReadAllText(file);
            string id = Regex.Match(text, "(?m)^  id: (.+)$").Groups[1].Value.Trim();
            Match letters = Regex.Match(text, @"name: Letters\r?\n    amount: (\d+)");
            bool listed = !letters.Success || Regex.IsMatch(config, "\"" + Regex.Escape(id) + "\"\\s*:\\s*\\{\\s*letters:\\s*" + letters.Groups[1].Value + "\\s*\\}");
            T.Check(id.Length > 0 && listed, Path.GetFileName(file) + (letters.Success ? " pays " + letters.Groups[1].Value + " letters and config.ts lists it" : " pays no letters"));
        }

        T.Section("pullable characters are listed on the server");
        foreach (string file in Directory.GetFiles(Path.Combine(Root, "Assets/_Project/ScriptableObjects/Characters"), "*.asset"))
        {
            string text = File.ReadAllText(file);
            string typeId = Regex.Match(text, @"(?m)^  typeId: (\d+)").Groups[1].Value;
            string name = Regex.Match(text, @"(?m)^  displayName: (.+)$").Groups[1].Value.Trim();
            T.Check(Regex.IsMatch(config, @"typeId: " + typeId + @"\b"), name + " (typeId " + typeId + ") is in the server's pool");
        }
    }
}
