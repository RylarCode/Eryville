using System;
using System.Collections.Generic;

// Where the player is in the story: which chapters are finished, and the story values (whole numbers that choices change, such as
// "kindness"). A value nobody has set is 0. Names are not case sensitive. This is what a save keeps (see StoryManager.Export / Restore).
public class StoryState
{
    private readonly Dictionary<string, int> values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // Raised when a value or a finished chapter changes
    public event Action Changed;

    public IEnumerable<KeyValuePair<string, int>> Values => values;
    public IReadOnlyCollection<string> Done => done;

    public int Get(string name)
    {
        return !string.IsNullOrEmpty(name) && values.TryGetValue(name.Trim(), out int value) ? value : 0;
    }

    public void Set(string name, int value)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        name = name.Trim();
        if (Get(name) == value && (value != 0 || !values.ContainsKey(name))) return;
        values[name] = value;
        Changed?.Invoke();
    }

    public void Add(string name, int amount)
    {
        Set(name, Get(name) + amount);
    }

    public bool IsDone(string chapterId)
    {
        return !string.IsNullOrEmpty(chapterId) && done.Contains(chapterId.Trim());
    }

    // Returns whether it was not finished before
    public bool MarkDone(string chapterId)
    {
        if (string.IsNullOrWhiteSpace(chapterId) || !done.Add(chapterId.Trim())) return false;
        Changed?.Invoke();
        return true;
    }

    public void Clear()
    {
        if (values.Count == 0 && done.Count == 0) return;
        values.Clear();
        done.Clear();
        Changed?.Invoke();
    }

    public StoryState Clone()
    {
        StoryState copy = new StoryState();
        foreach (KeyValuePair<string, int> pair in values) copy.values[pair.Key] = pair.Value;
        foreach (string id in done) copy.done.Add(id);
        return copy;
    }

    // Takes over another state's values (not its finished chapters)
    public void ReplaceValues(StoryState other)
    {
        values.Clear();
        foreach (KeyValuePair<string, int> pair in other.values) values[pair.Key] = pair.Value;
        Changed?.Invoke();
    }

    // --- Saving ---

    public void Export(List<StoryValueData> valuesOut, List<string> doneOut)
    {
        valuesOut.Clear();
        doneOut.Clear();
        foreach (KeyValuePair<string, int> pair in values) valuesOut.Add(new StoryValueData { name = pair.Key, value = pair.Value });
        doneOut.AddRange(done);
    }

    public void Import(List<StoryValueData> valuesIn, List<string> doneIn)
    {
        values.Clear();
        done.Clear();
        if (valuesIn != null) foreach (StoryValueData saved in valuesIn) if (saved != null && !string.IsNullOrWhiteSpace(saved.name)) values[saved.name.Trim()] = saved.value;
        if (doneIn != null) foreach (string id in doneIn) if (!string.IsNullOrWhiteSpace(id)) done.Add(id.Trim());
        Changed?.Invoke();
    }
}

[Serializable]
public class StoryValueData
{
    public string name;
    public int value;
}
