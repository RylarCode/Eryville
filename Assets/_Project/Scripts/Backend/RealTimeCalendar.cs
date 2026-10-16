using System;

// Turns the real time of day into the game's four times of day (Morning / Afternoon / Evening / Night), and
// real days into game days. A game day starts at the beginning of Morning, which is when wages are paid.
// Pure date arithmetic, with no clock of its own: it is given the player's local time.
public class RealTimeCalendar
{
    public float morningHour = 6f;     // Morning   06:00 - 12:00
    public float afternoonHour = 12f;  // Afternoon 12:00 - 18:00
    public float eveningHour = 18f;    // Evening   18:00 - 22:00
    public float nightHour = 22f;      // Night     22:00 - 06:00

    private static readonly DateTime Epoch = new DateTime(2000, 1, 1);

    public TimeOfDay PhaseAt(DateTime local)
    {
        double hour = local.TimeOfDay.TotalHours;
        if (hour >= nightHour || hour < morningHour) return TimeOfDay.Night;
        if (hour >= eveningHour) return TimeOfDay.Evening;
        if (hour >= afternoonHour) return TimeOfDay.Afternoon;
        return TimeOfDay.Morning;
    }

    // When the phase that contains this moment began
    public DateTime PhaseStart(DateTime local)
    {
        DateTime date = local.Date;
        switch (PhaseAt(local))
        {
            case TimeOfDay.Morning: return date.AddHours(morningHour);
            case TimeOfDay.Afternoon: return date.AddHours(afternoonHour);
            case TimeOfDay.Evening: return date.AddHours(eveningHour);
            default: return local.TimeOfDay.TotalHours >= nightHour ? date.AddHours(nightHour) : date.AddDays(-1).AddHours(nightHour);
        }
    }

    // When the next phase begins
    public DateTime NextPhaseStart(DateTime local)
    {
        DateTime date = local.Date;
        switch (PhaseAt(local))
        {
            case TimeOfDay.Morning: return date.AddHours(afternoonHour);
            case TimeOfDay.Afternoon: return date.AddHours(eveningHour);
            case TimeOfDay.Evening: return date.AddHours(nightHour);
            default: return local.TimeOfDay.TotalHours >= nightHour ? date.AddDays(1).AddHours(morningHour) : date.AddHours(morningHour);
        }
    }

    // A running count of game days: goes up by one every morning (at morningHour), so it can be compared
    public int DayNumber(DateTime local)
    {
        return (int)Math.Floor((local - TimeSpan.FromHours(morningHour) - Epoch).TotalDays);
    }
}
