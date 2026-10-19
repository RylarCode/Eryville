using System;
using UnityEngine;

// Whether the town is shown in 3/4 (tall pictures that can hide what is behind them) or flat (every picture squashed onto the
// cells it stands on, nothing hides anything, and clicks land on exactly the cells). The player toggles it with the View button;
// it also goes flat by itself while placing a building, moving one or building roads, then goes back.
// The change is a short squash / stretch (SecondsToFlatten).
public class CityView : MonoBehaviour
{
    public static CityView Instance { get; private set; }

    public const string FlatPreference = "FlatView";
    public const string SilhouettePreference = "Silhouettes";
    public const float SecondsToFlatten = 0.25f;

    // How far from 3/4 (0) to flat (1) the town is right now
    public float Blend { get; private set; }

    // The player's choice (remembered)
    public bool UserFlat
    {
        get { return PlayerPrefs.GetInt(FlatPreference, 0) == 1; }
        set { PlayerPrefs.SetInt(FlatPreference, value ? 1 : 0); PlayerPrefs.Save(); Changed?.Invoke(); }
    }

    // True while placing / moving buildings or building roads (the view is flat then whatever the player chose)
    public bool Editing { get; private set; }

    public bool TargetFlat => UserFlat || Editing;

    // Whether a faint outline of a character shows through a building that hides it (pictures only; it cannot be clicked)
    public bool Silhouettes
    {
        get { return PlayerPrefs.GetInt(SilhouettePreference, 1) == 1; }
        set { PlayerPrefs.SetInt(SilhouettePreference, value ? 1 : 0); PlayerPrefs.Save(); Changed?.Invoke(); }
    }

    // Raised when the choice or the editing state changes (for button labels)
    public event Action Changed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        Blend = TargetFlat ? 1f : 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Toggle()
    {
        UserFlat = !UserFlat;
    }

    private void Update()
    {
        bool editing = false;
        foreach (IPlacementMode mode in InputManager.PlacementModes)
        {
            if (mode.IsPlacing) { editing = true; break; }
        }
        if (editing != Editing)
        {
            Editing = editing;
            Changed?.Invoke();
        }

        float target = TargetFlat ? 1f : 0f;
        if (Blend == target) return;

        float step = Time.unscaledDeltaTime / SecondsToFlatten;
        Blend = target > Blend ? Mathf.Min(Blend + step, target) : Mathf.Max(Blend - step, target);
        Apply();
    }

    // Puts every building's picture where the current blend says
    public void Apply()
    {
        if (BuildingManager.Instance == null) return;
        foreach (Building building in BuildingManager.Instance.Buildings) building.ApplyView(Blend);
    }
}
