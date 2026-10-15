using System.Collections.Generic;
using System.Text;

// Self-awareness: what a character gains from every duplicate pull. Each character type defines its own
// levels (CharacterType.awarenessLevels), and each level has its own buffs. A character at level N has
// the buffs of levels 1..N, multiplied together.

public enum BuffStat
{
    Productivity,  // output of the building they work in
    MoveSpeed,     // walking speed
    DailyWage,     // what they cost per day (below 1 is cheaper)
    MoodRecovery,  // mood gained while resting (and from any other positive mood effect)
    MoodDrain,     // mood lost to anything that drains it (below 1 is better)
}

[System.Serializable]
public class AwarenessBuff
{
    public BuffStat stat = BuffStat.Productivity;
    public float multiplier = 1.05f;
}

[System.Serializable]
public class AwarenessLevel
{
    public string description;                         // optional text for the UI; the buffs are listed automatically when empty
    public List<AwarenessBuff> buffs = new List<AwarenessBuff>();

    public string Describe()
    {
        if (!string.IsNullOrEmpty(description)) return description;

        StringBuilder text = new StringBuilder();
        foreach (AwarenessBuff buff in buffs)
        {
            if (text.Length > 0) text.Append(", ");
            text.Append($"{buff.stat} x{buff.multiplier:0.##}");
        }
        return text.Length > 0 ? text.ToString() : "no effect";
    }
}
