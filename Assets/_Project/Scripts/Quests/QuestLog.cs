using System;
using System.Collections.Generic;

// Where each quest is (by id). A quest nobody has touched is Locked. This is what a save keeps.
public class QuestLog
{
    private readonly Dictionary<string, QuestStatus> status = new Dictionary<string, QuestStatus>(StringComparer.OrdinalIgnoreCase);

    public event Action Changed;

    public QuestStatus Get(string id)
    {
        return !string.IsNullOrEmpty(id) && status.TryGetValue(id.Trim(), out QuestStatus s) ? s : QuestStatus.Locked;
    }

    // Returns whether it changed
    public bool Set(string id, QuestStatus value)
    {
        if (string.IsNullOrWhiteSpace(id) || Get(id) == value) return false;
        status[id.Trim()] = value;
        Changed?.Invoke();
        return true;
    }

    public void Clear()
    {
        if (status.Count == 0) return;
        status.Clear();
        Changed?.Invoke();
    }

    public int Count(QuestStatus value)
    {
        int count = 0;
        foreach (QuestStatus s in status.Values) if (s == value) count++;
        return count;
    }

    public void Export(List<QuestSaveData> into)
    {
        into.Clear();
        foreach (KeyValuePair<string, QuestStatus> pair in status) into.Add(new QuestSaveData { id = pair.Key, status = (int)pair.Value });
    }

    public void Import(List<QuestSaveData> from)
    {
        status.Clear();
        if (from != null)
        {
            foreach (QuestSaveData saved in from)
            {
                if (saved == null || string.IsNullOrWhiteSpace(saved.id) || !Enum.IsDefined(typeof(QuestStatus), saved.status)) continue;
                status[saved.id.Trim()] = (QuestStatus)saved.status;
            }
        }
        Changed?.Invoke();
    }
}
