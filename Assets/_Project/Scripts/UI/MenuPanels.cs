using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Builds the pieces of the list-style menus (Town, Recruit) at runtime by copying the look of the UI that
// already exists in the scene: a panel in the details panel's slot, a grid of buttons across the top, and a
// scrolling list copied from the assign-job list.
public static class MenuPanels
{
    public const float HeaderRowHeight = 30f;
    public const float HeaderSpacing = 10f;

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

    // Two columns of buttons across the top of the panel, the given number of rows. Returns the header's transform.
    public static Transform CreateHeader(GameObject panel, int rows)
    {
        float height = rows * HeaderRowHeight + (rows - 1) * HeaderSpacing;
        GameObject header = new GameObject("Header", typeof(RectTransform), typeof(GridLayoutGroup));
        header.transform.SetParent(panel.transform, false);
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -10f);
        headerRect.sizeDelta = new Vector2(-20f, height);
        GridLayoutGroup grid = header.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(160f, HeaderRowHeight);
        grid.spacing = new Vector2(HeaderSpacing, HeaderSpacing);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.UpperLeft;
        return header.transform;
    }

    // Adds a button to the header; returns its label. Pass null for onClick to make a plain label.
    public static TMP_Text AddHeaderButton(Transform header, GameObject buttonPrefab, Action onClick, out Button button)
    {
        GameObject go = UnityEngine.Object.Instantiate(buttonPrefab, header);
        go.name = "HeaderButton";
        button = go.GetComponent<Button>();
        if (onClick != null) button.onClick.AddListener(() => onClick());
        else button.interactable = false;
        return go.GetComponentInChildren<TMP_Text>();
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
            if (content != null) ClearChildren(content);
        }
        if (content == null)
        {
            Debug.LogError("Menu: could not find the scrolling list to copy (AssignPanel/Scroll View/Viewport/Content).");
            content = panel.transform;
        }
        return content;
    }

    public static float HeaderHeight(int rows)
    {
        return rows * HeaderRowHeight + (rows - 1) * HeaderSpacing;
    }

    public static void ClearChildren(Transform parent)
    {
        foreach (Transform child in parent)
        {
            child.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }
}
