using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runs the story: knows every chapter (the StoryChapter assets in any Resources/Story folder), what the player has finished and the
// story values, starts the chapters that are due, and hands each one to the dialogue box. Starts by itself (nothing to add to the scene).
//
//  - Automatic chapters play on their own once their requirements are met and the game is quiet (not placing, no menu open, no dialogue).
//  - Talk chapters play from the Talk button on a character's details.
//  - Buildings can be locked behind a chapter (BuildingType.unlockedBy).
//  - What is finished and the values are saved with the game; a new session starts empty and the save fills it in.
public class StoryManager : MonoBehaviour
{
    public static StoryManager Instance { get; private set; }

    public const float CheckSeconds = 0.5f;
    public const float SettleSeconds = 1.5f; // how long the game must be quiet before a chapter starts by itself

    public StoryState State { get; } = new StoryState();

    // What the story can see of the town; replaced in tests
    public IStoryWorld World { get; set; } = new UnityStoryWorld();

    // Raised when the story values or the finished chapters change (menus and build cards refresh)
    public event Action Changed;
    public event Action<StoryChapter> ChapterFinished;

    private readonly List<StoryChapter> chapters = new List<StoryChapter>();
    private StoryPlayer player;
    private float nextCheck;
    private float quietFor;

    public IReadOnlyList<StoryChapter> Chapters => chapters;
    public bool IsPlaying => player != null;
    public StoryChapter Playing => player != null ? player.Chapter : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("StoryManager");
        go.AddComponent<StoryManager>();
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
        SetChapters(Resources.LoadAll<StoryChapter>("Story"));
        State.Changed += RaiseChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        State.Changed -= RaiseChanged;
        Instance = null;
    }

    private void RaiseChanged()
    {
        Changed?.Invoke();
    }

    // Each scene is a fresh start: the story is empty until a save puts it back
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetState();
    }

    // The chapters the story knows (in the order they are tried). Used at start-up and by tests / tools.
    public void SetChapters(IEnumerable<StoryChapter> found)
    {
        chapters.Clear();
        if (found != null) foreach (StoryChapter chapter in found) if (chapter != null) chapters.Add(chapter);
        chapters.Sort((a, b) =>
        {
            int byOrder = a.order.CompareTo(b.order);
            return byOrder != 0 ? byOrder : string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
        });
        Changed?.Invoke();
    }

    // Forgets all progress (and any dialogue in the middle)
    public void ResetState()
    {
        AbandonDialogue();
        State.Clear();
        quietFor = 0f;
    }

    // --- Saving ---

    public void Export(GameData data)
    {
        State.Export(data.storyValues, data.storyDone);
    }

    // (older saves have no story: it starts from the beginning)
    public void Restore(GameData data)
    {
        AbandonDialogue();
        State.Import(data.storyValues, data.storyDone);
        quietFor = 0f;
    }

    // --- What is due ---

    public bool IsDone(StoryChapter chapter)
    {
        return chapter != null && State.IsDone(chapter.Id);
    }

    // Not finished, and its requirements are met
    public bool IsAvailable(StoryChapter chapter)
    {
        return chapter != null && !State.IsDone(chapter.Id) && StoryCondition.All(chapter.requires, State, World);
    }

    // The chapter that starts next by itself, if any
    public StoryChapter NextAutomatic()
    {
        foreach (StoryChapter chapter in chapters) if (chapter.trigger == StoryTrigger.Automatic && IsAvailable(chapter)) return chapter;
        return null;
    }

    // The chapter this character's Talk button would start, if any
    public StoryChapter TalkChapterFor(CharacterType type)
    {
        if (type == null) return null;
        foreach (StoryChapter chapter in chapters) if (chapter.trigger == StoryTrigger.Talk && chapter.talkTo == type && IsAvailable(chapter)) return chapter;
        return null;
    }

    // Chapters that are the next step: not finished, with every chapter they need finished (what else they need is up to the player).
    // The Story menu shows their goals.
    public List<StoryChapter> NextSteps()
    {
        List<StoryChapter> steps = new List<StoryChapter>();
        foreach (StoryChapter chapter in chapters)
        {
            if (State.IsDone(chapter.Id)) continue;
            bool chaptersDone = true;
            foreach (StoryCondition condition in chapter.requires)
            {
                if (condition != null && condition.kind == StoryCondition.Kind.ChapterDone && !State.IsDone(condition.ChapterId)) { chaptersDone = false; break; }
            }
            if (chaptersDone) steps.Add(chapter);
        }
        return steps;
    }

    public List<StoryChapter> FinishedChapters()
    {
        List<StoryChapter> finished = new List<StoryChapter>();
        foreach (StoryChapter chapter in chapters) if (State.IsDone(chapter.Id)) finished.Add(chapter);
        return finished;
    }

    // --- Locked buildings ---

    public bool IsUnlocked(BuildingType type)
    {
        return type == null || type.unlockedBy == null || State.IsDone(type.unlockedBy.Id);
    }

    public string UnlockHint(BuildingType type)
    {
        return type != null && type.unlockedBy != null ? $"Locked: finish \"{type.unlockedBy.Title}\"" : "";
    }

    // --- Playing ---

    // Whether the game is calm enough to start a chapter by itself
    private bool Quiet()
    {
        if (GameManager.Instance == null || UIManager.Instance == null || UIManager.Instance.Dialogue == null) return false;
        if (GameSession.Instance != null && !GameSession.Instance.Ready) return false;
        if (GameManager.Instance.currentGameState != GameManager.gameState.Normal) return false;
        if (UIManager.Instance.IsRevealOpen) return false;
        if (BuildingPlacer.Instance != null && (BuildingPlacer.Instance.IsPlacing || BuildingPlacer.Instance.IsMoving)) return false;
        if (RoadPlacer.Instance != null && RoadPlacer.Instance.IsPlacing) return false;
        return true;
    }

    private void Update()
    {
        if (player != null) return;
        if (!Quiet()) { quietFor = 0f; return; }

        quietFor += Time.unscaledDeltaTime;
        if (quietFor < SettleSeconds || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + CheckSeconds;

        StoryChapter next = NextAutomatic();
        if (next != null) Play(next);
    }

    // Starts a chapter. replay: the player has seen it: nothing it does is kept and it can be skipped. Returns whether it started.
    public bool Play(StoryChapter chapter, bool replay = false)
    {
        if (chapter == null || player != null) return false;
        DialogueBox dialogue = UIManager.Instance != null ? UIManager.Instance.Dialogue : null;
        if (dialogue == null) return false;

        StoryPlayer started = new StoryPlayer(chapter, State, World, replay);
        if (started.Finished)
        {
            // nothing to show (every line was conditional and none applied): it still counts
            if (started.Commit()) FinishedCommitted(chapter);
            return true;
        }
        player = started;
        dialogue.Open(started, chapter.PortraitFor, replay, OnDialogueClosed);
        return true;
    }

    private void OnDialogueClosed(bool completed)
    {
        StoryPlayer closed = player;
        player = null;
        if (closed != null && completed && closed.Commit()) FinishedCommitted(closed.Chapter);
    }

    private void FinishedCommitted(StoryChapter chapter)
    {
        quietFor = 0f; // let the player breathe before the next chapter
        ChapterFinished?.Invoke(chapter);
        Changed?.Invoke();
        if (GameSession.Instance != null) GameSession.Instance.SaveNow();
    }

    // Closes the dialogue without finishing (a new session, a reset): nothing is kept
    public void AbandonDialogue()
    {
        if (player == null) return;
        player = null;
        if (UIManager.Instance != null && UIManager.Instance.Dialogue != null) UIManager.Instance.Dialogue.Close(false);
    }
}
