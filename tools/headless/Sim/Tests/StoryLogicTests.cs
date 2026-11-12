using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// The story, quest, notification and loyalty rules on their own (a fake town answers the conditions)
public static class StoryLogicTests
{
    public sealed class FakeWorld : IStoryWorld
    {
        public int Buildings, Characters;
        public Dictionary<string, float> Items = new Dictionary<string, float>();
        public List<string> Given = new List<string>();
        public Dictionary<string, QuestStatus> Quests = new Dictionary<string, QuestStatus>();
        public Func<string, QuestStatus> QuestOf;
        public List<string> StartedQuests = new List<string>();
        public Dictionary<CharacterType, int> Loyalties = new Dictionary<CharacterType, int>();
        public int BuildingCount(BuildingType type) => Buildings;
        public int CharacterCount(CharacterType type) => Characters;
        public float ItemAmount(string item) => Items.TryGetValue(item, out float v) ? v : 0f;
        public void Give(string item, float amount) => Given.Add(item + ":" + amount);
        public QuestStatus QuestStatus(string id) => QuestOf != null ? QuestOf(id) : (Quests.TryGetValue(id, out QuestStatus q) ? q : global::QuestStatus.Locked);
        public void StartQuest(string id) => StartedQuests.Add(id);
        public int Loyalty(CharacterType t) => t != null && Loyalties.TryGetValue(t, out int v) ? v : -1;
        public void AddLoyalty(CharacterType t, int a) { if (t != null) Loyalties[t] = Math.Max(0, Math.Min(100, Loyalty(t) < 0 ? a : Loyalty(t) + a)); }
    }

    public static StoryChapter Chapter(string id, string script, StoryTrigger trigger = StoryTrigger.Automatic, int order = 0, CharacterType talkTo = null, params StoryCondition[] requires)
    {
        StoryChapter c = ScriptableObject.CreateInstance<StoryChapter>();
        c.name = id; c.id = id; c.title = "T-" + id; c.script = script; c.trigger = trigger; c.order = order; c.talkTo = talkTo; c.requires = requires.ToList();
        return c;
    }

    public static Quest Quest(string id, QuestCategory cat, QuestStart start, int order, List<StoryCondition> requires, List<QuestObjective> objectives, params StoryEffect[] rewards)
    {
        Quest q = ScriptableObject.CreateInstance<Quest>();
        q.name = id; q.id = id; q.title = "Q-" + id; q.category = cat; q.start = start; q.order = order;
        q.requires = requires ?? new List<StoryCondition>(); q.objectives = objectives ?? new List<QuestObjective>(); q.rewards = rewards.ToList();
        return q;
    }
    public static QuestObjective Objective(StoryCondition.Kind kind, int amount = 1, string label = "", string name = "") => new QuestObjective { label = label, condition = new StoryCondition { kind = kind, amount = amount, name = name } };
    public static List<StoryCondition> QuestDone(string id) => new List<StoryCondition> { new StoryCondition { kind = StoryCondition.Kind.QuestDone, name = id } };

    public static void Script()
    {
        T.Section("reading a script");
        var problems = new List<string>();
        var beats = StoryScript.Parse(string.Join("\n", new[] {
            "# a comment", "// another", "",
            "Mayor: Welcome to Eryville!", "A cold wind blows.", "Narrator: Hmm.", "Mayor: One.\\nTwo. You have {kindness} points.",
            "> Help the farmer | kindness +1 | give Gold 20", "> Ignore them | kindness -2 | pride =3",
            "=> Farmer: Thank you!", "=> Farmer: Really.",
            "[if kindness >= 2 and not done intro] Mayor: Well done.", "@add kindness 1", "@set pride 0", "@give Wood 5",
            "[if flagged] > Secret option", "[if kindness > 0] Mayor: positive", "this line has : a colon" }), problems);
        T.Check(problems.Count == 0, "a good script reads without problems: " + string.Join("; ", problems));
        T.Check(beats.Count == 12 && beats[0].speaker == "Mayor" && beats[1].speaker == "" && beats[2].speaker == "" && beats[2].text == "Hmm.", "lines and narration (no colon, or Narrator:)");
        T.Check(beats[3].text == "One.\nTwo. You have {kindness} points.", "\\n is a new line");
        T.Check(beats[4].kind == StoryBeatKind.Choice && beats[4].options.Count == 2 && beats[4].options[0].effects.Count == 2 && beats[4].options[0].effects[1].kind == StoryEffect.Kind.Give && beats[4].options[1].effects[0].amount == -2 && beats[4].options[1].effects[1].kind == StoryEffect.Kind.Set, "choices with +, -, = and give");
        T.Check(beats[4].options[1].replies.Count == 2 && beats[4].options[0].replies.Count == 0, "replies belong to the option above them");
        T.Check(beats[5].onlyIf.Count == 2 && beats[5].onlyIf[1].kind == StoryCondition.Kind.ChapterNotDone, "[if a and b]");
        T.Check(beats[6].kind == StoryBeatKind.Effect && beats[8].effect.kind == StoryEffect.Kind.Give, "@add / @set / @give");
        T.Check(beats[10].onlyIf[0].amount == 1, "'> 0' means at least 1");
        var bad = new List<string>();
        StoryScript.Parse("[if ???] Mayor: x\n=> Orphan: reply\n@jump 3\n> | kindness ++\nMayor: fine", bad);
        T.Check(bad.Count == 5 && bad.All(m => m.StartsWith("line ")), "mistakes are reported with line numbers: " + bad.Count);

        T.Section("quests and loyalty in scripts");
        var q = StoryScript.Parse("[if quest done a] X: one\n[if not quest done a and quest active b] X: two\n@quest build-mine\n> Sure | quest build-mine | loyalty +2\n[if loyalty >= 40] X: close\n@loyalty -3\n> bad | loyalty =3", problems = new List<string>());
        T.Check(q[0].onlyIf[0].kind == StoryCondition.Kind.QuestDone && q[1].onlyIf[0].compare == StoryCondition.Compare.NotEqual && q[1].onlyIf[1].kind == StoryCondition.Kind.QuestActive, "quest done / not quest done / quest active");
        T.Check(q[2].effect.kind == StoryEffect.Kind.StartQuest && q[3].options[0].effects[0].kind == StoryEffect.Kind.StartQuest && q[3].options[0].effects[1].kind == StoryEffect.Kind.Loyalty, "@quest and | quest, | loyalty +2");
        T.Check(q[4].onlyIf[0].kind == StoryCondition.Kind.Loyalty && q[5].effect.kind == StoryEffect.Kind.Loyalty && q[5].effect.amount == -3 && problems.Count == 1, "'loyalty' is the character's loyalty, and cannot be set with '='");
    }

    public static void Player()
    {
        T.Section("a chapter plays on a copy");
        StoryChapter c = Chapter("lesson", string.Join("\n", new[] {
            "Mayor: Hello. You have {kindness} points.", "> Be kind | kindness +2", "> Be rude | kindness -1 | give Gold 7", "=> Mayor: Rude!",
            "[if kindness >= 2] Mayor: You are kind.", "[if kindness < 2] Mayor: You are not kind.", "@add visits 1", "Mayor: Bye." }));
        c.rewards = new List<StoryEffect> { new StoryEffect(StoryEffect.Kind.Give, "Letters", 3) };
        StoryState real = new StoryState(); FakeWorld w = new FakeWorld();
        StoryPlayer p = new StoryPlayer(c, real, w);
        T.Check(p.Text == "Hello. You have 0 points.", "values are filled in");
        p.Advance(); T.Check(p.IsChoice && p.Choices.Count == 2, "then the choice");
        p.Choose(0); T.Check(p.Text == "You are kind." && real.Get("kindness") == 0, "later lines react to the choice; nothing is kept yet");
        p.Advance(); p.Advance(); T.Check(p.Finished, "it ends");
        T.Check(p.Commit() && real.Get("kindness") == 2 && real.Get("visits") == 1 && real.IsDone("lesson") && w.Given.SequenceEqual(new[] { "Letters:3" }), "committing keeps the values, gives the rewards, marks it done");
        StoryState other = new StoryState(); FakeWorld w2 = new FakeWorld();
        StoryPlayer p2 = new StoryPlayer(c, other, w2); p2.Advance(); p2.Choose(1);
        T.Check(p2.Text == "Rude!", "the other option's reply");
        StoryPlayer replay = new StoryPlayer(c, other, w2, true); replay.Advance(); replay.Choose(0); replay.Advance(); replay.Advance(); replay.Advance();
        T.Check(replay.Finished && !replay.Commit() && other.Get("kindness") == 0 && w2.Given.Count == 0, "a replay changes nothing");

        T.Section("conditions about the town");
        FakeWorld world = new FakeWorld { Buildings = 2 }; world.Items["Gold"] = 50.9f;
        StoryState st = new StoryState(); st.Set("Kindness", 3); st.MarkDone("Intro");
        T.Check(st.Get("kindness") == 3 && st.IsDone("intro"), "names are not case sensitive");
        T.Check(new StoryCondition { kind = StoryCondition.Kind.HasBuilding, amount = 2 }.Test(st, world) && !new StoryCondition { kind = StoryCondition.Kind.HasBuilding, amount = 3 }.Test(st, world), "HasBuilding counts");
        T.Check(new StoryCondition { kind = StoryCondition.Kind.HasItem, name = "Gold", amount = 50 }.Test(st, world) && !new StoryCondition { kind = StoryCondition.Kind.HasItem, name = "Gold", amount = 51 }.Test(st, world), "HasItem counts whole units");

        T.Section("loyalty has a subject");
        CharacterType condo = Town.NewCharacter("Condo", 2, Rarity.Epic, 1);
        world.Loyalties[condo] = 30;
        var c40 = new StoryCondition { kind = StoryCondition.Kind.Loyalty, amount = 40 };
        var c20 = new StoryCondition { kind = StoryCondition.Kind.Loyalty, amount = 20 };
        T.Check(!c40.Test(st, world, condo) && c20.Test(st, world, condo) && !c20.Test(st, world), "about the subject; with none, not met");
        c40.Measure(st, world, out int cur, out int tgt, condo); T.Check(cur == 30 && tgt == 40, "progress 30 of 40");
        StoryChapter talk = Chapter("talk", "Condo: hi\n[if loyalty >= 30] Condo: close\n> nice | loyalty +4\n@loyalty -1", StoryTrigger.Talk, 0, condo);
        StoryPlayer tp = new StoryPlayer(talk, st, world); tp.Advance();
        T.Check(tp.Text == "close", "in a Talk chapter, loyalty is the Talk To character's");
        tp.Advance(); tp.Choose(0);
        T.Check(tp.Commit() && world.Loyalty(condo) == 33, "loyalty changes are kept at the end: 30 +4 -1");
    }

    public static void Notifications()
    {
        float now = 100f; NotificationCenter c = new NotificationCenter(() => now);
        T.Section("stacking");
        var a = c.Post("A"); c.Post("B"); var cc = c.Post("C");
        T.Check(c.Visible[0] == cc && c.Visible[2] == a, "newest on top");
        c.Post("D"); c.Post("E");
        T.Check(c.Visible.Count == NotificationCenter.MaxVisible && !a.Visible && c.History.Count == 5, "at most " + NotificationCenter.MaxVisible + " show; the oldest stays in the history");
        T.Section("merging, expiry, cooldown");
        c.Reset(); c.Post("x"); now = 101; c.Post("y"); now = 102;
        var again = c.Post("x", NotifyKind.Alert);
        T.Check(c.Visible.Count == 2 && again.Count == 2 && c.Visible[0] == again && again.Display == "x (x2)" && again.Kind == NotifyKind.Alert, "a repeat merges and moves to the top");
        c.Reset(); var info = c.Post("info"); var warn = c.Post("warn", NotifyKind.Warning);
        now += 4.1f; c.Tick(); T.Check(!info.Visible && warn.Visible, "info goes after 4 s, a warning later");
        c.Hold(warn, true); now += 30f; c.Tick(); T.Check(warn.Visible, "held: it stays");
        c.Hold(warn, false); c.Tick(); T.Check(!warn.Visible, "let go: it goes");
        c.Reset(); T.Check(c.Post("h", NotifyKind.Info, null, null, "k", 60f) != null && c.Post("h", NotifyKind.Info, null, null, "k", 60f) == null, "a cooldown ignores the same key");
        int taps = 0; var t = c.Post("tap", NotifyKind.Info, null, () => taps++); c.Tap(t);
        T.Check(taps == 1 && !t.Visible, "tapping runs the action and dismisses");
        T.Section("sound hook");
        var sounds = new List<NotifyKind>(); Notify.Sound = k => sounds.Add(k);
        Notify.Show("s1", NotifyKind.Success); Notify.Show("s1", NotifyKind.Success); Notify.Show("s2", NotifyKind.Alert);
        T.Check(sounds.SequenceEqual(new[] { NotifyKind.Success, NotifyKind.Alert }), "once per new message, not for a merged repeat");
    }

    public static void QuestsAndLoyalty()
    {
        T.Section("objectives");
        FakeWorld w = new FakeWorld { Buildings = 2 }; StoryState st = new StoryState();
        var o3 = new QuestObjective { label = "Have three", condition = new StoryCondition { kind = StoryCondition.Kind.HasBuilding, amount = 3 } };
        T.Check(o3.Text(st, w) == "Have three (2/3)" && !o3.IsMet(st, w), "progress is shown");
        BuildingType mine = Town.NewBuilding("Gold Mine", 1, 3);
        T.Check(new QuestObjective { condition = new StoryCondition { kind = StoryCondition.Kind.HasBuilding, amount = 1, building = mine } }.Text(st, w) == "Build a Gold Mine", "without a label the text is made up");
        T.Section("the log");
        QuestLog log = new QuestLog(); log.Set("x", QuestStatus.Active); log.Set("y", QuestStatus.Complete);
        var saved = new List<QuestSaveData>(); log.Export(saved);
        QuestLog back = new QuestLog(); back.Import(saved);
        T.Check(back.Get("X") == QuestStatus.Active && back.Get("y") == QuestStatus.Complete && back.Get("z") == QuestStatus.Locked, "saves and loads (ids not case sensitive)");
        T.Section("checks");
        var c1 = Quest("same", QuestCategory.Main, QuestStart.Automatic, 1, null, new List<QuestObjective> { Objective(StoryCondition.Kind.HasBuilding) });
        var c2 = Quest("same", QuestCategory.Side, QuestStart.Offered, 2, QuestDone("ghost"), null);
        var found = QuestChecks.Problems(new[] { c1, c2 }, null);
        T.Check(found.Any(m => m.Contains("same id")) && found.Any(m => m.Contains("ghost")) && found.Any(m => m.Contains("no objectives")), "duplicate ids, missing quests, empty quests: " + found.Count);

        T.Section("loyalty rules");
        LoyaltySettings s = LoyaltySettings.Current;
        CharacterType condo = Town.NewCharacter("Condo", 2, Rarity.Epic, 1);
        OwnedCharacter o = new OwnedCharacter(condo) { Mood = 80f };
        T.Check(o.Loyalty == 0f, "a new character starts at 0");
        T.Check(LoyaltyRules.DailyChange(o, true) == s.paidHappy + s.homeless, "paid and happy (no home here): " + (s.paidHappy + s.homeless));
        o.Mood = 10f; T.Check(LoyaltyRules.DailyChange(o, false) == s.unpaid + s.unhappy + s.homeless, "unpaid and unhappy");
        o.Loyalty = 99.5f; o.Mood = 80f; LoyaltyRules.ApplyPayday(o, true);
        T.Check(o.Loyalty <= 100f && T.Near(o.LastLoyaltyChange, Math.Min(100f, 99.5f + s.paidHappy + s.homeless) - 99.5f), "never above 100, and the change shown is what happened");
    }
}
