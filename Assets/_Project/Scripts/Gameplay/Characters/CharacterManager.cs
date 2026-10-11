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
        if (GameClock.Instance != null) GameClock.Instance.TimeOfDayChanged += timeMoveChangeSchedule;
    }

    private void OnDestroy()
    {
        if (GameClock.Instance != null) GameClock.Instance.TimeOfDayChanged -= timeMoveChangeSchedule;
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

    public void CharacterStartsWorkinginBuilding (Character character, Building building)
    {
        character.StartWork();
        building?.WorkerArrived(character);
    }

    public void CharacterStopsWorkinginBuilding(Character character, Building building)
    {
        character.StopWork();
        building?.WorkerLeft(character);
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

    public void CharacterStartsRestinginBuilding(Character character, Building building)
    {
        character.StartResting(building);
    }

    public void CharacterStopsRestinginBuilding(Character character, Building building)
    {
        character.StopResting(building);
    }



    public void timeMoveChangeSchedule(TimeOfDay newtime)
    {
        foreach (Character character in AllHiredCharacters)
        {
            CharacterStopsWorkinginBuilding(character, character.WorkBuilding);
            CharacterStopsRestinginBuilding(character, character.HomeBuilding);
            CharacterStopsRestinginBuilding(character, character.rest1Building);
            CharacterStopsRestinginBuilding(character, character.rest2Building);

            character.ScheduleMoving(newtime);
        }
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
