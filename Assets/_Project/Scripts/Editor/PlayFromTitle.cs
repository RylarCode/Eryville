using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// In the editor, pressing Play normally starts the scene that is open. With this on (the default), Play always starts at the
// title screen, like the real game, whichever scene you are editing. Turn it off under Eryville > Play From Title Screen
// to test GameScene directly.
public static class PlayFromTitle
{
    private const string MenuPath = "Eryville/Play From Title Screen";
    private const string Preference = "Eryville.PlayFromTitle";
    private const string TitleScenePath = "Assets/_Project/Scenes/MainMenuScene.unity";

    private static bool Enabled => EditorPrefs.GetBool(Preference, true);

    [InitializeOnLoadMethod]
    private static void Apply()
    {
        EditorSceneManager.playModeStartScene = Enabled ? AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScenePath) : null;
    }

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        EditorPrefs.SetBool(Preference, !Enabled);
        Apply();
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }
}
