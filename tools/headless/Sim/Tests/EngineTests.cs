using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The stand-ins themselves behave like Unity where the game relies on it (so a failure elsewhere is the game's, not the harness's)
public static class EngineTests
{
    private class Probe : MonoBehaviour
    {
        public List<string> calls = new List<string>();
        public int steps;
        private void Awake() => calls.Add("Awake");
        private void OnEnable() => calls.Add("OnEnable");
        private void Start() => calls.Add("Start");
        private void Update() => calls.Add("Update");
        private void OnDisable() => calls.Add("OnDisable");
        private void OnDestroy() => calls.Add("OnDestroy");
        public IEnumerator Routine() { steps = 1; yield return null; steps = 2; yield return new WaitForSeconds(1f); steps = 3; }
    }

    [System.Serializable] public class Saved { public int a; public string s; public List<int> l = new List<int>(); public Vector3 v; public long big; public Inner inner = new Inner(); [SerializeField] private float hidden = 2f; public float Hidden => hidden; }
    [System.Serializable] public class Inner { public bool flag; public List<string> names = new List<string>(); }

    public static void Run()
    {
        T.Section("lifecycle");
        GameObject go = new GameObject("Probe");
        Probe p = go.AddComponent<Probe>();
        T.Check(string.Join(",", p.calls) == "Awake,OnEnable", "AddComponent wakes and enables at once: " + string.Join(",", p.calls));
        World.Tick(0.1f);
        T.Check(string.Join(",", p.calls) == "Awake,OnEnable,Start,Update", "the first frame starts then updates");
        go.SetActive(false); World.Tick(0.1f);
        T.Check(p.calls[p.calls.Count - 1] == "OnDisable", "an inactive object is disabled and not updated");
        go.SetActive(true);
        T.Check(p.calls[p.calls.Count - 1] == "OnEnable", "...and enabled again");
        Object.Destroy(go);
        T.Check(go != null, "destroyed objects stay until the end of the frame");
        World.EndFrame();
        T.Check(go == null && p == null && p.calls.Contains("OnDestroy"), "then they are gone (== null) and told");

        T.Section("coroutines");
        Probe q = new GameObject("Q").AddComponent<Probe>();
        q.StartCoroutine(q.Routine());
        T.Check(q.steps == 1, "a coroutine runs to its first yield at once");
        World.Tick(0.1f); T.Check(q.steps == 2, "yield null waits a frame");
        World.Run(0.5f); T.Check(q.steps == 2, "WaitForSeconds waits");
        World.Run(0.6f); T.Check(q.steps == 3, "...and goes on");

        T.Section("hierarchy and prefabs");
        GameObject parent = new GameObject("Parent");
        GameObject child = new GameObject("Child");
        child.transform.SetParent(parent.transform);
        parent.transform.position = new Vector3(5, 0, 0);
        T.Check(child.transform.position == new Vector3(5, 0, 0) && parent.transform.Find("Child") == child.transform, "children follow their parent and are found by name");
        parent.SetActive(false);
        T.Check(!child.activeInHierarchy && child.activeSelf, "a child of an inactive parent is inactive in the hierarchy");
        GameObject prefab = World.MakePrefab(new GameObject("Prefab", typeof(SpriteRenderer), typeof(BoxCollider2D)));
        Probe pp = prefab.AddComponent<Probe>();
        T.Check(pp.calls.Count == 0, "components on a prefab do not wake");
        GameObject copy = Object.Instantiate(prefab, new Vector3(1, 2, 0), Quaternion.identity);
        T.Check(copy.name == "Prefab(Clone)" && copy.transform.position == new Vector3(1, 2, 0) && copy.GetComponent<Probe>() != null && copy.GetComponent<Probe>().calls.Contains("Awake") && copy.GetComponent<Probe>() != pp, "Instantiate copies a prefab into the world and wakes the copy");

        T.Section("physics and camera");
        Camera cam = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" }.GetComponent<Camera>();
        T.Check(Camera.main == cam, "Camera.main is the camera tagged MainCamera");
        Vector3 world = new Vector3(2.5f, -1.5f, 0);
        Vector3 back = cam.ScreenToWorldPoint(cam.WorldToScreenPoint(world));
        T.Check(T.Near(back.x, world.x) && T.Near(back.y, world.y), "screen <-> world round trip");
        GameObject box = new GameObject("Box", typeof(BoxCollider2D));
        box.transform.position = new Vector3(10, 10, 0);
        box.GetComponent<BoxCollider2D>().size = new Vector2(2, 2);
        T.Check(Physics2D.RaycastAll(new Vector2(10.9f, 9.1f), Vector2.zero).Length == 1 && Physics2D.RaycastAll(new Vector2(11.1f, 10f), Vector2.zero).Length == 0, "a point is inside a box collider or not");

        T.Section("JsonUtility");
        Saved s = new Saved { a = 3, s = "hi \"there\"", v = new Vector3(1, 2, 3), big = 638000000000000000L };
        s.l.Add(7); s.inner.flag = true; s.inner.names.Add("x");
        string json = JsonUtility.ToJson(s, true);
        Saved r = JsonUtility.FromJson<Saved>(json);
        T.Check(r.a == 3 && r.s == "hi \"there\"" && r.l.Count == 1 && r.l[0] == 7 && r.v == new Vector3(1, 2, 3) && r.big == 638000000000000000L && r.inner.flag && r.inner.names[0] == "x" && r.Hidden == 2f, "fields, lists, vectors, longs, nested classes and [SerializeField] survive a round trip");
        T.Check(json.Contains("\"hidden\"") && !json.Contains("Hidden"), "private [SerializeField] fields are written, properties are not");
        bool threw = false; try { JsonUtility.FromJson<Saved>("not json"); } catch { threw = true; }
        T.Check(threw, "garbage is refused");
    }
}
