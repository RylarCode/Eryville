using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The testing tools as a menu (opened from the game menu, only in the editor and Development Builds):
// time, resources, characters, the world, recruitment and data. Each row does one thing from Cheats.
// Rows that destroy things need a second tap within a few seconds, so nothing is lost by accident.
public class CheatMenu
{
    private class Entry
    {
        public string section;           // heading row (when label is null)
        public Func<string> label;
        public Action run;
        public bool confirm;             // needs a second tap
    }

    private const float ConfirmSeconds = 4f;

    private readonly UIManager ui;
    private readonly GameObject buttonPrefab;
    private readonly GameObject panel;
    private readonly Transform content;
    private readonly List<Entry> entries = new List<Entry>();

    private Entry pending;               // the row waiting for its second tap
    private float pendingUntil;
    private string shownSignature;

    public GameObject Panel => panel;

    public CheatMenu(UIManager ui, GameObject buttonPrefab, GameObject detailsPanel, GameObject listPanel)
    {
        this.ui = ui;
        this.buttonPrefab = buttonPrefab;

        panel = MenuPanels.CreatePanel("CheatPanel", detailsPanel);
        Transform header = MenuPanels.CreateHeader(panel, 1);
        MenuPanels.AddHeaderButton(header, buttonPrefab, null, out Button unused).text = "Testing tools";
        MenuPanels.AddHeaderButton(header, buttonPrefab, () => ui.ToggleCheatMenu(), out Button close).text = "Close";
        content = MenuPanels.CreateScrollList(panel, listPanel, MenuPanels.HeaderHeight(1));

        BuildEntries();
        panel.SetActive(false);
    }

    private void Section(string name) { entries.Add(new Entry { section = name }); }
    private void Add(string label, Action run, bool confirm = false) { entries.Add(new Entry { label = () => label, run = run, confirm = confirm }); }
    private void Add(Func<string> label, Action run, bool confirm = false) { entries.Add(new Entry { label = label, run = run, confirm = confirm }); }
    private void Info(Func<string> label) { entries.Add(new Entry { label = label }); } // text only, not a button

    private void BuildEntries()
    {
        Section("Time");
        Info(Cheats.DescribeClock);
        Add(() => GameClock.Instance.IsRealTime ? "Switch to the in-game clock (30 s per time of day, for testing)" : "Switch to the real-time clock", () => Cheats.ToggleClockMode());
        Add(() => GameClock.Instance.IsRealTime ? "Put the clock back to the real time" : "(real-time clock only) put the clock back", Cheats.ResetTimeOffset);
        Add("Next time of day (moves the real-time clock too)", Cheats.NextTimeOfDay);
        Add("Skip to next morning (wages are paid)", Cheats.SkipToMorning);
        Add(() => $"Game speed: x{Time.timeScale:0.#} (tap to change)", () => Cheats.CycleGameSpeed());
        Add("Pay wages now", Cheats.RunPayroll);
        Info(Cheats.DescribeLighting);
        Add(() => "Lighting (day / night tint): " + (DayNightCycle.Instance != null && DayNightCycle.Instance.LightingOn ? "on" : "off") + " (tap to change)", Cheats.ToggleLighting);
        Add(() => DayNightCycle.Instance != null && DayNightCycle.Instance.PreviewHour.HasValue ? $"Look at {DayNightCycle.Instance.PreviewHour.Value:0}:00 (tap for 3 hours later)" : "Look at an hour of the day (tap to start)", Cheats.CyclePreviewHour);

        Section("Resources");
        Add("+1000 gold", () => Cheats.AddGold(1000));
        Add("+500 wood", () => Cheats.AddWood(500));
        Add("+10 recruitment letters", () => Cheats.AddLetters(10));
        Add("Set gold to 0 (see unpaid wages)", Cheats.ZeroGold);

        Section("Characters (ignore the ownership limit)");
        foreach (CharacterType type in CharacterManager.Instance.AllCharacters)
        {
            if (type == null) continue;
            CharacterType captured = type;
            Add($"Spawn {type.displayName}", () => Cheats.SpawnCharacter(captured));
        }
        Add("Everyone: mood 100, loyalty 100", () => Cheats.SetEveryone(100f, 100f));
        Add("Everyone: mood 10, loyalty unchanged", () => Cheats.SetEveryone(10f, -1f));
        Add("Dismiss all characters", Cheats.DismissAllCharacters, true);

        Section("World");
        Add(() => $"World state: {GameManager.Instance.worldState} (tap to change)", () => Cheats.CycleWorldState());
        Add("Remove all buildings", Cheats.RemoveAllBuildings, true);
        Add("Remove all roads", Cheats.RemoveAllRoads, true);

        Section("Saving");
        Add(() => "Auto-load and autosave (real time only): " + (PlayerPrefs.GetInt(GameSession.AutoPreference, 1) == 1 ? "on" : "off"), () => Cheats.ToggleAutomaticSession());
        Add("Pretend the game was closed for 3 hours", () => Cheats.SimulateTimeAway(3f));
        Info(() => OfflineProgress.LastReport.seconds > 0f ? "Last time away: " + OfflineProgress.LastReport : "Last time away: nothing yet");
        Info(Cheats.DescribeBackend);
        if (BackendSelector.FirebaseConfigured)
        {
            Add(() => BackendSelector.WantsLocal ? "Next start: use the LOCAL backend (tap for Firebase)" : "Next start: use FIREBASE (tap for local)", () => Cheats.ToggleBackendPreference());
            Add("Forget this device's online account (a new player next start)", Cheats.ForgetAccount, true);
        }

        Section("Recruitment");
        Add("Reset pity and pull count", Cheats.ResetRecruitment);

        Section("Data");
        Add("RESET GAME: everything and the save file", Cheats.ResetGame, true);
        Add("Delete the save file only", () => Cheats.DeleteSaveFile(), true);

        Section("Tools");
        Add("Show / hide the performance panel", () => { if (PerfOverlay.Instance != null) PerfOverlay.Instance.Toggle(); });
    }

    public void Show()
    {
        pending = null;
        Refresh(true);
    }

    // Brings the rows up to date (labels like the game speed change); only rebuilds them when something changed
    public void Refresh(bool force = false)
    {
        if (pending != null && Time.unscaledTime > pendingUntil) pending = null;

        StringBuilder signature = new StringBuilder();
        foreach (Entry entry in entries) signature.Append(LabelOf(entry)).Append('|');
        if (!force && signature.ToString() == shownSignature) return;
        shownSignature = signature.ToString();

        MenuPanels.ClearChildren(content);
        foreach (Entry entry in entries)
        {
            if (entry.label == null)
            {
                MenuPanels.MakeRow(buttonPrefab, content, "Section", entry.section, null, false);
                continue;
            }
            if (entry.run == null) // text only
            {
                MenuPanels.MakeRow(buttonPrefab, content, "Info", LabelOf(entry), null, false);
                continue;
            }
            Entry captured = entry;
            GameObject row = MenuPanels.MakeRow(buttonPrefab, content, "Cheat", LabelOf(entry), null);
            row.GetComponent<Button>().onClick.AddListener(() => OnRowClicked(captured));
        }
    }

    private string LabelOf(Entry entry)
    {
        if (entry.label == null) return entry.section;
        return entry == pending ? "Tap again to confirm: " + entry.label() : entry.label();
    }

    private void OnRowClicked(Entry entry)
    {
        if (entry.confirm && pending != entry)
        {
            pending = entry;
            pendingUntil = Time.unscaledTime + ConfirmSeconds;
            Refresh(true);
            return;
        }

        pending = null;
        entry.run();
        Refresh(true);
    }
}
