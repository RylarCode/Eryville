using System.Collections.Generic;

// Where, in general terms, a character wants to be at a given time of day.
// Home and Work are the character's assigned buildings; Rest1 / Rest2 are resolved from the
// character type's rest options (see CharacterManager.ResolveRestPlace).
public enum ScheduleSlot { Home, Work, Rest1, Rest2, Others }

// One row of a CharacterType's default schedule (edited in the Inspector).
[System.Serializable]
public class ScheduleEntry
{
    public TimeOfDay time;
    public ScheduleSlot destination;
}

// Runtime lookup built from a type's schedule rows. Each character builds its own,
// so editing one never touches the shared asset.
public class CharacterSchedule
{
    private readonly Dictionary<TimeOfDay, ScheduleSlot> slots = new Dictionary<TimeOfDay, ScheduleSlot>();

    public CharacterSchedule(IEnumerable<ScheduleEntry> entries)
    {
        if (entries == null) return;
        foreach (ScheduleEntry entry in entries)
        {
            slots[entry.time] = entry.destination; // if a time appears twice the last row wins
        }
    }

    // False when the schedule has no row for that time (the character then keeps doing what it was doing)
    public bool TryGetSlot(TimeOfDay time, out ScheduleSlot slot)
    {
        return slots.TryGetValue(time, out slot);
    }
}
