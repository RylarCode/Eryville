// Headless stand-in for Unity's object model: objects, game objects, components, transforms, MonoBehaviour messages
// (Awake, OnEnable, Start, Update, LateUpdate, OnDisable, OnDestroy), coroutines, and a frame loop (World.Tick).
// It is not Unity: it is just enough of it, behaving the same way, for the game's code to run in tests.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace UnityEngine
{
    public enum FindObjectsSortMode { None, InstanceID }
    public enum FindObjectsInactive { Exclude, Include }
    public enum HideFlags { None = 0, HideInHierarchy = 1, DontSave = 52, HideAndDontSave = 61 }
    public enum Space { World, Self }
    public enum SendMessageOptions { RequireReceiver, DontRequireReceiver }

    public class Object
    {
        private static int nextId = 1;
        private readonly int instanceId = nextId++;
        private string objectName = "";

        // Destroyed objects compare equal to null, like in Unity (at the end of the frame Destroy was called in)
        public bool destroyed { get; internal set; }
        public HideFlags hideFlags { get; set; }

        public virtual string name { get => objectName; set => objectName = value; }
        public int GetInstanceID() => instanceId;
        public override string ToString() => $"{name} ({GetType().Name})";

        public static bool operator ==(Object a, Object b)
        {
            bool aNull = (object)a == null || a.destroyed, bNull = (object)b == null || b.destroyed;
            if (aNull || bNull) return aNull && bNull;
            return ReferenceEquals(a, b);
        }
        public static bool operator !=(Object a, Object b) => !(a == b);
        public static implicit operator bool(Object o) => o != null;
        public override bool Equals(object o) => o is Object other ? this == other : (o == null && destroyed);
        public override int GetHashCode() => instanceId;

        public static void Destroy(Object o) => World.Destroy(o);
        public static void Destroy(Object o, float delay) => World.Destroy(o);
        public static void DestroyImmediate(Object o) { World.Destroy(o); World.EndFrame(); }
        public static void DestroyImmediate(Object o, bool allowAssets) => DestroyImmediate(o);
        public static void DontDestroyOnLoad(Object o)
        {
            GameObject go = o as GameObject ?? (o as Component)?.gameObject;
            if (go != null) go.transform.root.gameObject.dontDestroyOnLoad = true;
        }

        public static T Instantiate<T>(T original) where T : Object => (T)World.Instantiate(original, null);
        public static T Instantiate<T>(T original, Transform parent) where T : Object => (T)World.Instantiate(original, parent);
        public static T Instantiate<T>(T original, Transform parent, bool worldPositionStays) where T : Object => (T)World.Instantiate(original, parent);
        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object
        {
            T copy = (T)World.Instantiate(original, null);
            Transform t = (copy as GameObject)?.transform ?? (copy as Component)?.transform;
            if (t != null) { t.position = position; t.rotation = rotation; }
            return copy;
        }
        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation, Transform parent) where T : Object
        {
            T copy = (T)World.Instantiate(original, parent);
            Transform t = (copy as GameObject)?.transform ?? (copy as Component)?.transform;
            if (t != null) { t.position = position; t.rotation = rotation; }
            return copy;
        }
        public static Object Instantiate(Object original) => World.Instantiate(original, null);

        public static T FindFirstObjectByType<T>() where T : Object => World.FindObjects<T>(false).FirstOrDefault();
        public static T FindFirstObjectByType<T>(FindObjectsInactive inactive) where T : Object => World.FindObjects<T>(inactive == FindObjectsInactive.Include).FirstOrDefault();
        public static T FindAnyObjectByType<T>() where T : Object => FindFirstObjectByType<T>();
        public static T FindAnyObjectByType<T>(FindObjectsInactive inactive) where T : Object => FindFirstObjectByType<T>(inactive);
        public static T FindObjectOfType<T>() where T : Object => FindFirstObjectByType<T>();
        public static T FindObjectOfType<T>(bool includeInactive) where T : Object => World.FindObjects<T>(includeInactive).FirstOrDefault();
        public static T[] FindObjectsByType<T>(FindObjectsSortMode sort) where T : Object => World.FindObjects<T>(false).ToArray();
        public static T[] FindObjectsByType<T>(FindObjectsInactive inactive, FindObjectsSortMode sort) where T : Object => World.FindObjects<T>(inactive == FindObjectsInactive.Include).ToArray();
        public static T[] FindObjectsOfType<T>() where T : Object => World.FindObjects<T>(false).ToArray();
        public static T[] FindObjectsOfType<T>(bool includeInactive) where T : Object => World.FindObjects<T>(includeInactive).ToArray();
    }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject => (T)CreateInstance(typeof(T));
        public static ScriptableObject CreateInstance(Type type)
        {
            ScriptableObject so = (ScriptableObject)Activator.CreateInstance(type, true);
            so.name = "";
            World.CallMessage(so, "Awake");
            World.CallMessage(so, "OnEnable");
            return so;
        }
        public static ScriptableObject CreateInstance(string typeName) => CreateInstance(Type.GetType(typeName));
    }

    public class GameObject : Object
    {
        internal readonly List<Component> components = new List<Component>();
        internal bool dontDestroyOnLoad;
        internal bool isPrefab;      // a template (made with World.MakePrefab): never ticked, never found, no Awake
        private bool active = true;

        public Transform transform { get; private set; }
        public string tag { get; set; } = "Untagged";
        public int layer { get; set; }
        public bool isStatic { get; set; }
        public UnityEngine.SceneManagement.Scene scene => UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        public GameObject gameObject => this;

        public GameObject() : this("New Game Object") { }
        public GameObject(string name) : this(name, new Type[0]) { }
        public GameObject(string name, params Type[] componentTypes)
        {
            this.name = name;
            bool rect = componentTypes.Any(t => typeof(RectTransform).IsAssignableFrom(t));
            transform = rect ? new RectTransform() : new Transform();
            Attach(transform);
            World.Register(this);
            foreach (Type t in componentTypes) if (!typeof(Transform).IsAssignableFrom(t)) AddComponent(t);
        }

        internal void Attach(Component c)
        {
            c.gameObject = this;
            components.Add(c);
        }

        internal void ReplaceTransform(Transform t) { components.Remove(transform); transform = t; components.Insert(0, t); t.gameObject = this; }

        public bool activeSelf => active;
        public bool activeInHierarchy => active && !isPrefab && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);

        public void SetActive(bool value)
        {
            if (active == value) return;
            bool before = activeInHierarchy;
            List<GameObject> tree = World.Tree(this).ToList();
            Dictionary<GameObject, bool> was = tree.ToDictionary(g => g, g => g.activeInHierarchy);
            active = value;
            if (before == activeInHierarchy) return;
            foreach (GameObject g in tree)
            {
                bool now = g.activeInHierarchy;
                if (was[g] == now) continue;
                foreach (Component c in g.components.ToList())
                {
                    if (!(c is MonoBehaviour mb)) continue;
                    if (now) { mb.EnsureAwake(); if (mb.enabled) World.CallMessage(mb, "OnEnable"); }
                    else if (mb.awoken && mb.enabled) World.CallMessage(mb, "OnDisable");
                }
            }
        }

        public Component AddComponent(Type type)
        {
            foreach (RequireComponent req in type.GetCustomAttributes(typeof(RequireComponent), true))
            {
                foreach (Type needed in new[] { req.m_Type0, req.m_Type1, req.m_Type2 })
                    if (needed != null && GetComponent(needed) == null) AddComponent(needed);
            }
            if (typeof(RectTransform).IsAssignableFrom(type) && !(transform is RectTransform))
            {
                RectTransform rt = new RectTransform();
                rt.CopyFrom(transform);
                ReplaceTransform(rt);
                return rt;
            }
            if (typeof(Transform).IsAssignableFrom(type)) return transform;
            Component c = (Component)Activator.CreateInstance(type, true);
            Attach(c);
            if (c is MonoBehaviour mb && activeInHierarchy)
            {
                mb.EnsureAwake();
                if (mb.enabled && !mb.destroyed) World.CallMessage(mb, "OnEnable");
            }
            return c;
        }
        public T AddComponent<T>() where T : Component => (T)AddComponent(typeof(T));

        public Component GetComponent(Type type) => components.FirstOrDefault(c => !c.destroyed && type.IsInstanceOfType(c));
        public T GetComponent<T>() { foreach (Component c in components) if (!c.destroyed && c is T t) return t; return default; }
        public bool TryGetComponent<T>(out T component) { component = GetComponent<T>(); return component != null; }
        public T[] GetComponents<T>() => components.Where(c => !c.destroyed).OfType<T>().ToArray();
        public void GetComponents<T>(List<T> into) { into.Clear(); into.AddRange(GetComponents<T>()); }
        public T GetComponentInChildren<T>() => GetComponentInChildren<T>(false);
        public T GetComponentInChildren<T>(bool includeInactive)
        {
            foreach (GameObject g in World.Tree(this))
            {
                if (!includeInactive && g != this && !ActiveBelow(g)) continue;
                T found = g.GetComponent<T>();
                if (found != null) return found;
            }
            return default;
        }
        public T[] GetComponentsInChildren<T>() => GetComponentsInChildren<T>(false);
        public T[] GetComponentsInChildren<T>(bool includeInactive) => World.Tree(this).Where(g => includeInactive || g == this || ActiveBelow(g)).SelectMany(g => g.GetComponents<T>()).ToArray();

        // Whether a descendant is active counting only the objects between it and this one (what GetComponentInChildren looks at)
        private bool ActiveBelow(GameObject g)
        {
            for (Transform t = g.transform; t != null && t != transform; t = t.parent) if (!t.gameObject.activeSelf) return false;
            return true;
        }
        public T GetComponentInParent<T>() { for (Transform t = transform; t != null; t = t.parent) { T found = t.gameObject.GetComponent<T>(); if (found != null) return found; } return default; }
        public T GetComponentInParent<T>(bool includeInactive) => GetComponentInParent<T>();
        public T[] GetComponentsInParent<T>() { List<T> list = new List<T>(); for (Transform t = transform; t != null; t = t.parent) list.AddRange(t.gameObject.GetComponents<T>()); return list.ToArray(); }

        public bool CompareTag(string t) => tag == t;
        public void SendMessage(string method, SendMessageOptions o = SendMessageOptions.DontRequireReceiver) { foreach (Component c in components.ToList()) World.CallMessage(c, method); }
        public void SendMessage(string method, object value, SendMessageOptions o = SendMessageOptions.DontRequireReceiver) { foreach (Component c in components.ToList()) World.CallMessage(c, method, value); }

        public static GameObject Find(string name) => World.All.FirstOrDefault(g => !g.destroyed && g.activeInHierarchy && g.name == name);
        public static GameObject FindWithTag(string tag) => World.All.FirstOrDefault(g => !g.destroyed && g.activeInHierarchy && g.tag == tag);
        public static GameObject FindGameObjectWithTag(string tag) => FindWithTag(tag);
        public static GameObject[] FindGameObjectsWithTag(string tag) => World.All.Where(g => !g.destroyed && g.activeInHierarchy && g.tag == tag).ToArray();
        public static GameObject CreatePrimitive(PrimitiveType type) => new GameObject(type.ToString());
    }

    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }

    public class Component : Object
    {
        public GameObject gameObject { get; internal set; }
        public Transform transform => gameObject?.transform;
        public override string name { get => gameObject != null ? gameObject.name : base.name; set { if (gameObject != null) gameObject.name = value; else base.name = value; } }
        public string tag { get => gameObject.tag; set => gameObject.tag = value; }
        public Component GetComponent(Type type) => gameObject.GetComponent(type);
        public T GetComponent<T>() => gameObject.GetComponent<T>();
        public bool TryGetComponent<T>(out T component) => gameObject.TryGetComponent(out component);
        public T[] GetComponents<T>() => gameObject.GetComponents<T>();
        public void GetComponents<T>(List<T> into) => gameObject.GetComponents(into);
        public T GetComponentInChildren<T>() => gameObject.GetComponentInChildren<T>();
        public T GetComponentInChildren<T>(bool includeInactive) => gameObject.GetComponentInChildren<T>(includeInactive);
        public T[] GetComponentsInChildren<T>() => gameObject.GetComponentsInChildren<T>();
        public T[] GetComponentsInChildren<T>(bool includeInactive) => gameObject.GetComponentsInChildren<T>(includeInactive);
        public T GetComponentInParent<T>() => gameObject.GetComponentInParent<T>();
        public T GetComponentInParent<T>(bool includeInactive) => gameObject.GetComponentInParent<T>();
        public T[] GetComponentsInParent<T>() => gameObject.GetComponentsInParent<T>();
        public bool CompareTag(string t) => gameObject.CompareTag(t);
        public void SendMessage(string method, SendMessageOptions o = SendMessageOptions.DontRequireReceiver) => gameObject.SendMessage(method, o);
        public void SendMessage(string method, object value, SendMessageOptions o = SendMessageOptions.DontRequireReceiver) => gameObject.SendMessage(method, value, o);
    }

    public class Behaviour : Component
    {
        private bool isEnabled = true;
        public bool enabled
        {
            get => isEnabled;
            set
            {
                if (isEnabled == value) return;
                isEnabled = value;
                if (this is MonoBehaviour mb && gameObject != null && gameObject.activeInHierarchy && mb.awoken) World.CallMessage(mb, value ? "OnEnable" : "OnDisable");
            }
        }
        public bool isActiveAndEnabled => enabled && gameObject != null && gameObject.activeInHierarchy;
    }

    public class Coroutine
    {
        internal readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        internal object waitingFor;
        internal float waitUntil = -1f;
        internal bool realtime;
        internal bool done;
    }

    public class YieldInstruction { }
    public class WaitForSeconds : YieldInstruction { public float seconds; public WaitForSeconds(float s) { seconds = s; } }
    public class WaitForSecondsRealtime : CustomYieldInstruction { public float waitTime; private float until = -1f; public WaitForSecondsRealtime(float t) { waitTime = t; } public override bool keepWaiting { get { if (until < 0f) until = Time.unscaledTime + waitTime; return Time.unscaledTime < until; } } }
    public class WaitForEndOfFrame : YieldInstruction { }
    public class WaitForFixedUpdate : YieldInstruction { }
    public abstract class CustomYieldInstruction : IEnumerator { public abstract bool keepWaiting { get; } public object Current => null; public bool MoveNext() => keepWaiting; public void Reset() { } }
    public sealed class WaitUntil : CustomYieldInstruction { private readonly Func<bool> p; public WaitUntil(Func<bool> p) { this.p = p; } public override bool keepWaiting => !p(); }
    public sealed class WaitWhile : CustomYieldInstruction { private readonly Func<bool> p; public WaitWhile(Func<bool> p) { this.p = p; } public override bool keepWaiting => p(); }
    public class AsyncOperation : YieldInstruction { public bool isDone = true; public float progress = 1f; public bool allowSceneActivation = true; public int priority; public event Action<AsyncOperation> completed { add { value?.Invoke(this); } remove { } } }

    public class MonoBehaviour : Behaviour
    {
        internal bool awoken, started;
        internal readonly List<Coroutine> coroutines = new List<Coroutine>();
        public bool useGUILayout { get; set; } = true;

        internal void EnsureAwake()
        {
            if (awoken || gameObject == null || gameObject.isPrefab) return;
            awoken = true;
            World.CallMessage(this, "Awake");
        }

        public Coroutine StartCoroutine(IEnumerator routine)
        {
            Coroutine c = new Coroutine();
            c.stack.Push(routine);
            coroutines.Add(c);
            World.Step(this, c); // runs to its first yield straight away, like Unity
            return c;
        }
        public Coroutine StartCoroutine(string method) => StartCoroutine((IEnumerator)GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(this, null));
        public void StopCoroutine(Coroutine c) { if (c != null) { c.done = true; coroutines.Remove(c); } }
        public void StopCoroutine(IEnumerator routine) { foreach (Coroutine c in coroutines.ToList()) if (c.stack.Contains(routine)) StopCoroutine(c); }
        public void StopAllCoroutines() { foreach (Coroutine c in coroutines) c.done = true; coroutines.Clear(); }

        private readonly List<(string method, float at, float repeat)> invokes = new List<(string, float, float)>();
        public void Invoke(string method, float time) => invokes.Add((method, Time.time + time, -1f));
        public void InvokeRepeating(string method, float time, float repeat) => invokes.Add((method, Time.time + time, repeat));
        public void CancelInvoke() => invokes.Clear();
        public void CancelInvoke(string method) => invokes.RemoveAll(i => i.method == method);
        public bool IsInvoking(string method) => invokes.Any(i => i.method == method);
        internal void RunInvokes()
        {
            for (int i = 0; i < invokes.Count; i++)
            {
                var inv = invokes[i];
                if (Time.time < inv.at) continue;
                World.CallMessage(this, inv.method);
                if (inv.repeat > 0f) invokes[i] = (inv.method, inv.at + inv.repeat, inv.repeat);
                else { invokes.RemoveAt(i); i--; }
            }
        }

        public static void print(object message) => Debug.Log(message);
    }

    public class Transform : Component, IEnumerable
    {
        private Transform parentTransform;
        internal readonly List<Transform> childList = new List<Transform>();

        // Local values; world values are worked out through the parents (position and scale; rotation only around z)
        public Vector3 localPosition { get; set; }
        public Vector3 localScale { get; set; } = Vector3.one;
        public Quaternion localRotation { get; set; } = Quaternion.identity;
        public bool hasChanged { get; set; }

        // The children (also the destroyed ones until the end of the frame, like Unity). Not a Unity API: for tests.
        public List<Transform> children => childList;

        internal void CopyFrom(Transform o)
        {
            localPosition = o.localPosition; localScale = o.localScale; localRotation = o.localRotation;
            parentTransform = o.parentTransform;
            if (parentTransform != null) { int i = parentTransform.childList.IndexOf(o); if (i >= 0) parentTransform.childList[i] = this; }
            childList.AddRange(o.childList);
            foreach (Transform c in childList) c.parentTransform = this;
        }

        public Transform parent { get => parentTransform; set => SetParent(value, true); }
        public Transform root { get { Transform t = this; while (t.parentTransform != null) t = t.parentTransform; return t; } }
        public int childCount => childList.Count;
        public Transform GetChild(int i) => childList[i];
        public IEnumerator GetEnumerator() => childList.ToList().GetEnumerator();

        public void SetParent(Transform newParent) => SetParent(newParent, true);
        public void SetParent(Transform newParent, bool worldPositionStays)
        {
            if (newParent == this) return;
            Vector3 wp = position; Vector3 ws = lossyScale;
            bool wasActive = gameObject != null && gameObject.activeInHierarchy;
            parentTransform?.childList.Remove(this);
            parentTransform = newParent;
            newParent?.childList.Add(this);
            if (worldPositionStays)
            {
                position = wp;
                Vector3 ps = newParent != null ? newParent.lossyScale : Vector3.one;
                localScale = new Vector3(Div(ws.x, ps.x), Div(ws.y, ps.y), Div(ws.z, ps.z));
            }
            if (gameObject != null && wasActive != gameObject.activeInHierarchy)
            {
                // parent's active state changed ours: send the messages
                bool now = gameObject.activeInHierarchy;
                foreach (GameObject g in World.Tree(gameObject))
                    foreach (MonoBehaviour mb in g.components.OfType<MonoBehaviour>().ToList())
                    {
                        if (now) { mb.EnsureAwake(); if (mb.enabled) World.CallMessage(mb, "OnEnable"); }
                        else if (mb.awoken && mb.enabled) World.CallMessage(mb, "OnDisable");
                    }
            }
        }
        private static float Div(float a, float b) => b == 0f ? a : a / b;

        public Vector3 lossyScale { get { Vector3 s = localScale; for (Transform p = parentTransform; p != null; p = p.parentTransform) s = Vector3.Scale(s, p.localScale); return s; } }

        public Vector3 position
        {
            get
            {
                if (parentTransform == null) return localPosition;
                Vector3 scaled = Vector3.Scale(localPosition, parentTransform.lossyScale);
                return parentTransform.position + parentTransform.rotation * scaled;
            }
            set
            {
                if (parentTransform == null) { localPosition = value; return; }
                Vector3 d = value - parentTransform.position;
                Quaternion inv = Quaternion.Euler(-parentTransform.rotation.eulerAngles);
                d = inv * d;
                Vector3 ps = parentTransform.lossyScale;
                localPosition = new Vector3(Div(d.x, ps.x), Div(d.y, ps.y), Div(d.z, ps.z));
            }
        }
        public Quaternion rotation
        {
            get { Vector3 e = localRotation.eulerAngles; for (Transform p = parentTransform; p != null; p = p.parentTransform) e += p.localRotation.eulerAngles; return Quaternion.Euler(e); }
            set { Vector3 e = value.eulerAngles; for (Transform p = parentTransform; p != null; p = p.parentTransform) e -= p.localRotation.eulerAngles; localRotation = Quaternion.Euler(e); }
        }
        public Vector3 eulerAngles { get => rotation.eulerAngles; set => rotation = Quaternion.Euler(value); }
        public Vector3 localEulerAngles { get => localRotation.eulerAngles; set => localRotation = Quaternion.Euler(value); }
        public Vector3 right => rotation * Vector3.right;
        public Vector3 up => rotation * Vector3.up;
        public Vector3 forward => Vector3.forward;

        public void Translate(Vector3 d) => position += d;
        public void Translate(Vector3 d, Space s) => position += d;
        public void Translate(float x, float y, float z) => position += new Vector3(x, y, z);
        public void Rotate(Vector3 e) => localRotation = Quaternion.Euler(localRotation.eulerAngles + e);
        public void Rotate(float x, float y, float z) => Rotate(new Vector3(x, y, z));
        public void SetPositionAndRotation(Vector3 p, Quaternion r) { position = p; rotation = r; }
        public void LookAt(Transform t) { }
        public Vector3 TransformPoint(Vector3 p) { Transform helper = this; return position + rotation * Vector3.Scale(p, lossyScale); }
        public Vector3 InverseTransformPoint(Vector3 p) { Vector3 d = Quaternion.Euler(-rotation.eulerAngles) * (p - position); Vector3 s = lossyScale; return new Vector3(Div(d.x, s.x), Div(d.y, s.y), Div(d.z, s.z)); }
        public Vector3 TransformDirection(Vector3 d) => rotation * d;
        public Vector3 TransformVector(Vector3 v) => rotation * Vector3.Scale(v, lossyScale);

        public Transform Find(string path)
        {
            Transform current = this;
            foreach (string part in path.Split('/'))
            {
                current = current.childList.FirstOrDefault(c => !c.destroyed && c.name == part);
                if (current == null) return null;
            }
            return current;
        }
        public Transform FindChild(string name) => Find(name);
        public bool IsChildOf(Transform p) { for (Transform t = this; t != null; t = t.parentTransform) if (t == p) return true; return false; }
        public int GetSiblingIndex() => parentTransform != null ? parentTransform.childList.IndexOf(this) : World.RootIndex(this);
        public void SetSiblingIndex(int i)
        {
            if (parentTransform == null) return;
            List<Transform> list = parentTransform.childList;
            list.Remove(this);
            list.Insert(Math.Max(0, Math.Min(i, list.Count)), this);
        }
        public void SetAsFirstSibling() => SetSiblingIndex(0);
        public void SetAsLastSibling() { if (parentTransform != null) SetSiblingIndex(parentTransform.childList.Count); }
        public void DetachChildren() { foreach (Transform c in childList.ToList()) c.SetParent(null); }
    }

    public static class Time
    {
        public static float deltaTime;
        public static float unscaledDeltaTime;
        public static float time;
        public static float unscaledTime;
        public static float realtimeSinceStartup => unscaledTime;
        public static double realtimeSinceStartupAsDouble => unscaledTime;
        public static double timeAsDouble => time;
        public static float timeScale = 1f;
        public static float fixedDeltaTime = 0.02f;
        public static float smoothDeltaTime => deltaTime;
        public static float maximumDeltaTime = 0.3333f;
        public static int frameCount;
        public static float timeSinceLevelLoad => time;
    }

    public static class Debug
    {
        public static readonly List<string> Logs = new List<string>();
        public static readonly List<string> Warnings = new List<string>();
        public static readonly List<string> Errors = new List<string>();
        public static bool Echo; // print every message (when debugging a test)
        public static bool isDebugBuild = true;
        public static bool developerConsoleVisible { get; set; }

        private static void Add(List<string> list, string kind, object message)
        {
            string text = message?.ToString() ?? "null";
            list.Add(text);
            if (list.Count > 5000) list.RemoveRange(0, 1000);
            if (Echo) Console.WriteLine($"[{kind}] {text}");
        }
        public static void Log(object m) => Add(Logs, "log", m);
        public static void Log(object m, Object context) => Log(m);
        public static void LogFormat(string f, params object[] a) => Log(string.Format(f, a));
        public static void LogWarning(object m) => Add(Warnings, "warning", m);
        public static void LogWarning(object m, Object context) => LogWarning(m);
        public static void LogWarningFormat(string f, params object[] a) => LogWarning(string.Format(f, a));
        public static void LogError(object m) => Add(Errors, "error", m);
        public static void LogError(object m, Object context) => LogError(m);
        public static void LogErrorFormat(string f, params object[] a) => LogError(string.Format(f, a));
        public static void LogException(Exception e) => Add(Errors, "exception", e.ToString());
        public static void LogException(Exception e, Object context) => LogException(e);
        public static void Assert(bool condition) { if (!condition) LogError("Assertion failed"); }
        public static void Assert(bool condition, object message) { if (!condition) LogError(message); }
        public static void DrawLine(Vector3 a, Vector3 b) { }
        public static void DrawLine(Vector3 a, Vector3 b, Color c) { }
        public static void DrawLine(Vector3 a, Vector3 b, Color c, float d) { }
        public static void DrawRay(Vector3 a, Vector3 b) { }
        public static void DrawRay(Vector3 a, Vector3 b, Color c) { }
        public static void Break() { }
        public static void ClearAll() { Logs.Clear(); Warnings.Clear(); Errors.Clear(); }
    }

    public enum RuntimePlatform { WindowsEditor, OSXEditor, LinuxEditor, WindowsPlayer, OSXPlayer, LinuxPlayer, Android, IPhonePlayer, WebGLPlayer }
    public enum SystemLanguage { English }

    public static class Application
    {
        public static string dataPath = "";
        public static string persistentDataPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "eryville-headless");
        public static string temporaryCachePath = System.IO.Path.GetTempPath();
        public static string streamingAssetsPath = "";
        public static string version = "0.1";
        public static string unityVersion = "6000.0.44f1";
        public static string productName = "Eryville";
        public static string companyName = "DefaultCompany";
        public static string identifier = "com.DefaultCompany.Eryville";
        public static bool isEditor = true;
        public static bool isPlaying = true;
        public static bool isMobilePlatform;
        public static bool isFocused = true;
        public static bool runInBackground;
        public static int targetFrameRate = -1;
        public static RuntimePlatform platform = RuntimePlatform.LinuxEditor;
        public static SystemLanguage systemLanguage = SystemLanguage.English;
        public static bool QuitCalled;
        public static void Quit() { QuitCalled = true; }
        public static void Quit(int code) { QuitCalled = true; }
        public static void OpenURL(string url) { }
        public static event Action<string, string, LogType> logMessageReceived { add { } remove { } }
        public static event Action quitting { add { } remove { } }
        public static event Action<bool> focusChanged { add { } remove { } }
    }

    public enum LogType { Error, Assert, Warning, Log, Exception }

    public static class SystemInfo
    {
        public static string deviceModel = "Headless";
        public static string graphicsDeviceName = "None";
        public static string processorType = "Headless CPU";
        public static int processorCount = Environment.ProcessorCount;
        public static int systemMemorySize = 8192;
        public static string deviceUniqueIdentifier = "headless-device";
        public static string operatingSystem = "Linux";
    }

    public static class QualitySettings { public static int vSyncCount; }

    public static class Screen
    {
        public static int width = 1920;
        public static int height = 1080;
        public static float dpi = 96f;
        public static Rect? SafeAreaOverride; // tests can set a notch
        public static Rect safeArea => SafeAreaOverride ?? new Rect(0, 0, width, height);
        public static bool fullScreen;
        public static ScreenOrientation orientation = ScreenOrientation.Portrait;
        public static int sleepTimeout;
    }
    public enum ScreenOrientation { Portrait, PortraitUpsideDown, LandscapeLeft, LandscapeRight, AutoRotation }
    public static class SleepTimeout { public const int NeverSleep = -1; public const int SystemSetting = -2; }

    public static class PlayerPrefs
    {
        private static readonly Dictionary<string, object> values = new Dictionary<string, object>();
        public static int GetInt(string k, int d = 0) => values.TryGetValue(k, out object v) && v is int i ? i : d;
        public static void SetInt(string k, int v) => values[k] = v;
        public static float GetFloat(string k, float d = 0f) => values.TryGetValue(k, out object v) && v is float f ? f : d;
        public static void SetFloat(string k, float v) => values[k] = v;
        public static string GetString(string k, string d = "") => values.TryGetValue(k, out object v) && v is string s ? s : d;
        public static void SetString(string k, string v) => values[k] = v;
        public static bool HasKey(string k) => values.ContainsKey(k);
        public static void DeleteKey(string k) => values.Remove(k);
        public static void DeleteAll() => values.Clear();
        public static void Save() { }
    }

    // Things put here (by tests) are what Resources.Load / LoadAll find. Path "Items/Wheat" is found by Load("Items/Wheat") and LoadAll("Items").
    public static class Resources
    {
        private static readonly Dictionary<string, Object> assets = new Dictionary<string, Object>();
        public static void Add(string path, Object asset) { assets[path] = asset; }
        public static void Remove(string path) { assets.Remove(path); }
        public static void Clear() { assets.Clear(); }
        public static T Load<T>(string path) where T : Object => assets.TryGetValue(path, out Object o) ? o as T : null;
        public static Object Load(string path) => assets.TryGetValue(path, out Object o) ? o : null;
        public static Object Load(string path, Type type) => assets.TryGetValue(path, out Object o) && type.IsInstanceOfType(o) ? o : null;
        public static T[] LoadAll<T>(string folder) where T : Object
        {
            string prefix = string.IsNullOrEmpty(folder) ? "" : folder.TrimEnd('/') + "/";
            return assets.Where(p => (prefix.Length == 0 || p.Key.StartsWith(prefix)) && p.Value is T).Select(p => (T)p.Value).ToArray();
        }
        public static void UnloadUnusedAssets() { }
    }

    // --- Attributes (they only matter to the editor) ---
    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeReference : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class HideInInspector : Attribute { }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)] public class PropertyAttribute : Attribute { }
    public sealed class HeaderAttribute : PropertyAttribute { public HeaderAttribute(string header) { } }
    public sealed class TooltipAttribute : PropertyAttribute { public TooltipAttribute(string tooltip) { } }
    public sealed class RangeAttribute : PropertyAttribute { public RangeAttribute(float min, float max) { } }
    public sealed class MinAttribute : PropertyAttribute { public MinAttribute(float min) { } }
    public sealed class SpaceAttribute : PropertyAttribute { public SpaceAttribute() { } public SpaceAttribute(float h) { } }
    public sealed class TextAreaAttribute : PropertyAttribute { public TextAreaAttribute() { } public TextAreaAttribute(int min, int max) { } }
    public sealed class MultilineAttribute : PropertyAttribute { public MultilineAttribute() { } public MultilineAttribute(int lines) { } }
    public sealed class ColorUsageAttribute : PropertyAttribute { public ColorUsageAttribute(bool alpha) { } public ColorUsageAttribute(bool alpha, bool hdr) { } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class CreateAssetMenuAttribute : Attribute { public string fileName, menuName; public int order; }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequireComponent : Attribute
    {
        public Type m_Type0, m_Type1, m_Type2;
        public RequireComponent(Type a) { m_Type0 = a; }
        public RequireComponent(Type a, Type b) { m_Type0 = a; m_Type1 = b; }
        public RequireComponent(Type a, Type b, Type c) { m_Type0 = a; m_Type1 = b; m_Type2 = c; }
    }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DisallowMultipleComponent : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class ExecuteAlways : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class ExecuteInEditMode : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class AddComponentMenu : Attribute { public AddComponentMenu(string menu) { } public AddComponentMenu(string menu, int order) { } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int order) { } }
    [AttributeUsage(AttributeTargets.Method)] public sealed class ContextMenu : Attribute { public ContextMenu(string name) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class ContextMenuItemAttribute : PropertyAttribute { public ContextMenuItemAttribute(string name, string function) { } }
    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, AfterAssembliesLoaded, BeforeSplashScreen, SubsystemRegistration }
    [AttributeUsage(AttributeTargets.Method)] public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute() { } public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } }

    // --- The frame loop ---

    // Everything that exists, and one frame at a time. Not a Unity API: tests drive the game through it
    // (World.Tick(seconds) runs one frame; World.Run(seconds) runs many).
    public static class World
    {
        public static readonly List<GameObject> All = new List<GameObject>();
        private static readonly List<Object> pendingDestroy = new List<Object>();
        private static readonly Dictionary<(Type, string), MethodInfo> methods = new Dictionary<(Type, string), MethodInfo>();

        internal static void Register(GameObject go) => All.Add(go);
        internal static int RootIndex(Transform t) => All.Where(g => g.transform.parent == null).Select(g => g.transform).ToList().IndexOf(t);

        // Every game object under (and including) this one
        public static IEnumerable<GameObject> Tree(GameObject root)
        {
            yield return root;
            foreach (Transform child in root.transform.childList.ToList())
                foreach (GameObject g in Tree(child.gameObject)) yield return g;
        }

        // A template: kept out of the world (not ticked, not found), like a prefab asset. Components on it are not woken.
        public static GameObject MakePrefab(GameObject go)
        {
            foreach (GameObject g in Tree(go)) { g.isPrefab = true; All.Remove(g); }
            return go;
        }

        internal static IEnumerable<T> FindObjects<T>(bool includeInactive) where T : Object
        {
            foreach (GameObject g in All.ToList())
            {
                if (g.destroyed || g.isPrefab || (!includeInactive && !g.activeInHierarchy)) continue;
                if (g is T asGo) { yield return asGo; continue; }
                foreach (Component c in g.components.ToList())
                {
                    if (c.destroyed || !(c is T t)) continue;
                    if (!includeInactive && c is Behaviour b && !b.enabled && !(c is Transform)) continue;
                    yield return t;
                }
            }
        }

        private static MethodInfo Method(Type type, string name, int args)
        {
            if (methods.TryGetValue((type, name + args), out MethodInfo cached)) return cached;
            MethodInfo found = null;
            for (Type t = type; t != null && t != typeof(object) && found == null; t = t.BaseType)
                found = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                         .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == args && !m.IsAbstract);
            methods[(type, name + args)] = found;
            return found;
        }

        // Sends a Unity message (Awake, Update...) if the object has one. Exceptions are logged like Unity does, not thrown.
        public static void CallMessage(object target, string name, params object[] args)
        {
            MethodInfo m = Method(target.GetType(), name, args.Length);
            if (m == null) return;
            try
            {
                object result = m.Invoke(target, args);
                if (result is IEnumerator routine && target is MonoBehaviour mb && (name == "Start")) mb.StartCoroutine(routine);
            }
            catch (TargetInvocationException e)
            {
                if (ThrowExceptions) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException ?? e).Throw();
                Debug.LogException(e.InnerException ?? e);
            }
        }

        // When true (the default in tests), an exception in a Unity message stops the test, so it is not missed
        public static bool ThrowExceptions = true;

        public static Object Instantiate(Object original, Transform parent)
        {
            if (original is GameObject go) return CloneTree(go, parent);
            if (original is Component c)
            {
                GameObject copy = CloneTree(c.gameObject, parent);
                int index = c.gameObject.components.IndexOf(c);
                return copy.components[index];
            }
            if (original is ScriptableObject so)
            {
                ScriptableObject clone = (ScriptableObject)Activator.CreateInstance(so.GetType(), true);
                CopyFields(so, clone, null);
                clone.name = so.name + "(Clone)";
                return clone;
            }
            throw new NotSupportedException("Cannot instantiate " + original);
        }

        private static GameObject CloneTree(GameObject original, Transform parent)
        {
            Dictionary<Object, Object> map = new Dictionary<Object, Object>();
            GameObject root = CloneShell(original, map);
            root.name = original.name + "(Clone)";
            foreach (KeyValuePair<Object, Object> pair in map.ToList())
                if (pair.Key is Component src && !(src is Transform)) CopyFields(src, (Component)pair.Value, map);
            if (parent != null) root.transform.SetParent(parent, false);
            foreach (GameObject g in Tree(root))
                if (g.activeInHierarchy)
                    foreach (MonoBehaviour mb in g.components.OfType<MonoBehaviour>().ToList()) { mb.EnsureAwake(); if (mb.enabled) CallMessage(mb, "OnEnable"); }
            return root;
        }

        private static GameObject CloneShell(GameObject src, Dictionary<Object, Object> map)
        {
            bool rect = src.transform is RectTransform;
            GameObject copy = new GameObject(src.name, rect ? new[] { typeof(RectTransform) } : new Type[0]);
            map[src] = copy;
            map[src.transform] = copy.transform;
            copy.tag = src.tag; copy.layer = src.layer;
            CopyTransform(src.transform, copy.transform);
            foreach (Component c in src.components)
            {
                if (c is Transform || c.destroyed) continue;
                Component nc = (Component)Activator.CreateInstance(c.GetType(), true);
                copy.Attach(nc);
                map[c] = nc;
            }
            // a clone starts inactive while its parts are put together, so nothing wakes up half made
            bool active = src.activeSelf;
            typeof(GameObject).GetField("active", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(copy, false);
            foreach (Transform child in src.transform.childList)
            {
                if (child.destroyed) continue;
                GameObject cc = CloneShell(child.gameObject, map);
                cc.transform.SetParent(copy.transform, false);
                CopyTransform(child, cc.transform);
            }
            typeof(GameObject).GetField("active", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(copy, active);
            return copy;
        }

        private static void CopyTransform(Transform from, Transform to)
        {
            to.localPosition = from.localPosition; to.localScale = from.localScale; to.localRotation = from.localRotation;
            if (from is RectTransform a && to is RectTransform b) b.CopyRect(a);
        }

        // Copies a component's fields the way Unity does: the serialized ones (public, or [SerializeField]) are copied, deeply, with
        // references to things inside the copied object pointed at their copies; everything else starts fresh. The stand-ins' own
        // classes (UnityEngine...) keep all their fields, since their properties stand for what Unity serializes. UnityEvents are
        // never shared: a copy gets its own, with the inspector (persistent) listeners only.
        private static void CopyFields(object src, object dst, Dictionary<Object, Object> map)
        {
            for (Type t = src.GetType(); t != null && t != typeof(MonoBehaviour) && t != typeof(Behaviour) && t != typeof(Component) && t != typeof(ScriptableObject) && t != typeof(Object) && t != typeof(object); t = t.BaseType)
            {
                bool engine = t.Namespace != null && (t.Namespace.StartsWith("UnityEngine") || t.Namespace.StartsWith("TMPro"));
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (f.IsNotSerialized) continue;
                    bool serialized = f.IsPublic || f.GetCustomAttribute<SerializeField>() != null;
                    if (!engine && !serialized) continue;
                    object value = f.GetValue(src);
                    if (value is UnityEngine.Events.UnityEventBase ev) { f.SetValue(dst, CloneEvent(ev)); continue; }
                    if (f.IsInitOnly && value != null && !(value is Object) && !f.FieldType.IsValueType && f.FieldType != typeof(string)) continue; // readonly collections: the copy has its own
                    try { f.SetValue(dst, CloneValue(value, map, engine)); } catch { }
                }
            }
        }

        private static object CloneEvent(UnityEngine.Events.UnityEventBase ev)
        {
            UnityEngine.Events.UnityEventBase copy = (UnityEngine.Events.UnityEventBase)Activator.CreateInstance(ev.GetType(), true);
            copy.persistent.AddRange(ev.persistent);
            return copy;
        }

        private static object CloneValue(object value, Dictionary<Object, Object> map, bool engine)
        {
            if (value == null) return null;
            if (value is Object o) return map != null && map.TryGetValue(o, out Object mapped) ? mapped : o;
            Type t = value.GetType();
            if (t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t.IsValueType) return value;
            if (value is Delegate) return engine ? value : null;
            if (value is UnityEngine.Events.UnityEventBase ev) return CloneEvent(ev);
            if (value is Array arr)
            {
                Array copy = Array.CreateInstance(t.GetElementType(), arr.Length);
                for (int i = 0; i < arr.Length; i++) copy.SetValue(CloneValue(arr.GetValue(i), map, engine), i);
                return copy;
            }
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                IList copy = (IList)Activator.CreateInstance(t);
                foreach (object item in (IList)value) copy.Add(CloneValue(item, map, engine));
                return copy;
            }
            if (t.GetCustomAttribute<SerializableAttribute>() != null && !t.IsGenericType)
            {
                object copy = Activator.CreateInstance(t, true);
                CopyFields(value, copy, map);
                return copy;
            }
            return engine ? value : null; // something Unity does not serialize: the copy does not get it
        }

        public static void Destroy(Object o)
        {
            if ((object)o == null || o.destroyed || pendingDestroy.Contains(o)) return;
            pendingDestroy.Add(o);
        }

        private static void DestroyNow(Object o)
        {
            if (o.destroyed) return;
            if (o is GameObject go)
            {
                foreach (GameObject g in Tree(go).Reverse().ToList())
                {
                    foreach (Component c in g.components.ToList())
                    {
                        if (c is MonoBehaviour mb && mb.awoken)
                        {
                            if (g.activeInHierarchy && mb.enabled) CallMessage(mb, "OnDisable");
                            CallMessage(mb, "OnDestroy");
                        }
                        c.destroyed = true;
                    }
                    g.destroyed = true;
                    All.Remove(g);
                }
                go.transform.parent?.childList.Remove(go.transform);
            }
            else if (o is Component c)
            {
                if (c is Transform) { DestroyNow(c.gameObject); return; }
                if (c is MonoBehaviour mb && mb.awoken)
                {
                    if (c.gameObject.activeInHierarchy && mb.enabled) CallMessage(mb, "OnDisable");
                    CallMessage(mb, "OnDestroy");
                }
                c.destroyed = true;
                c.gameObject?.components.Remove(c);
            }
            else o.destroyed = true;
        }

        // Ends the frame: whatever was destroyed this frame is gone now
        public static void EndFrame()
        {
            while (pendingDestroy.Count > 0)
            {
                Object o = pendingDestroy[0];
                pendingDestroy.RemoveAt(0);
                DestroyNow(o);
            }
        }

        // One frame of dt seconds: Start (once), Update, coroutines, LateUpdate, then the end of the frame
        public static void Tick(float dt)
        {
            Time.unscaledDeltaTime = dt;
            Time.deltaTime = dt * Time.timeScale;
            Time.unscaledTime += dt;
            Time.time += Time.deltaTime;
            Time.frameCount++;

            List<MonoBehaviour> live = Live();
            foreach (MonoBehaviour mb in live)
            {
                if (mb.destroyed || mb.started || !mb.isActiveAndEnabled) continue;
                mb.started = true;
                CallMessage(mb, "Start");
            }
            foreach (MonoBehaviour mb in live) if (!mb.destroyed && mb.isActiveAndEnabled) CallMessage(mb, "Update");
            foreach (MonoBehaviour mb in live)
            {
                if (mb.destroyed || !mb.gameObject.activeInHierarchy) continue;
                mb.RunInvokes();
                foreach (Coroutine c in mb.coroutines.ToList()) Step(mb, c);
            }
            foreach (MonoBehaviour mb in live) if (!mb.destroyed && mb.isActiveAndEnabled) CallMessage(mb, "LateUpdate");
            InputSystemFrame?.Invoke();
            EndFrame();
        }

        // Called at the end of every frame (the input stand-ins clear their "pressed this frame" flags)
        public static Action InputSystemFrame;

        // Several frames: seconds of game in steps of dt
        public static void Run(float seconds, float dt = 0.05f)
        {
            for (float t = 0f; t < seconds - 1e-6f; t += dt) Tick(Math.Min(dt, seconds - t));
        }

        private static List<MonoBehaviour> Live()
        {
            List<MonoBehaviour> list = new List<MonoBehaviour>();
            foreach (GameObject g in All.ToList())
            {
                if (g.destroyed || !g.activeInHierarchy) continue;
                foreach (Component c in g.components) if (c is MonoBehaviour mb && !mb.destroyed) list.Add(mb);
            }
            return list;
        }

        // Moves a coroutine on: runs it until it waits for something that has not happened yet
        internal static void Step(MonoBehaviour owner, Coroutine c)
        {
            while (!c.done && c.stack.Count > 0)
            {
                if (c.waitingFor is CustomYieldInstruction cy && cy.keepWaiting) return;
                if (c.waitingFor is Coroutine other && !other.done) return;
                if (c.waitUntil >= 0f && (c.realtime ? Time.unscaledTime : Time.time) < c.waitUntil) return;
                c.waitingFor = null; c.waitUntil = -1f;

                IEnumerator top = c.stack.Peek();
                bool more;
                try { more = top.MoveNext(); }
                catch (Exception e)
                {
                    c.done = true; owner.coroutines.Remove(c);
                    if (ThrowExceptions) throw;
                    Debug.LogException(e);
                    return;
                }
                if (!more) { c.stack.Pop(); continue; }

                object y = top.Current;
                if (y == null || y is WaitForEndOfFrame || y is WaitForFixedUpdate) return;            // next frame
                if (y is WaitForSeconds w) { c.waitUntil = Time.time + w.seconds; c.realtime = false; return; }
                if (y is CustomYieldInstruction ci) { c.waitingFor = ci; if (ci.keepWaiting) return; continue; }
                if (y is Coroutine co) { c.waitingFor = co; if (!co.done) return; continue; }
                if (y is AsyncOperation ao) { if (!ao.isDone) { c.waitingFor = new WaitUntil(() => ao.isDone); return; } continue; }
                if (y is IEnumerator nested) { c.stack.Push(nested); continue; }
                return;
            }
            if (c.stack.Count == 0) { c.done = true; owner.coroutines.Remove(c); }
        }

        // Back to nothing: every object gone, the clock at zero (between test files)
        public static void Reset()
        {
            foreach (GameObject g in All.ToList()) Destroy(g);
            EndFrame();
            All.Clear();
            Time.time = Time.unscaledTime = 0f; Time.timeScale = 1f; Time.frameCount = 0;
        }
    }
}
