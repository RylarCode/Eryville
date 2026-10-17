using UnityEngine;
using UnityEngine.Rendering.Universal;

// Put this on a building (or anything) that has a Light2D child, e.g. a point light over a window or a lamp:
// the light fades in as it gets dark and out again in the morning.
public class NightLight : MonoBehaviour
{
    [SerializeField] private Light2D lamp;                    // the light to drive (found in the children if left empty)
    [SerializeField] private float fullIntensity = 1f;        // how bright it is at night
    [SerializeField, Range(0f, 1f)] private float startsAtDarkness = 0.15f;  // how dark the day has to be before it begins to glow
    [SerializeField, Range(0f, 1f)] private float fullAtDarkness = 0.55f;    // ...and when it is at full strength

    public float Strength { get; private set; }

    private void Awake()
    {
        if (lamp == null) lamp = GetComponentInChildren<Light2D>();
    }

    private void Update()
    {
        float darkness = DayNightCycle.Instance != null ? DayNightCycle.Instance.Darkness : 0f;
        Strength = fullAtDarkness <= startsAtDarkness ? (darkness >= fullAtDarkness ? 1f : 0f)
                                                       : Mathf.Clamp((darkness - startsAtDarkness) / (fullAtDarkness - startsAtDarkness), 0f, 1f);
        if (lamp != null) lamp.intensity = fullIntensity * Strength;
    }
}
