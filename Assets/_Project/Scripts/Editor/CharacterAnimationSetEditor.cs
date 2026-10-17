using UnityEditor;
using UnityEngine;

// Adds a "Fill from sprite sheet" box at the top of a Character Animations asset
[CustomEditor(typeof(CharacterAnimationSet))]
public class CharacterAnimationSetEditor : Editor
{
    private Texture2D sheet;
    private string message = "";

    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "Name the frames of a sliced sprite sheet like walk_down_0, walk_down_1, walk_left_0, idle_up_0, enter_right_0, " +
            "exit_down_0, walk_happy_left_0, walk_sad_up_0 ... then drop the sheet here and press the button.\n" +
            "Animations: idle, walk, walk_happy, walk_sad, enter, exit. Directions: down, up, left, right.",
            MessageType.Info);

        sheet = (Texture2D)EditorGUILayout.ObjectField("Sprite sheet", sheet, typeof(Texture2D), false);
        using (new EditorGUI.DisabledScope(sheet == null))
        {
            if (GUILayout.Button("Fill from sprite sheet"))
            {
                message = CharacterAssetTools.FillAnimations((CharacterAnimationSet)target, sheet);
            }
        }
        if (message.Length > 0) EditorGUILayout.HelpBox(message, MessageType.None);

        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}
