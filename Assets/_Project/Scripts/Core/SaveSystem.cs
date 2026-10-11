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
    public const int CurrentVersion = 1;
    public const string DefaultFileName = "savegame.json";

    private static string PathFor(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) fileName = DefaultFileName;
        return Path.Combine(Application.persistentDataPath, Path.GetFileName(fileName)); // file name only, no folders
    }

    // --- Save ---

    public static bool Save(string fileName = DefaultFileName)
    {
        string path = PathFor(fileName);
        try
        {
            string json = JsonUtility.ToJson(Capture(), true);

            // Write to a temp file first so a failed write can't destroy the previous save
            string temp = path + ".tmp";
            File.WriteAllText(temp, json);
            File.Copy(temp, path, true);
            File.Delete(temp);

            Debug.Log($"Game saved to {path}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save game to {path}: {e}");
            return false;
        }
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
                cellX = building.cell.x,
                cellY = building.cell.y,
                position = building.transform.position,
            });
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
                workBuildingId = owned.WorkBuilding != null ? owned.WorkBuilding.Id : "",
                homeBuildingId = owned.HomeBuilding != null ? owned.HomeBuilding.Id : "",
                hasAvatar = owned.HasAvatar,
                position = owned.HasAvatar ? owned.Avatar.transform.position : Vector3.zero,
            });
        }

        return data;
    }

    // --- Load ---

    public static bool Load(string fileName = DefaultFileName)
    {
        string path = PathFor(fileName);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"No save file at {path}.");
            return false;
        }

        // 1. Read and check the file BEFORE touching the running game, so a bad file loses nothing
        GameData data;
        try
        {
            data = JsonUtility.FromJson<GameData>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogError($"Could not read save file {path}: {e.Message}");
            return false;
        }
        if (data == null || data.version < 1 || data.version > CurrentVersion)
        {
            Debug.LogError($"Unsupported or corrupt save file (version {data?.version}).");
            return false;
        }

        Dictionary<int, BuildingType> buildingTypes = IndexBuildingTypes();
        Dictionary<int, CharacterType> characterTypes = IndexCharacterTypes();

        // 2. Clear the world through the managers, so their registries are emptied too
        CharacterManager.Instance.ClearAll();
        BuildingManager.Instance.ClearAll();

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
            buildingsById[building.Id] = building;
        }

        // 5. Characters: restore the record, re-attach job and home, then give it its body back
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

        Debug.Log($"Game loaded from {path}: {buildingsById.Count} buildings, {CharacterManager.Instance.Roster.Count} characters.");
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
