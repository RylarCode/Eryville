using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Saves and loads the game to a JSON file in Application.persistentDataPath.
//
// Save: reads the state out of the managers (the single sources of truth) into GameData.
// Load: validates the file first, and only then clears the world and rebuilds it through the same
// managers the game itself uses (BuildingManager.Build, CharacterManager.Restore / SpawnAvatar...),
// so everything a loaded building or character needs (sprite, size, registration, assignments) is set up.
public static class SaveSystem
{
    public const int CurrentVersion = 9; // 2 added roads, 3 added mood, 4 added wages (and recruitment), 5 the real-time clock, 6 the chosen recipe of each building (items are saved with the resources), 7 how full each character is, 8 what each character has been eating, 9 the story (finished chapters and values); older saves still load with defaults
    public const string DefaultFileName = "savegame.json";

    private static string SlotFor(string fileName)
    {
        return string.IsNullOrEmpty(fileName) ? DefaultFileName : fileName;
    }

    public static bool HasSave(string fileName = DefaultFileName)
    {
        bool has = false;
        Backend.Current.HasBlob(SlotFor(fileName), result => has = result);
        return has;
    }

    // Deletes the save (if there is one)
    public static bool Delete(string fileName = DefaultFileName)
    {
        bool existed = HasSave(fileName);
        bool ok = false;
        Backend.Current.DeleteBlob(SlotFor(fileName), result => ok = result.ok);
        return existed && ok;
    }

    // --- Save ---

    // Hands the game to the backend as one piece of text. Returns whether it went through; with a backend that
    // answers later it returns true once the request is sent.
    public static bool Save(string fileName = DefaultFileName)
    {
        if (StressTest.IsActive)
        {
            Debug.LogWarning("Not saving: a stress test is running (Clear it first), so its characters and buildings do not end up in the save.");
            return false;
        }

        string json;
        try
        {
            json = JsonUtility.ToJson(Capture(), true);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save game: {e}");
            return false;
        }

        bool answered = false, ok = false;
        Backend.Current.SaveBlob(SlotFor(fileName), json, result => { answered = true; ok = result.ok; });
        return answered ? ok : true;
    }

    private static GameData Capture()
    {
        GameData data = new GameData { version = CurrentVersion };

        foreach (Resource resource in ResourceManager.Instance.resources.Values)
        {
            data.resources.Add(new ResourceData { name = resource.resourceName, amount = resource.currentAmount });
        }

        data.timeOfDay = (int)GameClock.Instance.Current;
        data.phaseElapsed = GameClock.Instance.Elapsed;
        data.worldState = (int)GameManager.Instance.worldState;

        foreach (Building building in BuildingManager.Instance.Buildings)
        {
            if (building.buildingType == null) continue;
            data.buildings.Add(new BuildingData
            {
                id = building.Id,
                typeId = building.buildingType.typeId,
                cellX = building.Cell.x,
                cellY = building.Cell.y,
                position = building.transform.position,
                recipeIndex = building.Producer != null ? building.Producer.ActiveRecipeIndex : 0,
            });
        }

        foreach (KeyValuePair<Vector2Int, RoadType> road in MapGrid.Instance.Roads)
        {
            data.roads.Add(new RoadData { cellX = road.Key.x, cellY = road.Key.y, typeId = road.Value.typeId });
        }

        foreach (OwnedCharacter owned in CharacterManager.Instance.Roster)
        {
            data.characters.Add(new CharacterData
            {
                id = owned.Id,
                typeId = owned.Type.typeId,
                displayName = owned.DisplayName,
                level = owned.Level,
                loyalty = owned.Loyalty,
                mood = owned.Mood,
                wagePaid = owned.WagePaid,
                satiety = owned.Satiety,
                lastMealKey = owned.Hunger.LastMealKey,
                recentMeals = new List<string>(owned.Hunger.RecentMeals),
                awareness = owned.AwarenessLevel,
                workBuildingId = owned.WorkBuilding != null ? owned.WorkBuilding.Id : "",
                homeBuildingId = owned.HomeBuilding != null ? owned.HomeBuilding.Id : "",
                hasAvatar = owned.HasAvatar,
                position = owned.HasAvatar ? owned.Avatar.transform.position : Vector3.zero,
            });
        }

        data.lastSeenUtcTicks = Backend.Current.ServerUtcNow.Ticks;
        if (WageSystem.Instance != null) data.payrollDay = WageSystem.Instance.LastPayrollDay;

        if (GachaSystem.Instance != null)
        {
            data.gachaTotalPulls = GachaSystem.Instance.TotalPulls;
            data.gachaPity.AddRange(GachaSystem.Instance.PityCounters);
        }

        if (StoryManager.Instance != null) StoryManager.Instance.Export(data);

        return data;
    }

    // --- Load ---

    // Asks the backend for the saved game and rebuilds the world from it. Returns whether it worked; with a backend
    // that answers later it returns true once the request is sent.
    // finished (optional) is called with the outcome once the load is over, whenever the backend answers.
    public static bool Load(string fileName = DefaultFileName, Action<bool> finished = null)
    {
        bool answered = false, ok = false;
        Backend.Current.LoadBlob(SlotFor(fileName), (result, json) =>
        {
            answered = true;
            ok = result.ok && json != null && Apply(json);
            finished?.Invoke(ok);
        });
        return answered ? ok : true;
    }

    // Rebuilds the world from the text of a save. Checks it first and changes nothing if it is no good.
    private static bool Apply(string json)
    {
        // 1. Read and check the text BEFORE touching the running game, so a bad file loses nothing
        GameData data;
        try
        {
            data = JsonUtility.FromJson<GameData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"Could not read save file: {e.Message}");
            return false;
        }
        if (data == null || data.version < 1 || data.version > CurrentVersion)
        {
            Debug.LogError($"Unsupported or corrupt save file (version {data?.version}).");
            return false;
        }

        Dictionary<int, BuildingType> buildingTypes = IndexBuildingTypes();
        Dictionary<int, CharacterType> characterTypes = IndexCharacterTypes();
        Dictionary<int, RoadType> roadTypes = IndexRoadTypes();

        // 2. Clear the world through the managers, so their registries are emptied too
        CharacterManager.Instance.ClearAll();
        BuildingManager.Instance.ClearAll();
        RoadManager.Instance.ClearAll();

        // 3. Global state
        foreach (ResourceData resource in data.resources)
        {
            ResourceManager.Instance.SetResourceAmount(resource.name, resource.amount);
        }
        if (Enum.IsDefined(typeof(WorldState), data.worldState)) GameManager.Instance.worldState = (WorldState)data.worldState;
        if (Enum.IsDefined(typeof(TimeOfDay), data.timeOfDay)) GameClock.Instance.SetTime((TimeOfDay)data.timeOfDay, data.phaseElapsed);

        // 4. Buildings (no cost is charged: they were already paid for)
        Dictionary<string, Building> buildingsById = new Dictionary<string, Building>();
        foreach (BuildingData saved in data.buildings)
        {
            if (!buildingTypes.TryGetValue(saved.typeId, out BuildingType type))
            {
                Debug.LogWarning($"Save refers to unknown building type {saved.typeId}; skipped.");
                continue;
            }

            Building building = BuildingManager.Instance.Build(type, new Vector2Int(saved.cellX, saved.cellY), saved.position, saved.id);
            if (building == null)
            {
                Debug.LogWarning($"Could not rebuild {type.buildingName} at ({saved.cellX},{saved.cellY}); skipped.");
                continue;
            }
            if (data.version >= 6 && building.Producer != null) building.Producer.ActiveRecipeIndex = saved.recipeIndex;
            buildingsById[building.Id] = building;
        }

        // 5. Roads (before the characters, so their first walk already knows about them)
        int roadCount = 0;
        foreach (RoadData saved in data.roads)
        {
            if (!roadTypes.TryGetValue(saved.typeId, out RoadType type))
            {
                Debug.LogWarning($"Save refers to unknown road type {saved.typeId}; skipped.");
                continue;
            }
            if (RoadManager.Instance.Build(type, new Vector2Int(saved.cellX, saved.cellY))) roadCount++;
        }

        // 6. Characters: restore the record, re-attach job and home, then give it its body back
        foreach (CharacterData saved in data.characters)
        {
            if (!characterTypes.TryGetValue(saved.typeId, out CharacterType type))
            {
                Debug.LogWarning($"Save refers to unknown character type {saved.typeId}; skipped.");
                continue;
            }

            OwnedCharacter owned = new OwnedCharacter(type, saved.id);
            if (!string.IsNullOrEmpty(saved.displayName)) owned.DisplayName = saved.displayName;
            owned.Level = saved.level;
            owned.Loyalty = saved.loyalty;
            if (data.version >= 3) owned.Mood = saved.mood; // older saves have no mood: keep the type's starting mood
            if (data.version >= 7) { owned.Satiety = saved.satiety; owned.Hunger.LastMealKey = saved.lastMealKey; }
            if (data.version >= 8 && saved.recentMeals != null) owned.Hunger.RecentMeals = new List<string>(saved.recentMeals); // older saves: everyone starts full
            if (data.version >= 4)
            {
                owned.WagePaid = saved.wagePaid;
                owned.AwarenessLevel = saved.awareness;
            }
            CharacterManager.Instance.Restore(owned);

            if (TryGetBuilding(buildingsById, saved.workBuildingId, out Building work) && !CharacterManager.Instance.AssignWorker(owned, work))
            {
                Debug.LogWarning($"{owned.DisplayName} could not be reassigned to work at {work.DisplayName} (no free slot).");
            }
            if (TryGetBuilding(buildingsById, saved.homeBuildingId, out Building home) && !CharacterManager.Instance.AssignResident(owned, home))
            {
                Debug.LogWarning($"{owned.DisplayName} could not move back into {home.DisplayName} (no free housing).");
            }

            // Spawned last so it already knows its job and home when it picks where to go
            if (saved.hasAvatar) CharacterManager.Instance.SpawnAvatar(owned, saved.position);
        }

        if (GachaSystem.Instance != null)
        {
            if (data.version >= 4) GachaSystem.Instance.RestoreState(data.gachaTotalPulls, data.gachaPity);
            else GachaSystem.Instance.RestoreState(0, null);
        }

        if (data.version >= 5 && WageSystem.Instance != null) WageSystem.Instance.LastPayrollDay = data.payrollDay;

        // The story (an older save has none: it starts from the beginning)
        if (StoryManager.Instance != null) StoryManager.Instance.Restore(data);

        // Time away: in real time the world carried on while the game was closed
        if (data.version >= 5 && data.lastSeenUtcTicks > 0 && GameClock.Instance != null && GameClock.Instance.IsRealTime)
        {
            DateTime lastSeen = new DateTime(data.lastSeenUtcTicks, DateTimeKind.Utc);
            OfflineProgress.Apply((float)(Backend.Current.ServerUtcNow - lastSeen).TotalSeconds);
        }

        Debug.Log($"Game loaded: {buildingsById.Count} buildings, {roadCount} road cells, {CharacterManager.Instance.Roster.Count} characters.");
        return true;
    }

    private static bool TryGetBuilding(Dictionary<string, Building> buildingsById, string id, out Building building)
    {
        building = null;
        return !string.IsNullOrEmpty(id) && buildingsById.TryGetValue(id, out building);
    }

    // The types a save can refer to: everything the placer / hire menu knows about
    private static Dictionary<int, BuildingType> IndexBuildingTypes()
    {
        Dictionary<int, BuildingType> index = new Dictionary<int, BuildingType>();
        foreach (BuildingType type in BuildingPlacer.Instance.AllBuildings)
        {
            if (type == null) continue;
            if (index.ContainsKey(type.typeId))
            {
                Debug.LogWarning($"Duplicate BuildingType typeId {type.typeId} ({type.buildingName}); saves may load the wrong building.");
                continue;
            }
            index[type.typeId] = type;
        }
        return index;
    }

    private static Dictionary<int, RoadType> IndexRoadTypes()
    {
        Dictionary<int, RoadType> index = new Dictionary<int, RoadType>();
        foreach (RoadType type in RoadManager.Instance.RoadTypes)
        {
            if (type == null) continue;
            if (index.ContainsKey(type.typeId))
            {
                Debug.LogWarning($"Duplicate RoadType typeId {type.typeId} ({type.displayName}); saves may load the wrong road.");
                continue;
            }
            index[type.typeId] = type;
        }
        return index;
    }

    private static Dictionary<int, CharacterType> IndexCharacterTypes()
    {
        Dictionary<int, CharacterType> index = new Dictionary<int, CharacterType>();
        foreach (CharacterType type in CharacterManager.Instance.AllCharacters)
        {
            if (type == null) continue;
            if (index.ContainsKey(type.typeId))
            {
                Debug.LogWarning($"Duplicate CharacterType typeId {type.typeId} ({type.displayName}); saves may load the wrong character.");
                continue;
            }
            index[type.typeId] = type;
        }
        return index;
    }
}
