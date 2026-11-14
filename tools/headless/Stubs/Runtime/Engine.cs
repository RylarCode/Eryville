// Headless stand-ins for the rest of the engine the game touches: sprites and renderers, colliders and 2D physics, the camera,
// grids and tilemaps, 2D lights, JsonUtility, UnityEvents, scenes, profiling and web requests.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace UnityEngine
{
    public class UnityException : Exception { public UnityException(string m) : base(m) { } }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum TextureWrapMode { Repeat, Clamp, Mirror, MirrorOnce }
    public enum TextureFormat { RGBA32 = 4, ARGB32 = 5, RGB24 = 3, Alpha8 = 1 }
    public enum SpriteDrawMode { Simple, Sliced, Tiled }
    public enum SpriteMaskInteraction { None, VisibleInsideMask, VisibleOutsideMask }
    public enum SpriteMeshType { FullRect, Tight }

    public class Texture : Object
    {
        public int width { get; set; }
        public int height { get; set; }
        public FilterMode filterMode { get; set; }
        public TextureWrapMode wrapMode { get; set; }
    }

    public class Texture2D : Texture
    {
        private Color32[] pixels;
        public bool isReadable { get; set; } = true;
        public Texture2D(int width, int height) { this.width = width; this.height = height; }
        public Texture2D(int width, int height, TextureFormat format, bool mipChain) : this(width, height) { }
        public Color32[] GetPixels32()
        {
            if (!isReadable) throw new UnityException("Texture is not readable");
            return pixels != null ? (Color32[])pixels.Clone() : new Color32[width * height];
        }
        public static Texture2D whiteTexture { get; } = new Texture2D(4, 4);
        public static Texture2D blackTexture { get; } = new Texture2D(4, 4);
        public void SetPixel(int x, int y, Color c) { }
        public void SetPixels(Color[] colors) { }
        public void SetPixels32(Color32[] colors) { pixels = (Color32[])colors.Clone(); }
        public Color GetPixel(int x, int y) => Color.white;
        public void Apply() { }
        public void Apply(bool updateMipmaps) { }
        public bool LoadImage(byte[] data) => true;
    }

    public class Sprite : Object
    {
        public Texture2D texture { get; private set; }
        public Rect rect { get; private set; }
        public Vector2 pivot { get; private set; }     // in pixels
        public float pixelsPerUnit { get; private set; } = 100f;
        public Bounds bounds { get; private set; }

        public Sprite() : this(100f, 100f) { }

        // Not a Unity API (Unity sprites come from assets): a sprite of a size in pixels, the pivot at its centre by default
        public Sprite(float widthPixels, float heightPixels, float pixelsPerUnit = 100f, float pivotX = 0.5f, float pivotY = 0.5f)
        {
            Set(new Texture2D((int)widthPixels, (int)heightPixels), new Rect(0, 0, widthPixels, heightPixels), new Vector2(pivotX, pivotY), pixelsPerUnit);
        }

        private void Set(Texture2D tex, Rect r, Vector2 normalizedPivot, float ppu)
        {
            texture = tex; rect = r; pixelsPerUnit = ppu <= 0f ? 100f : ppu;
            pivot = new Vector2(normalizedPivot.x * r.width, normalizedPivot.y * r.height);
            Vector3 size = new Vector3(r.width / pixelsPerUnit, r.height / pixelsPerUnit, 0f);
            Vector3 center = new Vector3((0.5f - normalizedPivot.x) * size.x, (0.5f - normalizedPivot.y) * size.y, 0f);
            bounds = new Bounds(center, size);
        }

        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot) => Create(texture, rect, pivot, 100f);
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit)
        {
            Sprite s = new Sprite();
            s.Set(texture, rect, pivot, pixelsPerUnit);
            return s;
        }
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, SpriteMeshType mesh) => Create(texture, rect, pivot, pixelsPerUnit);
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, SpriteMeshType mesh, Vector4 border) => Create(texture, rect, pivot, pixelsPerUnit);
    }

    public class Material : Object
    {
        public Shader shader;
        public Color color = Color.white;
        public Material(Shader s) { shader = s; }
        public Material(Material m) { shader = m.shader; color = m.color; }
        public void SetColor(string n, Color c) { color = c; }
        public void SetFloat(string n, float f) { }
    }
    public class Shader : Object { public static Shader Find(string name) => new Shader { name = name }; }

    public class Renderer : Component
    {
        private bool isEnabled = true;
        public bool enabled { get => isEnabled; set => isEnabled = value; }
        public int sortingOrder { get; set; }
        public string sortingLayerName { get; set; } = "Default";
        public int sortingLayerID { get; set; }
        public Material material { get; set; } = new Material((Shader)null);
        public Material sharedMaterial { get => material; set => material = value; }
        public bool isVisible => enabled && gameObject.activeInHierarchy;
        public virtual Bounds bounds => new Bounds(transform.position, Vector3.zero);
    }

    public class SpriteRenderer : Renderer
    {
        public Sprite sprite { get; set; }
        public Color color { get; set; } = Color.white;
        public bool flipX { get; set; }
        public bool flipY { get; set; }
        public SpriteDrawMode drawMode { get; set; }
        public Vector2 size { get; set; } = Vector2.one;
        public SpriteMaskInteraction maskInteraction { get; set; }
        public override Bounds bounds
        {
            get
            {
                if (sprite == null) return new Bounds(transform.position, Vector3.zero);
                Vector3 s = transform.lossyScale;
                Bounds b = sprite.bounds;
                return new Bounds(transform.position + Vector3.Scale(b.center, s), new Vector3(Math.Abs(b.size.x * s.x), Math.Abs(b.size.y * s.y), 0f));
            }
        }
    }

    public class LineRenderer : Renderer { public int positionCount; public float startWidth, endWidth; public Color startColor, endColor; public bool useWorldSpace = true; public void SetPosition(int i, Vector3 p) { } public void SetPositions(Vector3[] p) { positionCount = p.Length; } }

    // --- 2D physics ---

    public class Collider2D : Behaviour
    {
        public Vector2 offset { get; set; }
        public bool isTrigger { get; set; }
        public virtual Bounds bounds => new Bounds(transform.position + (Vector3)offset, Vector3.zero);
        public virtual bool OverlapPoint(Vector2 p)
        {
            Bounds b = bounds;
            return p.x >= b.min.x && p.x <= b.max.x && p.y >= b.min.y && p.y <= b.max.y;
        }
    }
    public class BoxCollider2D : Collider2D
    {
        public Vector2 size { get; set; } = Vector2.one;
        public bool autoTiling { get; set; }
        public override Bounds bounds
        {
            get
            {
                Vector3 s = transform.lossyScale;
                Vector3 center = transform.position + new Vector3(offset.x * s.x, offset.y * s.y, 0f);
                return new Bounds(center, new Vector3(Math.Abs(size.x * s.x), Math.Abs(size.y * s.y), 0f));
            }
        }
    }
    public enum CapsuleDirection2D { Vertical, Horizontal }
    public class CapsuleCollider2D : BoxCollider2D { public CapsuleDirection2D direction { get; set; } }
    public class CircleCollider2D : Collider2D
    {
        public float radius { get; set; } = 0.5f;
        public override Bounds bounds { get { float r = radius * Math.Abs(transform.lossyScale.x); return new Bounds(transform.position + (Vector3)offset, new Vector3(r * 2, r * 2, 0)); } }
    }
    public class PolygonCollider2D : BoxCollider2D { }
    public enum RigidbodyType2D { Dynamic, Kinematic, Static }
    public class Rigidbody2D : Component { public RigidbodyType2D bodyType; public float gravityScale = 1f; public Vector2 velocity; public bool simulated = true; public void MovePosition(Vector2 p) { transform.position = p; } }

    public struct RaycastHit2D
    {
        public Collider2D collider;
        public Vector2 point;
        public float distance;
        public Transform transform => collider != null ? collider.transform : null;
        public static implicit operator bool(RaycastHit2D h) => h.collider != null;
    }

    public struct ContactFilter2D { public bool useTriggers; }

    public static class Physics2D
    {
        // Tests can say exactly what is under the pointer; otherwise the enabled colliders are tested against the point
        public static RaycastHit2D[] NextHits;
        public static bool queriesHitTriggers = true;

        private static IEnumerable<Collider2D> At(Vector2 p) =>
            World.All.Where(g => !g.destroyed && g.activeInHierarchy).SelectMany(g => g.GetComponents<Collider2D>()).Where(c => c.enabled && c.OverlapPoint(p));

        public static RaycastHit2D[] RaycastAll(Vector2 origin, Vector2 direction) => RaycastAll(origin, direction, float.PositiveInfinity);
        public static RaycastHit2D[] RaycastAll(Vector2 origin, Vector2 direction, float distance) => RaycastAll(origin, direction, distance, ~0);
        public static RaycastHit2D[] RaycastAll(Vector2 origin, Vector2 direction, float distance, int layerMask)
        {
            if (NextHits != null) return NextHits;
            return At(origin).Select(c => new RaycastHit2D { collider = c, point = origin }).ToArray();
        }
        public static RaycastHit2D Raycast(Vector2 origin, Vector2 direction) => RaycastAll(origin, direction).FirstOrDefault();
        public static RaycastHit2D Raycast(Vector2 origin, Vector2 direction, float distance) => RaycastAll(origin, direction, distance).FirstOrDefault();
        public static RaycastHit2D Raycast(Vector2 origin, Vector2 direction, float distance, int layerMask) => RaycastAll(origin, direction, distance, layerMask).FirstOrDefault();
        public static Collider2D OverlapPoint(Vector2 p) => NextHits != null ? NextHits.FirstOrDefault().collider : At(p).FirstOrDefault();
        public static Collider2D OverlapPoint(Vector2 p, int layerMask) => OverlapPoint(p);
        public static Collider2D[] OverlapPointAll(Vector2 p) => NextHits != null ? NextHits.Select(h => h.collider).ToArray() : At(p).ToArray();
        public static Collider2D[] OverlapPointAll(Vector2 p, int layerMask) => OverlapPointAll(p);
        public static Collider2D[] OverlapCircleAll(Vector2 p, float r) => World.All.Where(g => !g.destroyed && g.activeInHierarchy).SelectMany(g => g.GetComponents<Collider2D>()).Where(c => c.enabled && Vector2.Distance(c.bounds.center, p) <= r).ToArray();
        public static Collider2D[] OverlapBoxAll(Vector2 p, Vector2 size, float angle) => OverlapCircleAll(p, size.magnitude / 2f);
    }

    public struct LayerMask
    {
        public int value;
        public static implicit operator int(LayerMask m) => m.value;
        public static implicit operator LayerMask(int v) => new LayerMask { value = v };
        public static int GetMask(params string[] names) => ~0;
        public static int NameToLayer(string name) => 0;
        public static string LayerToName(int layer) => "Default";
    }

    // --- Camera ---

    public enum CameraClearFlags { Skybox = 1, SolidColor = 2, Depth = 3, Nothing = 4 }

    public class Camera : Behaviour
    {
        private static Camera explicitMain;
        public static Camera main
        {
            get
            {
                if (explicitMain != null) return explicitMain;
                return World.All.Where(g => !g.destroyed && g.activeInHierarchy && g.tag == "MainCamera").Select(g => g.GetComponent<Camera>()).FirstOrDefault(c => c != null);
            }
            set => explicitMain = value;
        }
        public bool orthographic { get; set; } = true;
        public float orthographicSize { get; set; } = 5f;
        public float fieldOfView { get; set; } = 60f;
        public float nearClipPlane { get; set; } = 0.3f;
        public float farClipPlane { get; set; } = 1000f;
        public Color backgroundColor { get; set; } = Color.black;
        public CameraClearFlags clearFlags { get; set; }
        public int pixelWidth => Screen.width;
        public int pixelHeight => Screen.height;
        public float aspect => Screen.height > 0 ? (float)Screen.width / Screen.height : 1f;
        public int cullingMask { get; set; } = ~0;
        public float depth { get; set; }

        public Vector3 ScreenToWorldPoint(Vector3 s)
        {
            float halfH = orthographicSize, halfW = orthographicSize * aspect;
            float x = transform.position.x + (s.x / Math.Max(1, Screen.width) - 0.5f) * 2f * halfW;
            float y = transform.position.y + (s.y / Math.Max(1, Screen.height) - 0.5f) * 2f * halfH;
            return new Vector3(x, y, transform.position.z + s.z);
        }
        public Vector3 WorldToScreenPoint(Vector3 w)
        {
            float halfH = orthographicSize, halfW = orthographicSize * aspect;
            float x = ((w.x - transform.position.x) / (2f * halfW) + 0.5f) * Screen.width;
            float y = ((w.y - transform.position.y) / (2f * halfH) + 0.5f) * Screen.height;
            return new Vector3(x, y, w.z - transform.position.z);
        }
        public Vector3 ViewportToWorldPoint(Vector3 v) => ScreenToWorldPoint(new Vector3(v.x * Screen.width, v.y * Screen.height, v.z));
        public Vector3 WorldToViewportPoint(Vector3 w) { Vector3 s = WorldToScreenPoint(w); return new Vector3(s.x / Screen.width, s.y / Screen.height, s.z); }
        public Vector3 ScreenToViewportPoint(Vector3 s) => new Vector3(s.x / Screen.width, s.y / Screen.height, s.z);
        public Ray ScreenPointToRay(Vector3 s) => new Ray(ScreenToWorldPoint(s), Vector3.forward);
    }
    public struct Ray { public Vector3 origin, direction; public Ray(Vector3 o, Vector3 d) { origin = o; direction = d; } }

    // --- Grid and tilemaps ---

    public class GridLayout : Behaviour
    {
        public Vector3 cellSize { get; set; } = new Vector3(1, 1, 0);
        public Vector3 cellGap { get; set; }
        public Vector3Int WorldToCell(Vector3 w) { Vector3 l = w - transform.position; return new Vector3Int(Mathf.FloorToInt(l.x / cellSize.x), Mathf.FloorToInt(l.y / cellSize.y), 0); }
        public Vector3 CellToWorld(Vector3Int c) => transform.position + new Vector3(c.x * cellSize.x, c.y * cellSize.y, 0f);
        public Vector3 GetCellCenterWorld(Vector3Int c) => CellToWorld(c) + new Vector3(cellSize.x / 2f, cellSize.y / 2f, 0f);
        public Vector3 GetCellCenterLocal(Vector3Int c) => new Vector3((c.x + 0.5f) * cellSize.x, (c.y + 0.5f) * cellSize.y, 0f);
        public Vector3 CellToLocal(Vector3Int c) => new Vector3(c.x * cellSize.x, c.y * cellSize.y, 0f);
        public Vector3Int LocalToCell(Vector3 l) => new Vector3Int(Mathf.FloorToInt(l.x / cellSize.x), Mathf.FloorToInt(l.y / cellSize.y), 0);
    }
    public class Grid : GridLayout { }

    // --- Shared engine pieces ---

    public class Canvas : Behaviour
    {
        public RenderMode renderMode { get; set; }
        public int sortingOrder { get; set; }
        public bool overrideSorting { get; set; }
        public float scaleFactor { get; set; } = 1f;
        public Camera worldCamera { get; set; }
        public bool isRootCanvas => transform.parent == null || transform.parent.GetComponentInParent<Canvas>() == null;
        public Canvas rootCanvas { get { Canvas c = this; for (Transform t = transform.parent; t != null; t = t.parent) { Canvas p = t.GetComponent<Canvas>(); if (p != null) c = p; } return c; } }
        public string sortingLayerName { get; set; } = "Default";
        public static void ForceUpdateCanvases() { }
    }
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }

    public class CanvasGroup : Behaviour
    {
        public float alpha { get; set; } = 1f;
        public bool interactable { get; set; } = true;
        public bool blocksRaycasts { get; set; } = true;
        public bool ignoreParentGroups { get; set; }
    }

    public class CanvasRenderer : Component { public void SetAlpha(float a) { } public void SetColor(Color c) { } }

    public class AudioClip : Object { public float length; }
    public class AudioSource : Behaviour { public AudioClip clip; public float volume = 1f; public bool loop, playOnAwake; public bool isPlaying { get; private set; } public void Play() { isPlaying = true; } public void Stop() { isPlaying = false; } public void PlayOneShot(AudioClip c) { } public void PlayOneShot(AudioClip c, float v) { } }
    public class AudioListener : Behaviour { public static float volume = 1f; }

    public class GUIContent
    {
        public string text = "", tooltip = "";
        public Texture image;
        public GUIContent() { }
        public GUIContent(string text) { this.text = text; }
        public GUIContent(string text, string tooltip) { this.text = text; this.tooltip = tooltip; }
        public GUIContent(string text, Texture image) { this.text = text; this.image = image; }
        public static GUIContent none => new GUIContent();
    }
    public class GUIStyle { public GUIStyle() { } public GUIStyle(GUIStyle other) { } public int fontSize; public FontStyle fontStyle; public bool wordWrap, richText; public TextAnchor alignment; }
    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public class GUILayoutOption { }
    public static class GUILayout
    {
        public static bool Button(string text, params GUILayoutOption[] options) => false;
        public static bool Button(GUIContent content, params GUILayoutOption[] options) => false;
        public static GUILayoutOption Height(float h) => new GUILayoutOption();
        public static GUILayoutOption Width(float w) => new GUILayoutOption();
        public static GUILayoutOption ExpandWidth(bool e) => new GUILayoutOption();
        public static GUILayoutOption MinWidth(float w) => new GUILayoutOption();
        public static GUILayoutOption MaxWidth(float w) => new GUILayoutOption();
        public static void Label(string text, params GUILayoutOption[] options) { }
        public static void Space(float pixels) { }
        public static void FlexibleSpace() { }
        public static void BeginHorizontal(params GUILayoutOption[] options) { }
        public static void EndHorizontal() { }
        public static void BeginVertical(params GUILayoutOption[] options) { }
        public static void EndVertical() { }
        public static Vector2 BeginScrollView(Vector2 pos, params GUILayoutOption[] options) => pos;
        public static void EndScrollView() { }
        public static bool Toggle(bool value, string text, params GUILayoutOption[] options) => value;
        public static string TextField(string text, params GUILayoutOption[] options) => text;
    }
    public static class GUI
    {
        public static bool enabled = true;
        public static Color color = Color.white;
        public static bool changed;
        public static void Label(Rect r, string text) { }
        public static bool Button(Rect r, string text) => false;
    }

    public static class JsonUtility
    {
        public static string ToJson(object obj) => ToJson(obj, false);
        public static string ToJson(object obj, bool prettyPrint)
        {
            if (obj == null) return "";
            StringBuilder sb = new StringBuilder();
            WriteObject(sb, obj, prettyPrint, 0);
            return sb.ToString();
        }

        public static T FromJson<T>(string json) => (T)FromJson(json, typeof(T));
        public static object FromJson(string json, Type type)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON parse error: The document is empty.");
            object target = Activator.CreateInstance(type, true);
            FromJsonOverwrite(json, target);
            return target;
        }
        public static void FromJsonOverwrite(string json, object target)
        {
            using JsonDocument doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true });
            if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("JSON must represent an object type.");
            ReadInto(doc.RootElement, target);
        }

        // The fields Unity serializes: public ones and [SerializeField] ones, not static, not readonly-by-attribute, of serializable types
        private static IEnumerable<FieldInfo> Fields(Type t)
        {
            List<Type> chain = new List<Type>();
            for (Type x = t; x != null && x != typeof(object) && x != typeof(Object) && x != typeof(Component) && x != typeof(Behaviour) && x != typeof(MonoBehaviour) && x != typeof(ScriptableObject); x = x.BaseType) chain.Insert(0, x);
            foreach (Type x in chain)
                foreach (FieldInfo f in x.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (f.IsNotSerialized) continue;
                    if (!f.IsPublic && f.GetCustomAttribute<SerializeField>() == null) continue;
                    if (f.Name.Contains("<")) continue;
                    if (!Serializable(f.FieldType)) continue;
                    yield return f;
                }
        }

        private static bool Serializable(Type t)
        {
            if (t.IsPrimitive || t == typeof(string) || t.IsEnum || t == typeof(decimal)) return true;
            if (t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(Vector4) || t == typeof(Vector2Int) || t == typeof(Vector3Int) || t == typeof(Color) || t == typeof(Color32) || t == typeof(Rect) || t == typeof(Quaternion) || t == typeof(Bounds)) return true;
            if (t.IsArray) return t.GetArrayRank() == 1 && Serializable(t.GetElementType());
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) return Serializable(t.GetGenericArguments()[0]);
            if (typeof(Object).IsAssignableFrom(t)) return true; // written as an instance id, like Unity
            if (t.IsGenericType) return false;
            return t.GetCustomAttribute<SerializableAttribute>() != null || (t.IsValueType && !t.IsPrimitive && t.Namespace != "System");
        }

        private static void WriteObject(StringBuilder sb, object obj, bool pretty, int depth)
        {
            sb.Append('{');
            bool first = true;
            foreach (FieldInfo f in Fields(obj.GetType()))
            {
                if (!first) sb.Append(',');
                first = false;
                if (pretty) { sb.Append('\n'); sb.Append(' ', (depth + 1) * 4); }
                sb.Append('"').Append(f.Name).Append("\":");
                if (pretty) sb.Append(' ');
                WriteValue(sb, f.GetValue(obj), f.FieldType, pretty, depth + 1);
            }
            if (pretty && !first) { sb.Append('\n'); sb.Append(' ', depth * 4); }
            sb.Append('}');
        }

        private static void WriteValue(StringBuilder sb, object v, Type t, bool pretty, int depth)
        {
            if (t == typeof(string)) { sb.Append(v == null ? "\"\"" : JsonSerializer.Serialize((string)v)); return; }
            if (t == typeof(bool)) { sb.Append((bool)v ? "true" : "false"); return; }
            if (t.IsEnum) { sb.Append(Convert.ToInt64(v).ToString(CultureInfo.InvariantCulture)); return; }
            if (t == typeof(float)) { sb.Append(((float)v).ToString("R", CultureInfo.InvariantCulture)); return; }
            if (t == typeof(double)) { sb.Append(((double)v).ToString("R", CultureInfo.InvariantCulture)); return; }
            if (t.IsPrimitive || t == typeof(decimal)) { sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture)); return; }
            if (typeof(Object).IsAssignableFrom(t)) { sb.Append("{\"instanceID\":").Append(v is Object o && o != null ? o.GetInstanceID() : 0).Append('}'); return; }
            if (t.IsArray || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)))
            {
                Type et = t.IsArray ? t.GetElementType() : t.GetGenericArguments()[0];
                sb.Append('[');
                bool first = true;
                if (v != null)
                    foreach (object item in (IEnumerable)v)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteValue(sb, item ?? (et.IsValueType ? Activator.CreateInstance(et) : null), et, pretty, depth);
                    }
                sb.Append(']');
                return;
            }
            if (v == null) v = Activator.CreateInstance(t, true); // Unity writes an empty (default) object for null serializable classes
            if (t == typeof(Vector2)) { Vector2 a = (Vector2)v; sb.Append($"{{\"x\":{F(a.x)},\"y\":{F(a.y)}}}"); return; }
            if (t == typeof(Vector3)) { Vector3 a = (Vector3)v; sb.Append($"{{\"x\":{F(a.x)},\"y\":{F(a.y)},\"z\":{F(a.z)}}}"); return; }
            if (t == typeof(Vector2Int)) { Vector2Int a = (Vector2Int)v; sb.Append($"{{\"x\":{a.x},\"y\":{a.y}}}"); return; }
            if (t == typeof(Vector3Int)) { Vector3Int a = (Vector3Int)v; sb.Append($"{{\"x\":{a.x},\"y\":{a.y},\"z\":{a.z}}}"); return; }
            if (t == typeof(Color)) { Color a = (Color)v; sb.Append($"{{\"r\":{F(a.r)},\"g\":{F(a.g)},\"b\":{F(a.b)},\"a\":{F(a.a)}}}"); return; }
            WriteObject(sb, v, pretty, depth);
        }
        private static string F(float f) => f.ToString("R", CultureInfo.InvariantCulture);

        private static void ReadInto(JsonElement e, object target)
        {
            foreach (FieldInfo f in Fields(target.GetType()))
            {
                if (!e.TryGetProperty(f.Name, out JsonElement value)) continue;
                try { f.SetValue(target, Read(value, f.FieldType, f.GetValue(target))); }
                catch (Exception ex) when (!(ex is ArgumentException)) { }
            }
        }

        private static object Read(JsonElement e, Type t, object existing)
        {
            if (t == typeof(string)) return e.ValueKind == JsonValueKind.String ? e.GetString() : e.ToString();
            if (t == typeof(bool)) return e.ValueKind == JsonValueKind.True || (e.ValueKind == JsonValueKind.Number && e.GetDouble() != 0);
            if (t.IsEnum) return Enum.ToObject(t, e.ValueKind == JsonValueKind.Number ? e.GetInt64() : 0);
            if (t == typeof(float)) return (float)e.GetDouble();
            if (t == typeof(double)) return e.GetDouble();
            if (t == typeof(int)) return (int)e.GetDouble();
            if (t == typeof(long)) return e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out long l) ? l : (long)e.GetDouble();
            if (t.IsPrimitive) return Convert.ChangeType(e.GetDouble(), t, CultureInfo.InvariantCulture);
            if (typeof(Object).IsAssignableFrom(t)) return existing; // object references cannot come back from text
            if (t.IsArray || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)))
            {
                Type et = t.IsArray ? t.GetElementType() : t.GetGenericArguments()[0];
                IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(et));
                if (e.ValueKind == JsonValueKind.Array) foreach (JsonElement item in e.EnumerateArray()) list.Add(Read(item, et, null));
                if (!t.IsArray) return list;
                Array array = Array.CreateInstance(et, list.Count);
                list.CopyTo(array, 0);
                return array;
            }
            if (e.ValueKind != JsonValueKind.Object) return existing ?? Activator.CreateInstance(t, true);
            if (t == typeof(Vector2)) return new Vector2(Num(e, "x"), Num(e, "y"));
            if (t == typeof(Vector3)) return new Vector3(Num(e, "x"), Num(e, "y"), Num(e, "z"));
            if (t == typeof(Vector2Int)) return new Vector2Int((int)Num(e, "x"), (int)Num(e, "y"));
            if (t == typeof(Vector3Int)) return new Vector3Int((int)Num(e, "x"), (int)Num(e, "y"), (int)Num(e, "z"));
            if (t == typeof(Color)) return new Color(Num(e, "r"), Num(e, "g"), Num(e, "b"), Num(e, "a"));
            object obj = Activator.CreateInstance(t, true);
            ReadInto(e, obj);
            return obj;
        }
        private static float Num(JsonElement e, string n) => e.TryGetProperty(n, out JsonElement v) && v.ValueKind == JsonValueKind.Number ? (float)v.GetDouble() : 0f;
    }

    public static class Gizmos { public static Color color; public static void DrawWireCube(Vector3 c, Vector3 s) { } public static void DrawLine(Vector3 a, Vector3 b) { } public static void DrawWireSphere(Vector3 c, float r) { } }

    public class Animator : Behaviour { public RuntimeAnimatorController runtimeAnimatorController; public float speed = 1f; public void SetTrigger(string n) { } public void SetBool(string n, bool v) { } public void SetFloat(string n, float v) { } public void SetInteger(string n, int v) { } public void Play(string s) { } }
    public class RuntimeAnimatorController : Object { }
    public class TextAsset : Object { public string text { get; private set; } = ""; public byte[] bytes => System.Text.Encoding.UTF8.GetBytes(text); public TextAsset() { } public TextAsset(string text) { this.text = text ?? ""; } }
}

namespace UnityEngine.Serialization
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public sealed class FormerlySerializedAsAttribute : Attribute { public string oldName; public FormerlySerializedAsAttribute(string oldName) { this.oldName = oldName; } }
}

namespace UnityEngine.Scripting
{
    [AttributeUsage(AttributeTargets.All)] public sealed class PreserveAttribute : Attribute { }
}

namespace UnityEngine.Events
{
    public delegate void UnityAction();
    public delegate void UnityAction<T0>(T0 a);
    public delegate void UnityAction<T0, T1>(T0 a, T1 b);
    public enum UnityEventCallState { Off, EditorAndRuntime, RuntimeOnly }

    public abstract class UnityEventBase
    {
        // Listeners set in the scene (the inspector). Tests can add some to check they are switched off.
        public readonly List<(Action call, UnityEventCallState state)> persistent = new List<(Action, UnityEventCallState)>();
        public int GetPersistentEventCount() => persistent.Count;
        public void SetPersistentListenerState(int index, UnityEventCallState state) => persistent[index] = (persistent[index].call, state);
        public string GetPersistentMethodName(int index) => "";
        public Object GetPersistentTarget(int index) => null;
        protected void InvokePersistent() { foreach (var p in persistent.ToList()) if (p.state != UnityEventCallState.Off) p.call(); }
    }

    public class UnityEvent : UnityEventBase
    {
        private readonly List<UnityAction> calls = new List<UnityAction>();
        public void AddListener(UnityAction call) => calls.Add(call);
        public void RemoveListener(UnityAction call) => calls.Remove(call);
        public void RemoveAllListeners() => calls.Clear();
        public int ListenerCount => calls.Count; // not a Unity API: for tests
        public void Invoke() { InvokePersistent(); foreach (UnityAction c in calls.ToList()) c(); }
    }

    public class UnityEvent<T0> : UnityEventBase
    {
        private readonly List<UnityAction<T0>> calls = new List<UnityAction<T0>>();
        public void AddListener(UnityAction<T0> call) => calls.Add(call);
        public void RemoveListener(UnityAction<T0> call) => calls.Remove(call);
        public void RemoveAllListeners() => calls.Clear();
        public void Invoke(T0 a) { InvokePersistent(); foreach (UnityAction<T0> c in calls.ToList()) c(a); }
    }

    public class UnityEvent<T0, T1> : UnityEventBase
    {
        private readonly List<UnityAction<T0, T1>> calls = new List<UnityAction<T0, T1>>();
        public void AddListener(UnityAction<T0, T1> call) => calls.Add(call);
        public void RemoveListener(UnityAction<T0, T1> call) => calls.Remove(call);
        public void RemoveAllListeners() => calls.Clear();
        public void Invoke(T0 a, T1 b) { InvokePersistent(); foreach (UnityAction<T0, T1> c in calls.ToList()) c(a, b); }
    }
}

namespace UnityEngine.SceneManagement
{
    using UnityEngine.Events;
    public enum LoadSceneMode { Single, Additive }

    public struct Scene
    {
        public string name;
        public int buildIndex;
        public bool isLoaded => true;
        public bool IsValid() => true;
        public GameObject[] GetRootGameObjects() => World.All.Where(g => g.transform.parent == null && !g.destroyed).ToArray();
        public static bool operator ==(Scene a, Scene b) => a.name == b.name;
        public static bool operator !=(Scene a, Scene b) => a.name != b.name;
        public override bool Equals(object o) => o is Scene s && s == this;
        public override int GetHashCode() => name?.GetHashCode() ?? 0;
    }

    public static class SceneManager
    {
        private static Scene active = new Scene { name = "GameScene", buildIndex = 1 };
        public static event UnityAction<Scene, LoadSceneMode> sceneLoaded;
        public static event UnityAction<Scene> sceneUnloaded;
        public static event UnityAction<Scene, Scene> activeSceneChanged;
        public static string LastRequested; // not a Unity API: the scene the game last asked to load
        public static int sceneCount => 1;
        public static int sceneCountInBuildSettings => 2;
        public static Scene GetActiveScene() => active;
        public static Scene GetSceneAt(int i) => active;
        public static Scene GetSceneByName(string n) => active.name == n ? active : new Scene { name = n };

        // Loading a scene in the headless world only records it; tests call SimulateLoad to play out a scene change.
        public static void LoadScene(string name) { LastRequested = name; }
        public static void LoadScene(int index) { LastRequested = index.ToString(); }
        public static AsyncOperation LoadSceneAsync(string name) { LastRequested = name; return new AsyncOperation(); }
        public static AsyncOperation LoadSceneAsync(string name, LoadSceneMode mode) => LoadSceneAsync(name);
        public static AsyncOperation LoadSceneAsync(int index) { LastRequested = index.ToString(); return new AsyncOperation(); }

        // Not a Unity API: everything not kept with DontDestroyOnLoad goes, the new scene is active, sceneLoaded is raised
        public static void SimulateLoad(string name)
        {
            Scene old = active;
            foreach (GameObject g in World.All.ToList()) if (g.transform.parent == null && !g.dontDestroyOnLoad) World.Destroy(g);
            World.EndFrame();
            sceneUnloaded?.Invoke(old);
            active = new Scene { name = name, buildIndex = active.buildIndex + 1 };
            activeSceneChanged?.Invoke(old, active);
            sceneLoaded?.Invoke(active, LoadSceneMode.Single);
        }
    }
}

namespace UnityEngine.Profiling
{
    public static class Profiler
    {
        public static long GetTotalAllocatedMemoryLong() => GC.GetTotalMemory(false);
        public static long GetTotalReservedMemoryLong() => GC.GetTotalMemory(false);
        public static long GetMonoUsedSizeLong() => GC.GetTotalMemory(false);
        public static void BeginSample(string name) { }
        public static void EndSample() { }
    }
}

namespace UnityEngine.Rendering.Universal
{
    public class Light2D : Behaviour
    {
        public enum LightType { Parametric = 0, Freeform = 1, Sprite = 2, Point = 3, Global = 4 }
        public LightType lightType { get; set; } = LightType.Point;
        public Color color { get; set; } = Color.white;
        public float intensity { get; set; } = 1f;
        public float pointLightInnerRadius { get; set; }
        public float pointLightOuterRadius { get; set; } = 1f;
        public float pointLightInnerAngle { get; set; } = 360f;
        public float pointLightOuterAngle { get; set; } = 360f;
        public float falloffIntensity { get; set; } = 0.5f;
        public bool shadowsEnabled { get; set; }
        public int blendStyleIndex { get; set; }
        public int[] targetSortingLayers { get; set; } = new int[0];
    }
}

namespace UnityEngine.Tilemaps
{
    public class TileBase : ScriptableObject { }
    public class Tile : TileBase { public enum ColliderType { None, Sprite, Grid } public Sprite sprite; public Color color = Color.white; public ColliderType colliderType = ColliderType.Sprite; public Matrix4x4 transform = Matrix4x4.identity; }

    public class Tilemap : GridLayout
    {
        private readonly Dictionary<Vector3Int, TileBase> tiles = new Dictionary<Vector3Int, TileBase>();
        public Color color { get; set; } = Color.white;
        public Grid layoutGrid => GetComponentInParent<Grid>();
        public BoundsInt cellBounds
        {
            get
            {
                if (tiles.Count == 0) return new BoundsInt(0, 0, 0, 0, 0, 1);
                int minX = tiles.Keys.Min(k => k.x), minY = tiles.Keys.Min(k => k.y), maxX = tiles.Keys.Max(k => k.x), maxY = tiles.Keys.Max(k => k.y);
                return new BoundsInt(minX, minY, 0, maxX - minX + 1, maxY - minY + 1, 1);
            }
        }
        public BoundsInt BoundsOverride; // not used
        public void SetTile(Vector3Int p, TileBase t) { if (t == null) tiles.Remove(p); else tiles[p] = t; }
        public void SetTiles(Vector3Int[] ps, TileBase[] ts) { for (int i = 0; i < ps.Length; i++) SetTile(ps[i], ts[i]); }
        public TileBase GetTile(Vector3Int p) => tiles.TryGetValue(p, out TileBase t) ? t : null;
        public T GetTile<T>(Vector3Int p) where T : TileBase => GetTile(p) as T;
        public bool HasTile(Vector3Int p) => tiles.ContainsKey(p);
        public void ClearAllTiles() => tiles.Clear();
        public void CompressBounds() { }
        public void RefreshAllTiles() { }
        public void RefreshTile(Vector3Int p) { }
        public void SetColor(Vector3Int p, Color c) { }
        public int GetUsedTilesCount() => tiles.Values.Distinct().Count();
    }
    public class TilemapRenderer : Renderer { }
    public class TilemapCollider2D : Collider2D { }
}

namespace UnityEngine.Networking
{
    public class UploadHandler : IDisposable { public void Dispose() { } }
    public class UploadHandlerRaw : UploadHandler { public byte[] data; public string contentType; public UploadHandlerRaw(byte[] data) { this.data = data; } }
    public class DownloadHandler : IDisposable { public string text = ""; public byte[] data = new byte[0]; public void Dispose() { } }
    public class DownloadHandlerBuffer : DownloadHandler { }
    public class UnityWebRequestAsyncOperation : AsyncOperation { public UnityWebRequest webRequest; }

    // No network in tests: a request answers at once that it could not connect. The game's own HTTP layer is replaced in tests.
    public class UnityWebRequest : IDisposable
    {
        public enum Result { InProgress, Success, ConnectionError, ProtocolError, DataProcessingError }
        public const string kHttpVerbPOST = "POST";
        public const string kHttpVerbGET = "GET";
        public string url, method;
        public UploadHandler uploadHandler;
        public DownloadHandler downloadHandler = new DownloadHandlerBuffer();
        public int timeout;
        public long responseCode { get; private set; }
        public Result result { get; private set; } = Result.InProgress;
        public string error { get; private set; }
        public bool isDone { get; private set; }
        public UnityWebRequest() { }
        public UnityWebRequest(string url, string method) { this.url = url; this.method = method; }
        public UnityWebRequest(string url, string method, DownloadHandler d, UploadHandler u) { this.url = url; this.method = method; downloadHandler = d; uploadHandler = u; }
        public static UnityWebRequest Get(string url) => new UnityWebRequest(url, kHttpVerbGET);
        public static UnityWebRequest Post(string url, string data, string contentType) => new UnityWebRequest(url, kHttpVerbPOST);
        public static UnityWebRequest PostWwwForm(string url, string form) => new UnityWebRequest(url, kHttpVerbPOST);
        public void SetRequestHeader(string name, string value) { }
        public string GetResponseHeader(string name) => null;
        public UnityWebRequestAsyncOperation SendWebRequest()
        {
            result = Result.ConnectionError; error = "No network in the headless tests"; isDone = true;
            return new UnityWebRequestAsyncOperation { webRequest = this };
        }
        public void Abort() { }
        public void Dispose() { }
    }
}
