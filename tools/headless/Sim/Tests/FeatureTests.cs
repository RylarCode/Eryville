using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Story, quests, Connection and toasts in the running game (with the UI)
public static class FeatureTests
{
    // A backend that answers what the test says (and can hold an answer back, like a slow server)
    public sealed class ScriptedBackend : IGameBackend
    {
        public ClaimQuestResponse NextClaim; public bool Owns, Hold; public Action<ClaimQuestResponse> Held; public List<string> Claimed = new List<string>();
        private readonly LocalBackend inner = new LocalBackend();
        public string Name => "Scripted"; public DateTime ServerUtcNow => DateTime.UtcNow; public bool ServerOwnsBalances => Owns;
        public void Connect(Action<BackendResult> d) => d(BackendResult.Ok());
        public void SyncTime(Action<BackendResult> d) => d(BackendResult.Ok());
        public void SaveBlob(string s, string j, Action<BackendResult> d) => inner.SaveBlob(s, j, d);
        public void LoadBlob(string s, Action<BackendResult, string> d) => inner.LoadBlob(s, d);
        public void HasBlob(string s, Action<bool> d) => inner.HasBlob(s, d);
        public void DeleteBlob(string s, Action<BackendResult> d) => inner.DeleteBlob(s, d);
        public void GetPlayerState(Action<PlayerStateResponse> d) => d(new PlayerStateResponse());
        public void Pull(PullRequest r, Action<PullResponse> d) => inner.Pull(r, d);
        public void BuyLetter(BuyLetterRequest r, Action<BuyLetterResponse> d) => inner.BuyLetter(r, d);
        public void ClaimQuest(ClaimQuestRequest r, Action<ClaimQuestResponse> d) { Claimed.Add(r.questId); if (Hold) { Held = d; return; } d(NextClaim); }
        public void DevAction(DevRequest r, Action<DevResponse> d) => d(new DevResponse());
    }

    private static StoryManager Story(StoryLogicTests.FakeWorld world = null)
    {
        StoryManager sm = new GameObject("StoryManager").AddComponent<StoryManager>();
        if (world != null) sm.World = world;
        sm.SetChapters(new StoryChapter[0]);
        return sm;
    }

    private static QuestManager Quests() { QuestManager qm = new GameObject("QuestManager").AddComponent<QuestManager>(); qm.SetQuests(new Quest[0]); return qm; }

    // Taps the dialogue on to its end (or to a choice)
    private static void TapThrough(DialogueBox d, int max = 20) { for (int i = 0; i < max && d.IsOpen && !d.ShowingChoices; i++) d.OnTap(); }

    public static void StoryInGame()
    {
        Town t = Town.Build();
        StoryManager sm = Story(new StoryLogicTests.FakeWorld());
        DialogueBox dlg = t.ui.Dialogue;
        StoryChapter intro = StoryLogicTests.Chapter("intro", "Mayor: Welcome.\nMayor: Choose.\n> Warm | warmth +1\n> Cold | warmth -1\n=> Mayor: Noted.\nMayor: Done.", StoryTrigger.Automatic, 1);
        StoryChapter second = StoryLogicTests.Chapter("second", "Mayor: Second chapter.", StoryTrigger.Automatic, 2, null, new StoryCondition { kind = StoryCondition.Kind.ChapterDone, chapter = intro });
        sm.SetChapters(new[] { second, intro });

        T.Section("an automatic chapter");
        T.Check(sm.Chapters[0] == intro && sm.NextAutomatic() == intro, "chapters are tried in order");
        t.gm.currentGameState = GameManager.gameState.Menu; World.Run(3f);
        T.Check(!dlg.IsOpen, "nothing starts while a menu is open");
        t.gm.currentGameState = GameManager.gameState.Normal; World.Run(3f);
        T.Check(dlg.IsOpen && sm.Playing == intro && t.gm.currentGameState == GameManager.gameState.Menu, "once things are calm it opens (and the game waits)");
        T.Check(dlg.ShownSpeaker == "Mayor" && dlg.ShownText == "Welcome." && !dlg.SkipShown, "speaker and words; no Skip the first time");
        TapThrough(dlg);
        T.Check(dlg.ShowingChoices && dlg.ChoiceCount == 2, "a choice shows its options as buttons");
        Town.Click(dlg.transform.Find("Choices/Choice0"));
        TapThrough(dlg);
        T.Check(!dlg.IsOpen && sm.State.IsDone("intro") && sm.State.Get("warmth") == 1 && t.gm.currentGameState == GameManager.gameState.Normal, "at the end it closes and the choice is kept");
        World.Run(3f);
        T.Check(dlg.IsOpen && sm.Playing == second, "the next chapter follows after a breather");
        TapThrough(dlg);

        T.Section("the Story menu and replays");
        Town.Click(t.canvas.Find("OpenStoryButton"));
        List<string> rows = t.PanelRows("StoryPanel");
        T.Check(rows.Contains("Seen (tap to read again)") && rows.Any(r => r.StartsWith("T-intro")), "the menu lists what was seen: " + string.Join(" | ", rows));
        Town.Click(Town.RowStartingWith(t.Content("StoryPanel"), "T-intro"));
        T.Check(dlg.IsOpen && dlg.SkipShown, "a seen chapter plays again, with Skip");
        dlg.Close(false);
        T.Check(!dlg.IsOpen && sm.State.Get("warmth") == 1, "Skip closes it; a replay changes nothing");

        T.Section("locked buildings");
        StoryChapter unlocker = StoryLogicTests.Chapter("unlocker", "Mayor: Unlocked.", StoryTrigger.Manual);
        t.Lumberyard.unlockedBy = unlocker;
        sm.SetChapters(new[] { intro, second, unlocker });
        World.Tick(0.02f);
        Transform card = t.ui.buildingButtonContainer.Find("LumberyardButton");
        T.Check(!sm.IsUnlocked(t.Lumberyard) && !card.GetComponent<Button>().interactable && Town.TextOf(card).Contains("Locked"), "a building locked behind a chapter is greyed out: " + Town.TextOf(card).Replace("\n", " "));
        t.placer.SelectBuildingToPlace(t.Lumberyard);
        T.Check(!t.placer.IsPlacing, "and cannot be placed");
        sm.Play(unlocker); TapThrough(dlg);
        T.Check(sm.IsUnlocked(t.Lumberyard) && card.GetComponent<Button>().interactable && Town.TextOf(card) == "Lumberyard", "finishing the chapter unlocks it");
        T.Check(Notify.Center.History.Any(n => n.Text == "Chapter finished: T-unlocker") && Notify.Center.History.Any(n => n.Text == "New building unlocked: Lumberyard"), "and says so");
        t.Lumberyard.unlockedBy = null;

        T.Section("saving the story");
        sm.State.Set("pride", -2);
        T.Check(SaveSystem.Save("story.json"), "saved");
        sm.State.Clear();
        T.Check(SaveSystem.Load("story.json") && sm.State.IsDone("intro") && sm.State.Get("warmth") == 1 && sm.State.Get("pride") == -2, "loading puts the story back");
    }

    public static void QuestsInGame()
    {
        Town t = Town.Build();
        StoryLogicTests.FakeWorld world = new StoryLogicTests.FakeWorld();
        StoryManager sm = Story(world);
        QuestManager qm = Quests();
        world.QuestOf = id => qm.StatusOf(id);
        Quest m1 = StoryLogicTests.Quest("m1", QuestCategory.Main, QuestStart.Automatic, 10, null, new List<QuestObjective> { StoryLogicTests.Objective(StoryCondition.Kind.HasBuilding, 1, "Have a building") }, new StoryEffect(StoryEffect.Kind.Give, "Gold", 50));
        Quest m2 = StoryLogicTests.Quest("m2", QuestCategory.Main, QuestStart.Automatic, 20, StoryLogicTests.QuestDone("m1"), new List<QuestObjective> { StoryLogicTests.Objective(StoryCondition.Kind.HasCharacter, 1, "Have a character") }, new StoryEffect(StoryEffect.Kind.Give, "Letters", 2));
        Quest m3 = StoryLogicTests.Quest("m3", QuestCategory.Main, QuestStart.Automatic, 30, null, new List<QuestObjective> { StoryLogicTests.Objective(StoryCondition.Kind.HasItem, 5, "Hold 5 Wheat", "Wheat") });
        Quest s1 = StoryLogicTests.Quest("s1", QuestCategory.Side, QuestStart.Offered, 100, StoryLogicTests.QuestDone("m1"), new List<QuestObjective> { StoryLogicTests.Objective(StoryCondition.Kind.HasBuilding, 1, "Side objective") }, new StoryEffect(StoryEffect.Kind.Give, "Wood", 25));
        qm.SetQuests(new[] { s1, m3, m2, m1 });

        T.Section("main quests one at a time");
        qm.Evaluate();
        T.Check(qm.StatusOf(m1) == QuestStatus.Active && qm.StatusOf(m2) == QuestStatus.Locked && qm.StatusOf(m3) == QuestStatus.Locked && qm.StatusOf(s1) == QuestStatus.Locked, "only the first main quest starts");
        world.Buildings = 1; qm.Evaluate();
        T.Check(qm.StatusOf(m1) == QuestStatus.Ready && Notify.Center.History.Any(n => n.Text.StartsWith("Quest ready: Q-m1")), "objectives done: ready to claim");
        T.Check(qm.StatusOf(m2) == QuestStatus.Locked, "the next waits until the reward is claimed");
        Town.Click(t.canvas.Find("OpenQuestButton"));
        List<string> rows = t.PanelRows("QuestPanel");
        T.Check(rows[0] == "Main quest" && rows[1].StartsWith("Claim: Q-m1") && rows.Any(r => r.Contains("[x] Have a building")), "the menu offers to claim it: " + string.Join(" | ", rows));
        Town.Click(Town.RowStartingWith(t.Content("QuestPanel"), "Claim:"));
        rows = t.PanelRows("QuestPanel");
        T.Check(qm.StatusOf(m1) == QuestStatus.Complete && world.Given.Contains("Gold:50"), "claiming pays the reward and completes it");
        T.Check(qm.StatusOf(m2) == QuestStatus.Active && qm.StatusOf(m3) == QuestStatus.Locked && qm.StatusOf(s1) == QuestStatus.Available && rows.Any(r => r.StartsWith("Accept: Q-s1")), "the next main quest starts; the side quest is offered: " + string.Join(" | ", rows));
        Town.Click(Town.RowStartingWith(t.Content("QuestPanel"), "Accept:"));
        T.Check(qm.StatusOf(s1) == QuestStatus.Active || qm.StatusOf(s1) == QuestStatus.Ready, "Accept starts it");
        t.ui.ToggleQuestMenu();

        T.Section("letters paid by the server");
        ScriptedBackend server = new ScriptedBackend { Owns = true, NextClaim = new ClaimQuestResponse { lettersAfter = 7, lettersGranted = 2 } };
        Backend.Current = server;
        world.Characters = 1; qm.Evaluate(); world.Given.Clear();
        T.Check(qm.StatusOf(m2) == QuestStatus.Ready, "m2 ready");
        server.Hold = true;
        T.Check(qm.Claim(m2) && qm.IsClaiming(m2) && !qm.Claim(m2) && server.Claimed.Count == 1, "while the server answers, the claim cannot be repeated");
        server.Held(server.NextClaim);
        T.Check(qm.StatusOf(m2) == QuestStatus.Complete && t.rm.Get(ResourceManager.Letters) == 7f && !world.Given.Any(g => g.StartsWith("Letters")), "the server pays the letters; the game shows what it says and does not add any itself");
        qm.Log.Set("m2", QuestStatus.Ready); server.Hold = false; server.NextClaim = ClaimQuestResponse.Failed("The server is down.");
        T.Check(qm.Claim(m2) && qm.StatusOf(m2) == QuestStatus.Ready && Notify.Center.History.Any(n => n.Kind == NotifyKind.Alert && n.Text.Contains("The server is down.")), "if it fails, the quest stays ready and the player is told");
        Backend.Current = new LocalBackend();

        T.Section("saving quests");
        T.Check(SaveSystem.Save("quests.json"), "saved");
        qm.ResetState();
        T.Check(SaveSystem.Load("quests.json") && qm.StatusOf(m1) == QuestStatus.Complete && qm.StatusOf(m2) == QuestStatus.Ready, "loading puts them back");
    }

    public static void Connection()
    {
        Town t = Town.Build();
        StoryManager sm = Story();
        QuestManager qm = Quests();
        ConnectionWatcher watcher = sm.GetComponent<ConnectionWatcher>();
        CharacterType condo = t.Condo;
        StoryChapter c1 = StoryLogicTests.Chapter("condo-1", "Condo: first", StoryTrigger.Talk, 10, condo);
        StoryChapter c2 = StoryLogicTests.Chapter("condo-2", "Condo: second", StoryTrigger.Talk, 20, condo, new StoryCondition { kind = StoryCondition.Kind.ChapterDone, name = "condo-1" }, new StoryCondition { kind = StoryCondition.Kind.Loyalty, amount = 10 });
        c2.rewards = new List<StoryEffect> { new StoryEffect { kind = StoryEffect.Kind.Buff, stat = BuffStat.MoveSpeed, multiplier = 1.5f } };
        StoryChapter c3 = StoryLogicTests.Chapter("condo-3", "Condo: third", StoryTrigger.Talk, 40, condo, new StoryCondition { kind = StoryCondition.Kind.Loyalty, amount = 60 });
        sm.SetChapters(new[] { c1, c2, c3 });

        T.Section("what belongs to a character");
        var items = global::Connection.ItemsFor(condo);
        T.Check(items.Count == 3 && items[0].Chapter == c1 && global::Connection.HasAny(condo) && !global::Connection.HasAny(t.Firsy), "Condo's chapters, in order");
        T.Check(global::Connection.StateOf(items[0]) == ConnectionState.Locked && global::Connection.Missing(items[0]).SequenceEqual(new[] { "Have Condo" }), "nothing while Condo is not owned (it says so)");
        OwnedCharacter owned = t.Hire(condo, 3, 3);
        watcher.ResetAnnouncements(); Notify.Center.Reset(); watcher.Check();
        T.Check(global::Connection.StateOf(items[0]) == ConnectionState.Available && global::Connection.Missing(items[1]).Contains("Loyalty 10 (now 0)"), "owned: the first can be played; the next needs loyalty 10 (now 0)");
        T.Check(!Notify.Center.History.Any(n => n.Text.Contains("has something to share")), "what was already playable when the game loaded is not announced");

        T.Section("the Connection button and menu");
        t.ui.ShowCharacterDetails(owned);
        Transform button = t.detailsPanel.transform.Find("ConnectionButton");
        T.Check(button.gameObject.activeSelf && Town.TextOf(button) == "Connection (1)", "Condo's details: Connection (1)");
        Town.Click(button);
        List<string> rows = t.PanelRows("ConnectionPanel");
        T.Check(rows[0] == "Talk: T-condo-1" && rows[1] == "Next: T-condo-2" && rows.Contains("   Needs: Loyalty 10 (now 0)") && !rows.Any(r => r.Contains("condo-3")), "what can be played, and only the next locked one: " + string.Join(" | ", rows));
        Town.Click(Town.RowStartingWith(t.Content("ConnectionPanel"), "Talk:"));
        TapThrough(t.ui.Dialogue);
        T.Check(sm.State.IsDone("condo-1"), "Talk plays it");
        owned.Loyalty = 10f; watcher.Check();
        Notification note = Notify.Center.History.FirstOrDefault(n => n.Text == "Condo has something to share");
        T.Check(note != null && note.HasTarget, "reaching loyalty 10 unlocks the next, and the player is told");
        Notify.Center.Tap(note);
        T.Check(t.Panel("ConnectionPanel").gameObject.activeInHierarchy, "tapping the message opens Condo's Connection");

        T.Section("bonuses");
        float speed = owned.MoveSpeed;
        t.ui.PlayConnectionChapter(c2, false); TapThrough(t.ui.Dialogue);
        watcher.Check();
        T.Check(T.Near(owned.BuffMultiplier(BuffStat.MoveSpeed), 1.5f) && T.Near(owned.MoveSpeed, speed * 1.5f), "finishing it gives Condo its bonus, at once");
        t.ui.OpenConnection(owned);
        rows = t.PanelRows("ConnectionPanel");
        T.Check(rows.Contains("Bonuses") && rows.Contains("   MoveSpeed x1.5") && rows.Contains("Read again: T-condo-1"), "the menu lists the bonus and what can be read again: " + string.Join(" | ", rows));
        sm.State.Clear(); watcher.Check();
        T.Check(owned.BuffMultiplier(BuffStat.MoveSpeed) == 1f, "bonuses follow the story: forgetting the chapter removes it");
    }

    public static void Toasts()
    {
        Town t = Town.Build();
        ToastView view = t.ui.Toasts;
        T.Section("stacking");
        Notify.Show("first"); World.Run(1f);
        float h = view.ActiveItems[0].Height;
        Notify.Show("second"); Notify.Show("third"); World.Run(1.2f);
        var items = view.ActiveItems;
        T.Check(items.Count == 3 && items[0].ShownText == "third" && items[2].ShownText == "first", "newest on top");
        T.Check(items[0].Y == 0f && T.Near(items[1].Y, -(h + ToastView.Spacing), 0.01f), "the older ones are pushed down");
        Notify.Show("third"); World.Tick(0.05f);
        T.Check(view.ActiveItems[0].ShownText == "third (x2)", "a repeat counts up on the same banner");

        T.Section("X, swipe, tap");
        Notify.Center.Reset(); World.Run(0.6f);
        Notify.Show("keep"); Notify.Show("close me"); World.Run(0.5f);
        Town.Click(view.ActiveItems[0].transform.Find("Close"));
        World.Tick(0.05f);
        T.Check(view.ActiveItems.Count == 1 && view.ActiveItems[0].ShownText == "keep", "the X dismisses it");
        ToastItem one = view.ActiveItems[0];
        one.DragBy(one.Width * 0.1f); one.EndDrag(); World.Run(1f);
        T.Check(one.DragX == 0f && view.ActiveItems.Count == 1, "a short drag springs back");
        one.DragBy(-one.Width * 0.5f); one.EndDrag(); World.Tick(0.05f);
        T.Check(view.ActiveItems.Count == 0, "a long swipe dismisses it");
        int runs = 0; Notify.Show("do it", NotifyKind.Info, null, () => runs++); World.Run(0.5f);
        view.ActiveItems[0].Tap(); World.Run(0.5f);
        T.Check(runs == 1 && view.ActiveItems.Count == 0, "tapping runs its action and dismisses it");
        Notify.Show("timeout"); World.Run(3.8f); T.Check(view.ActiveItems.Count == 1, "a message stays about 4 s");
        World.Run(0.5f); T.Check(view.ActiveItems.Count == 0, "then goes");

        T.Section("history");
        Notify.Center.Reset(); Notify.Show("old one"); Notify.Show("new one", NotifyKind.Success, null, () => runs += 10);
        Town.Click(t.canvas.Find("MenuPanel/NotificationsButton"));
        List<string> rows = t.PanelRows("NotificationPanel");
        T.Check(rows.Count == 2 && rows[0].EndsWith("new one") && rows[1].EndsWith("old one"), "the history lists them newest first, with the time: " + string.Join(" | ", rows));
        Town.Click(Town.Rows(t.Content("NotificationPanel"))[0]);
        T.Check(runs == 11 && !t.Panel("NotificationPanel").gameObject.activeInHierarchy, "tapping a row closes the menu and goes there");
    }

    // The Firebase client against a pretend network
    public sealed class FakeHttp : IHttpClient
    {
        public readonly List<(string url, string body)> Calls = new List<(string, string)>();
        public Func<string, string, HttpResult> Handler;
        public void Post(string url, string body, string contentType, string bearerToken, Action<HttpResult> done) { Calls.Add((url, body)); done(Handler(url, body)); }
    }
    private static HttpResult Ok(string body) => new HttpResult { status = 200, body = body };
    private static string Wrap(object result) => "{\"result\":" + JsonUtility.ToJson(result) + "}";

    public static void FirebaseClient()
    {
        FakeHttp http = new FakeHttp();
        http.Handler = (url, body) =>
        {
            if (url.Contains("accounts:signUp")) return Ok("{\"idToken\":\"tok\",\"refreshToken\":\"ref\",\"expiresIn\":\"3600\",\"localId\":\"uid-123\"}");
            if (url.EndsWith("/getTime")) return Ok(Wrap(new TimeResponse { serverMs = 1700000000000 }));
            if (url.EndsWith("/pull")) return Ok(Wrap(new PullResponse { lettersAfter = 4, totalPulls = 1, rolls = new List<PullRollData> { new PullRollData { typeId = 1 } } }));
            if (url.EndsWith("/claimQuest")) return Ok(Wrap(new ClaimQuestResponse { lettersAfter = 7, lettersGranted = 2 }));
            return new HttpResult { status = 404, body = "{\"error\":{\"message\":\"no such function\"}}" };
        };
        FirebaseBackend fb = new FirebaseBackend(new FirebaseConfig { projectId = "eryville-test", apiKey = "key" }, http);
        T.Section("calls");
        BackendResult connected = null; fb.Connect(r => connected = r);
        T.Check(connected != null && connected.ok && fb.ServerOwnsBalances, "signs in anonymously and syncs the clock");
        PullResponse pulled = null; fb.Pull(new PullRequest { poolId = "Tavern", count = 1 }, r => pulled = r);
        T.Check(pulled.ok && pulled.lettersAfter == 4 && pulled.rolls.Count == 1 && http.Calls.Last().url.EndsWith("/pull") && http.Calls.Last().body.Contains("\"poolId\":\"Tavern\""), "a pull is sent and read back");
        ClaimQuestResponse claimed = null; fb.ClaimQuest(new ClaimQuestRequest { questId = "hire-someone" }, r => claimed = r);
        T.Check(claimed.ok && claimed.lettersGranted == 2 && http.Calls.Last().body.Contains("\"questId\":\"hire-someone\""), "claiming a quest sends only its id");
        T.Section("failures");
        http.Handler = (u, b) => new HttpResult { status = 403, body = "{\"error\":{\"message\":\"nope\",\"status\":\"PERMISSION_DENIED\"}}" };
        fb.Pull(new PullRequest { poolId = "Tavern", count = 1 }, r => pulled = r);
        T.Check(!pulled.ok && pulled.error.Contains("nope") && !pulled.offline, "a refusal says why and is not 'offline'");
        http.Handler = (u, b) => new HttpResult { networkError = true };
        fb.Pull(new PullRequest { poolId = "Tavern", count = 1 }, r => pulled = r);
        T.Check(!pulled.ok && pulled.offline, "no network: offline");
    }
}
