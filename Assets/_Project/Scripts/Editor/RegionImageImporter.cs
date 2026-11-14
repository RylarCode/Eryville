using UnityEditor;
using UnityEngine;

// The region image (Resources/Regions/RegionMap...) is read pixel by pixel and its colours must stay exact: this sets its import settings
// whenever it is imported (readable, no compression, no smoothing, no mipmaps), so a new drawing from Krita just works.
public class RegionImageImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.Contains("/Resources/Regions/") || !System.IO.Path.GetFileName(assetPath).StartsWith("RegionMap")) return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.isReadable = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = false;
        importer.sRGBTexture = false;
        importer.npotScale = TextureImporterNPOTScale.None;
    }
}
