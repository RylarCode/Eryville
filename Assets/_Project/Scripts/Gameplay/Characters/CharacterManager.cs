using System;
using System.Collections.Generic;
using UnityEngine;

// The roster: the characters the player owns, and the rules for hiring, dismissing and assigning them.
// Owned characters exist as data; each one may also have a visible Character (avatar) in the world.
public class CharacterManager : MonoBehaviour
{
    public static CharacterManager Instance { get; private set; }

    public List<CharacterType> AllCharacters; // The character types that can be hired (drag the ScriptableObjects here)

    private readonly List<OwnedCharacter> roster = new List<OwnedCharacter>();
    public IReadOnlyList<OwnedCharacter> Roster => roster;

    // Raised whenever someone joins or leaves the roster
    public event Action RosterChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (GetComponent<HungerSystem>() == null) gameObject.AddComponent<HungerSystem>(); // keeps everyone fed
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

    // Adds a new owned character of the given type to the roster, gives it a home if one has space,
    // and puts its avatar in the world at spawnPoint. Costs are the caller's business (e.g. the future gacha).
    // How many characters of this type the player owns
    public int OwnedCount(CharacterType type)
    {
        int count = 0;
        foreach (OwnedCharacter owned in roster)
        {
            if (owned.Type == type) count++;
        }
        return count;
    }

    // Everyone the player owns of this type
    public List<OwnedCharacter> OwnedOf(CharacterType type)
    {
        List<OwnedCharacter> list = new List<OwnedCharacter>();
        foreach (OwnedCharacter owned in roster)
        {
            if (owned.Type == type) list.Add(owned);
        }
        return list;
    }

    // Whether the player may own one more of this type (see CharacterType.maxOwned)
    public bool CanHire(CharacterType type)
    {
        return type != null && (type.maxOwned <= 0 || OwnedCount(type) < type.maxOwned);
    }

    public OwnedCharacter Hire(CharacterType type, Vector3 spawnPoint)
    {
        if (type == null)
        {
            Debug.LogError("CharacterManager.Hire: character type is missing.");
            return null;
        }
        if (!CanHire(type))
        {
            Debug.Log($"Cannot hire another {type.displayName}: the limit is {type.maxOwned}.");
            return null;
        }

        OwnedCharacter owned = new OwnedCharacter(type);
        roster.Add(owned);

        Building home = FindHomeWithSpace();
        if (home != null)
        {
            AssignResident(owned, home);
        }
        else
        {
            Debug.Log("No Home");
        }

        SpawnAvatar(owned, spawnPoint);
        RosterChanged?.Invoke();
        return owned;
    }

    // Adds an already-built owned character to the roster (used when loading a game)
    public void Restore(OwnedCharacter owned)
    {
        if (owned == null || roster.Contains(owned)) return;
        roster.Add(owned);
        RosterChanged?.Invoke();
    }

    // Dismisses everyone (used when loading a game)
    public void ClearAll()
    {
        foreach (OwnedCharacter owned in new List<OwnedCharacter>(roster)) Dismiss(owned);
    }

    // Removes a character from the game: frees their job and home first, so no building keeps a stale reference.
    public void Dismiss(OwnedCharacter character)
    {
        if (character == null) return;

        character.WorkBuilding?.RemoveWorker(character);
        character.HomeBuilding?.RemoveResident(character);
        DespawnAvatar(character);
        roster.Remove(character);
        RosterChanged?.Invoke();
    }

    // --- Avatars ---

    // Puts the character's visible body in the world (does nothing if it already has one)
    public Character SpawnAvatar(OwnedCharacter owned, Vector3 position)
    {
        if (owned.HasAvatar) return owned.Avatar;

        if (owned.Type.characterPrefab == null)
        {
            Debug.LogError($"{owned.Type.displayName} has no character prefab.");
            return null;
        }

        GameObject go = Instantiate(owned.Type.characterPrefab, position, Quaternion.identity);
        go.name = owned.DisplayName;
        if (owned.Type.bodyScale > 0f && owned.Type.bodyScale != 1f) go.transform.localScale *= owned.Type.bodyScale; // (drawn bigger or smaller than the shared prefab)

        Character avatar = go.GetComponent<Character>();
        if (avatar == null)
        {
            Debug.LogError($"Prefab for {owned.Type.displayName} has no Character component.");
            Destroy(go);
            return null;
        }

        avatar.Bind(owned);
        return avatar;
    }

    // Takes the character's body out of the world; the owned character itself stays on the roster
    public void DespawnAvatar(OwnedCharacter owned)
    {
        if (!owned.HasAvatar) return;

        Character avatar = owned.Avatar;
        owned.Avatar = null;
        Destroy(avatar.gameObject);
    }

    // --- Assigning ---

    // Returns false if the building is full (or the character already works there)
    public bool AssignWorker(OwnedCharacter character, Building building)
    {
        return building != null && building.TryAssignWorker(character);
    }

    public void UnassignWorker(OwnedCharacter character, Building building)
    {
        building?.RemoveWorker(character);
    }

    // Returns false if the building has no free housing (or the character already lives there)
    public bool AssignResident(OwnedCharacter character, Building building)
    {
        return building != null && building.TryAssignResident(character);
    }

    public void UnassignResident(OwnedCharacter character, Building building)
    {
        building?.RemoveResident(character);
    }

    // --- Schedule ---

    // Every character re-reads its schedule when the time of day changes
    public void OnTimeOfDayChanged(TimeOfDay newTime)
    {
        foreach (OwnedCharacter owned in roster)
        {
            if (owned.HasAvatar) owned.Avatar.ApplySchedule(newTime);
        }
    }

    // Where a character goes for a Rest1 / Rest2 schedule slot: the nearest built building among the
    // character type's rest options for this slot and the current world state. With no match
    // (no option defined, e.g. in war, or nothing built yet) the character stays home.
    public Building ResolveRestPlace(Character character, ScheduleSlot slot)
    {
        WorldState state = GameManager.Instance != null ? GameManager.Instance.worldState : WorldState.Peace;
        CharacterType type = character.Type;

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

    public OwnedCharacter FindHomeless()
    {
        foreach (OwnedCharacter character in roster)
        {
            if (character.HomeBuilding == null)
            {
                return character;
            }
        }

        return null;
    }
}
