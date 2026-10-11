using UnityEngine;
using UnityEngine.SceneManagement; // Required for scene management
using System.IO; // For File operations
using System.Collections.Generic;
using UnityEngine.Rendering.UI; // For Lists
// using Newtonsoft.Json; // If using Newtonsoft in saving

public class GameManager : MonoBehaviour
{
    // Singleton pattern: Ensures only one instance exists
    public static GameManager Instance { get; private set; }

    // --- Game State Example ---
    public int currentGold = 100;
    public int currentWood = 50;
    // Add other resources, game state variables (paused, game over, etc.)
    public enum gameState { Normal, Menu }
    public gameState currentGameState = gameState.Normal;


    // Need references to your building/character prefabs to reinstantiate them
    [Header("Prefabs for Loading")]
    [SerializeField] private List<GameObject> buildingPrefabs; // Assign all placeable building prefabs
    [SerializeField] private List<GameObject> characterPrefabs; // Assign all spawnable character prefabs


    private void Awake()
    {
        // Singleton implementation
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // Destroy duplicate
            return;
        }
        Instance = this;
        // Optional: Keep the GameManager across scene loads
        // DontDestroyOnLoad(gameObject);
        // Be careful with DontDestroyOnLoad - ensure cleanup if needed
    }

    void Start()
    {
        // Initial setup when the game scene starts
        // Debug.Log("Game Manager Initialized.");

    }


    // Test button: skip to the next time of day (the GameClock also advances it on its own)
    public void AdvanceTimeOfDay()
    {
        GameClock.Instance.Advance();
    }

    public void LoadMainMenu()
    {
        SceneManager.LoadScene("MainMenuScene");
    }

    public void QuitGame()
    {
        Debug.Log("Quitting Game..."); // Editor log
        Application.Quit();
    }




    public void SaveGame(string saveFileName = "savegame.json")
    {
        Debug.Log("Saving Game...");
        GameData data = new GameData();

        // 1. Populate Game Manager Data
        data.gold = this.currentGold;
        data.wood = this.currentWood;
        // ... other global state

        // 2. Populate Building Data
        // Need a way to find all active buildings in the scene
        Building[] allBuildings = FindObjectsByType<Building>(FindObjectsSortMode.None); // Simple way, might be slow for many objects
        foreach (Building building in allBuildings)
        {
            BuildingData bData = new BuildingData();
            // You need a reliable way to get the prefab name/ID
            // Maybe store it on the Building script itself when instantiated
            bData.prefabName = building.gameObject.name; // !! This is fragile - better store prefab ID/name on Building script
            bData.position = building.transform.position;
            // bData.currentHealth = building.currentHealth; // Add health property if needed
            data.buildings.Add(bData);
        }

        // 3. Populate Character Data
        Character[] allCharacters = FindObjectsByType<Character>(FindObjectsSortMode.None);
        foreach (Character character in allCharacters)
        {
            CharacterData cData = new CharacterData();
            cData.prefabName = character.gameObject.name; // !! Same fragility as buildings
            cData.position = character.transform.position;
            cData.currentActivity = character.CurrentActivity;
            // cData.currentHealth = character.currentHealth;
            data.characters.Add(cData);
        }


        // 4. Serialize to JSON
        string json = JsonUtility.ToJson(data, true); // Use JsonUtility (built-in, simple)
                                                      // string json = JsonConvert.SerializeObject(data, Formatting.Indented); // Use Newtonsoft (more robust)

        // 5. Write JSON string to a file
        Debug.Log("SaveFile: " + saveFileName);
        string path = Path.Combine(Application.persistentDataPath, saveFileName);
        try
        {
            File.WriteAllText(path, json);
            Debug.Log($"Game saved successfully to {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save game to {path}: {e.Message}");
        }
    }

    // Helper to find prefab by name (fragile, use IDs ideally)
    private GameObject GetPrefabByName(string name, List<GameObject> prefabList)
    {
        foreach (var prefab in prefabList)
        {
            // Need a consistent way to match saved name/ID to prefab
            // This basic check might work if saved name matches prefab name
            if (prefab.name == name || name.StartsWith(prefab.name + "_") || name.StartsWith(prefab.name + " (")) // Try variations
            {
                return prefab;
            }
        }
        Debug.LogWarning($"Prefab not found for name: {name}");
        return null;
    }


    public void LoadGame(string saveFileName = "savegame.json")
    {
        string path = Path.Combine(Application.persistentDataPath, saveFileName);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"Save file not found at {path}. Starting new game?");
            // Optionally load default state or show error
            return;
        }

        Debug.Log("Loading Game...");
        try
        {
            // 1. Read JSON string from file
            string json = File.ReadAllText(path);

            // 2. Deserialize JSON to GameData object
            GameData data = JsonUtility.FromJson<GameData>(json); // Use JsonUtility
                                                                  // GameData data = JsonConvert.DeserializeObject<GameData>(json); // Use Newtonsoft

            // --- Apply Loaded Data ---

            // Clear existing scene objects (important!)
            ClearCurrentSceneState(); // You need to implement this function


            // 3. Apply Game Manager Data
            this.currentGold = data.gold;
            this.currentWood = data.wood;
            // UIManager.Instance?.UpdateResourceUI(this.currentGold, this.currentWood); // Update UI
                                                                                      // ... apply other global state


            // 4. Re-instantiate Buildings
            foreach (BuildingData bData in data.buildings)
            {
                // Find the correct prefab based on saved name/ID
                GameObject prefab = GetPrefabByName(bData.prefabName, buildingPrefabs);
                if (prefab != null)
                {
                    GameObject loadedBuildingGO = Instantiate(prefab, bData.position, Quaternion.identity);
                    Building loadedBuilding = loadedBuildingGO.GetComponent<Building>();
                    if (loadedBuilding != null)
                    {
                        // Apply loaded state
                        // loadedBuilding.currentHealth = bData.currentHealth;
                        // Call a method like loadedBuilding.InitializeFromLoad(bData);
                        loadedBuilding.OnPlaced(); // Re-run placement logic? Or specific load logic?
                    }
                    // Make sure the loaded building correctly occupies grid space (re-register)
                    // TODO: load should go through BuildingManager.Build(type, cell, worldPosition) so the building is registered and occupies its cells
                }
                else
                {
                    Debug.LogWarning($"Could not find prefab for saved building: {bData.prefabName}");
                }
            }


            // 5. Re-instantiate Characters
            foreach (CharacterData cData in data.characters)
            {
                GameObject prefab = GetPrefabByName(cData.prefabName, characterPrefabs);
                if (prefab != null)
                {
                    GameObject loadedCharGO = Instantiate(prefab, cData.position, Quaternion.identity);
                    Character loadedChar = loadedCharGO.GetComponent<Character>();
                    if (loadedChar != null)
                    {
                        // Apply loaded state
                        // TODO: restore the activity (and destination) when load is reworked
                        // loadedChar.currentHealth = cData.currentHealth;
                        // Maybe set destination if they were moving? Needs more saved data.
                        // loadedChar.InitializeFromLoad(cData);
                    }
                }
                else
                {
                    Debug.LogWarning($"Could not find prefab for saved character: {cData.prefabName}");
                }
            }

            Debug.Log("Game loaded successfully.");

        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load game from {path}: {e.Message}");
            // Handle error, maybe load default state
        }
    }

    private void ClearCurrentSceneState()
    {
        Debug.Log("Clearing current scene state...");
        // Destroy all existing buildings and characters before loading
        Building[] buildings = FindObjectsByType<Building>(FindObjectsSortMode.None);
        foreach (var building in buildings) Destroy(building.gameObject);

        Character[] characters = FindObjectsByType<Character>(FindObjectsSortMode.None);
        foreach (var character in characters) Destroy(character.gameObject);

        // Reset any other relevant managers or state
        // Clear occupied grid data if using a data structure
        // Reset timers, etc.
    }
}



