using UnityEngine;

// While a building's picture hides a character that is walking about behind it, a faint dark outline of the character shows
// through (so you can see where your people are). It is only a picture: it has no collider, so it cannot be clicked, and characters
// that are inside a building (working, resting) have none. Can be switched off with CityView.Silhouettes.
public class CharacterSilhouette : MonoBehaviour
{
    private const float CheckEvery = 0.1f;   // seconds between looks at what hides the character

    private SpriteRenderer body;
    private SpriteRenderer shade;
    private float timer;

    public bool Showing => shade != null && shade.enabled;

    private void Awake()
    {
        body = GetComponent<SpriteRenderer>();
        timer = Random.value * CheckEvery; // characters do not all look at the same moment
    }

    private void LateUpdate()
    {
        if (body == null) return;

        // Follow the body's picture
        if (shade != null && shade.enabled)
        {
            shade.sprite = body.sprite;
            shade.sortingOrder = DepthSorting.Ghost - 20;
        }

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = CheckEvery;

        bool covered = body.enabled && CityView.Instance != null && CityView.Instance.Silhouettes && IsCovered();
        if (covered && shade == null) shade = MakeShade();
        if (shade != null)
        {
            shade.enabled = covered;
            if (covered) { shade.sprite = body.sprite; shade.sortingOrder = DepthSorting.Ghost - 20; }
        }
    }

    private SpriteRenderer MakeShade()
    {
        GameObject child = new GameObject("Silhouette");
        child.transform.SetParent(transform, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.color = new Color(0.12f, 0.12f, 0.22f, 0.55f);
        renderer.enabled = false;
        return renderer;
    }

    // Whether the picture of a building that is drawn in front of the character overlaps it
    private bool IsCovered()
    {
        if (BuildingManager.Instance == null || body.sprite == null) return false;

        Vector3 scale = transform.localScale;
        float halfWidth = body.sprite.bounds.size.x * Mathf.Abs(scale.x) * 0.5f;
        float halfHeight = body.sprite.bounds.size.y * Mathf.Abs(scale.y) * 0.5f;
        float centerX = transform.position.x + body.sprite.bounds.center.x * scale.x;
        float centerY = transform.position.y + body.sprite.bounds.center.y * scale.y;

        foreach (Building building in BuildingManager.Instance.Buildings)
        {
            if (building.Body == null || building.Body.sortingOrder <= body.sortingOrder) continue; // behind the character: it hides nothing

            Vector3 artCenter = building.ArtCenter;
            float artHalfWidth = building.ArtSizeNow.x * 0.5f;
            float artHalfHeight = building.ArtSizeNow.y * 0.5f;
            if (Mathf.Abs(centerX - artCenter.x) < halfWidth + artHalfWidth && Mathf.Abs(centerY - artCenter.y) < halfHeight + artHalfHeight) return true;
        }
        return false;
    }
}
