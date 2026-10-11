using System.Collections.Generic;
using UnityEngine;

// The roster: who has been hired, and the rules for hiring, dismissing and assigning them.
public class CharacterManager : MonoBehaviour
{
    public static CharacterManager Instance { get; private set; }

    public List<CharacterType> AllCharacters; // Drag all ScriptableObjects here OR load them dynamically
    public List<Character> AllHiredCharacters;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // Every character re-reads its schedule whenever the time of day changes
        if (GameClock.Instance != null) GameClock.Instance.TimeOfDayChanged += OnTimeOfDayChanged;
    }

    private void OnDestroy()
    {
        if (GameClock.Instance != null) GameClock.Instance.TimeOfDayChanged -= OnTimeOfDayChanged;
    }

    // --- Hiring and dismissing ---

    // Creates a character of the given type at spawnPoint and adds it to the roster.
    // Costs are the caller's business (e.g. the future gacha).
    public Character Hire(CharacterType type, Vector3 spawnPoint)
    {
        if (type == null || type.characterPrefab == null)
        {
            Debug.LogError("CharacterManager.Hire: character type or its prefab is missing.");
            return null;
        }

        GameObject newCharGO = Instantiate(type.characterPrefab, spawnPoint, Quaternion.identity);
        newCharGO.name = type.displayName;

        Character character = newCharGO.GetComponent<Character>();
        if (character == null)
        {
            Debug.LogError($"Prefab for {type.displayName} has no Character component.");
            Destroy(newCharGO);
            return null;
        }

        character.Initialize(type);
        AllHiredCharacters.Add(character);
        return character;
    }

    // Removes a character from the game: frees their job and home first, so no building keeps a stale reference.
    public void Dismiss(Character character)
    {
        if (character == null) return;

        character.WorkBuilding?.RemoveWorker(character);
        character.HomeBuilding?.RemoveResident(character);
        AllHiredCharacters.Remove(character);
        Destroy(character.gameObject);
    }

    // --- Assigning ---

    // Returns false if the building is full (or the character already works there)
    public bool AssignWorker(Character character, Building building)
    {
        return building != null && building.TryAssignWorker(character);
    }

    public void UnassignWorker(Character character, Building building)
    {
        building?.RemoveWorker(character);
    }

    // Returns false if the building has no free housing (or the character already lives there)
    public bool AssignResident(Character character, Building building)
    {
        return building != null && building.TryAssignResident(character);
    }

    public void UnassignResident(Character character, Building building)
    {
        building?.RemoveResident(character);
    }

    // --- Schedule ---

    // Every character re-reads its schedule when the time of day changes
    public void OnTimeOfDayChanged(TimeOfDay newTime)
    {
        foreach (Character character in AllHiredCharacters)
        {
            character.ApplySchedule(newTime);
        }
    }

    // Where a character goes for a Rest1 / Rest2 schedule slot: the nearest built building among the
    // character type's rest options for this slot and the current world state. With no match
    // (no option defined, e.g. in war, or nothing built yet) the character stays home.
    public Building ResolveRestPlace(Character character, ScheduleSlot slot)
    {
        WorldState state = GameManager.Instance != null ? GameManager.Instance.worldState : WorldState.Peace;
        CharacterType type = character.characterType;

        Building best = null;
        float bestDistance = float.MaxValue;
        if (type != null && type.restOptions != null)
        {
            foreach (RestOption option in type.restOptions)
            {
                if (option.slot != slot || option.worldState != state || option.place == null) continue;

                Building candidate = BuildingManager.Instance.FindNearest(option.place, character.transform.position);
                if (candidate == null) continue;

                float distance = Vector3.Distance(candidate.transform.position, character.transform.position);
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
        }

        return best != null ? best : character.HomeBuilding;
    }

    // --- Lookups ---

    public Building FindHomeWithSpace()
    {
        foreach(Building building in BuildingManager.Instance.Buildings)
        {
            if (building.HasHousingSlot)
            {
                return building;
            }
        }

        return null;
    }

    public Character FindHomeless()
    {
        foreach (Character character in AllHiredCharacters)
        {
            if (character.HomeBuilding == null)
            {
                return character;
            }
        }

        return null;
    }
}
