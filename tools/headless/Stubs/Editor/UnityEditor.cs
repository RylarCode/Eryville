// Headless stand-ins for the Unity editor API, only so the editor scripts (Assets/_Project/Scripts/Editor) can be compiled.
// They do nothing: the editor tools themselves can only be tried in Unity.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method)] public sealed class MenuItem : Attribute { public MenuItem(string path) { } public MenuItem(string path, bool validate) { } public MenuItem(string path, bool validate, int priority) { } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class CustomEditor : Attribute { public CustomEditor(Type t) { } public CustomEditor(Type t, bool editorForChildClasses) { } }
    [AttributeUsage(AttributeTargets.Method)] public sealed class InitializeOnLoadMethodAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class InitializeOnLoadAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class CanEditMultipleObjects : Attribute { }

    public enum MessageType { None, Info, Warning, Error }
    public enum PlayModeStateChange { EnteredEditMode, ExitingEditMode, EnteredPlayMode, ExitingPlayMode }

    public class Editor : ScriptableObject
    {
        public UnityEngine.Object target;
        public UnityEngine.Object[] targets = new UnityEngine.Object[0];
        public SerializedObject serializedObject => new SerializedObject(target);
        public virtual void OnInspectorGUI() { }
        public bool DrawDefaultInspector() => false;
        public void Repaint() { }
    }

    public class EditorWindow : ScriptableObject
    {
        public Vector2 minSize, maxSize;
        public GUIContent titleContent = new GUIContent();
        public static T GetWindow<T>() where T : EditorWindow => CreateInstance<T>();
        public static T GetWindow<T>(string title) where T : EditorWindow => CreateInstance<T>();
        public static T GetWindow<T>(bool utility, string title) where T : EditorWindow => CreateInstance<T>();
        public void Show() { }
        public void Close() { }
        public void Repaint() { }
    }

    public class SerializedObject
    {
        public UnityEngine.Object targetObject;
        public SerializedObject(UnityEngine.Object o) { targetObject = o; }
        public SerializedProperty FindProperty(string name) => new SerializedProperty();
        public bool ApplyModifiedProperties() => true;
        public void Update() { }
    }

    public class SerializedProperty
    {
        private readonly List<SerializedProperty> items = new List<SerializedProperty>();
        public int arraySize => items.Count;
        public UnityEngine.Object objectReferenceValue;
        public int intValue;
        public float floatValue;
        public string stringValue;
        public bool boolValue;
        public SerializedProperty GetArrayElementAtIndex(int i) => items[i];
        public void InsertArrayElementAtIndex(int i) => items.Insert(i, new SerializedProperty());
        public void DeleteArrayElementAtIndex(int i) => items.RemoveAt(i);
    }

    public static class EditorGUILayout
    {
        public static UnityEngine.Object ObjectField(string label, UnityEngine.Object obj, Type type, bool allowSceneObjects, params GUILayoutOption[] o) => obj;
        public static UnityEngine.Object ObjectField(GUIContent label, UnityEngine.Object obj, Type type, bool allowSceneObjects, params GUILayoutOption[] o) => obj;
        public static UnityEngine.Object ObjectField(UnityEngine.Object obj, Type type, bool allowSceneObjects, params GUILayoutOption[] o) => obj;
        public static void Space() { }
        public static void Space(float width) { }
        public static void HelpBox(string message, MessageType type) { }
        public static void HelpBox(string message, MessageType type, bool wide) { }
        public static void LabelField(string label, params GUILayoutOption[] o) { }
        public static void LabelField(string label, GUIStyle style, params GUILayoutOption[] o) { }
        public static void LabelField(string label, string label2, params GUILayoutOption[] o) { }
        public static void LabelField(GUIContent label, params GUILayoutOption[] o) { }
        public static string TextField(string label, string text, params GUILayoutOption[] o) => text;
        public static string TextField(GUIContent label, string text, params GUILayoutOption[] o) => text;
        public static string TextArea(string text, params GUILayoutOption[] o) => text;
        public static bool Toggle(string label, bool value, params GUILayoutOption[] o) => value;
        public static bool Toggle(GUIContent label, bool value, params GUILayoutOption[] o) => value;
        public static Enum EnumPopup(string label, Enum value, params GUILayoutOption[] o) => value;
        public static Enum EnumPopup(GUIContent label, Enum value, params GUILayoutOption[] o) => value;
        public static int IntField(string label, int value, params GUILayoutOption[] o) => value;
        public static int IntField(GUIContent label, int value, params GUILayoutOption[] o) => value;
        public static float FloatField(string label, float value, params GUILayoutOption[] o) => value;
        public static float FloatField(GUIContent label, float value, params GUILayoutOption[] o) => value;
        public static Vector2Int Vector2IntField(string label, Vector2Int value, params GUILayoutOption[] o) => value;
        public static Vector2Int Vector2IntField(GUIContent label, Vector2Int value, params GUILayoutOption[] o) => value;
        public static Vector2 Vector2Field(string label, Vector2 value, params GUILayoutOption[] o) => value;
        public static bool PropertyField(SerializedProperty p, params GUILayoutOption[] o) => false;
        public static bool PropertyField(SerializedProperty p, GUIContent label, params GUILayoutOption[] o) => false;
        public static void BeginHorizontal(params GUILayoutOption[] o) { }
        public static void EndHorizontal() { }
        public static void BeginVertical(params GUILayoutOption[] o) { }
        public static void EndVertical() { }
        public static bool Foldout(bool foldout, string content) => foldout;
    }

    public static class EditorGUI
    {
        public class DisabledScope : IDisposable { public DisabledScope(bool disabled) { } public void Dispose() { } }
        public class IndentLevelScope : IDisposable { public IndentLevelScope() { } public IndentLevelScope(int increment) { } public void Dispose() { } }
        public class ChangeCheckScope : IDisposable { public bool changed => false; public void Dispose() { } }
        public static int indentLevel;
        public static void BeginChangeCheck() { }
        public static bool EndChangeCheck() => false;
    }

    public static class EditorStyles
    {
        public static GUIStyle boldLabel = new GUIStyle();
        public static GUIStyle label = new GUIStyle();
        public static GUIStyle helpBox = new GUIStyle();
        public static GUIStyle wordWrappedLabel = new GUIStyle();
        public static GUIStyle miniLabel = new GUIStyle();
    }

    public static class EditorGUIUtility
    {
        public static void PingObject(UnityEngine.Object o) { }
        public static void PingObject(int id) { }
        public static float labelWidth;
        public static float singleLineHeight = 18f;
    }

    public static class EditorUtility
    {
        public static void SetDirty(UnityEngine.Object o) { }
        public static void FocusProjectWindow() { }
        public static bool DisplayDialog(string title, string message, string ok) => true;
        public static bool DisplayDialog(string title, string message, string ok, string cancel) => true;
        public static void DisplayProgressBar(string title, string info, float progress) { }
        public static void ClearProgressBar() { }
        public static string SaveFilePanelInProject(string title, string defaultName, string extension, string message) => "";
    }

    public static class AssetDatabase
    {
        public static string GetAssetPath(UnityEngine.Object o) => "";
        public static void SaveAssets() { }
        public static void Refresh() { }
        public static void CreateAsset(UnityEngine.Object o, string path) { }
        public static T LoadAssetAtPath<T>(string path) where T : UnityEngine.Object => null;
        public static UnityEngine.Object LoadAssetAtPath(string path, Type type) => null;
        public static UnityEngine.Object[] LoadAllAssetsAtPath(string path) => new UnityEngine.Object[0];
        public static bool IsValidFolder(string path) => false;
        public static string CreateFolder(string parent, string name) => "";
        public static string GenerateUniqueAssetPath(string path) => path;
        public static string[] FindAssets(string filter) => new string[0];
        public static string[] FindAssets(string filter, string[] folders) => new string[0];
        public static string GUIDToAssetPath(string guid) => "";
        public static string AssetPathToGUID(string path) => "";
        public static bool CopyAsset(string from, string to) => false;
        public static bool DeleteAsset(string path) => false;
        public static string MoveAsset(string from, string to) => "";
        public static string RenameAsset(string path, string newName) => "";
        public static void AddObjectToAsset(UnityEngine.Object o, string path) { }
        public static void ImportAsset(string path) { }
    }

    public static class PrefabUtility
    {
        public static GameObject LoadPrefabContents(string path) => new GameObject("Prefab");
        public static void UnloadPrefabContents(GameObject contents) { }
        public static GameObject SaveAsPrefabAsset(GameObject root, string path) => root;
        public static GameObject SaveAsPrefabAsset(GameObject root, string path, out bool success) { success = true; return root; }
        public static UnityEngine.Object InstantiatePrefab(UnityEngine.Object prefab) => prefab;
    }

    public static class Selection
    {
        public static UnityEngine.Object activeObject;
        public static GameObject activeGameObject;
        public static UnityEngine.Object[] objects = new UnityEngine.Object[0];
    }

    public static class Undo
    {
        public static void RecordObject(UnityEngine.Object o, string name) { }
        public static void RegisterCreatedObjectUndo(UnityEngine.Object o, string name) { }
        public static void DestroyObjectImmediate(UnityEngine.Object o) { }
    }

    public static class EditorPrefs
    {
        private static readonly Dictionary<string, object> values = new Dictionary<string, object>();
        public static bool GetBool(string k, bool d = false) => values.TryGetValue(k, out object v) && v is bool b ? b : d;
        public static void SetBool(string k, bool v) => values[k] = v;
        public static int GetInt(string k, int d = 0) => values.TryGetValue(k, out object v) && v is int i ? i : d;
        public static void SetInt(string k, int v) => values[k] = v;
        public static string GetString(string k, string d = "") => values.TryGetValue(k, out object v) && v is string s ? s : d;
        public static void SetString(string k, string v) => values[k] = v;
    }

    public static class EditorApplication
    {
        public static bool isPlaying;
        public static bool isPlayingOrWillChangePlaymode;
        public static event Action<PlayModeStateChange> playModeStateChanged { add { } remove { } }
        public static Action update;
        public static void EnterPlaymode() { }
        public static void ExitPlaymode() { }
    }

    public class SceneAsset : UnityEngine.Object { }

    public class AssetImporter : UnityEngine.Object { public string assetPath; }
    public enum TextureImporterType { Default, NormalMap, Sprite = 8 }
    public enum TextureImporterCompression { Uncompressed, Compressed, CompressedHQ, CompressedLQ }
    public enum TextureImporterNPOTScale { None, ToNearest, ToLarger, ToSmaller }
    public class TextureImporter : AssetImporter
    {
        public TextureImporterType textureType; public bool isReadable, mipmapEnabled, alphaIsTransparency, sRGBTexture;
        public TextureImporterCompression textureCompression; public FilterMode filterMode; public TextureImporterNPOTScale npotScale;
    }
    public class AssetPostprocessor { public string assetPath { get; set; } public AssetImporter assetImporter { get; set; } }

    public static class Menu
    {
        public static void SetChecked(string path, bool value) { }
        public static bool GetChecked(string path) => false;
    }

    public struct EditorBuildSettingsScene { public string path; public bool enabled; }
    public static class EditorBuildSettings { public static EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[0]; }
}

namespace UnityEditor.SceneManagement
{
    public static class EditorSceneManager
    {
        public static SceneAsset playModeStartScene;
        public static bool MarkSceneDirty(UnityEngine.SceneManagement.Scene scene) => true;
        public static bool SaveOpenScenes() => true;
        public static UnityEngine.SceneManagement.Scene GetActiveScene() => UnityEngine.SceneManagement.SceneManager.GetActiveScene();
    }
}
