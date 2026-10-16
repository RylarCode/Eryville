using System.Collections.Generic;
using UnityEngine;

// What happened while the game was closed. Production and wages go by the real clock, so coming back after a few hours
// should not feel like nothing happened. The characters are not simulated while away: the resources are worked out
// from the buildings instead, as if every building with paid workers had been running part of the time.
//
// The numbers here are simple placeholders: the efficiency says how much of the away time counts (workers walk,
// rest and sleep), and the cap says how long away is counted at most, so leaving the game for a week is not a jackpot.
public static class OfflineProgress
{
    public static float MaxSeconds = 8f * 3600f;   // at most this much away time counts
    public static float MinSeconds = 60f;          // a short absence (switching apps) is not worth a report
    public static float Efficiency = 0.5f;         // the share of that time buildings are producing

    public struct Report
    {
        public float seconds;   // away time that counted (after the cap)
        public float gold;
        public float wood;

        public bool Anything => gold > 0.5f || wood > 0.5f;
        public override string ToString() { return $"Away {seconds / 3600f:0.#} h: +{gold:0} gold, +{wood:0} wood"; }
    }

    public static Report LastReport { get; private set; }

    // Gold and wood the buildings make in the given away time. A building only counts if it has workers and at
    // least one of them is paid (an unpaid worker stays home); its output is its base rate times its workers' productivity.
    public static Report Compute(IEnumerable<Building> buildings, float awaySeconds)
    {
        Report report = new Report { seconds = Mathf.Clamp(awaySeconds, 0f, MaxSeconds) };
        if (awaySeconds < MinSeconds) { report.seconds = 0f; return report; }

        foreach (Building building in buildings)
        {
            BuildingType type = building.buildingType;
            if (type == null || building.Producer == null) continue;

            float multiplier = 1f;
            int paidWorkers = 0;
            foreach (OwnedCharacter worker in building.Workers)
            {
                if (!worker.WagePaid) continue;
                paidWorkers++;
                multiplier *= worker.Productivity;
            }
            if (building.WorkerSlots > 0 && paidWorkers == 0) continue;

            report.gold += type.goldGenerationPerSec * multiplier * report.seconds * Efficiency;
            report.wood += type.woodGenerationPerSec * multiplier * report.seconds * Efficiency;
        }
        return report;
    }

    // Works out and gives what was earned. Returns the report.
    public static Report Apply(float awaySeconds)
    {
        Report report = Compute(BuildingManager.Instance.Buildings, awaySeconds);
        if (report.Anything)
        {
            ResourceManager.Instance.changeResourceAmount(ResourceManager.Gold, report.gold);
            ResourceManager.Instance.changeResourceAmount(ResourceManager.Wood, report.wood);
        }
        LastReport = report;
        Debug.Log("Offline progress: " + report);
        return report;
    }
}
