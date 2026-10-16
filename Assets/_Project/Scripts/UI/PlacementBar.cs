using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The strip that appears above the bottom bar while a building (or road) is being placed on a phone:
// the finger only aims, and this strip's buttons do the building or leave the mode.
public class PlacementBar
{
    private readonly GameObject root;
    private readonly GameObject confirmButton;
    private readonly GameObject cancelButton;
    private readonly TMP_Text confirmLabel;

    public RectTransform Rect => root.GetComponent<RectTransform>();
    public bool Visible => root.activeSelf;

    public PlacementBar(Transform canvas, GameObject fontSource, Action onConfirm, Action onCancel)
    {
        float scale = MenuPanels.Scale;
        root = new GameObject("PlacementBar", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        root.transform.SetParent(canvas, false);
        root.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.85f);

        HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f * scale;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        confirmButton = MenuPanels.MakeBarButton(fontSource, root.transform, "ConfirmButton", "Place", onConfirm);
        cancelButton = MenuPanels.MakeBarButton(fontSource, root.transform, "CancelButton", "Cancel", onCancel);
        confirmLabel = confirmButton.GetComponentInChildren<TMP_Text>();
        root.SetActive(false);
    }

    // Shows the strip with the given text on the main button; the second button (Cancel) can be left out
    public void Show(string confirmText, bool showCancel)
    {
        confirmLabel.text = confirmText;
        cancelButton.SetActive(showCancel);
        root.SetActive(true);
    }

    public void Hide()
    {
        root.SetActive(false);
    }

    public void Destroy()
    {
        UnityEngine.Object.Destroy(root);
    }
}
