using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

// A chapter's scenes, as written. One thing per line:
//
//   # a note to yourself (also // ...)
//   Mayor: Welcome to Eryville!          a line spoken by Mayor (the picture comes from the chapter's Cast, or from a character of that name)
//   A cold wind blows.                   no colon: narration (nobody speaking)
//   Mayor: First line.\nSecond line.     \n starts a new line inside one speech bubble
//   Mayor: You have {kindness} points.   {name} shows a story value
//   > Help the farmer | kindness +1      a choice: options on consecutive ">" lines; after "|" what picking it does
//   > Ignore them     | kindness -1 | give Gold 20
//   => Farmer: Thank you!                a reply, shown only if the option above it was picked (several allowed)
//   @add kindness 1                      change a value on the spot (@set name n, @give Gold 50 too)
//   @quest build-mine                    start a quest (in an option: "> Sure | quest build-mine")
//   [if kindness >= 2] Mayor: Well done. only plays when it is true (also in front of ">" and "@"); join with "and"
//
// Conditions: "name >= 2" (also <= < > == !=), "name" (is more than 0), "not name", "done chapter-id", "not done chapter-id",
// "quest done quest-id" (finished and claimed), "quest active quest-id" (started, not claimed yet), "not quest done quest-id".
// What an option does: "name +1", "name -2", "name =3" (set), "give Item amount".
public enum StoryBeatKind { Line, Choice, Effect }

public class StoryBeat
{
    public StoryBeatKind kind;
    public string speaker = "";                                 // Line: who speaks ("" = narration)
    public string text = "";
    public List<StoryCondition> onlyIf = new List<StoryCondition>();
    public List<StoryOption> options = new List<StoryOption>(); // Choice
    public StoryEffect effect;                                  // Effect
    public int sourceLine;                                      // 1-based, for messages
}

public class StoryOption
{
    public string text = "";
    public List<StoryEffect> effects = new List<StoryEffect>();
    public List<StoryCondition> onlyIf = new List<StoryCondition>();
    public List<StoryBeat> replies = new List<StoryBeat>();
}

public static class StoryScript
{
    private static readonly Regex IfPrefix = new Regex(@"^\[if\s+(.*?)\]\s*(.*)$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex And = new Regex(@"\s+and\s+", RegexOptions.IgnoreCase);
    private static readonly Regex Compared = new Regex(@"^([A-Za-z_][A-Za-z0-9_]*)\s*(>=|<=|==|!=|=|>|<)\s*(-?\d+)$");
    private static readonly Regex Plain = new Regex(@"^([A-Za-z_][A-Za-z0-9_]*)$");
    private static readonly Regex DoneOf = new Regex(@"^(not\s+)?done\s+([A-Za-z0-9_.\-]+)$", RegexOptions.IgnoreCase);
    private static readonly Regex NotPlain = new Regex(@"^not\s+([A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.IgnoreCase);
    private static readonly Regex Change = new Regex(@"^([A-Za-z_][A-Za-z0-9_]*)\s*([+\-=])\s*(\d+)$");
    private static readonly Regex Give = new Regex(@"^give\s+(.+?)\s+(-?\d+)$", RegexOptions.IgnoreCase);
    private static readonly Regex Directive = new Regex(@"^@(set|add|give|quest)\s+(.+)$", RegexOptions.IgnoreCase);
    private static readonly Regex QuestChange = new Regex(@"^quest\s+([A-Za-z0-9_.\-]+)$", RegexOptions.IgnoreCase);
    private static readonly Regex QuestCondition = new Regex(@"^(not\s+)?quest\s+(done|active)\s+([A-Za-z0-9_.\-]+)$", RegexOptions.IgnoreCase);
    private static readonly Regex ValueRef = new Regex(@"\{([A-Za-z_][A-Za-z0-9_]*)\}");

    // Reads a script. Lines it cannot understand are left out and described in problems (with their line number).
    public static List<StoryBeat> Parse(string script, List<string> problems = null)
    {
        List<StoryBeat> beats = new List<StoryBeat>();
        if (string.IsNullOrEmpty(script)) return beats;

        StoryBeat choice = null; // the choice that ">" lines are being added to
        string[] lines = script.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            int number = i + 1;
            string line = lines[i].Trim().TrimStart('﻿');
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//")) continue;

            List<StoryCondition> conditions = new List<StoryCondition>();
            Match prefix = IfPrefix.Match(line);
            if (prefix.Success)
            {
                bool readable = true;
                foreach (string part in And.Split(prefix.Groups[1].Value))
                {
                    if (TryParseCondition(part, out StoryCondition condition)) conditions.Add(condition);
                    else
                    {
                        problems?.Add($"line {number}: cannot read the condition \"{part.Trim()}\" (try: name >= 2, done chapter-id, not done chapter-id)");
                        readable = false;
                    }
                }
                if (!readable) continue;
                line = prefix.Groups[2].Value.Trim();
                if (line.Length == 0) { problems?.Add($"line {number}: a condition with nothing after it"); continue; }
            }

            if (line.StartsWith("=>"))
            {
                if (choice == null || choice.options.Count == 0) { problems?.Add($"line {number}: a reply (=>) must come right after a choice option (>)"); continue; }
                StoryBeat reply = ParseLine(line.Substring(2).Trim(), number);
                reply.onlyIf = conditions;
                choice.options[choice.options.Count - 1].replies.Add(reply);
                continue;
            }

            if (line.StartsWith(">"))
            {
                StoryOption option = ParseOption(line.Substring(1), number, problems);
                option.onlyIf = conditions;
                if (choice == null)
                {
                    choice = new StoryBeat { kind = StoryBeatKind.Choice, sourceLine = number };
                    beats.Add(choice);
                }
                choice.options.Add(option);
                continue;
            }

            choice = null;
            if (line.StartsWith("@"))
            {
                Match directive = Directive.Match(line);
                StoryEffect effect = directive.Success ? ParseDirective(directive.Groups[1].Value.ToLowerInvariant(), directive.Groups[2].Value.Trim()) : null;
                if (effect == null) { problems?.Add($"line {number}: cannot read \"{line}\" (try: @add name 1, @set name 3, @give Gold 50)"); continue; }
                beats.Add(new StoryBeat { kind = StoryBeatKind.Effect, effect = effect, onlyIf = conditions, sourceLine = number });
                continue;
            }

            StoryBeat spoken = ParseLine(line, number);
            spoken.onlyIf = conditions;
            beats.Add(spoken);
        }
        return beats;
    }

    private static StoryBeat ParseLine(string line, int number)
    {
        StoryBeat beat = new StoryBeat { kind = StoryBeatKind.Line, sourceLine = number };
        int colon = line.IndexOf(':');
        if (colon >= 0)
        {
            beat.speaker = line.Substring(0, colon).Trim();
            beat.text = line.Substring(colon + 1).Trim();
        }
        else beat.text = line;
        if (string.Equals(beat.speaker, "Narrator", StringComparison.OrdinalIgnoreCase)) beat.speaker = "";
        beat.text = beat.text.Replace("\\n", "\n");
        return beat;
    }

    private static StoryOption ParseOption(string text, int number, List<string> problems)
    {
        StoryOption option = new StoryOption();
        string[] parts = text.Split('|');
        option.text = parts[0].Trim().Replace("\\n", "\n");
        if (option.text.Length == 0) problems?.Add($"line {number}: a choice option with no text");
        for (int i = 1; i < parts.Length; i++)
        {
            string token = parts[i].Trim();
            if (token.Length == 0) continue;
            StoryEffect effect = ParseChange(token);
            if (effect == null) problems?.Add($"line {number}: cannot read \"{token}\" (try: name +1, name -1, name =3, give Gold 50)");
            else option.effects.Add(effect);
        }
        return option;
    }

    // "kindness +1", "kindness -2", "kindness =3", "give Gold 50"
    public static StoryEffect ParseChange(string text)
    {
        text = text.Trim();
        Match quest = QuestChange.Match(text);
        if (quest.Success) return new StoryEffect(StoryEffect.Kind.StartQuest, quest.Groups[1].Value, 1);
        Match give = Give.Match(text);
        if (give.Success) return new StoryEffect(StoryEffect.Kind.Give, give.Groups[1].Value.Trim(), int.Parse(give.Groups[2].Value));
        Match change = Change.Match(text);
        if (!change.Success) return null;
        int amount = int.Parse(change.Groups[3].Value);
        switch (change.Groups[2].Value)
        {
            case "+": return new StoryEffect(StoryEffect.Kind.Add, change.Groups[1].Value, amount);
            case "-": return new StoryEffect(StoryEffect.Kind.Add, change.Groups[1].Value, -amount);
            default: return new StoryEffect(StoryEffect.Kind.Set, change.Groups[1].Value, amount);
        }
    }

    // "@set name 3" / "@add name -1" / "@give Gold 50" (the part after the @ is split already)
    private static StoryEffect ParseDirective(string verb, string rest)
    {
        string[] pieces = rest.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (verb == "quest") return pieces.Length == 1 ? new StoryEffect(StoryEffect.Kind.StartQuest, pieces[0], 1) : null;
        if (pieces.Length < 2 || !int.TryParse(pieces[pieces.Length - 1], out int amount)) return null;
        string name = string.Join(" ", pieces, 0, pieces.Length - 1);
        if (verb == "give") return new StoryEffect(StoryEffect.Kind.Give, name, amount);
        if (pieces.Length != 2 || !Plain.IsMatch(name)) return null;
        return new StoryEffect(verb == "set" ? StoryEffect.Kind.Set : StoryEffect.Kind.Add, name, amount);
    }

    // "kindness >= 2", "kindness", "not kindness", "done intro", "not done intro"
    public static bool TryParseCondition(string text, out StoryCondition condition)
    {
        condition = null;
        text = text.Trim();

        Match questCondition = QuestCondition.Match(text);
        if (questCondition.Success)
        {
            condition = new StoryCondition
            {
                kind = questCondition.Groups[2].Value.ToLowerInvariant() == "done" ? StoryCondition.Kind.QuestDone : StoryCondition.Kind.QuestActive,
                name = questCondition.Groups[3].Value,
                compare = questCondition.Groups[1].Success ? StoryCondition.Compare.NotEqual : StoryCondition.Compare.AtLeast
            };
            return true;
        }

        Match done = DoneOf.Match(text);
        if (done.Success)
        {
            condition = new StoryCondition { kind = done.Groups[1].Success ? StoryCondition.Kind.ChapterNotDone : StoryCondition.Kind.ChapterDone, name = done.Groups[2].Value };
            return true;
        }

        Match compared = Compared.Match(text);
        if (compared.Success)
        {
            int amount = int.Parse(compared.Groups[3].Value);
            StoryCondition.Compare compare;
            switch (compared.Groups[2].Value)
            {
                case ">=": compare = StoryCondition.Compare.AtLeast; break;
                case "<=": compare = StoryCondition.Compare.AtMost; break;
                case ">": compare = StoryCondition.Compare.AtLeast; amount += 1; break;
                case "<": compare = StoryCondition.Compare.AtMost; amount -= 1; break;
                case "!=": compare = StoryCondition.Compare.NotEqual; break;
                default: compare = StoryCondition.Compare.Exactly; break;
            }
            condition = new StoryCondition { kind = StoryCondition.Kind.Value, name = compared.Groups[1].Value, compare = compare, amount = amount };
            return true;
        }

        Match plain = Plain.Match(text);
        if (plain.Success)
        {
            condition = new StoryCondition { kind = StoryCondition.Kind.Value, name = plain.Groups[1].Value, compare = StoryCondition.Compare.AtLeast, amount = 1 };
            return true;
        }

        Match notPlain = NotPlain.Match(text);
        if (notPlain.Success)
        {
            condition = new StoryCondition { kind = StoryCondition.Kind.Value, name = notPlain.Groups[1].Value, compare = StoryCondition.Compare.Exactly, amount = 0 };
            return true;
        }
        return false;
    }

    // Puts the story values into a text: "You have {kindness} points" -> "You have 3 points"
    public static string Fill(string text, StoryState state)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
        return ValueRef.Replace(text, match => state.Get(match.Groups[1].Value).ToString());
    }
}
