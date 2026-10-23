using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "NewBuildingType", menuName = "Scriptable Objects/BuildingType")]
public class BuildingType : ScriptableObject
{
    public string buildingName;
    public int typeId;               // unique per BuildingType asset; save files refer to the type by this
    public Sprite sprite;
    public int goldCost;
    public int woodCost;

    [Header("Story")]
    [Tooltip("Locked until this story chapter is finished. Empty: available from the start.")]
    public StoryChapter unlockedBy;
    [Tooltip("While locked, leave it out of the Build menu (otherwise it shows greyed out, saying what unlocks it).")]
    public bool hideWhileLocked = false;

    [Header("Moving")]
    public bool movable = true;   // can the player pick this building up and put it somewhere else?
    public int moveGoldCost;      // free for now; raise to charge for moving
    public int moveWoodCost;
    public Vector2Int size = new Vector2Int(1, 1); // Size in grid cells (the ground it stands on)
    [FormerlySerializedAs("buildingcategoryprefab")]
    public GameObject buildingPrefab;
    public enum Role { Resource, Home, Special }
    [FormerlySerializedAs("buildingclass")]
    public Role role = Role.Resource;




    [Header("Art")]
    [Tooltip("FitFootprint: the picture is stretched over the building's cells (top-down). ThreeQuarter: the picture keeps its shape, stands on the cells and rises above them, hiding what is behind it.")]
    public BuildingArtStyle artStyle = BuildingArtStyle.FitFootprint;
    [Tooltip("ThreeQuarter only: how wide the picture is, in cells. 0 = as wide as the building's cells.")]
    public float artWidth = 0f;
    [Tooltip("ThreeQuarter only: nudge the picture (in cells) if it does not line up with the ground it stands on.")]
    public Vector2 artOffset = Vector2.zero;
    [Tooltip("ThreeQuarter only, optional: a true top-down picture shown in the flat view (the 3/4 picture, squashed, is used when this is empty).")]
    public Sprite topDownSprite;
    public BuildingAnimationSet animations;           // closed / open animations. Optional.
    public BuildingOpenRule openRule = BuildingOpenRule.Auto;

    [Header("Production")]
    [Tooltip("What this building makes (and uses up). Empty: the old gold / wood per second below are used. Several recipes: the first is made unless another is chosen.")]
    public List<Recipe> recipes = new List<Recipe>();
    [Tooltip("Extra room this building adds to the town's stock, e.g. a Granary: 500 wheat.")]
    public List<ItemAmount> storageBonus = new List<ItemAmount>();

    [Header("Dining")]
    [Tooltip("How many characters can sit down to eat here at once (a Tavern, a Bakery with tables...). 0 = not a dining place.")]
    public int diningSlots = 0;
    [Tooltip("How good the meals are: it multiplies the mood a meal gives (1 = ordinary).")]
    public float mealQuality = 1f;

    [Header("Simple production (the original way; use Recipes for anything with inputs)")]
    public int goldGenerationPerSec;
    public int woodGenerationPerSec;
    public int workerSlots;   // how many characters can be assigned to work here
    public int housingSlots;  // how many characters can live here

    // What the building can make: its recipes, or if it has none a recipe made from the gold / wood per second fields
    // (so the Gold Mine and Lumberyard are recipes with no inputs, and everything runs the same way)
    public IReadOnlyList<Recipe> Recipes
    {
        get
        {
            if (recipes != null && recipes.Count > 0) return recipes;
            if (goldGenerationPerSec <= 0 && woodGenerationPerSec <= 0) return NoRecipes;

            if (legacyRecipes == null || legacyGold != goldGenerationPerSec || legacyWood != woodGenerationPerSec)
            {
                legacyGold = goldGenerationPerSec;
                legacyWood = woodGenerationPerSec;
                List<ItemAmount> outputs = new List<ItemAmount>();
                if (goldGenerationPerSec > 0) outputs.Add(new ItemAmount(ItemCatalog.Get(ResourceManager.Gold), goldGenerationPerSec));
                if (woodGenerationPerSec > 0) outputs.Add(new ItemAmount(ItemCatalog.Get(ResourceManager.Wood), woodGenerationPerSec));
                legacyRecipes = new List<Recipe> { Recipe.Create(buildingName + " output", 1f, null, outputs) };
            }
            return legacyRecipes;
        }
    }

    private static readonly List<Recipe> NoRecipes = new List<Recipe>();
    [System.NonSerialized] private List<Recipe> legacyRecipes;
    [System.NonSerialized] private int legacyGold, legacyWood;

    public bool Produces => Recipes.Count > 0;
}
