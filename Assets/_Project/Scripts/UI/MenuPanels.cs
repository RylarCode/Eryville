using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Builds the pieces of the menus at runtime so they all look the same and nothing overlaps:
//  - panels in the details panel's slot (Town, Recruit), with a header of buttons that share the width
//    (so they fit whatever the panel width is) and a scrolling list below it,
//  - list rows (an icon on the left, a text that shrinks to fit) and cards (an icon above a caption)
//    that replace the old button prefab, whose caption sat on top of its picture.
public static class MenuPanels
{
    public const float HeaderRowHeight = 34f;
    public const float HeaderSpacing = 6f;
    public const float RowHeight = 44f;

    private static readonly Color RowColor = new Color(0.96f, 0.96f, 0.96f, 1f);
    private static readonly Color InfoColor = new Color(0.80f, 0.86f, 0.93f, 1f);
    private static readonly Color TextColor = new Color(0.08f, 0.08f, 0.1f, 1f);
    private static readonly Color DisabledTextColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    // --- Panels ---

    // A new panel in the same place, size and look as the details panel (and the same spot in the draw order)
    public static GameObject CreatePanel(string name, GameObject detailsPanel)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(detailsPanel.transform.parent, false);
        panel.transform.SetSiblingIndex(detailsPanel.transform.GetSiblingIndex());
        RectTransform rect = panel.GetComponent<RectTransform>();
        RectTransform detailsRect = detailsPanel.GetComponent<RectTransform>();
        if (detailsRect != null)
        {
            rect.anchorMin = detailsRect.anchorMin;
            rect.anchorMax = detailsRect.anchorMax;
            rect.pivot = detailsRect.pivot;
            rect.anchoredPosition = detailsRect.anchoredPosition;
            rect.sizeDelta = detailsRect.sizeDelta;
        }
        Image detailsImage = detailsPanel.GetComponent<Image>();
        if (detailsImage != null)
        {
            Image image = panel.GetComponent<Image>();
            image.sprite = detailsImage.sprite;
            image.color = detailsImage.color;
            image.type = detailsImage.type;
        }
        return panel;
    }

    public static float HeaderHeight(int rows)
    {
        return rows * HeaderRowHeight + (rows - 1) * HeaderSpacing;
    }

    // The top strip of the panel: the given number of rows, two cells in each, sharing the panel's width
    public static Transform CreateHeader(GameObject panel, int rows)
    {
        GameObject header = new GameObject("Header", typeof(RectTransform), typeof(VerticalLayoutGroup));
        header.transform.SetParent(panel.transform, false);
        RectTransform rect = header.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -10f);
        rect.sizeDelta = new Vector2(-20f, HeaderHeight(rows));
        VerticalLayoutGroup layout = header.GetComponent<VerticalLayoutGroup>();
        layout.spacing = HeaderSpacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return header.transform;
    }

    // Adds a cell to the header (a new row is started after every two). With an onClick it is a button,
    // without one it is a plain label. Returns its text.
    public static TMP_Text AddHeaderButton(Transform header, GameObject buttonPrefab, Action onClick, out Button button)
    {
        Transform row = header.childCount > 0 && header.GetChild(header.childCount - 1).childCount < 2
            ? header.GetChild(header.childCount - 1)
            : NewHeaderRow(header);

        GameObject cell = new GameObject("Cell", typeof(RectTransform), typeof(Image));
        cell.transform.SetParent(row, false);
        Image image = cell.GetComponent<Image>();
        image.color = onClick != null ? RowColor : InfoColor;

        button = null;
        if (onClick != null)
        {
            button = cell.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());
        }

        return AddLabel(cell.transform, buttonPrefab, 9f, 16f, TextAlignmentOptions.Center, 4f, 4f);
    }

    private static Transform NewHeaderRow(Transform header)
    {
        GameObject row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(header, false);
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = HeaderSpacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        LayoutElement element = row.GetComponent<LayoutElement>();
        element.minHeight = HeaderRowHeight;
        element.preferredHeight = HeaderRowHeight;
        return row.transform;
    }

    // A scrolling list below the header, copied from the one in listPanel. Returns the content transform to put rows in.
    public static Transform CreateScrollList(GameObject panel, GameObject listPanel, float headerHeight)
    {
        Transform content = null;
        Transform sourceScroll = listPanel.transform.Find("Scroll View");
        if (sourceScroll != null)
        {
            GameObject scroll = UnityEngine.Object.Instantiate(sourceScroll.gameObject, panel.transform);
            scroll.name = "Scroll";
            RectTransform scrollRect = scroll.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(10f, 10f);
            scrollRect.offsetMax = new Vector2(-10f, -(headerHeight + 20f));
            content = scroll.transform.Find("Viewport/Content");
            if (content != null)
            {
                ClearChildren(content);
                ConfigureListContent(content);
            }
        }
        if (content == null)
        {
            Debug.LogError("Menu: could not find the scrolling list to copy (AssignPanel/Scroll View/Viewport/Content).");
            content = panel.transform;
        }
        return content;
    }

    // Makes a scroll list's content stack its rows at full width, each at the height it asks for,
    // and grow to fit them (so the scroll bar works)
    public static void ConfigureListContent(Transform content)
    {
        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        if (layout == null) layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    public static void ClearChildren(Transform parent)
    {
        foreach (Transform child in parent)
        {
            child.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    // --- Rows and cards ---

    // A list row: a small picture on the left, the text beside it (shrinking to fit). Not clickable when
    // clickable is false (a plain note). The row has its own Button, Image and TMP_Text, like the prefab it replaces.
    public static GameObject MakeRow(GameObject buttonPrefab, Transform parent, string name, string text, Sprite icon, bool clickable = true)
    {
        GameObject row = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        Image image = row.GetComponent<Image>();
        image.color = clickable ? RowColor : InfoColor;
        LayoutElement element = row.GetComponent<LayoutElement>();
        element.minHeight = RowHeight;
        element.preferredHeight = RowHeight;

        if (clickable)
        {
            Button button = row.AddComponent<Button>();
            button.targetGraphic = image;
        }

        if (icon != null) AddIcon(row.transform, icon, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(36f, 36f), new Vector2(6f, 0f));
        AddLabel(row.transform, buttonPrefab, 10f, 16f, TextAlignmentOptions.Left, icon != null ? 48f : 8f, 6f).text = text;
        return row;
    }

    // A card for the strips of big buttons (build, hire): the prefab's button with the picture above and the
    // caption below it, instead of the caption lying on the picture
    public static GameObject MakeCard(GameObject buttonPrefab, Transform parent, string name, Sprite icon, string text)
    {
        GameObject card = UnityEngine.Object.Instantiate(buttonPrefab, parent);
        card.name = name;

        TMP_Text label = card.GetComponentInChildren<TMP_Text>();
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, icon != null ? 0.3f : 1f);
        labelRect.offsetMin = new Vector2(2f, 2f);
        labelRect.offsetMax = new Vector2(-2f, -2f);
        label.text = text;
        label.enableAutoSizing = true;
        label.fontSizeMin = 8f;
        label.fontSizeMax = 18f;
        label.color = TextColor;
        label.alignment = TextAlignmentOptions.Center;

        if (icon != null)
        {
            GameObject picture = AddIcon(card.transform, icon, new Vector2(0.06f, 0.3f), new Vector2(0.94f, 0.96f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            picture.transform.SetAsFirstSibling();
        }
        return card;
    }

    // Greys a button's text along with the button itself, so a disabled button reads as disabled
    public static void SetButtonEnabled(Button button, TMP_Text label, bool enabled)
    {
        button.interactable = enabled;
        label.color = enabled ? TextColor : DisabledTextColor;
    }

    // --- Pieces ---

    private static GameObject AddIcon(Transform parent, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 position)
    {
        GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(parent, false);
        RectTransform rect = icon.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = icon.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return icon;
    }

    private static TMP_Text AddLabel(Transform parent, GameObject fontSource, float minSize, float maxSize, TextAlignmentOptions alignment, float left, float right)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, 2f);
        rect.offsetMax = new Vector2(-right, -2f);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        TMP_Text source = fontSource != null ? fontSource.GetComponentInChildren<TMP_Text>() : null;
        if (source != null) text.font = source.font; // same font as the rest of the UI
        text.enableAutoSizing = true;
        text.fontSizeMin = minSize;
        text.fontSizeMax = maxSize;
        text.color = TextColor;
        text.alignment = alignment;
        text.raycastTarget = false; // clicks go to the row, not the text
        return text;
    }
}
