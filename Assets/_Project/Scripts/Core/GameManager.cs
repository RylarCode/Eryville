using UnityEngine;
using UnityEngine.SceneManagement; // Required for scene management

public class GameManager : MonoBehaviour
{
    // Singleton pattern: Ensures only one instance exists
    public static GameManager Instance { get; private set; }

    // Game state variables (paused, game over, etc.). Resources live in ResourceManager.
    public enum gameState { Normal, Menu }
    public gameState currentGameState = gameState.Normal;

    // Whether the game's clock, production and mood stand still. They do while a menu is open, except in real time:
    // the real clock cannot be paused, so neither can the game.
    public bool SimulationPaused => currentGameState != gameState.Normal && !(GameClock.Instance != null && GameClock.Instance.IsRealTime);

    // Overall situation of the world; characters pick rest places based on it (see CharacterType.restOptions)
    public WorldState worldState = WorldState.Peace;


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




    // Called by the Save / Load buttons in the scene (the file name is set on the button)
    public void SaveGame(string saveFileName = SaveSystem.DefaultFileName)
    {
        SaveSystem.Save(saveFileName);
    }

    public void LoadGame(string saveFileName = SaveSystem.DefaultFileName)
    {
        SaveSystem.Load(saveFileName);
    }
}
