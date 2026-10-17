using UnityEditor;
using UnityEngine;

// Adds a "Fill from sprite sheet" box at the top of a Building Animations asset
[CustomEditor(typeof(BuildingAnimationSet))]
public class BuildingAnimationSetEditor : Editor
{
    private Texture2D sheet;
    private string message = "";

    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "Name the frames of a sliced sprite sheet closed_0, closed_1, open_0, open_1, opening_0, closing_0 ... " +
            "then drop the sheet here and press the button.\n" +
            "closed / open loop. opening / closing play once when the building opens or closes (optional).",
            MessageType.Info);

        sheet = (Texture2D)EditorGUILayout.ObjectField("Sprite sheet", sheet, typeof(Texture2D), false);
        using (new EditorGUI.DisabledScope(sheet == null))
        {
            if (GUILayout.Button("Fill from sprite sheet"))
            {
                message = BuildingAssetTools.FillAnimations((BuildingAnimationSet)target, sheet);
            }
        }
        if (message.Length > 0) EditorGUILayout.HelpBox(message, MessageType.None);

        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}
