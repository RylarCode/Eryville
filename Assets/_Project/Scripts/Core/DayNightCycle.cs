using UnityEngine;
using UnityEngine.Rendering.Universal;

// Lights the world according to the time of day: dawn, day, dusk and night, blended smoothly.
// It drives one global 2D light (found in the scene, or created) from GameClock.HourOfDay and a LightingProfile.
// Added to the GameClock's object by the clock itself, so nothing needs setting up in the scene.
// Sprites and tilemaps that use a lit material (the 2D renderer's default) are tinted; the UI is not.
public class DayNightCycle : MonoBehaviour
{
    public const string EnabledPreference = "DayNightLighting";

    public static DayNightCycle Instance { get; private set; }

    [SerializeField, Tooltip("How the world is lit through the day. Empty: the built-in look (dawn pink, day white, dusk orange / purple, night blue).")]
    private LightingProfile profile;

    private Light2D globalLight;
    private Color applied = Color.white;

    // Testing: look at this hour whatever the clock says (null = follow the clock)
    public float? PreviewHour { get; set; }

    public bool LightingOn
    {
        get => PlayerPrefs.GetInt(EnabledPreference, 1) == 1;
        set { PlayerPrefs.SetInt(EnabledPreference, value ? 1 : 0); PlayerPrefs.Save(); Apply(); }
    }

    public LightingProfile Profile
    {
        get
        {
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<LightingProfile>(); // the built-in look
            }
            return profile;
        }
        set { profile = value; }
    }

    // The hour being shown, and the colour, look and darkness it gives
    public float Hour => PreviewHour ?? (GameClock.Instance != null ? GameClock.Instance.HourOfDay : 12f);
    public Color CurrentColor { get; private set; } = Color.white;
    public DayStage Stage => Profile.StageAt(Hour);
    public float Darkness => LightingProfile.Darkness(CurrentColor);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void LateUpdate()
    {
        Apply();
    }

    // Works out the colour for now and puts it on the light (only when it changed)
    public void Apply()
    {
        CurrentColor = LightingOn ? Profile.Evaluate(Hour) : Color.white;
        if (Mathf.Abs(CurrentColor.r - applied.r) + Mathf.Abs(CurrentColor.g - applied.g) + Mathf.Abs(CurrentColor.b - applied.b) < 0.001f && globalLight != null) return;

        if (globalLight == null) globalLight = FindOrCreateLight();
        globalLight.color = CurrentColor;
        applied = CurrentColor;
    }

    // Uses the scene's global light if it has one, so a hand-made light is respected; otherwise makes one
    private Light2D FindOrCreateLight()
    {
        foreach (Light2D existing in Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None))
        {
            if (existing != null && existing.lightType == Light2D.LightType.Global) return existing;
        }
        GameObject go = new GameObject("Day Night Light");
        go.transform.SetParent(transform, false);
        Light2D created = go.AddComponent<Light2D>();
        created.lightType = Light2D.LightType.Global;
        created.intensity = 1f;
        return created;
    }
}
