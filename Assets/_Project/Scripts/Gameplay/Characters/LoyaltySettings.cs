using UnityEngine;

// Every number of the daily loyalty rules in one place. The asset lives at Resources/LoyaltySettings; if it is missing the defaults below
// are used. Loyalty runs from 0 to 100, starts at 0 for a new character, and changes once a day, at payday (see LoyaltyRules), so it is
// slow, and the same whether the game was open or not. Story choices and quest rewards can move it too.
[CreateAssetMenu(fileName = "LoyaltySettings", menuName = "Scriptable Objects/Loyalty Settings")]
public class LoyaltySettings : ScriptableObject
{
    [Header("Wages")]
    [Tooltip("Paid, and in a good mood")]
    public float paidHappy = 2f;
    [Tooltip("The mood that counts as good")]
    public float happyMood = 60f;
    [Tooltip("Paid, ordinary mood")]
    public float paid = 1f;
    [Tooltip("Not paid (it also costs mood: see WageSystem)")]
    public float unpaid = -3f;

    [Header("Living")]
    [Tooltip("In a bad mood")]
    public float unhappy = -1f;
    [Tooltip("The mood below which a character counts as unhappy")]
    public float unhappyMood = 30f;
    [Tooltip("Has no home")]
    public float homeless = -1f;

    private static LoyaltySettings current;

    public static LoyaltySettings Current
    {
        get
        {
            if (current == null) current = Resources.Load<LoyaltySettings>("LoyaltySettings");
            if (current == null) current = CreateInstance<LoyaltySettings>();
            return current;
        }
        set { current = value; }
    }
}
