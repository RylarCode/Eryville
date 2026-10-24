using System.Linq;
using UnityEditor;
using UnityEngine;

// Eryville > New Character...: makes everything a new character needs in one go (see docs/NEW_CHARACTER.md):
// the character type, its animation set (filled from a named sprite sheet), a body scale that makes it as tall as the template
// (the template's prefab is shared), a free typeId, and its entries in the CharacterManager and the gacha pools.
// Everything else (schedule, mood, speed, awareness levels) is copied from a template character, so the new one
// behaves like it until you tune it in the inspector.
public class NewCharacterWizard : EditorWindow
{
    private const string CharacterFolder = "Assets/_Project/ScriptableObjects/Characters";

    private string characterName = "";
    private Texture2D animationSheet;
    private Sprite portrait;
    private Sprite face;
    private Sprite stillSprite;
    private CharacterType template;
    private Rarity rarity = Rarity.Common;
    private bool addToGame = true;
    private bool addToPools = true;
    private string result = "";

    public static void Open()
    {
        GetWindow<NewCharacterWizard>("New Character").minSize = new Vector2(380, 520);
    }

    private void OnEnable()
    {
        if (template == null) template = CharacterAssetTools.FindAll<CharacterType>().OrderBy(t => t.typeId).FirstOrDefault();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("1. Who", EditorStyles.boldLabel);
        characterName = EditorGUILayout.TextField("Name", characterName);
        rarity = (Rarity)EditorGUILayout.EnumPopup("Rarity", rarity);
        template = (CharacterType)EditorGUILayout.ObjectField(new GUIContent("Copy settings from", "Schedule, mood, speed, wage, awareness levels and the prefab are copied from this character."), template, typeof(CharacterType), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2. Pictures (all optional, can be added later)", EditorStyles.boldLabel);
        animationSheet = (Texture2D)EditorGUILayout.ObjectField(new GUIContent("Animation sprite sheet", "Sliced, with frames named like walk_down_0. Fills every animation."), animationSheet, typeof(Texture2D), false);
        stillSprite = (Sprite)EditorGUILayout.ObjectField(new GUIContent("Small sprite", "Used if the sheet has no idle / walk frames. Left empty it is taken from the sheet."), stillSprite, typeof(Sprite), false);
        portrait = (Sprite)EditorGUILayout.ObjectField(new GUIContent("Full portrait", "Shown after recruiting and when you view the character."), portrait, typeof(Sprite), false);
        face = (Sprite)EditorGUILayout.ObjectField(new GUIContent("Face icon", "Small, for lists and buttons."), face, typeof(Sprite), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("3. Where it is added", EditorStyles.boldLabel);
        addToGame = EditorGUILayout.Toggle(new GUIContent("Hireable in this scene", "Adds it to the CharacterManager's list in the open scene."), addToGame);
        addToPools = EditorGUILayout.Toggle(new GUIContent("Add to gacha pools", "Adds it to every gacha pool that lists its characters."), addToPools);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(characterName)))
        {
            if (GUILayout.Button("Create character", GUILayout.Height(32))) result = Create();
        }
        if (result.Length > 0) EditorGUILayout.HelpBox(result, MessageType.Info);
    }

    private string Create()
    {
        string name = characterName.Trim();
        foreach (char bad in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(bad.ToString(), "");
        if (name.Length == 0) return "That name cannot be used as a file name.";

        string typePath = $"{CharacterFolder}/{name}/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<Object>(typePath) != null) return $"{typePath} already exists. Pick another name or open that character.";

        CharacterAssetTools.EnsureFolder($"{CharacterFolder}/{name}");

        // The type: a copy of the template's settings (or a blank one)
        CharacterType type = template != null ? Instantiate(template) : CreateInstance<CharacterType>();
        type.name = name;
        type.displayName = characterName.Trim();
        type.typeId = CharacterAssetTools.NextTypeId();
        type.rarity = rarity;
        type.portrait = portrait;
        type.face = face;
        AssetDatabase.CreateAsset(type, typePath);

        // The animations, filled from the sheet
        string notes = "";
        CharacterAnimationSet set = CreateInstance<CharacterAnimationSet>();
        AssetDatabase.CreateAsset(set, $"{CharacterFolder}/{name}/{name} Animations.asset");
        if (animationSheet != null) notes += " " + CharacterAssetTools.FillAnimations(set, animationSheet);
        type.animations = set;

        Sprite small = stillSprite != null ? stillSprite : AnimationNaming.StillPicture(set);
        if (small == null) small = portrait != null ? portrait : (template != null ? template.sprite : null);
        type.sprite = small;

        // The body: the template's prefab is shared, and the character's own body scale makes the new picture stand as tall as the template's does
        GameObject templatePrefab = template != null ? template.characterPrefab : null;
        if (templatePrefab != null)
        {
            type.characterPrefab = templatePrefab;
            type.bodyScale = FittedScale(template, small);
        }
        else
        {
            notes += " There was no template prefab: assign the character prefab by hand.";
        }
        EditorUtility.SetDirty(type);

        string where = "";
        if (addToGame) where += CharacterAssetTools.AddToCharacterManager(type) ? " Added to the CharacterManager (save the scene)." : " Not added to the CharacterManager: open GameScene and run it again, or add it by hand.";
        if (addToPools) where += $" Added to {CharacterAssetTools.AddToGachaPools(type)} gacha pool(s).";

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = type;
        EditorGUIUtility.PingObject(type);
        return $"Created {type.displayName} (typeId {type.typeId}).{notes}{where}\nNext: tune rarity, wage, schedule and mood in the inspector, then run Eryville > Check Characters. With the Firebase backend, also add it to firebase/functions/src/config.ts.";
    }

    // The body scale that makes the new picture as tall as the template's picture is in the world
    private static float FittedScale(CharacterType template, Sprite newSprite)
    {
        float baseScale = template.bodyScale > 0f ? template.bodyScale : 1f;
        Sprite before = template.sprite;
        if (before == null || newSprite == null || before.bounds.size.y <= 0f || newSprite.bounds.size.y <= 0f) return baseScale;
        return baseScale * before.bounds.size.y / newSprite.bounds.size.y;
    }
}
