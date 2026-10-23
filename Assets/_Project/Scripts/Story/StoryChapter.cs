using System;
using System.Collections.Generic;
using UnityEngine;

public enum StoryTrigger
{
    Automatic, // plays by itself as soon as its requirements are met (and nothing else is going on)
    Talk,      // plays when the player selects the "Talk to" character and taps Talk, once the requirements are met
    Manual     // only plays when something starts it (the Story menu's replay, a cheat, later: an event)
}

[Serializable]
public class StoryCast
{
    public string name;      // as it is written in the script, before the colon
    public Sprite portrait;
}

// One piece of the story: a few scenes of dialogue, when they play, and what finishing them gives. Make one with
// Assets > Create > Scriptable Objects > Story Chapter (or Eryville > New Story Chapter) in a Resources/Story folder, and the game finds it.
// How to write the script: see StoryScript, or docs/STORY.md.
[CreateAssetMenu(fileName = "NewChapter", menuName = "Scriptable Objects/Story Chapter")]
public class StoryChapter : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Short unique name used by saves and by other chapters (e.g. 'welcome'). Empty: the asset's name. Do not change it once players have saves.")]
    public string id;
    public string title;
    [TextArea(2, 4)] [Tooltip("Shown in the Story menu once the chapter is done.")]
    public string summary;
    [Tooltip("Chapters that could start at once go in this order (lowest first).")]
    public int order;

    [Header("When it plays")]
    public StoryTrigger trigger = StoryTrigger.Automatic;
    [Tooltip("Talk chapters: whose Talk button starts it.")]
    public CharacterType talkTo;
    [Tooltip("Everything here must be true before it can play. Put 'Chapter done' here to put chapters in order.")]
    public List<StoryCondition> requires = new List<StoryCondition>();
    [TextArea(1, 3)] [Tooltip("Optional hint for the Story menu while this is the next step, e.g. 'Build a Gold Mine'.")]
    public string goal;

    [Header("When it ends")]
    [Tooltip("Given once, when the chapter finishes.")]
    public List<StoryEffect> rewards = new List<StoryEffect>();

    [Header("The scenes")]
    [Tooltip("Pictures for the people who speak but are not characters of the game (a character of that name is found by itself).")]
    public List<StoryCast> cast = new List<StoryCast>();
    [TextArea(12, 40)] [Tooltip("The dialogue, one thing per line. See docs/STORY.md.")]
    public string script;
    [Tooltip("Optional: a text file with the dialogue instead of the box above (easier to write in a text editor).")]
    public TextAsset scriptFile;

    [NonSerialized] private List<StoryBeat> parsed;
    [NonSerialized] private List<string> problems;

    public string Id => !string.IsNullOrWhiteSpace(id) ? id.Trim() : name;
    public string Title => !string.IsNullOrWhiteSpace(title) ? title.Trim() : Id;
    public string ScriptText => scriptFile != null ? scriptFile.text : script;

    // The scenes, read from the script (once)
    public IReadOnlyList<StoryBeat> Beats
    {
        get { Parse(); return parsed; }
    }

    // What was wrong with the script (lines that could not be read)
    public IReadOnlyList<string> ScriptProblems
    {
        get { Parse(); return problems; }
    }

    private void Parse()
    {
        if (parsed != null) return;
        problems = new List<string>();
        parsed = StoryScript.Parse(ScriptText, problems);
    }

    // Forget what was read (after the script changed)
    public void Reparse()
    {
        parsed = null;
        problems = null;
    }

    private void OnValidate()
    {
        Reparse();
    }

    // The picture for someone who speaks: the chapter's cast first, then a character of the game with that name
    public Sprite PortraitFor(string speaker)
    {
        if (string.IsNullOrWhiteSpace(speaker)) return null;
        foreach (StoryCast member in cast)
        {
            if (member != null && string.Equals(member.name?.Trim(), speaker.Trim(), StringComparison.OrdinalIgnoreCase)) return member.portrait;
        }
        if (CharacterManager.Instance != null)
        {
            foreach (CharacterType type in CharacterManager.Instance.AllCharacters)
            {
                if (type != null && string.Equals(type.displayName?.Trim(), speaker.Trim(), StringComparison.OrdinalIgnoreCase)) return type.PortraitOrSprite;
            }
        }
        return null;
    }
}
