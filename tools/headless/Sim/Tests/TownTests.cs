using System.Linq;
using UnityEngine;

public static class TownTests
{
    public static void Run()
    {
        Town t = Town.Build();
        T.Section("building");
        Building home = t.Place(t.Home, 0, 0);
        Building mine = t.Place(t.Mine, 10, 10);
        T.Check(home != null && mine != null, "buildings are built");
        T.Check(t.Place(t.Home, 1, 1) == null, "an overlapping building is refused");
        T.Check(t.bm.IsCellOccupied(new Vector2Int(9, 9)) && t.bm.IsCellOccupied(new Vector2Int(11, 11)) && !t.bm.IsCellOccupied(new Vector2Int(12, 12)), "a 3x3 footprint is centred on its anchor");
        T.Check(t.bm.GetBuildingAt(new Vector2Int(11, 9)) == mine && mine.Producer != null && home.Producer == null, "buildings are found by cell; only producers produce");

        T.Section("characters and jobs");
        OwnedCharacter firsy = t.Hire(t.Firsy, 5, 5);
        T.Check(firsy != null && firsy.HasAvatar && firsy.HomeBuilding == home, "a hired character gets a body and moves into the free home");
        T.Check(t.cm.AssignWorker(firsy, mine) && firsy.WorkBuilding == mine && mine.Workers.Contains(firsy), "assigning a job");

        T.Section("details panel (names from docs/NAMING.md)");
        t.ui.ShowCharacterDetails(firsy);
        string text = t.detailsText.text;
        T.Check(t.detailsPanel.activeInHierarchy && text.Contains("Name: Firsy") && text.Contains("Satiety: ") && text.Contains("Job: Gold Mine") && text.Contains("Awareness: ") && text.Contains("Home: Home"), "the details say Satiety, Job, Awareness: " + text.Replace("\n", " | "));
        T.Check(!text.Contains("Fed:") && !text.Contains("Working Location") && !text.Contains("Self-awareness"), "and none of the old names");
    }
}
