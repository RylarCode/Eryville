using System.Collections.Generic;

// Walks through one chapter: shows the lines that apply, asks the choices, and keeps the story values the choices change.
// Nothing touches the real story until Commit: the player works on a copy, so leaving half way (or replaying a chapter) changes
// nothing, and a chapter's own later lines can already react to what was picked earlier in it.
public class StoryPlayer
{
    public class ChoiceView
    {
        public string Text;
        public int Index; // into the choice's options
    }

    private class Frame
    {
        public IList<StoryBeat> beats;
        public int next;
    }

    private readonly StoryChapter chapter;
    private readonly StoryState real;
    private readonly IStoryWorld world;
    private readonly StoryState scratch;
    private readonly List<StoryEffect> gives = new List<StoryEffect>();
    private readonly Stack<Frame> frames = new Stack<Frame>();
    private StoryBeat choiceBeat;

    public bool Replay { get; }
    public bool Finished { get; private set; }

    // What is on screen now: a line (Speaker may be "" for narration), or a choice
    public string Speaker { get; private set; } = "";
    public string Text { get; private set; } = "";
    public IReadOnlyList<ChoiceView> Choices => choices;
    private readonly List<ChoiceView> choices = new List<ChoiceView>();
    public bool IsChoice => choices.Count > 0;
    public StoryChapter Chapter => chapter;

    public StoryPlayer(StoryChapter chapter, StoryState state, IStoryWorld world, bool replay = false)
    {
        this.chapter = chapter;
        this.real = state;
        this.world = world;
        this.Replay = replay;
        scratch = state.Clone();
        frames.Push(new Frame { beats = new List<StoryBeat>(chapter.Beats), next = 0 });
        Step();
    }

    // Goes on from a line to the next thing (does nothing while a choice is waiting)
    public void Advance()
    {
        if (Finished || IsChoice) return;
        Step();
    }

    // Picks one of the visible options (by its place in Choices)
    public void Choose(int visibleIndex)
    {
        if (Finished || !IsChoice || visibleIndex < 0 || visibleIndex >= choices.Count) return;
        StoryOption option = choiceBeat.options[choices[visibleIndex].Index];
        foreach (StoryEffect effect in option.effects) effect.Apply(scratch, gives);
        choices.Clear();
        choiceBeat = null;
        if (option.replies.Count > 0) frames.Push(new Frame { beats = option.replies, next = 0 });
        Step();
    }

    private void Step()
    {
        choices.Clear();
        choiceBeat = null;
        while (frames.Count > 0)
        {
            Frame frame = frames.Peek();
            if (frame.next >= frame.beats.Count) { frames.Pop(); continue; }
            StoryBeat beat = frame.beats[frame.next++];
            if (!StoryCondition.All(beat.onlyIf, scratch, world)) continue;

            switch (beat.kind)
            {
                case StoryBeatKind.Effect:
                    beat.effect.Apply(scratch, gives);
                    break;
                case StoryBeatKind.Line:
                    Speaker = beat.speaker ?? "";
                    Text = StoryScript.Fill(beat.text, scratch);
                    return;
                case StoryBeatKind.Choice:
                    for (int i = 0; i < beat.options.Count; i++)
                    {
                        if (!StoryCondition.All(beat.options[i].onlyIf, scratch, world)) continue;
                        choices.Add(new ChoiceView { Text = StoryScript.Fill(beat.options[i].text, scratch), Index = i });
                    }
                    if (choices.Count == 0) break; // nothing to pick from: carry on
                    choiceBeat = beat;
                    Speaker = "";
                    Text = "";
                    return;
            }
        }
        Finished = true;
        Speaker = "";
        Text = "";
    }

    // The chapter is over: the values it changed are kept, its rewards are given, and it counts as done. A replay changes nothing.
    // Returns whether anything was committed.
    public bool Commit()
    {
        if (Replay || !Finished) return false;

        real.ReplaceValues(scratch);
        List<StoryEffect> toGive = new List<StoryEffect>(gives);
        foreach (StoryEffect reward in chapter.rewards) if (reward != null) reward.Apply(real, toGive);
        foreach (StoryEffect give in toGive)
        {
            if (world != null && !string.IsNullOrWhiteSpace(give.name)) world.Give(give.name.Trim(), give.amount);
        }
        real.MarkDone(chapter.Id);
        return true;
    }
}
