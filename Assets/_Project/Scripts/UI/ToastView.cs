using System.Collections.Generic;
using UnityEngine;

// The toasts: banners stacked down from the top of the screen. A new one slides in on top and pushes the others down; they fade away by
// themselves. Tap one to do what it is about (see Notify / NotifyActions), tap its X or swipe it sideways to dismiss it. What is shown
// comes from Notify.Center. Created by UIManager.
public class ToastView : MonoBehaviour
{
    public const float Spacing = 8f;

    private RectTransform area;
    private GameObject fontSource;
    private readonly Dictionary<int, ToastItem> items = new Dictionary<int, ToastItem>();
    private readonly List<ToastItem> leaving = new List<ToastItem>();

    public RectTransform Area => area;

    // The toasts on screen, newest (top) first
    public List<ToastItem> ActiveItems
    {
        get
        {
            List<ToastItem> list = new List<ToastItem>();
            foreach (Notification note in Notify.Center.Visible) if (items.TryGetValue(note.Id, out ToastItem item)) list.Add(item);
            return list;
        }
    }

    public int LeavingCount => leaving.Count;

    public static ToastView Create(Transform canvas, GameObject fontSource)
    {
        GameObject go = new GameObject("ToastArea", typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        ToastView view = go.AddComponent<ToastView>();
        view.fontSource = fontSource;
        view.area = go.GetComponent<RectTransform>();
        view.area.anchorMin = new Vector2(0.5f, 1f);
        view.area.anchorMax = new Vector2(0.5f, 1f);
        view.area.pivot = new Vector2(0.5f, 1f);
        view.area.anchoredPosition = new Vector2(0f, -24f);
        view.area.sizeDelta = new Vector2(520f, 10f);
        Notify.Center.Reset(); // a new game session starts with nothing on screen
        return view;
    }

    private void Update()
    {
        Notify.Center.Tick();
        Sync();

        float y = 0f;
        foreach (ToastItem item in ActiveItems)
        {
            item.TargetY = -y;
            y += item.Height + Spacing;
        }

        float dt = Time.unscaledDeltaTime;
        foreach (ToastItem item in items.Values) item.Animate(dt);
        for (int i = leaving.Count - 1; i >= 0; i--)
        {
            ToastItem item = leaving[i];
            item.Animate(dt);
            if (item.Gone)
            {
                leaving.RemoveAt(i);
                item.gameObject.SetActive(false);
                Destroy(item.gameObject);
            }
        }
    }

    // Makes a banner for each new message and sends away those that are gone from the list
    private void Sync()
    {
        foreach (Notification note in Notify.Center.Visible)
        {
            if (items.ContainsKey(note.Id)) { items[note.Id].Refresh(); continue; }
            items[note.Id] = ToastItem.Create(this, note, fontSource);
            transform.SetAsLastSibling(); // above the menus
        }

        List<int> gone = null;
        foreach (KeyValuePair<int, ToastItem> pair in items)
        {
            if (pair.Value.Note.Visible) continue;
            if (gone == null) gone = new List<int>();
            gone.Add(pair.Key);
        }
        if (gone == null) return;
        foreach (int id in gone)
        {
            ToastItem item = items[id];
            items.Remove(id);
            item.BeginLeave();
            leaving.Add(item);
        }
    }
}
