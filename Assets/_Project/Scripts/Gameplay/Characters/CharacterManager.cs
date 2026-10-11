using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

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
        GameObject newCharGO = Instantiate(hiredCharacter.characterCategoryPrefab, spawnPoint, Quaternion.identity);
        newCharGO.name = hiredCharacter.Name;
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

    public void assignCharacter(Character character, Building building)
    {
        building.registerCharacter(character);
        character.AssignedWork(building);
    }

    public void unassignCharacter(Character character, Building building)
    {
        building.unregisterCharacter(character);
        character.UnassignedWork(building);
    }

    public void CharacterStartsWorkinginBuilding (Character character, Building building)
    {
        character.StartWork();
        building?.ChracterStartWorking(character);
    }

    public void CharacterStopsWorkinginBuilding(Character character, Building building)
    {
        character.StopWork();
        building?.ChracterStopWorking(character);
    }

    public void assignCharacterHouse(Character character, Building building)
    {
        building.registerCharacter(character);
        character.AssignedHome(building);
    }

    public void unassignCharacterHouse(Character character, Building building)
    {
        building.unregisterCharacter(character);
        character.UnassignedHome(building);
    }

    public void CharacterStartsRestinginBuilding(Character character, Building building)
    {
        character.StartResting(building);
        building?.ChracterStartWorking(character);
    }

    public void CharacterStopsRestinginBuilding(Character character, Building building)
    {
        character.StopResting(building);
        building?.ChracterStopWorking(character);
    }



    public void timeMoveChangeSchedule(GameManager.timeOfDay newtime)
    {
        foreach (Character character in AllHiredCharacters)
        {
            CharacterStopsWorkinginBuilding(character, character.workingBuilding);
            CharacterStopsRestinginBuilding(character, character.homeBuilding);
            CharacterStopsRestinginBuilding(character, character.rest1Building);
            CharacterStopsRestinginBuilding(character, character.rest2Building);

            character.ScheduleMoving(newtime);
        }
    }

    public Building getHome()
    {
        foreach(Building building in BuildingPlacer.Instance.AllPlacedBuildings)
        {
            if (building.buildingclass == BuildingType.buildingClass.Home)
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
            if (character.homeBuilding == null)
            {
                return character;
            }
        }

        return null;
    }


}
