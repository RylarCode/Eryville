using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One banner: the message (with an icon if it has one), an X, and the touch handling: tap = do its action, swipe = dismiss.
public class ToastItem : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public const float EnterSeconds = 0.2f;
    public const float LeaveSeconds = 0.22f;
    public const float SwipeShare = 0.35f; // how far (of the banner's width) a swipe must go to dismiss
    public const float TapSlop = 10f;      // a touch that moved more than this is not a tap

    public Notification Note { get; private set; }
    public float Width { get; private set; }
    public float Height { get; private set; }
    public float Y { get; private set; }
    public float TargetY;
    public float DragX { get; private set; }
    public bool Gone { get; private set; }
    public bool Leaving { get; private set; }
    public string ShownText => label != null ? label.text : "";

    private RectTransform rect;
    private CanvasGroup group;
    private TMP_Text label;
    private Image background;
    private Image icon;
    private bool dragging;
    private float moved;
    private float leaveDirection;
    private float leaveTime;
    private float enterTime;

    private static readonly Color InfoColor = new Color(0.14f, 0.18f, 0.26f, 0.96f);
    private static readonly Color SuccessColor = new Color(0.12f, 0.36f, 0.22f, 0.96f);
    private static readonly Color WarningColor = new Color(0.55f, 0.4f, 0.08f, 0.97f);
    private static readonly Color AlertColor = new Color(0.55f, 0.14f, 0.14f, 0.97f);

    public static Color ColorOf(NotifyKind kind)
    {
        switch (kind)
        {
            case NotifyKind.Success: return SuccessColor;
            case NotifyKind.Warning: return WarningColor;
            case NotifyKind.Alert: return AlertColor;
            default: return InfoColor;
        }
    }

    public static ToastItem Create(ToastView view, Notification note, GameObject fontSource)
    {
        float scale = MenuPanels.Scale;
        bool phone = UILayout.Instance != null && UILayout.Instance.IsPortrait;

        GameObject go = new GameObject("Toast" + note.Id, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        go.transform.SetParent(view.Area, false);
        ToastItem item = go.AddComponent<ToastItem>();
        item.Note = note;
        item.Width = phone ? 1000f : 520f;
        item.Height = (phone ? 56f : 52f) * (phone ? 1.6f : 1f);
        item.rect = go.GetComponent<RectTransform>();
        item.rect.anchorMin = new Vector2(0.5f, 1f);
        item.rect.anchorMax = new Vector2(0.5f, 1f);
        item.rect.pivot = new Vector2(0.5f, 1f);
        item.rect.sizeDelta = new Vector2(item.Width, item.Height);
        item.group = go.GetComponent<CanvasGroup>();
        item.group.alpha = 0f;
        item.background = go.GetComponent<Image>();
        item.background.color = ColorOf(note.Kind);

        float inset = 12f;
        float closeSize = item.Height * 0.7f;
        if (note.Icon != null)
        {
            GameObject picture = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            picture.transform.SetParent(go.transform, false);
            RectTransform pictureRect = picture.GetComponent<RectTransform>();
            pictureRect.anchorMin = new Vector2(0f, 0.5f);
            pictureRect.anchorMax = new Vector2(0f, 0.5f);
            pictureRect.pivot = new Vector2(0f, 0.5f);
            pictureRect.anchoredPosition = new Vector2(inset, 0f);
            pictureRect.sizeDelta = new Vector2(item.Height * 0.72f, item.Height * 0.72f);
            item.icon = picture.GetComponent<Image>();
            item.icon.sprite = note.Icon;
            item.icon.preserveAspect = true;
            item.icon.raycastTarget = false;
            inset += item.Height * 0.72f + 10f;
        }

        item.label = MenuPanels.MakeLabel(fontSource, go.transform, "Label", 9f * scale, 17f * scale * (phone ? 1.4f : 1f), TextAlignmentOptions.Left);
        item.label.color = Color.white;
        RectTransform labelRect = item.label.GetComponent<RectTransform>();
        labelRect.offsetMin = new Vector2(inset, 4f);
        labelRect.offsetMax = new Vector2(-(closeSize + 20f), -4f);

        GameObject close = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
        close.transform.SetParent(go.transform, false);
        RectTransform closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 0.5f);
        closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.anchoredPosition = new Vector2(-10f, 0f);
        closeRect.sizeDelta = new Vector2(closeSize, closeSize);
        Image closeImage = close.GetComponent<Image>();
        closeImage.color = new Color(0f, 0f, 0f, 0.3f);
        Button closeButton = close.GetComponent<Button>();
        closeButton.targetGraphic = closeImage;
        closeButton.onClick.AddListener(item.Close);
        TMP_Text x = MenuPanels.MakeLabel(fontSource, close.transform, "X", 9f * scale, 20f * scale, TextAlignmentOptions.Center);
        x.text = "X";
        x.color = Color.white;

        item.Refresh();
        item.Y = item.Height; // starts just above and slides down into place
        item.TargetY = 0f;
        item.ApplyPosition();
        return item;
    }

    // Brings the text up to date (a merged repeat counts up)
    public void Refresh()
    {
        if (label != null && label.text != Note.Display) label.text = Note.Display;
        if (background != null) background.color = ColorOf(Note.Kind);
    }

    // --- Touch ---

    public void OnPointerClick(PointerEventData eventData)
    {
        Tap();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragging = true;
        Notify.Center.Hold(Note, true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        DragBy(eventData.delta.x / scale);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        EndDrag();
    }

    // Tapping the banner: it does what it is about (and goes away). Not when the touch was really a drag.
    public void Tap()
    {
        if (Leaving || moved > TapSlop) return;
        Notify.Center.Tap(Note);
    }

    public void Close()
    {
        if (Leaving) return;
        Notify.Center.Dismiss(Note);
    }

    // A sideways drag moves the banner with the finger (it stays on screen while touched)
    public void DragBy(float dx)
    {
        if (Leaving) return;
        dragging = true;
        Notify.Center.Hold(Note, true);
        DragX += dx;
        moved += Mathf.Abs(dx);
    }

    // Let go: far enough sideways and it is dismissed, otherwise it springs back
    public void EndDrag()
    {
        dragging = false;
        Notify.Center.Hold(Note, false);
        if (Mathf.Abs(DragX) >= Width * SwipeShare) Notify.Center.Dismiss(Note);
        else moved = 0f;
    }

    // --- Movement ---

    public void BeginLeave()
    {
        if (Leaving) return;
        Leaving = true;
        leaveTime = 0f;
        leaveDirection = DragX > 1f ? 1f : DragX < -1f ? -1f : 0f; // a swipe carries on the way it went; otherwise it fades
        dragging = false;
    }

    public void Animate(float dt)
    {
        Y = Mathf.Lerp(Y, TargetY, Mathf.Min(1f, 14f * dt));
        if (Mathf.Abs(Y - TargetY) < 0.2f) Y = TargetY;

        if (Leaving)
        {
            leaveTime += dt;
            float t = Mathf.Min(1f, leaveTime / LeaveSeconds);
            group.alpha = 1f - t;
            DragX += leaveDirection * Width * 2f * dt / LeaveSeconds * 0.6f;
            if (t >= 1f) Gone = true;
        }
        else
        {
            enterTime += dt;
            group.alpha = Mathf.Min(1f, enterTime / EnterSeconds);
            if (!dragging && DragX != 0f)
            {
                DragX = Mathf.Lerp(DragX, 0f, Mathf.Min(1f, 18f * dt));
                if (Mathf.Abs(DragX) < 0.5f) DragX = 0f;
            }
        }
        ApplyPosition();
    }

    private void ApplyPosition()
    {
        rect.anchoredPosition = new Vector2(DragX, Y);
    }
}
