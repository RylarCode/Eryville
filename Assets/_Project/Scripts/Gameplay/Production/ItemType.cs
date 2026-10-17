using System.Collections.Generic;
using UnityEngine;

public enum ItemCategory
{
    Currency,     // gold, recruitment letters
    Material,     // wood, stone, planks...
    Ingredient,   // wheat, flour, fish: made by some buildings, used by others
    Meal,         // bread, grilled fish: ready to eat
}

// One kind of thing the town can have a stock of: gold, wood, wheat, flour, bread... Items are data: a new one is an asset,
// no code. What makes an item are buildings' recipes (see Recipe) and, for food, characters eating it.
// Item assets live in a Resources/Items folder so the game finds them by itself.
[CreateAssetMenu(fileName = "NewItem", menuName = "Scriptable Objects/Item")]
public class ItemType : ScriptableObject
{
    [Tooltip("The name shown in the game and used in save files. Unique, and do not rename it once players have saves.")]
    public string itemName;
    public ItemCategory category = ItemCategory.Material;
    public Sprite icon;                                   // optional: without one a plain square in placeholderColor is shown
    public Color placeholderColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    [Tooltip("How many the town can store without any storage buildings. 0 = no limit.")]
    public float capacity = 0f;

    [Header("Food")]
    public bool edible;
    [Tooltip("How much of a character's hunger one unit takes away (satiety runs 0-100).")]
    public float nourishment = 0f;

    [Tooltip("Free labels, e.g. \"fish\" or \"baked\". Characters' specialties and favourites can match on them.")]
    public List<string> tags = new List<string>();

    public string Name => string.IsNullOrEmpty(itemName) ? name : itemName;

    private Sprite placeholder;

    // The icon, or a plain coloured square when none was made yet
    public Sprite Icon
    {
        get
        {
            if (icon != null) return icon;
            if (placeholder == null)
            {
                Texture2D texture = new Texture2D(8, 8);
                for (int x = 0; x < 8; x++)
                {
                    for (int y = 0; y < 8; y++)
                    {
                        bool edge = x == 0 || y == 0 || x == 7 || y == 7;
                        texture.SetPixel(x, y, edge ? new Color(placeholderColor.r * 0.6f, placeholderColor.g * 0.6f, placeholderColor.b * 0.6f, 1f) : placeholderColor);
                    }
                }
                texture.Apply();
                placeholder = Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
            }
            return placeholder;
        }
    }

    public bool HasTag(string tag)
    {
        return tags != null && tags.Contains(tag);
    }
}
