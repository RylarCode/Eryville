using System;
using UnityEngine;

// Pays the wages. At the start of every day (the Morning phase) each owned character is paid their
// daily wage out of gold, in roster order. In the game's own clock that is when Morning begins. In real time
// it is the first moment the game sees that a new real day has started (which can be when it is opened):
// one payday for the new day, however many days were missed. Whoever cannot be paid is marked unpaid: they refuse to work
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

    // The game day the last payday was for (real time only; -1 = none yet). Saved with the game.
    public int LastPayrollDay { get; set; } = -1;

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
        if (GameClock.Instance != null && GameClock.Instance.IsRealTime) return; // real time works by the day, see Update
        if (time == TimeOfDay.Morning && !StressTest.IsActive) RunPayroll(); // a stress test makes the days fly by: no wages meanwhile
    }

    private void Update()
    {
        GameClock clock = GameClock.Instance;
        if (clock == null || !clock.IsRealTime || StressTest.IsActive) return;

        int day = clock.DayNumber;
        if (LastPayrollDay < 0) { LastPayrollDay = day; return; } // a new game: the first payday is the next morning
        if (day <= LastPayrollDay) return;

        LastPayrollDay = day;
        RunPayroll();
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
        if (LastDue > 0)
        {
            if (LastUnpaidCount == 0) Notify.Show($"Wages paid: {LastPaid} gold", NotifyKind.Success, null, null, "wages");
            else Notify.Show($"Could not pay {LastUnpaidCount} {(LastUnpaidCount == 1 ? "wage" : "wages")} ({LastPaid}/{LastDue} gold paid): they stop working until paid", NotifyKind.Alert, null, null, "wages");
        }
        PayrollRun?.Invoke();
    }
}
