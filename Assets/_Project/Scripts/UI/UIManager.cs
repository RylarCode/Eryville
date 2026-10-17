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

    // The Town menu (everything you own) and the button that opens it
    private TownMenu town;
    private float nextTownRefresh;

    // The Recruit menu (the tavern's gacha)
    private RecruitMenu recruit;
    private float nextRecruitRefresh;

    // The screen that shows who was recruited, and the character's full picture on the details panel
    private RevealPopup reveal;
    private Image detailsPortrait;

    // The testing tools (editor and Development Builds only)
    private CheatMenu cheats;
    private float nextCheatRefresh;
    private const float BuildingRefreshInterval = 0.5f;

    // Phone layout: the screen-shape dependent layout, and whether a menu was tucked away while placing something
    private UILayout layout;
    private bool panelCollapsed;

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

        layout = new UILayout(characterPanel.transform.parent, buttonPrefab);
        layout.OnBarConfirm = ConfirmPlacementFromBar;
        layout.OnBarCancel = CancelPlacementFromBar;

        MenuPanels.ConfigureListContent(assignButtonContainer);
        CreateDetailsPortrait();
        reveal = new RevealPopup(characterPanel.transform.parent, buttonPrefab);
        CreateMoveButton();
        town = new TownMenu(this, buttonPrefab, characterPanel, assignPanel);
        CreateMenuButton("OpenTownButton", "Town", 1, ToggleTownMenu);
        recruit = new RecruitMenu(this, buttonPrefab, characterPanel, assignPanel);
        CreateMenuButton("OpenRecruitButton", "Recruit", 2, ToggleRecruitMenu);
        SetUpCheats();
        if (BuildingPlacer.Instance != null) BuildingPlacer.Instance.MovingEnded += OnMovingEnded;

        // The panel now shows more text than a character used to need: shrink it to fit instead of overflowing
        characterText.enableAutoSizing = true;
        characterText.fontSizeMin = 14f;
        characterText.fontSizeMax = 36f;

        GenerateBuildingPanelButtons();
        GenerateHirePanelButtons();
        CharacterManager.Instance.RosterChanged += RefreshHireButtons;

        if (BuildingPlacer.Instance != null) BuildingPlacer.Instance.PlacingEnded += OnPlacementEnded;
        if (RoadPlacer.Instance != null) RoadPlacer.Instance.ModeEnded += OnPlacementEnded;

        layout.Apply();
        ApplyTouchMode();
    }

    // On a phone the finger only aims and a Place button builds
    private void ApplyTouchMode()
    {
        if (BuildingPlacer.Instance != null) BuildingPlacer.Instance.RequireConfirm = layout.IsPortrait;
    }

    // The screen changed between portrait and landscape: menus and cards were sized for the old shape, so make them again
    private void RebuildForNewLayout()
    {
        if (openPanel != null && (openPanel == town.Panel || openPanel == recruit.Panel)) TogglePanel(openPanel);
        if (cheats != null && openPanel == cheats.Panel) TogglePanel(openPanel);
        recruit.Dispose();
        reveal.Destroy();
        reveal = new RevealPopup(characterPanel.transform.parent, buttonPrefab);
        List<GameObject> oldPanels = new List<GameObject> { town.Panel, recruit.Panel };
        if (cheats != null) oldPanels.Add(cheats.Panel);
        foreach (GameObject old in oldPanels)
        {
            old.name += "(old)";
            old.SetActive(false);
            Destroy(old);
        }
        town = new TownMenu(this, buttonPrefab, characterPanel, assignPanel);
        recruit = new RecruitMenu(this, buttonPrefab, characterPanel, assignPanel);
        if (cheats != null) cheats = new CheatMenu(this, buttonPrefab, characterPanel, assignPanel);

        foreach (Transform container in new[] { buildingButtonContainer, hireButtonContainer })
        {
            foreach (Transform card in container)
            {
                card.gameObject.SetActive(false);
                Destroy(card.gameObject);
            }
        }
        hireButtons.Clear();
        GenerateBuildingPanelButtons();
        GenerateHirePanelButtons();

        layout.Apply();
    }

    // --- Placing on a phone ---

    // Choosing something to place hides the menu so the map is visible; the game stays paused until placing ends
    private void CollapsePanelForPlacement()
    {
        if (!layout.IsPortrait || openPanel == null || !openPanel.activeSelf) return;
        openPanel.SetActive(false);
        panelCollapsed = true;
    }

    // Placing ended (built, cancelled, or a menu took over): if the menu was tucked away, the menu mode ends with it
    private void OnPlacementEnded()
    {
        if (!panelCollapsed) return;
        panelCollapsed = false;
        openPanel = null;
        GameManager.Instance.currentGameState = GameManager.gameState.Normal;
        if (GridlinesGO != null) GridlinesGO.SetActive(false);
    }

    private void ConfirmPlacementFromBar()
    {
        BuildingPlacer buildings = BuildingPlacer.Instance;
        if (buildings != null && buildings.IsPlacing) buildings.ConfirmPlacement();
        else if (RoadPlacer.Instance != null && RoadPlacer.Instance.IsPlacing) RoadPlacer.Instance.Cancel(); // "Done"
    }

    private void CancelPlacementFromBar()
    {
        if (BuildingPlacer.Instance != null) BuildingPlacer.Instance.CancelPlacing();
    }

    private void UpdatePlacementBar()
    {
        PlacementBar bar = layout.Bar;
        if (bar == null) return;

        BuildingPlacer buildings = BuildingPlacer.Instance;
        bool placingBuilding = buildings != null && buildings.IsPlacing;
        bool placingRoad = RoadPlacer.Instance != null && RoadPlacer.Instance.IsPlacing;
        bool menuInTheWay = openPanel != null && openPanel.activeInHierarchy;

        if (layout.IsPortrait && (placingBuilding || placingRoad) && !menuInTheWay)
        {
            bar.Show(placingBuilding ? (buildings.IsMoving ? "Move here" : "Place") : "Done", placingBuilding);
        }
        else
        {
            bar.Hide();
        }
    }

    private void OnDestroy()
    {
        if (InputManager.Instance != null) InputManager.Instance.OnPointerClick -= HandleWorldClick;
        if (BuildingPlacer.Instance != null) BuildingPlacer.Instance.MovingEnded -= OnMovingEnded;
        if (CharacterManager.Instance != null) CharacterManager.Instance.RosterChanged -= RefreshHireButtons;
        recruit?.Dispose();
        if (BuildingPlacer.Instance != null) BuildingPlacer.Instance.PlacingEnded -= OnPlacementEnded;
        if (RoadPlacer.Instance != null) RoadPlacer.Instance.ModeEnded -= OnPlacementEnded;
    }

    private void Update()
    {
        if (layout != null && layout.Tick())
        {
            ApplyTouchMode();
            RebuildForNewLayout();
        }
        if (layout != null) UpdatePlacementBar();
        SyncPortraitVisibility();
        if (cheats != null && cheats.Panel.activeInHierarchy && Time.unscaledTime >= nextCheatRefresh)
        {
            nextCheatRefresh = Time.unscaledTime + BuildingRefreshInterval;
            cheats.Refresh();
        }

        // Escape cancels a move in progress, or closes the character panels, or else whatever menu is open
        // (Android's Back button counts as Escape)
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (reveal != null && reveal.IsOpen) reveal.Close();
            else if (RoadPlacer.Instance != null && RoadPlacer.Instance.IsPlacing) RoadPlacer.Instance.Cancel();
            else if (panelCollapsed && BuildingPlacer.Instance != null && BuildingPlacer.Instance.IsPlacing) BuildingPlacer.Instance.CancelPlacing();
            else if (BuildingPlacer.Instance != null && BuildingPlacer.Instance.IsMoving) BuildingPlacer.Instance.CancelMoving();
            else if (CharacterPanelsOpen) CloseCharacterPanels();
            else if (openPanel != null) TogglePanel(openPanel);
        }

        // The Town list changes while you watch too (characters walk about, buildings fill up)
        if (town != null && town.Panel.activeInHierarchy && Time.unscaledTime >= nextTownRefresh)
        {
            nextTownRefresh = Time.unscaledTime + BuildingRefreshInterval;
            town.Refresh();
        }
        if (recruit != null && recruit.Panel.activeInHierarchy && Time.unscaledTime >= nextRecruitRefresh)
        {
            nextRecruitRefresh = Time.unscaledTime + BuildingRefreshInterval;
            recruit.Refresh();
        }

        // A building's details change while you watch (workers arrive and leave): keep them current
        if (panelMode == PanelMode.Building && characterPanel.activeInHierarchy && Time.unscaledTime >= nextBuildingRefresh)
        {
            nextBuildingRefresh = Time.unscaledTime + BuildingRefreshInterval;
            if (selectedBuilding == null) CloseCharacterPanels(); // the building was removed
            else RefreshBuildingPanel();
        }

        // A character's mood and speed change over time too
        if (panelMode == PanelMode.Character && selectedCharacter != null && characterPanel.activeInHierarchy && Time.unscaledTime >= nextBuildingRefresh)
        {
            nextBuildingRefresh = Time.unscaledTime + BuildingRefreshInterval;
            RefreshCharacterText();
        }
    }

    private bool CharacterPanelsOpen => characterPanel.activeInHierarchy || assignPanel.activeInHierarchy;

    private void HandleWorldClick(Vector2 screenPosition)
    {
        if (CharacterPanelsOpen) CloseCharacterPanels();
    }

    // Closes the details panel together with the list panel beside it
    // Closes every menu and cancels any placing, leaving the game running (used when something resets the world)
    public void CloseEverything()
    {
        if (BuildingPlacer.Instance != null) BuildingPlacer.Instance.CancelPlacing();
        if (RoadPlacer.Instance != null) RoadPlacer.Instance.Cancel();
        panelCollapsed = false;
        GameObject menu = openPanel; // CloseCharacterPanels forgets it
        CloseCharacterPanels();
        if (menu != null) menu.SetActive(false);
        openPanel = null;
        GameManager.Instance.currentGameState = GameManager.gameState.Normal;
        if (GridlinesGO != null) GridlinesGO.SetActive(false);
    }

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
                // Gold, wood and letters always; the other items once the town has some
                bool core = res.resourceName == ResourceManager.Gold || res.resourceName == ResourceManager.Wood || res.resourceName == ResourceManager.Letters;
                if (!core && res.currentAmount < 1f) continue;
                float capacity = ResourceManager.Instance != null ? ResourceManager.Instance.Capacity(res.resourceName) : 0f;
                resourceTextInWords += capacity > 0f
                    ? $"{res.resourceName}: {Mathf.FloorToInt(res.currentAmount)}/{Mathf.FloorToInt(capacity)}\n"
                    : $"{res.resourceName}: {Mathf.FloorToInt(res.currentAmount)}\n";
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
        panelCollapsed = false; // opening or closing a menu by hand ends the tucked-away state
        if (!panel.activeInHierarchy)
        {
            //Debug.Log("Opening Menu" + panel.name);
            if (BuildingPlacer.Instance.IsMoving) BuildingPlacer.Instance.CancelMoving(); // opening a menu abandons a move
            if (RoadPlacer.Instance != null) RoadPlacer.Instance.Cancel(); // ...and road building
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
            if (RoadPlacer.Instance != null) RoadPlacer.Instance.Cancel();
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
            GameObject buttonGO = MenuPanels.MakeCard(buttonPrefab, buildingButtonContainer, $"{building.buildingName}Button", building.sprite, building.buildingName);
            Button button = buttonGO.GetComponent<Button>();

            // Capture the variable for the listener
            BuildingType captured = building;
            button.onClick.AddListener(() =>
            {
                BuildingPlacer.Instance.SelectBuildingToPlace(captured);
                CollapsePanelForPlacement();
            });
        }

        GenerateRoadButtons();
    }

    // Roads are in the same menu as buildings: one button per kind of road, and one to take roads away
    void GenerateRoadButtons()
    {
        if (RoadPlacer.Instance == null) return;

        foreach (RoadType road in RoadManager.Instance.RoadTypes)
        {
            GameObject buttonGO = MenuPanels.MakeCard(buttonPrefab, buildingButtonContainer, $"{road.displayName}Button", road.sprite, $"{road.displayName} ({road.goldCost}G)");

            RoadType captured = road;
            buttonGO.GetComponent<Button>().onClick.AddListener(() =>
            {
                RoadPlacer.Instance.StartPainting(captured);
                CollapsePanelForPlacement();
            });
        }

        GameObject eraser = MenuPanels.MakeCard(buttonPrefab, buildingButtonContainer, "RemoveRoadButton", null, "Remove Road");
        eraser.GetComponent<Button>().onClick.AddListener(() =>
        {
            RoadPlacer.Instance.StartErasing();
            CollapsePanelForPlacement();
        });
    }

    // One hire button per character type; each shows how many you own out of the limit and
    // switches itself off once you have as many as you are allowed
    private class HireButton
    {
        public CharacterType type;
        public Button button;
        public TMP_Text label;
    }
    private readonly List<HireButton> hireButtons = new List<HireButton>();

    void GenerateHirePanelButtons()
    {
        foreach (CharacterType character in CharacterManager.Instance.AllCharacters)
        {
            GameObject buttonGO = MenuPanels.MakeCard(buttonPrefab, hireButtonContainer, $"{character.displayName}Button", character.FaceOrSprite, character.displayName);
            Button button = buttonGO.GetComponent<Button>();

            hireButtons.Add(new HireButton { type = character, button = button, label = buttonGO.GetComponentInChildren<TMP_Text>() });

            // Capture the variable for the listener
            button.onClick.AddListener(() => CharacterManager.Instance.Hire(character, new Vector3(0,0,0)));
        }

        RefreshHireButtons();
    }

    private void RefreshHireButtons()
    {
        foreach (HireButton hire in hireButtons)
        {
            int owned = CharacterManager.Instance.OwnedCount(hire.type);
            hire.label.text = hire.type.maxOwned > 0
                ? $"{hire.type.displayName} ({owned}/{hire.type.maxOwned})"
                : $"{hire.type.displayName} ({owned})";
            hire.button.interactable = CharacterManager.Instance.CanHire(hire.type);
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
        SetDetailsPortrait(character.Type.PortraitOrSprite);
        if (assignJobButton != null) assignJobButton.SetActive(true);
        if (moveButton != null) moveButton.SetActive(false);
        RefreshCharacterText();
    }

    private void RefreshCharacterText()
    {
        OwnedCharacter c = selectedCharacter;
        characterText.text = $"Name: {c.DisplayName}\nLevel: {c.Level}\nLoyalty: {c.Loyalty:0}\nMood: {c.Mood:0}\nSpeed: {c.DescribeSpeed()}\nWorking Location: {c.WorkBuilding?.DisplayName}\nHome: {c.HomeBuilding?.DisplayName}\nRarity: {c.Type.rarity}\nSelf-awareness: {c.DescribeAwareness()}\nWage: {c.DailyWage}/day{(c.WagePaid ? "" : " (UNPAID - refuses to work)")}";
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

        SetDetailsPortrait(null);
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

        if (building.Producer != null && type != null && type.Produces)
        {
            ResourceProducer producer = building.Producer;
            Recipe recipe = producer.ActiveRecipe;
            if (type.Recipes.Count > 1 && recipe != null) text.AppendLine($"Recipe: {recipe.DisplayName}");
            if (recipe != null)
            {
                foreach (ItemAmount output in recipe.outputs) if (output.item != null) text.AppendLine($"{output.Name}: +{ResourceProducer.RateText(producer.OutputPerSec(output.Name))}");
                foreach (ItemAmount input in recipe.inputs) if (input.item != null) text.AppendLine($"Uses {input.Name}: -{ResourceProducer.RateText(producer.InputPerSec(input.Name))}");
            }
            if (producer.Status != LineStatus.Working) text.AppendLine(producer.StatusText);
            if (building.WorkersOnSite.Count > 0) text.AppendLine($"Worker bonus: x{producer.WorkerMultiplier:0.##}");
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
            MenuPanels.MakeRow(buttonPrefab, assignButtonContainer, "NobodyLabel", "Nobody yet", null, false);
        }
    }

    // One clickable row: clicking it opens that character's details (even if they are hidden inside the building)
    private void AddOccupantButton(OwnedCharacter occupant, string role)
    {
        GameObject buttonGO = MenuPanels.MakeRow(buttonPrefab, assignButtonContainer, $"{occupant.DisplayName}OccupantButton", $"{occupant.DisplayName} - {role}", occupant.Type.FaceOrSprite);

        OwnedCharacter captured = occupant;
        buttonGO.GetComponent<Button>().onClick.AddListener(() => ShowCharacterDetails(captured));
    }

    // --- Town menu ---

    // Makes a menu button as a copy of the Hire button, placed to its right (slot 1 is the first place to the
    // right, slot 2 the next...). The copy's scene-assigned click action is switched off.
    private void CreateMenuButton(string buttonName, string text, int slot, UnityAction onClick)
    {
        Transform canvas = characterPanel.transform.parent;
        Transform source = canvas.Find("OpenHireButton");
        if (source == null) source = canvas.Find("OpenBuildButton");
        if (source == null)
        {
            Debug.LogWarning($"Could not find the Hire / Build button to copy, so there is no {text} button.");
            return;
        }

        GameObject menuButton = Instantiate(source.gameObject, canvas);
        menuButton.name = buttonName;
        menuButton.transform.SetSiblingIndex(source.GetSiblingIndex() + slot);

        RectTransform sourceRect = source.GetComponent<RectTransform>();
        RectTransform rect = menuButton.GetComponent<RectTransform>();
        if (sourceRect != null && rect != null) rect.anchoredPosition = sourceRect.anchoredPosition + new Vector2((sourceRect.sizeDelta.x + 10f) * slot, 0f);

        Button button = menuButton.GetComponent<Button>();
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
        }
        button.onClick.AddListener(onClick);

        TMP_Text label = menuButton.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = text;
    }

    // The testing tools: a "Cheats" button in the game menu (a copy of its Quit button, below it) opens them.
    // The old test button that skipped the time of day is part of these tools now, so it goes away.
    private void SetUpCheats()
    {
        Transform canvas = characterPanel.transform.parent;
        Transform oldTestButton = canvas.Find("TestButtonforFunctions");
        if (oldTestButton != null) oldTestButton.gameObject.SetActive(false);

        if (!Cheats.Available) return;

        cheats = new CheatMenu(this, buttonPrefab, characterPanel, assignPanel);

        Transform menuPanel = canvas.Find("MenuPanel");
        Transform quit = menuPanel != null ? menuPanel.Find("QuitButton") : null;
        if (quit == null)
        {
            Debug.LogWarning("Could not find the game menu's Quit button to copy, so there is no Cheats button.");
            return;
        }

        GameObject cheatsButton = Instantiate(quit.gameObject, menuPanel);
        cheatsButton.name = "CheatsButton";
        RectTransform quitRect = quit.GetComponent<RectTransform>();
        RectTransform rect = cheatsButton.GetComponent<RectTransform>();
        float step = quitRect.sizeDelta.y + 10f;
        rect.anchoredPosition = quitRect.anchoredPosition + new Vector2(0f, -step);
        RectTransform menuRect = menuPanel.GetComponent<RectTransform>();
        if (menuRect != null) menuRect.sizeDelta += new Vector2(0f, step); // the panel grows upward (its pivot is at the bottom)

        Button button = cheatsButton.GetComponent<Button>();
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
        }
        button.onClick.AddListener(ToggleCheatMenu);
        TMP_Text label = cheatsButton.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = "Cheats";
    }

    public void ToggleCheatMenu()
    {
        if (cheats == null) return;
        bool opening = !cheats.Panel.activeInHierarchy;
        TogglePanel(cheats.Panel);
        if (opening) cheats.Show();
    }

    // Shows the full pictures of whoever was just recruited, one after another
    public void ShowReveal(List<PullResult> results)
    {
        if (reveal != null) reveal.Show(results);
    }

    // The character's full picture: beside the details panel on a wide screen, in the left part of the sheet on a phone
    private void CreateDetailsPortrait()
    {
        GameObject go = new GameObject("PortraitImage", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(characterPanel.transform, false);
        go.transform.SetAsFirstSibling();
        RectTransform rect = go.GetComponent<RectTransform>();
        // Wide screen: just left of the panel (the job list, when open, covers it)
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-10f, 0f);
        rect.sizeDelta = new Vector2(300f, 450f);
        detailsPortrait = go.GetComponent<Image>();
        detailsPortrait.preserveAspect = true;
        detailsPortrait.raycastTarget = false;
        go.SetActive(false);
    }

    // Shows (or hides, with null) the portrait on the details panel
    private void SetDetailsPortrait(Sprite sprite)
    {
        if (detailsPortrait == null) return;
        detailsPortrait.sprite = sprite;
        if (layout != null) layout.SetDetailsHavePortrait(sprite != null);
        SyncPortraitVisibility();
    }

    // The portrait shows while a character's details are open (on a wide screen the job list takes its place)
    private void SyncPortraitVisibility()
    {
        if (detailsPortrait == null) return;
        bool want = detailsPortrait.sprite != null && panelMode == PanelMode.Character && characterPanel.activeInHierarchy;
        if (want && layout != null && !layout.IsPortrait && assignPanel.activeSelf) want = false;
        if (detailsPortrait.gameObject.activeSelf != want) detailsPortrait.gameObject.SetActive(want);
    }

    public void ToggleRecruitMenu()
    {
        if (recruit == null) return;
        bool opening = !recruit.Panel.activeInHierarchy;
        TogglePanel(recruit.Panel);
        if (opening) recruit.Show();
    }

    public void ToggleTownMenu()
    {
        if (town == null) return;
        bool opening = !town.Panel.activeInHierarchy;
        TogglePanel(town.Panel); // pauses and closes other menus, like Build and Hire do
        if (opening) town.Show();
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

            GameObject buttonGO = MenuPanels.MakeRow(buttonPrefab, assignButtonContainer, $"{building.name}AssignButton", $"{building.DisplayName} ({building.Workers.Count}/{building.WorkerSlots})", building.buildingType.sprite);
            Button button = buttonGO.GetComponent<Button>();

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
