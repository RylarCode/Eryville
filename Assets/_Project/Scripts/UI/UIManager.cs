using UnityEngine;
using UnityEngine.UI;
using TMPro; // Import TextMeshPro namespace
using UnityEngine.InputSystem;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Text;

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
    public GameObject assignPanel;          // the list panel beside the details panel (assign-job list / occupants list)
    public Transform assignButtonContainer; // where that list's buttons go

    [Header("Character / Building Details")]
    public GameObject characterPanel; // The right-hand details panel, used for both characters and buildings
    [SerializeField] private TextMeshProUGUI characterText;
    [SerializeField] private GameObject assignJobButton; // the "Assign" button on that panel; hidden when showing a building
    private GameObject moveButton; // "Move" button, made at runtime as a copy of the Assign button; shown for buildings only

    // What the details panel is currently showing
    private enum PanelMode { None, Character, Building }
    private PanelMode panelMode = PanelMode.None;
    private OwnedCharacter selectedCharacter;
    private Building selectedBuilding;
    private string occupantListSignature; // what the occupants list currently shows, so it is only rebuilt when it changes
    private float nextBuildingRefresh;
    private const float BuildingRefreshInterval = 0.5f;

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

    private void OnEnable()
    {
        Character.Selected += OnCharacterSelected;
        Building.Selected += ShowBuildingDetails;
    }

    private void OnDisable()
    {
        Character.Selected -= OnCharacterSelected;
        Building.Selected -= ShowBuildingDetails;
    }

    private void OnCharacterSelected(Character avatar)
    {
        ShowCharacterDetails(avatar.Owned);
    }

    void Start()
    {
        // Clicking empty ground closes the details panel
        if (InputManager.Instance != null) InputManager.Instance.OnPointerClick += HandleWorldClick;

        if (assignJobButton == null)
        {
            Transform found = characterPanel.transform.Find("AssignButton");
            if (found != null) assignJobButton = found.gameObject;
        }

        CreateMoveButton();
        if (BuildingPlacer.Instance != null) BuildingPlacer.Instance.MovingEnded += OnMovingEnded;

        // The panel now shows more text than a character used to need: shrink it to fit instead of overflowing
        characterText.enableAutoSizing = true;
        characterText.fontSizeMin = 14f;
        characterText.fontSizeMax = 36f;

        GenerateBuildingPanelButtons();
        GenerateHirePanelButtons();
    }

    private void OnDestroy()
    {
        if (InputManager.Instance != null) InputManager.Instance.OnPointerClick -= HandleWorldClick;
        if (BuildingPlacer.Instance != null) BuildingPlacer.Instance.MovingEnded -= OnMovingEnded;
    }

    private void Update()
    {
        // Escape cancels a move in progress, or closes the character panels, or else whatever menu is open
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (BuildingPlacer.Instance != null && BuildingPlacer.Instance.IsMoving) BuildingPlacer.Instance.CancelMoving();
            else if (CharacterPanelsOpen) CloseCharacterPanels();
            else if (openPanel != null) TogglePanel(openPanel);
        }

        // A building's details change while you watch (workers arrive and leave): keep them current
        if (panelMode == PanelMode.Building && characterPanel.activeInHierarchy && Time.unscaledTime >= nextBuildingRefresh)
        {
            nextBuildingRefresh = Time.unscaledTime + BuildingRefreshInterval;
            if (selectedBuilding == null) CloseCharacterPanels(); // the building was removed
            else RefreshBuildingPanel();
        }
    }

    private bool CharacterPanelsOpen => characterPanel.activeInHierarchy || assignPanel.activeInHierarchy;

    private void HandleWorldClick(Vector2 screenPosition)
    {
        if (CharacterPanelsOpen) CloseCharacterPanels();
    }

    // Closes the details panel together with the list panel beside it
    private void CloseCharacterPanels()
    {
        characterPanel.SetActive(false);
        assignPanel.SetActive(false);
        ClearList();
        panelMode = PanelMode.None;
        selectedCharacter = null;
        selectedBuilding = null;
        openPanel = null;
        GameManager.Instance.currentGameState = GameManager.gameState.Normal;
        GridlinesGO.SetActive(false);
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
            if (BuildingPlacer.Instance.IsMoving) BuildingPlacer.Instance.CancelMoving(); // opening a menu abandons a move
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
            if (panel != assignPanel) // closing just the list must not forget what is selected
            {
                selectedCharacter = null;
                selectedBuilding = null;
            }
            BuildingPlacer.Instance.buildingToPlace = null;
            if (BuildingPlacer.Instance.currentIndicatorInstance != null) BuildingPlacer.Instance.currentIndicatorInstance.SetActive(false);
            GridlinesGO.SetActive(false);

            ClearList();
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
            buttonGO.name = $"{character.displayName}Button";
            Button button = buttonGO.GetComponent<Button>();

            // Assuming you have an Image and Text on the button prefab
            buttonGO.GetComponent<Image>().sprite = character.sprite;
            buttonGO.GetComponentInChildren<TMP_Text>().text = character.displayName;

            // Capture the variable for the listener
            button.onClick.AddListener(() => CharacterManager.Instance.Hire(character, new Vector3(0,0,0)));
        }
    }

    // --- Details panel (characters and buildings share the right-hand panel) ---

    private void OpenDetailsPanel()
    {
        if (!characterPanel.activeInHierarchy) TogglePanel(characterPanel);
    }

    // Removes every button from the list panel beside the details panel
    private void ClearList()
    {
        foreach (Transform child in assignButtonContainer)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    public void ShowCharacterDetails(OwnedCharacter character)
    {
        // Clicking the selected character again closes the panel; clicking another one switches the selection
        if (panelMode == PanelMode.Character && selectedCharacter == character && characterPanel.activeInHierarchy)
        {
            CloseCharacterPanels();
            return;
        }

        OpenDetailsPanel();
        panelMode = PanelMode.Character;
        selectedCharacter = character;
        selectedBuilding = null;

        assignPanel.SetActive(false); // the assign-job list only opens when its button is pressed
        ClearList();
        if (assignJobButton != null) assignJobButton.SetActive(true);
        if (moveButton != null) moveButton.SetActive(false);
        RefreshCharacterText();
    }

    private void RefreshCharacterText()
    {
        OwnedCharacter c = selectedCharacter;
        characterText.text = $"Name: {c.DisplayName}\nLevel: {c.Level}\nLoyalty: {c.Loyalty}\nWorking Location: {c.WorkBuilding?.DisplayName}\nHome: {c.HomeBuilding?.DisplayName}";
    }

    public void ShowBuildingDetails(Building building)
    {
        // Same rules as for characters: click again to close, click another to switch
        if (panelMode == PanelMode.Building && selectedBuilding == building && characterPanel.activeInHierarchy)
        {
            CloseCharacterPanels();
            return;
        }

        OpenDetailsPanel();
        panelMode = PanelMode.Building;
        selectedBuilding = building;
        selectedCharacter = null;

        if (assignJobButton != null) assignJobButton.SetActive(false); // assigning a job makes no sense here
        if (moveButton != null) moveButton.SetActive(building.buildingType != null && building.buildingType.movable);
        ClearList();
        occupantListSignature = null;
        nextBuildingRefresh = Time.unscaledTime + BuildingRefreshInterval;
        RefreshBuildingPanel();
        assignPanel.SetActive(true); // the list beside the panel shows who works and lives here
    }

    private void RefreshBuildingPanel()
    {
        characterText.text = BuildBuildingText(selectedBuilding);

        // Rebuild the occupants list only when it actually changed
        string signature = OccupantSignature(selectedBuilding);
        if (signature != occupantListSignature)
        {
            occupantListSignature = signature;
            RebuildOccupantList(selectedBuilding);
        }
    }

    private static string BuildBuildingText(Building building)
    {
        BuildingType type = building.buildingType;
        StringBuilder text = new StringBuilder();
        text.AppendLine(building.DisplayName);
        if (type != null) text.AppendLine($"Size: {type.size.x}x{type.size.y}");

        if (building.Producer != null)
        {
            if (type.goldGenerationPerSec > 0) text.AppendLine($"Gold: +{building.Producer.GoldPerSec:0.#}/s");
            if (type.woodGenerationPerSec > 0) text.AppendLine($"Wood: +{building.Producer.WoodPerSec:0.#}/s");
            if (building.WorkersOnSite.Count > 0) text.AppendLine($"Worker bonus: x{building.Producer.WorkerMultiplier:0.##}");
        }
        if (building.WorkerSlots > 0)
        {
            text.AppendLine($"Workers: {building.Workers.Count}/{building.WorkerSlots} ({building.WorkersOnSite.Count} on site)");
        }
        if (building.HousingSlots > 0)
        {
            text.AppendLine($"Residents: {building.Residents.Count}/{building.HousingSlots}");
        }
        return text.ToString();
    }

    // A string that changes whenever the occupants list would look different
    private static string OccupantSignature(Building building)
    {
        StringBuilder signature = new StringBuilder();
        foreach (OwnedCharacter worker in building.Workers)
        {
            signature.Append(worker.Id).Append("|w|").Append(building.IsWorkerOnSite(worker)).Append(';');
        }
        foreach (OwnedCharacter resident in building.Residents)
        {
            signature.Append(resident.Id).Append("|r;");
        }
        return signature.ToString();
    }

    private void RebuildOccupantList(Building building)
    {
        ClearList();

        foreach (OwnedCharacter worker in building.Workers)
        {
            AddOccupantButton(worker, building.IsWorkerOnSite(worker) ? "Worker (on site)" : "Worker");
        }
        foreach (OwnedCharacter resident in building.Residents)
        {
            AddOccupantButton(resident, "Resident");
        }

        if (building.Workers.Count == 0 && building.Residents.Count == 0)
        {
            // Nothing to click: a plain label so the list is not just blank
            GameObject label = Instantiate(buttonPrefab, assignButtonContainer);
            label.name = "NobodyLabel";
            label.GetComponentInChildren<TMP_Text>().text = "Nobody yet";
        }
    }

    // One clickable row: clicking it opens that character's details (even if they are hidden inside the building)
    private void AddOccupantButton(OwnedCharacter occupant, string role)
    {
        GameObject buttonGO = Instantiate(buttonPrefab, assignButtonContainer);
        buttonGO.name = $"{occupant.DisplayName}OccupantButton";
        buttonGO.GetComponent<Image>().sprite = occupant.Type.sprite;
        buttonGO.GetComponentInChildren<TMP_Text>().text = $"{occupant.DisplayName} - {role}";

        OwnedCharacter captured = occupant;
        buttonGO.GetComponent<Button>().onClick.AddListener(() => ShowCharacterDetails(captured));
    }

    // --- Moving buildings ---

    // Makes the "Move" button as a copy of the Assign button, so it looks the same and sits in the same
    // spot (which is free in building view). The copy's scene-assigned click action is switched off.
    private void CreateMoveButton()
    {
        if (assignJobButton == null) return;

        moveButton = Instantiate(assignJobButton, assignJobButton.transform.parent);
        moveButton.name = "MoveButton";

        Button button = moveButton.GetComponent<Button>();
        if (button != null)
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
            }
            button.onClick.AddListener(StartMovingSelectedBuilding);
        }

        TMP_Text label = moveButton.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = "Move";
        moveButton.SetActive(false);
    }

    // Called by the "Move" button: closes the panel and lets the player drop the building somewhere else.
    // The game is paused with the grid shown while they choose, like when placing from the build menu.
    public void StartMovingSelectedBuilding()
    {
        if (selectedBuilding == null || BuildingPlacer.Instance == null) return;

        Building building = selectedBuilding;
        CloseCharacterPanels();
        BuildingPlacer.Instance.StartMoving(building);
        if (BuildingPlacer.Instance.IsMoving)
        {
            GameManager.Instance.currentGameState = GameManager.gameState.Menu;
            GridlinesGO.SetActive(true);
        }
    }

    private void OnMovingEnded()
    {
        GameManager.Instance.currentGameState = GameManager.gameState.Normal;
        GridlinesGO.SetActive(false);
    }

    // Called by the "Assign" button on the details panel (character mode)
    public void GenerateAssignCharactertoBuildingButtons()
    {
        if (selectedCharacter == null) return;

        ClearList(); // pressing the button again must not stack a second copy of the list
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
                if (selectedCharacter == null) return;
                if (CharacterManager.Instance.AssignWorker(selectedCharacter, building))
                {
                    RefreshCharacterText(); // show the new job
                }
                else
                {
                    Debug.Log($"Could not assign {selectedCharacter.DisplayName} to {building.DisplayName} (full or already working there).");
                }
            });
        }

        TogglePanel(assignPanel);
    }

}
