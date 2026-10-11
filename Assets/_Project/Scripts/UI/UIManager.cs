using UnityEngine;
using UnityEngine.UI;
using TMPro; // Import TextMeshPro namespace
using System.Collections.Generic;
using UnityEngine.Tilemaps;
using UnityEngine.TextCore.Text;
using System.Text.RegularExpressions;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    // --- UI Element References ---
    [Header("Resource Display")]
    [SerializeField] private TextMeshProUGUI resourceText;
    private string resourceTextInWords;

    // Add references for building menus.
    [Header("Building Menu")]
    public GameObject buttonPrefab; // Drag your button prefab here in inspector
    public Transform buildingButtonContainer; // The UI Panel (Vertical/Horizontal Layout Group)

    public Transform hireButtonContainer; // The UI Panel (Vertical/Horizontal Layout Group)
    public GameObject assignPanel;
    public Transform assignButtonContainer;
    public Character clickedCharacter;

    [Header("Character Details")]
    public GameObject characterPanel; // Character UI Panel
    [SerializeField] private TextMeshProUGUI characterText;

    // Gridlines
    [Header("Gridlines")]
    [SerializeField] private GameObject GridlinesGO; // Assign GridlinesTilemap


    private GameObject openPanel;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // DontDestroyOnLoad(gameObject); // Maybe, if UI persists across scenes

    }

    void Start()
    {
        GenerateBuildingPanelButtons();
        GenerateHirePanelButtons();
    }

    // --- Public Methods to Update UI ---

    public void UpdateResourceUI(Dictionary<string, Resource> resources)
    {
        if (resourceText != null)
        {
            resourceTextInWords = "";
            foreach (var res in resources.Values)
            {
                resourceTextInWords += $"{res.resourceName}: {Mathf.FloorToInt(res.currentAmount)}\n";
            }
            resourceText.text = resourceTextInWords;
        }
        else
        {
            Debug.LogWarning("Resource Text reference not set in UIManager.");
        }

    }



    public void TogglePanel(GameObject panel)
    {
        if (!panel.activeInHierarchy)
        {
            //Debug.Log("Opening Menu" + panel.name);
            if (openPanel != null && panel != assignPanel )
            {
                openPanel.SetActive(false);
            }
            GameManager.Instance.currentGameState = GameManager.gameState.Menu;
            panel.SetActive(true);
            openPanel = panel;
            GridlinesGO.SetActive(true);

        }
        else
        {
            //Debug.Log("Closing Menu" + panel.name);
            GameManager.Instance.currentGameState = GameManager.gameState.Normal;
            panel.SetActive(false);
            openPanel = null;
            clickedCharacter = null;
            BuildingPlacer.Instance.buildingToPlace = null;
            if (BuildingPlacer.Instance.currentIndicatorInstance != null) BuildingPlacer.Instance.currentIndicatorInstance.SetActive(false);
            GridlinesGO.SetActive(false);

            foreach (Transform child in assignButtonContainer)
            {
                Destroy(child.gameObject);
            }
        }
    }

    void GenerateBuildingPanelButtons()
    {
        foreach (BuildingType building in BuildingPlacer.Instance.AllBuildings)
        {
            GameObject buttonGO = Instantiate(buttonPrefab, buildingButtonContainer);
            buttonGO.name = $"{building.buildingName}Button";
            Button button = buttonGO.GetComponent<Button>();

            // Assuming you have an Image and Text on the button prefab
            buttonGO.GetComponent<Image>().sprite = building.sprite;
            buttonGO.GetComponentInChildren<TMP_Text>().text = building.buildingName;

            // Capture the variable for the listener
            BuildingType captured = building;
            button.onClick.AddListener(() => BuildingPlacer.Instance.SelectBuildingToPlace(captured));
        }
    }

    void GenerateHirePanelButtons()
    {
        foreach (CharacterType character in CharacterManager.Instance.AllCharacters)
        {
            GameObject buttonGO = Instantiate(buttonPrefab, hireButtonContainer);
            buttonGO.name = $"{character.Name}Button";
            Button button = buttonGO.GetComponent<Button>();

            // Assuming you have an Image and Text on the button prefab
            buttonGO.GetComponent<Image>().sprite = character.sprite;
            buttonGO.GetComponentInChildren<TMP_Text>().text = character.Name;

            // Capture the variable for the listener
            button.onClick.AddListener(() => CharacterManager.Instance.hireCharacter(character, new Vector3(0,0,0)));
        }
    }

    public void showCharacterDetails(Character character)
    {
        // Open the panel only if it is closed; clicking another character just switches the selection
        if (!characterPanel.activeInHierarchy) TogglePanel(characterPanel);
        characterText.text = $"Name: {character.characterName}\nLoyalty: {character.Loyalty}\nWorking Location: {character.WorkBuilding?.DisplayName}\nHome: {character.HomeBuilding?.DisplayName}";
        clickedCharacter = character;
    }

    public void GenerateAssignCharactertoBuildingButtons()
    {
        
        foreach (Building building in BuildingManager.Instance.Buildings)
        {
            if (building.WorkerSlots <= 0) continue; // nobody can work here (homes, city hall...)

            GameObject buttonGO = Instantiate(buttonPrefab, assignButtonContainer);
            buttonGO.name = $"{building.name}AssignButton";
            Button button = buttonGO.GetComponent<Button>();

            // Assuming you have an Image and Text on the button prefab
            buttonGO.GetComponent<Image>().sprite = building.buildingType.sprite;
            buttonGO.GetComponentInChildren<TMP_Text>().text = $"{building.DisplayName} ({building.Workers.Count}/{building.WorkerSlots})";

            button.onClick.AddListener(() =>
            {
                if (!CharacterManager.Instance.assignCharacter(clickedCharacter, building))
                {
                    Debug.Log($"Could not assign {clickedCharacter.characterName} to {building.DisplayName} (full or already working there).");
                }
            });
        }

        TogglePanel(assignPanel);
    }

}