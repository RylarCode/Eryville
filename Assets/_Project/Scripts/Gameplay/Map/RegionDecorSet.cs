using System;
using System.Collections.Generic;
using UnityEngine;

// The pictures scattered over land that is not cleared yet: trees on a forest, boulders on rocks, rubble on debris. One pool per terrain;
// every cell of a locked region gets a random pick (the same every time, so the land looks the same each visit). A terrain without a pool
// (or with no sprites in it) shows simple placeholder shapes. Make one with Assets > Create > Scriptable Objects > Region Decor Set in
// Resources/Regions (the game uses the first it finds). See docs/MAP.md and docs/ART_GUIDE.md.
[CreateAssetMenu(fileName = "RegionDecorSet", menuName = "Scriptable Objects/Region Decor Set")]
public class RegionDecorSet : ScriptableObject
{
    [Serializable]
    public class Pool
    {
        public RegionTerrain terrain = RegionTerrain.Forest;
        [Tooltip("Pictures drawn standing, the base at the bottom centre of each. The game picks among them at random.")]
        public List<Sprite> sprites = new List<Sprite>();
        [Range(0f, 1f), Tooltip("Chance that a cell of the region has one (0.5 = about every second cell).")]
        public float density = 0.5f;
        [Tooltip("Random size, as a multiple of the picture's own size (smallest, largest).")]
        public Vector2 scaleRange = new Vector2(0.85f, 1.15f);
    }

    public List<Pool> pools = new List<Pool>();

    public Pool PoolFor(RegionTerrain terrain)
    {
        foreach (Pool pool in pools)
            if (pool != null && pool.terrain == terrain && pool.sprites != null && pool.sprites.Exists(s => s != null)) return pool;
        return null;
    }
}
