// Headless stand-ins for the UI: RectTransform, uGUI (images, buttons, layout groups, scroll views), TextMeshPro,
// the event system, and the Input System (keyboard, mouse, touch, input actions).
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine
{
    public class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; } = new Vector2(0.5f, 0.5f);
        public Vector2 anchorMax { get; set; } = new Vector2(0.5f, 0.5f);
        public Vector2 pivot { get; set; } = new Vector2(0.5f, 0.5f);
        public Vector2 anchoredPosition { get; set; }
        public Vector2 sizeDelta { get; set; } = new Vector2(100f, 100f);
        public Vector3 anchoredPosition3D { get => new Vector3(anchoredPosition.x, anchoredPosition.y, localPosition.z); set => anchoredPosition = value; }

        // offsetMin / offsetMax: the lower-left and upper-right corners relative to the anchors (as in Unity)
        public Vector2 offsetMin
        {
            get => anchoredPosition - Vector2.Scale(sizeDelta, pivot);
            set { Vector2 d = value - offsetMin; sizeDelta -= d; anchoredPosition += Vector2.Scale(d, Vector2.one - pivot); }
        }
        public Vector2 offsetMax
        {
            get => anchoredPosition + Vector2.Scale(sizeDelta, Vector2.one - pivot);
            set { Vector2 d = value - offsetMax; sizeDelta += d; anchoredPosition += Vector2.Scale(d, pivot); }
        }

        // The rectangle in its own space, worked out from the parent's size (the root is the screen)
        public Rect rect
        {
            get
            {
                Vector2 parentSize = parent is RectTransform p ? p.rect.size : new Vector2(Screen.width, Screen.height);
                Vector2 size = sizeDelta + Vector2.Scale(anchorMax - anchorMin, parentSize);
                return new Rect(-pivot.x * size.x, -pivot.y * size.y, size.x, size.y);
            }
        }

        public void SetSizeWithCurrentAnchors(Axis axis, float size)
        {
            Vector2 parentSize = parent is RectTransform p ? p.rect.size : new Vector2(Screen.width, Screen.height);
            Vector2 s = sizeDelta;
            if (axis == Axis.Horizontal) s.x = size - (anchorMax.x - anchorMin.x) * parentSize.x; else s.y = size - (anchorMax.y - anchorMin.y) * parentSize.y;
            sizeDelta = s;
        }
        public void SetInsetAndSizeFromParentEdge(Edge edge, float inset, float size) { }
        public void GetWorldCorners(Vector3[] corners)
        {
            Rect r = rect; Vector3 p = position;
            corners[0] = p + new Vector3(r.xMin, r.yMin); corners[1] = p + new Vector3(r.xMin, r.yMax);
            corners[2] = p + new Vector3(r.xMax, r.yMax); corners[3] = p + new Vector3(r.xMax, r.yMin);
        }
        public void ForceUpdateRectTransforms() { }
        public enum Axis { Horizontal, Vertical }
        public enum Edge { Left, Right, Top, Bottom }

        internal void CopyRect(RectTransform o)
        {
            anchorMin = o.anchorMin; anchorMax = o.anchorMax; pivot = o.pivot; anchoredPosition = o.anchoredPosition; sizeDelta = o.sizeDelta;
        }
    }

    public static class RectTransformUtility
    {
        public static bool RectangleContainsScreenPoint(RectTransform rect, Vector2 screenPoint) => false;
        public static bool RectangleContainsScreenPoint(RectTransform rect, Vector2 screenPoint, Camera cam) => false;
        public static bool ScreenPointToLocalPointInRectangle(RectTransform rect, Vector2 screenPoint, Camera cam, out Vector2 local) { local = screenPoint; return true; }
    }
}

namespace UnityEngine.EventSystems
{
    public class UIBehaviour : MonoBehaviour
    {
        public virtual bool IsActive() => isActiveAndEnabled;
    }

    public class BaseEventData { public BaseEventData(EventSystem es) { } public GameObject selectedObject; public void Use() { } }

    public class PointerEventData : BaseEventData
    {
        public enum InputButton { Left, Right, Middle }
        public Vector2 position, delta, pressPosition;
        public InputButton button;
        public int pointerId, clickCount;
        public bool dragging, eligibleForClick;
        public GameObject pointerPress, pointerDrag, pointerEnter, rawPointerPress;
        public RaycastResult pointerCurrentRaycast, pointerPressRaycast;
        public PointerEventData(EventSystem es) : base(es) { }
    }

    public struct RaycastResult { public GameObject gameObject; public float distance; public int depth; public Vector2 screenPosition; }

    public interface IEventSystemHandler { }
    public interface IPointerClickHandler : IEventSystemHandler { void OnPointerClick(PointerEventData e); }
    public interface IPointerDownHandler : IEventSystemHandler { void OnPointerDown(PointerEventData e); }
    public interface IPointerUpHandler : IEventSystemHandler { void OnPointerUp(PointerEventData e); }
    public interface IPointerEnterHandler : IEventSystemHandler { void OnPointerEnter(PointerEventData e); }
    public interface IPointerExitHandler : IEventSystemHandler { void OnPointerExit(PointerEventData e); }
    public interface IBeginDragHandler : IEventSystemHandler { void OnBeginDrag(PointerEventData e); }
    public interface IDragHandler : IEventSystemHandler { void OnDrag(PointerEventData e); }
    public interface IEndDragHandler : IEventSystemHandler { void OnEndDrag(PointerEventData e); }
    public interface IScrollHandler : IEventSystemHandler { void OnScroll(PointerEventData e); }
    public interface IInitializePotentialDragHandler : IEventSystemHandler { void OnInitializePotentialDrag(PointerEventData e); }

    public class BaseInputModule : UIBehaviour { }
    public class StandaloneInputModule : BaseInputModule { }
    public class BaseRaycaster : UIBehaviour { }

    public class EventSystem : UIBehaviour
    {
        private static EventSystem explicitCurrent;
        public static EventSystem current
        {
            get => explicitCurrent != null ? explicitCurrent : World.All.Where(g => !g.destroyed && g.activeInHierarchy).Select(g => g.GetComponent<EventSystem>()).FirstOrDefault(e => e != null && e.enabled);
            set => explicitCurrent = value;
        }

        // Tests say whether the pointer is over the UI: OverUI for everywhere, OverAt for a given screen point
        public static bool OverUI;
        public static Func<Vector2, bool> OverAt;
        public GameObject currentSelectedGameObject { get; private set; }
        public bool sendNavigationEvents { get; set; } = true;
        public int pixelDragThreshold { get; set; } = 10;
        public bool IsPointerOverGameObject() => OverUI;
        public bool IsPointerOverGameObject(int pointerId) => OverUI;
        public void SetSelectedGameObject(GameObject go) { currentSelectedGameObject = go; }
        public void RaycastAll(PointerEventData data, List<RaycastResult> results)
        {
            bool over = OverAt != null ? OverAt(data.position) : OverUI;
            if (over) results.Add(new RaycastResult());
        }
    }

    public static class ExecuteEvents
    {
        public static bool Execute<T>(GameObject target, BaseEventData data, Action<T, BaseEventData> functor) where T : IEventSystemHandler
        {
            bool any = false;
            foreach (T handler in target.GetComponents<T>()) { functor(handler, data); any = true; }
            return any;
        }
    }
}

namespace UnityEngine.UI
{
    using UnityEngine.EventSystems;
    using UnityEngine.Events;

    public class Graphic : UIBehaviour
    {
        public virtual Color color { get; set; } = Color.white;
        public bool raycastTarget { get; set; } = true;
        public virtual Material material { get; set; }
        public Canvas canvas => GetComponentInParent<Canvas>();
        public RectTransform rectTransform => transform as RectTransform;
        public CanvasRenderer canvasRenderer => GetComponent<CanvasRenderer>();
        public void SetAllDirty() { }
        public void SetVerticesDirty() { }
        public void SetLayoutDirty() { }
        public void CrossFadeAlpha(float alpha, float duration, bool ignoreTimeScale) { }
        public void CrossFadeColor(Color c, float duration, bool ignoreTimeScale, bool useAlpha) { }
    }

    public class MaskableGraphic : Graphic { public bool maskable { get; set; } = true; }

    public class Image : MaskableGraphic
    {
        public enum Type { Simple, Sliced, Tiled, Filled }
        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }
        public Sprite sprite { get; set; }
        public Sprite overrideSprite { get => sprite; set => sprite = value; }
        public Type type { get; set; }
        public bool preserveAspect { get; set; }
        public float fillAmount { get; set; } = 1f;
        public FillMethod fillMethod { get; set; }
        public int fillOrigin { get; set; }
        public bool fillCenter { get; set; } = true;
        public float pixelsPerUnitMultiplier { get; set; } = 1f;
        public void SetNativeSize() { }
    }

    public class RawImage : MaskableGraphic { public Texture texture { get; set; } public Rect uvRect { get; set; } }
    public class Text : MaskableGraphic { public string text { get; set; } = ""; public int fontSize { get; set; } = 14; public TextAnchor alignment { get; set; } public Font font { get; set; } }
    public class Font : Object { }

    public struct ColorBlock
    {
        public Color normalColor, highlightedColor, pressedColor, selectedColor, disabledColor;
        public float colorMultiplier, fadeDuration;
        public static ColorBlock defaultColorBlock => new ColorBlock { normalColor = Color.white, highlightedColor = Color.white, pressedColor = Color.white, selectedColor = Color.white, disabledColor = Color.gray, colorMultiplier = 1, fadeDuration = 0.1f };
    }

    public class Selectable : UIBehaviour
    {
        public enum Transition { None, ColorTint, SpriteSwap, Animation }
        public bool interactable { get; set; } = true;
        public Graphic targetGraphic { get; set; }
        public Transition transition { get; set; } = Transition.ColorTint;
        public ColorBlock colors { get; set; } = ColorBlock.defaultColorBlock;
        public Image image { get => targetGraphic as Image; set => targetGraphic = value; }
        public bool IsInteractable() => interactable;
        public void Select() { }
    }

    public class Button : Selectable, IPointerClickHandler
    {
        public class ButtonClickedEvent : UnityEvent { }
        public ButtonClickedEvent onClick { get; set; } = new ButtonClickedEvent();
        public void OnPointerClick(PointerEventData e) { if (interactable && isActiveAndEnabled) onClick.Invoke(); }
    }

    public class Toggle : Selectable
    {
        public class ToggleEvent : UnityEvent<bool> { }
        private bool on;
        public bool isOn { get => on; set { if (on == value) return; on = value; onValueChanged.Invoke(value); } }
        public ToggleEvent onValueChanged { get; set; } = new ToggleEvent();
        public void SetIsOnWithoutNotify(bool v) { on = v; }
    }

    public class Slider : Selectable
    {
        public class SliderEvent : UnityEvent<float> { }
        private float v;
        public float minValue { get; set; }
        public float maxValue { get; set; } = 1f;
        public float value { get => v; set { float n = Mathf.Clamp(value, minValue, maxValue); if (n == v) return; v = n; onValueChanged.Invoke(n); } }
        public SliderEvent onValueChanged { get; set; } = new SliderEvent();
    }

    public class Scrollbar : Selectable { public float value { get; set; } public float size { get; set; } }

    public class ScrollRect : UIBehaviour
    {
        public enum MovementType { Unrestricted, Elastic, Clamped }
        public enum ScrollbarVisibility { Permanent, AutoHide, AutoHideAndExpandViewport }
        public RectTransform content { get; set; }
        public RectTransform viewport { get; set; }
        public bool horizontal { get; set; } = true;
        public bool vertical { get; set; } = true;
        public MovementType movementType { get; set; } = MovementType.Elastic;
        public float elasticity { get; set; } = 0.1f;
        public bool inertia { get; set; } = true;
        public float decelerationRate { get; set; } = 0.135f;
        public float scrollSensitivity { get; set; } = 1f;
        public Scrollbar horizontalScrollbar { get; set; }
        public Scrollbar verticalScrollbar { get; set; }
        public ScrollbarVisibility verticalScrollbarVisibility { get; set; }
        public ScrollbarVisibility horizontalScrollbarVisibility { get; set; }
        public float verticalNormalizedPosition { get; set; } = 1f;
        public float horizontalNormalizedPosition { get; set; }
        public Vector2 normalizedPosition { get; set; }
        public Vector2 velocity { get; set; }
        public void StopMovement() { velocity = Vector2.zero; }
    }

    public class Mask : UIBehaviour { public bool showMaskGraphic { get; set; } = true; }
    public class RectMask2D : UIBehaviour { public Vector4 padding { get; set; } public Vector2Int softness { get; set; } }

    public interface ILayoutElement { }
    public interface ILayoutController { }

    public class LayoutElement : UIBehaviour, ILayoutElement
    {
        public bool ignoreLayout { get; set; }
        public float minWidth { get; set; } = -1f;
        public float minHeight { get; set; } = -1f;
        public float preferredWidth { get; set; } = -1f;
        public float preferredHeight { get; set; } = -1f;
        public float flexibleWidth { get; set; } = -1f;
        public float flexibleHeight { get; set; } = -1f;
        public int layoutPriority { get; set; } = 1;
    }

    public abstract class LayoutGroup : UIBehaviour, ILayoutController
    {
        public RectOffset padding { get; set; } = new RectOffset();
        public TextAnchor childAlignment { get; set; }
    }

    public abstract class HorizontalOrVerticalLayoutGroup : LayoutGroup
    {
        public float spacing { get; set; }
        public bool childForceExpandWidth { get; set; } = true;
        public bool childForceExpandHeight { get; set; } = true;
        public bool childControlWidth { get; set; } = true;
        public bool childControlHeight { get; set; } = true;
        public bool childScaleWidth { get; set; }
        public bool childScaleHeight { get; set; }
        public bool reverseArrangement { get; set; }
    }
    public class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup { }

    public class GridLayoutGroup : LayoutGroup
    {
        public enum Corner { UpperLeft, UpperRight, LowerLeft, LowerRight }
        public enum Axis { Horizontal, Vertical }
        public enum Constraint { Flexible, FixedColumnCount, FixedRowCount }
        public Corner startCorner { get; set; }
        public Axis startAxis { get; set; }
        public Vector2 cellSize { get; set; } = new Vector2(100, 100);
        public Vector2 spacing { get; set; }
        public Constraint constraint { get; set; }
        public int constraintCount { get; set; } = 2;
    }

    public class ContentSizeFitter : UIBehaviour, ILayoutController
    {
        public enum FitMode { Unconstrained, MinSize, PreferredSize }
        public FitMode horizontalFit { get; set; }
        public FitMode verticalFit { get; set; }
    }

    public class AspectRatioFitter : UIBehaviour { public enum AspectMode { None, WidthControlsHeight, HeightControlsWidth, FitInParent, EnvelopeParent } public AspectMode aspectMode { get; set; } public float aspectRatio { get; set; } = 1f; }

    public class CanvasScaler : UIBehaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }
        public ScaleMode uiScaleMode { get; set; }
        public Vector2 referenceResolution { get; set; } = new Vector2(800, 600);
        public ScreenMatchMode screenMatchMode { get; set; }
        public float matchWidthOrHeight { get; set; }
        public float scaleFactor { get; set; } = 1f;
        public float referencePixelsPerUnit { get; set; } = 100f;
    }

    public class GraphicRaycaster : BaseRaycaster { public bool ignoreReversedGraphics { get; set; } = true; }

    public static class LayoutRebuilder
    {
        public static void ForceRebuildLayoutImmediate(RectTransform layoutRoot) { }
        public static void MarkLayoutForRebuild(RectTransform rect) { }
    }

    public class Outline : UIBehaviour { public Color effectColor { get; set; } public Vector2 effectDistance { get; set; } }
    public class Shadow : UIBehaviour { public Color effectColor { get; set; } public Vector2 effectDistance { get; set; } }
}

namespace UnityEngine
{
    public class RectOffset
    {
        public int left, right, top, bottom;
        public RectOffset() { }
        public RectOffset(int left, int right, int top, int bottom) { this.left = left; this.right = right; this.top = top; this.bottom = bottom; }
        public int horizontal => left + right;
        public int vertical => top + bottom;
    }
}

namespace TMPro
{
    using UnityEngine;
    using UnityEngine.UI;

    public enum TextAlignmentOptions
    {
        TopLeft = 257, Top = 258, TopRight = 260, TopJustified = 264,
        Left = 513, Center = 514, Right = 516, Justified = 520,
        BottomLeft = 1025, Bottom = 1026, BottomRight = 1028,
        BaselineLeft = 2049, Baseline = 2050, BaselineRight = 2052,
        MidlineLeft = 4097, Midline = 4098, MidlineRight = 4100,
        CaplineLeft = 8193, Capline = 8194, CaplineRight = 8196,
        Converted = 65535
    }
    public enum TextOverflowModes { Overflow, Ellipsis, Masking, Truncate, ScrollRect, Page, Linked }
    public enum TextWrappingModes { NoWrap, Normal, PreserveWhitespace, PreserveWhitespaceNoWrap }
    [Flags] public enum FontStyles { Normal = 0, Bold = 1, Italic = 2, Underline = 4, LowerCase = 8, UpperCase = 16, SmallCaps = 32, Strikethrough = 64 }

    public class TMP_FontAsset : ScriptableObject { }
    public class TMP_SpriteAsset : ScriptableObject { }

    public class TMP_Text : MaskableGraphic
    {
        private string value = "";
        public virtual string text { get => value; set => this.value = value ?? ""; }
        public TMP_FontAsset font { get; set; }
        public float fontSize { get; set; } = 36f;
        public float fontSizeMin { get; set; } = 18f;
        public float fontSizeMax { get; set; } = 72f;
        public bool enableAutoSizing { get; set; }
        public TextAlignmentOptions alignment { get; set; } = TextAlignmentOptions.TopLeft;
        public FontStyles fontStyle { get; set; }
        public bool enableWordWrapping { get; set; } = true;
        public TextWrappingModes textWrappingMode { get; set; } = TextWrappingModes.Normal;
        public TextOverflowModes overflowMode { get; set; }
        public bool richText { get; set; } = true;
        public int maxVisibleCharacters { get; set; } = 99999;
        public int maxVisibleLines { get; set; } = 99999;
        public float characterSpacing { get; set; }
        public float lineSpacing { get; set; }
        public Vector4 margin { get; set; }
        public Color faceColor { get; set; } = Color.white;
        public Color outlineColor { get; set; }
        public float outlineWidth { get; set; }
        public float preferredWidth => value.Length * fontSize * 0.5f;
        public float preferredHeight => fontSize * 1.2f * (1 + value.Count(c => c == '\n'));
        public int textInfoCharacterCount => GetParsedText().Length;
        public string GetParsedText() => System.Text.RegularExpressions.Regex.Replace(value, "<[^>]*>", "");
        public void SetText(string t) { text = t; }
        public void SetText(System.Text.StringBuilder t) { text = t.ToString(); }
        public void ForceMeshUpdate() { }
        public void ForceMeshUpdate(bool ignoreActiveState, bool forceTextReparsing) { }
        public Vector2 GetPreferredValues(string t) => new Vector2(t.Length * fontSize * 0.5f, fontSize * 1.2f);
    }

    public class TextMeshProUGUI : TMP_Text { }
    public class TextMeshPro : TMP_Text { public int sortingOrder { get; set; } public MeshRenderer renderer => GetComponent<MeshRenderer>(); }
}

namespace UnityEngine
{
    public class MeshRenderer : Renderer { }
    public class MeshFilter : Component { }
}
