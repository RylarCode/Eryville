using System.Collections.Generic;

// One reason a character's loyalty goes up or down at payday. LoyaltyRules adds up what every rule says, once a day.
// New reasons (a favourite workplace, gifts, living with a friend...) are new rules: write a class that implements this and give it to
// LoyaltyRules.Add. Nothing else needs to change.
public interface ILoyaltyRule
{
    // How much this rule changes the character's loyalty today (paid: whether today's wage was paid)
    float DailyChange(OwnedCharacter character, bool paid, LoyaltySettings settings);
}

// The daily loyalty change. WageSystem runs it for everyone right after paying wages.
public static class LoyaltyRules
{
    private static readonly List<ILoyaltyRule> rules = new List<ILoyaltyRule> { new WageLoyaltyRule(), new MoodLoyaltyRule(), new HomeLoyaltyRule() };

    public static IReadOnlyList<ILoyaltyRule> Rules => rules;

    public static void Add(ILoyaltyRule rule)
    {
        if (rule != null && !rules.Contains(rule)) rules.Add(rule);
    }

    public static void Remove(ILoyaltyRule rule)
    {
        rules.Remove(rule);
    }

    // What the rules say for this character today (without changing anything)
    public static float DailyChange(OwnedCharacter character, bool paid)
    {
        LoyaltySettings settings = LoyaltySettings.Current;
        float total = 0f;
        foreach (ILoyaltyRule rule in rules) total += rule.DailyChange(character, paid, settings);
        return total;
    }

    // Changes the character's loyalty by today's amount and remembers it (shown on the details panel). Returns the change.
    public static float ApplyPayday(OwnedCharacter character, bool paid)
    {
        float change = DailyChange(character, paid);
        float before = character.Loyalty;
        character.Loyalty += change;
        character.LastLoyaltyChange = character.Loyalty - before;
        return character.LastLoyaltyChange;
    }
}

// Paid: a little more loyal (more when happy). Not paid: less.
public class WageLoyaltyRule : ILoyaltyRule
{
    public float DailyChange(OwnedCharacter character, bool paid, LoyaltySettings settings)
    {
        if (!paid) return settings.unpaid;
        return character.Mood >= settings.happyMood ? settings.paidHappy : settings.paid;
    }
}

// Unhappy: a little less loyal
public class MoodLoyaltyRule : ILoyaltyRule
{
    public float DailyChange(OwnedCharacter character, bool paid, LoyaltySettings settings)
    {
        return character.Mood < settings.unhappyMood ? settings.unhappy : 0f;
    }
}

// No home: a little less loyal
public class HomeLoyaltyRule : ILoyaltyRule
{
    public float DailyChange(OwnedCharacter character, bool paid, LoyaltySettings settings)
    {
        return character.HomeBuilding == null ? settings.homeless : 0f;
    }
}
