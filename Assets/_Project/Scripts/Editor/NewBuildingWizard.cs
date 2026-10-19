using System.Linq;
using UnityEditor;
using UnityEngine;

// Eryville > New Building...: makes everything a new building needs in one go (see docs/NEW_BUILDING.md):
// the building type, its animation set (filled from a named sprite sheet), its prefab (a copy of the template's, so it
// has the same behaviour scripts), a free typeId, and its entry in the BuildingPlacer's list.
// Costs, workers, output and so on are copied from a template building until you tune them in the inspector.
public class NewBuildingWizard : EditorWindow
{
    private const string BuildingFolder = "Assets/_Project/ScriptableObjects/Buildings";
    private const string PrefabFolder = "Assets/_Project/Prefabs/Buildings";

    private string buildingName = "";
    private BuildingType template;
    private Vector2Int size = new Vector2Int(2, 2);
    private BuildingArtStyle artStyle = BuildingArtStyle.ThreeQuarter;
    private float artWidth = 0f;
    private Texture2D animationSheet;
    private Sprite stillSprite;
    private Sprite topDownSprite;
    private bool addToGame = true;
    private string result = "";

    public static void Open()
    {
        GetWindow<NewBuildingWizard>("New Building").minSize = new Vector2(380, 480);
    }

    private void OnEnable()
    {
        if (template == null) template = CharacterAssetTools.FindAll<BuildingType>().OrderBy(t => t.typeId).FirstOrDefault();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("1. What", EditorStyles.boldLabel);
        buildingName = EditorGUILayout.TextField("Name", buildingName);
        template = (BuildingType)EditorGUILayout.ObjectField(new GUIContent("Copy settings from", "Costs, workers, housing, output and the prefab (so the same behaviour scripts) are copied from this building. Pick one that works like the new one: a Home for a house, a Gold Mine for a producer."), template, typeof(BuildingType), false);
        size = EditorGUILayout.Vector2IntField("Size on the ground (cells)", size);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2. Pictures (optional, can be added later)", EditorStyles.boldLabel);
        artStyle = (BuildingArtStyle)EditorGUILayout.EnumPopup(new GUIContent("Art style", "FitFootprint: top-down, the picture covers exactly the cells. ThreeQuarter: the picture keeps its shape, stands on the cells and rises above them."), artStyle);
        if (artStyle == BuildingArtStyle.ThreeQuarter) artWidth = EditorGUILayout.FloatField(new GUIContent("Picture width (cells)", "0 = as wide as the cells"), artWidth);
        animationSheet = (Texture2D)EditorGUILayout.ObjectField(new GUIContent("Animation sprite sheet", "Sliced, with frames named like closed_0, open_0, opening_0, closing_0."), animationSheet, typeof(Texture2D), false);
        stillSprite = (Sprite)EditorGUILayout.ObjectField(new GUIContent("Still picture", "Used if the sheet has no closed / open frames. Left empty it is taken from the sheet."), stillSprite, typeof(Sprite), false);

        if (artStyle == BuildingArtStyle.ThreeQuarter) topDownSprite = (Sprite)EditorGUILayout.ObjectField(new GUIContent("Top-down picture (optional)", "Shown in the flat view. Empty: the 3/4 picture is squashed onto the cells."), topDownSprite, typeof(Sprite), false);

        EditorGUILayout.Space();
        addToGame = EditorGUILayout.Toggle(new GUIContent("Buildable in this scene", "Adds it to the BuildingPlacer's list in the open scene."), addToGame);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(buildingName)))
        {
            if (GUILayout.Button("Create building", GUILayout.Height(32))) result = Create();
        }
        if (result.Length > 0) EditorGUILayout.HelpBox(result, MessageType.Info);
    }

    private string Create()
    {
        string name = buildingName.Trim();
        foreach (char bad in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(bad.ToString(), "");
        if (name.Length == 0) return "That name cannot be used as a file name.";

        string typePath = $"{BuildingFolder}/{name}/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<Object>(typePath) != null) return $"{typePath} already exists. Pick another name or open that building.";

        CharacterAssetTools.EnsureFolder($"{BuildingFolder}/{name}");
        CharacterAssetTools.EnsureFolder(PrefabFolder);

        BuildingType type = template != null ? Instantiate(template) : CreateInstance<BuildingType>();
        type.name = name;
        type.buildingName = buildingName.Trim();
        type.typeId = BuildingAssetTools.NextTypeId();
        type.size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
        type.artStyle = artStyle;
        type.artWidth = artStyle == BuildingArtStyle.ThreeQuarter ? Mathf.Max(0f, artWidth) : 0f;
        type.artOffset = Vector2.zero;
        type.topDownSprite = artStyle == BuildingArtStyle.ThreeQuarter ? topDownSprite : null;
        AssetDatabase.CreateAsset(type, typePath);

        string notes = "";
        BuildingAnimationSet set = CreateInstance<BuildingAnimationSet>();
        AssetDatabase.CreateAsset(set, $"{BuildingFolder}/{name}/{name} Animations.asset");
        if (animationSheet != null) notes += " " + BuildingAssetTools.FillAnimations(set, animationSheet);
        type.animations = set;

        Sprite picture = stillSprite != null ? stillSprite : set.StillPicture();
        if (picture == null && template != null) picture = template.sprite;
        type.sprite = picture;

        GameObject templatePrefab = template != null ? template.buildingPrefab : null;
        if (templatePrefab != null)
        {
            string prefabPath = $"{PrefabFolder}/{name}.prefab";
            if (AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(templatePrefab), prefabPath))
            {
                type.buildingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }
            else
            {
                type.buildingPrefab = templatePrefab;
                notes += " The prefab could not be copied, so the template's is shared.";
            }
        }
        else
        {
            notes += " There was no template prefab: assign the building prefab by hand.";
        }
        EditorUtility.SetDirty(type);

        string where = "";
        if (addToGame) where = BuildingAssetTools.AddToBuildingPlacer(type) ? " Added to the BuildingPlacer (save the scene)." : " Not added to the BuildingPlacer: open GameScene and run it again, or add it by hand.";

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = type;
        EditorGUIUtility.PingObject(type);
        return $"Created {type.buildingName} (typeId {type.typeId}).{notes}{where}\nNext: set costs, workers, housing and output in the inspector, then run Eryville > Check Buildings.";
    }
}
