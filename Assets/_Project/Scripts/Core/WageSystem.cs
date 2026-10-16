using System;
using UnityEngine;

// Pays the wages. At the start of every day (the Morning phase) each owned character is paid their
// daily wage out of gold, in roster order. Whoever cannot be paid is marked unpaid: they refuse to work
// until the next payday covers them, and lose some loyalty and mood.
public class WageSystem : MonoBehaviour
{
    public static WageSystem Instance { get; private set; }

    [SerializeField] private float unpaidLoyaltyLoss = 10f;
    [SerializeField] private float unpaidMoodLoss = 10f;

    // What the latest payday did, for the UI
    public int LastDue { get; private set; }
    public int LastPaid { get; private set; }
    public int LastUnpaidCount { get; private set; }

    // Raised after every payday
    public event Action PayrollRun;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (GameClock.Instance != null) GameClock.Instance.TimeOfDayChanged += OnTimeOfDayChanged;
    }

    private void OnDestroy()
    {
        if (GameClock.Instance != null) GameClock.Instance.TimeOfDayChanged -= OnTimeOfDayChanged;
    }

    private void OnTimeOfDayChanged(TimeOfDay time)
    {
        if (time == TimeOfDay.Morning && !StressTest.IsActive) RunPayroll(); // a stress test makes the days fly by: no wages meanwhile
    }

    // What everyone costs per day
    public int TotalDailyWages()
    {
        int total = 0;
        foreach (OwnedCharacter owned in CharacterManager.Instance.Roster) total += owned.DailyWage;
        return total;
    }

    // Pays everyone who can be paid, in roster order. Public so it can also be run by hand (testing).
    public void RunPayroll()
    {
        ResourceManager resources = ResourceManager.Instance;
        LastDue = 0;
        LastPaid = 0;
        LastUnpaidCount = 0;

        foreach (OwnedCharacter owned in CharacterManager.Instance.Roster)
        {
            int wage = owned.DailyWage;
            LastDue += wage;

            if (wage <= 0 || (resources.GetResourceAmount(ResourceManager.Gold) >= wage && resources.changeResourceAmount(ResourceManager.Gold, -wage)))
            {
                LastPaid += wage;
                owned.WagePaid = true;
            }
            else
            {
                LastUnpaidCount++;
                owned.Loyalty -= unpaidLoyaltyLoss;
                owned.Mood -= unpaidMoodLoss;
                owned.WagePaid = false;
            }
        }

        Debug.Log($"Payday: {LastPaid}/{LastDue} gold paid, {LastUnpaidCount} unpaid.");
        PayrollRun?.Invoke();
    }
}
