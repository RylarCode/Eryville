using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runs the quests: knows every quest (the Quest assets in any Resources/Quests folder), where each one is (saved with the game), starts
// the ones that are due, notices when one is finished, and pays the reward when the player claims it. Starts by itself (nothing to add to
// the scene).
//
//  - A quest whose requirements are met starts by itself (Automatic) or becomes available to accept (Offered).
//  - Main quests come one at a time: the next one starts only after the previous one's reward was claimed.
//  - When every objective holds the quest is Ready (and stays ready); the player claims it in the Quests menu: that is when it counts as Complete.
//  - Nothing fails, is declined or abandoned.
// The conditions and effects are the story's (StoryCondition / StoryEffect), so quests and chapters can start and wait for each other.
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    public const float CheckSeconds = 0.5f;

    public QuestLog Log { get; } = new QuestLog();

    // Raised when any quest changes (menus and the build menu refresh)
    public event Action Changed;

    private readonly List<Quest> quests = new List<Quest>();
    private readonly HashSet<string> claiming = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly StoryState noStory = new StoryState();
    private readonly IStoryWorld fallbackWorld = new UnityStoryWorld();
    private float nextCheck;

    public IReadOnlyList<Quest> Quests => quests;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("QuestManager");
        go.AddComponent<QuestManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        SetQuests(Resources.LoadAll<Quest>("Quests"));
        Log.Changed += RaiseChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Log.Changed -= RaiseChanged;
        Instance = null;
    }

    private void RaiseChanged()
    {
        Changed?.Invoke();
    }

    // Each scene is a fresh start: the quests are empty until a save puts them back
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetState();
    }

    // The quests the game knows (in the order they are tried: by order, then id). Used at start-up and by tests / tools.
    public void SetQuests(IEnumerable<Quest> found)
    {
        quests.Clear();
        if (found != null) foreach (Quest quest in found) if (quest != null) quests.Add(quest);
        quests.Sort((a, b) =>
        {
            int byOrder = a.order.CompareTo(b.order);
            return byOrder != 0 ? byOrder : string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
        });
        Changed?.Invoke();
    }

    public void ResetState()
    {
        claiming.Clear();
        Log.Clear();
    }

    // --- Saving ---

    public void Export(GameData data)
    {
        Log.Export(data.quests);
    }

    // (an older save has no quests: they start from the beginning)
    public void Restore(GameData data)
    {
        claiming.Clear();
        Log.Import(data.quests);
    }

    // --- Looking ---

    private StoryState State => StoryManager.Instance != null ? StoryManager.Instance.State : noStory;
    private IStoryWorld World => StoryManager.Instance != null ? StoryManager.Instance.World : fallbackWorld;

    public QuestStatus StatusOf(string id)
    {
        return Log.Get(id);
    }

    public QuestStatus StatusOf(Quest quest)
    {
        return quest != null ? Log.Get(quest.Id) : QuestStatus.Locked;
    }

    public Quest Find(string id)
    {
        foreach (Quest quest in quests) if (string.Equals(quest.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase)) return quest;
        return null;
    }

    public bool IsClaiming(Quest quest)
    {
        return quest != null && claiming.Contains(quest.Id);
    }

    // The main quest the player is on (available, active or ready), if any
    public Quest CurrentMain()
    {
        foreach (Quest quest in quests)
        {
            if (quest.category != QuestCategory.Main) continue;
            QuestStatus status = StatusOf(quest);
            if (status == QuestStatus.Available || status == QuestStatus.Active || status == QuestStatus.Ready) return quest;
        }
        return null;
    }

    public List<Quest> WithStatus(QuestCategory category, params QuestStatus[] wanted)
    {
        List<Quest> found = new List<Quest>();
        foreach (Quest quest in quests)
        {
            if (quest.category != category) continue;
            if (Array.IndexOf(wanted, StatusOf(quest)) >= 0) found.Add(quest);
        }
        return found;
    }

    // How many quests are waiting for the player: ready to claim, or offered
    public int AttentionCount => Log.Count(QuestStatus.Ready) + Log.Count(QuestStatus.Available);

    // The objectives of a quest as the player reads them, each with whether it is done
    public List<KeyValuePair<string, bool>> ObjectiveLines(Quest quest)
    {
        List<KeyValuePair<string, bool>> lines = new List<KeyValuePair<string, bool>>();
        bool finished = StatusOf(quest) == QuestStatus.Ready || StatusOf(quest) == QuestStatus.Complete;
        foreach (QuestObjective objective in quest.objectives)
        {
            if (objective == null) continue;
            lines.Add(new KeyValuePair<string, bool>(objective.Text(State, World), finished || objective.IsMet(State, World)));
        }
        return lines;
    }

    // --- Moving along ---

    private bool Running()
    {
        if (GameManager.Instance == null) return false;
        if (GameSession.Instance != null && !GameSession.Instance.Ready) return false;
        return true;
    }

    private void Update()
    {
        if (!Running() || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + CheckSeconds;
        Evaluate();
    }

    private bool AnotherMainIsBusy(Quest quest)
    {
        foreach (Quest other in quests)
        {
            if (other == quest || other.category != QuestCategory.Main) continue;
            QuestStatus status = StatusOf(other);
            if (status == QuestStatus.Available || status == QuestStatus.Active || status == QuestStatus.Ready) return true;
        }
        return false;
    }

    // Starts the quests that are due and marks the finished ones ready. Returns whether anything changed.
    public bool Evaluate()
    {
        bool changed = false;
        StoryState state = State;
        IStoryWorld world = World;
        foreach (Quest quest in quests)
        {
            QuestStatus status = StatusOf(quest);
            if (status == QuestStatus.Locked)
            {
                if (!StoryCondition.All(quest.requires, state, world)) continue;
                if (quest.category == QuestCategory.Main && AnotherMainIsBusy(quest)) continue;

                if (quest.start == QuestStart.Offered)
                {
                    Log.Set(quest.Id, QuestStatus.Available);
                    Notify.Show($"New quest available: {quest.Title}", NotifyKind.Info, null, OpenMenu, "quest:" + quest.Id + ":available");
                }
                else
                {
                    Log.Set(quest.Id, QuestStatus.Active);
                    Notify.Show($"New quest: {quest.Title}", NotifyKind.Info, null, OpenMenu, "quest:" + quest.Id + ":new");
                }
                changed = true;
                status = StatusOf(quest);
            }

            if (status == QuestStatus.Active && AllObjectivesMet(quest, state, world))
            {
                Log.Set(quest.Id, QuestStatus.Ready);
                Notify.Show($"Quest ready: {quest.Title} (tap to claim)", NotifyKind.Success, null, OpenMenu, "quest:" + quest.Id + ":ready");
                changed = true;
            }
        }
        return changed;
    }

    private static bool AllObjectivesMet(Quest quest, StoryState state, IStoryWorld world)
    {
        foreach (QuestObjective objective in quest.objectives) if (objective != null && !objective.IsMet(state, world)) return false;
        return true;
    }

    private static void OpenMenu()
    {
        if (UIManager.Instance != null) UIManager.Instance.OpenQuestMenu();
    }

    // The player accepts an offered quest
    public bool Accept(Quest quest)
    {
        if (quest == null || StatusOf(quest) != QuestStatus.Available) return false;
        Log.Set(quest.Id, QuestStatus.Active);
        Evaluate();
        return true;
    }

    // A story chapter (or a cheat) starts a quest whatever its requirements say. Does nothing if it already started.
    public bool BeginQuest(Quest quest)
    {
        if (quest == null) return false;
        QuestStatus status = StatusOf(quest);
        if (status != QuestStatus.Locked && status != QuestStatus.Available) return false;
        Log.Set(quest.Id, QuestStatus.Active);
        Notify.Show($"New quest: {quest.Title}", NotifyKind.Info, null, OpenMenu, "quest:" + quest.Id + ":new");
        Evaluate();
        return true;
    }

    public bool StartById(string id)
    {
        Quest quest = Find(id);
        if (quest == null)
        {
            Debug.LogWarning($"A story started the quest \"{id}\", but there is no quest with that id.");
            return false;
        }
        return BeginQuest(quest);
    }

    // Testing tool: makes an active quest ready without doing its objectives
    public bool ForceReady(Quest quest)
    {
        if (quest == null || StatusOf(quest) != QuestStatus.Active) return false;
        Log.Set(quest.Id, QuestStatus.Ready);
        return true;
    }

    // --- Claiming ---

    // The player claims a ready quest's reward: it is paid (the server pays letters when playing online, which can take a moment or
    // fail) and only then is the quest Complete, which is what lets the next main quest start. finished(true/false) when it is over.
    public bool Claim(Quest quest, Action<bool> finished = null)
    {
        if (quest == null || StatusOf(quest) != QuestStatus.Ready || claiming.Contains(quest.Id)) return false;

        int letters = quest.Letters;
        IGameBackend backend = Backend.Current;
        if (letters > 0 && backend.ServerOwnsBalances)
        {
            claiming.Add(quest.Id);
            Changed?.Invoke();
            backend.ClaimQuest(new ClaimQuestRequest { questId = quest.Id }, response =>
            {
                claiming.Remove(quest.Id);
                if (response == null || !response.ok)
                {
                    Notify.Show($"Could not claim \"{quest.Title}\": {(response != null ? response.error : "no answer")}", NotifyKind.Alert, null, null, "claim-failed:" + quest.Id);
                    Changed?.Invoke();
                    finished?.Invoke(false);
                    return;
                }
                if (ResourceManager.Instance != null) ResourceManager.Instance.SetResourceAmount(ResourceManager.Letters, response.lettersAfter);
                FinishClaim(quest, true, response.lettersGranted > 0);
                finished?.Invoke(true);
            });
            return true;
        }

        FinishClaim(quest, false, true);
        finished?.Invoke(true);
        return true;
    }

    // serverPaidLetters: the server already gave the letters (so the game must not); lettersPaid: whether any were given this time
    private void FinishClaim(Quest quest, bool serverPaidLetters, bool lettersPaid)
    {
        IStoryWorld world = World;
        foreach (StoryEffect reward in quest.rewards)
        {
            if (reward == null) continue;
            switch (reward.kind)
            {
                case StoryEffect.Kind.Give:
                    if (string.IsNullOrWhiteSpace(reward.name)) break;
                    if (serverPaidLetters && reward.name.Trim() == ResourceManager.Letters) break;
                    world.Give(reward.name.Trim(), reward.amount);
                    break;
                case StoryEffect.Kind.Add: State.Add(reward.name, reward.amount); break;
                case StoryEffect.Kind.Set: State.Set(reward.name, reward.amount); break;
                case StoryEffect.Kind.StartQuest: StartById(reward.QuestId); break;
            }
        }

        Log.Set(quest.Id, QuestStatus.Complete);
        string given = quest.RewardText(lettersPaid);
        Notify.Show($"Quest complete: {quest.Title}" + (given.Length > 0 ? $" ({given})" : ""), NotifyKind.Success, null, null, "quest:" + quest.Id + ":complete");
        if (BuildingPlacer.Instance != null)
        {
            foreach (BuildingType type in BuildingPlacer.Instance.AllBuildings)
            {
                if (type != null && type.unlockedByQuest == quest) Notify.Show($"New building unlocked: {type.buildingName}", NotifyKind.Success, type.sprite, null, "unlock:" + type.typeId);
            }
        }
        Evaluate(); // the next main quest can start now
        if (GameSession.Instance != null) GameSession.Instance.SaveNow();
    }
}
