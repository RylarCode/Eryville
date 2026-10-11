using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // StartCoroutine(testLoop());

        // Every character re-reads its schedule whenever the time of day changes
        if (GameClock.Instance != null) GameClock.Instance.TimeOfDayChanged += OnTimeOfDayChanged;
    }

    private void OnDestroy()
    {
        if (GameClock.Instance != null) GameClock.Instance.TimeOfDayChanged -= OnTimeOfDayChanged;
    }

    IEnumerator testLoop()
    {
        int count = 0;
        while (true)
        {
            if (GameManager.Instance.currentGameState == GameManager.gameState.Normal)
            {
                hireCharacter(AllCharacters[0], new Vector3(count,0,0));
                count++;
            }
            yield return new WaitForSeconds(1f);

        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void hireCharacter(CharacterType hiredCharacter, Vector3 spawnPoint)
    {
        GameObject newCharGO = Instantiate(hiredCharacter.characterPrefab, spawnPoint, Quaternion.identity);
        newCharGO.name = hiredCharacter.displayName;
        newCharGO.GetComponent<Character>()?.Initialize(hiredCharacter);
        registerCharacter(newCharGO.GetComponent<Character>());
    }

    public void registerCharacter(Character character)
    {
        AllHiredCharacters.Add(character);
    }

    public void unregisterCharacter(Character character)
    { 
        AllHiredCharacters.Remove(character);
    }

    // Returns false if the building is full (or the character already works there)
    public bool assignCharacter(Character character, Building building)
    {
        return building != null && building.TryAssignWorker(character);
    }

    public void unassignCharacter(Character character, Building building)
    {
        building?.RemoveWorker(character);
    }

    // Returns false if the building has no free housing (or the character already lives there)
    public bool assignCharacterHouse(Character character, Building building)
    {
        return building != null && building.TryAssignResident(character);
    }

    public void unassignCharacterHouse(Character character, Building building)
    {
        building?.RemoveResident(character);
    }

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

    public Character getHomeless()
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
