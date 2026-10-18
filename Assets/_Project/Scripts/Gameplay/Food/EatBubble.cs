using UnityEngine;

// A small picture of the food floating over a character while it eats. Made and driven by Character.ShowEating.
public class EatBubble : MonoBehaviour
{
    private SpriteRenderer icon;
    private float remaining;

    public bool Showing => remaining > 0f;
    public Sprite Sprite => icon != null ? icon.sprite : null;

    private void Awake()
    {
        icon = GetComponent<SpriteRenderer>();
        if (icon == null) icon = gameObject.AddComponent<SpriteRenderer>();
        icon.sortingOrder = DepthSorting.Ghost - 10;
        icon.enabled = false;
    }

    // Shows the picture for a few seconds, above the body
    public void Show(Sprite sprite, float seconds, float height)
    {
        if (icon == null) icon = GetComponent<SpriteRenderer>();
        icon.sprite = sprite;
        icon.enabled = true;
        transform.localPosition = new Vector3(0f, height, 0f);
        remaining = seconds;
    }

    private void Update()
    {
        if (remaining <= 0f) return;
        remaining -= Time.deltaTime;
        if (remaining <= 0f && icon != null) icon.enabled = false;
    }
}
