using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

// The project's own region image and Region assets, read straight from the files (so tests use exactly what the game ships)
public static class SampleMap
{
    private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.."));
    public static string Folder => Path.Combine(Root, "Assets/_Project/Resources/Regions");

    public class Image { public int Width, Height; public Color32[] Pixels; }

    // Reads an 8-bit RGB or RGBA PNG (not interlaced) into pixels from the bottom left, like Texture2D.GetPixels32
    public static Image ReadPng(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        int pos = 8, width = 0, height = 0, colourType = 0;
        MemoryStream compressed = new MemoryStream();
        while (pos < data.Length)
        {
            int length = (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
            string type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
            if (type == "IHDR") { width = (data[pos + 8] << 24) | (data[pos + 9] << 16) | (data[pos + 10] << 8) | data[pos + 11]; height = (data[pos + 12] << 24) | (data[pos + 13] << 16) | (data[pos + 14] << 8) | data[pos + 15]; colourType = data[pos + 17]; }
            else if (type == "IDAT") compressed.Write(data, pos + 8, length);
            else if (type == "IEND") break;
            pos += 12 + length;
        }
        int bpp = colourType == 6 ? 4 : colourType == 2 ? 3 : throw new NotSupportedException("PNG colour type " + colourType);
        compressed.Position = 2; // skip the zlib header
        byte[] raw;
        using (DeflateStream inflate = new DeflateStream(compressed, CompressionMode.Decompress)) { MemoryStream output = new MemoryStream(); inflate.CopyTo(output); raw = output.ToArray(); }

        int stride = width * bpp;
        byte[] rows = new byte[height * stride];
        for (int y = 0; y < height; y++)
        {
            int filter = raw[y * (stride + 1)];
            for (int i = 0; i < stride; i++)
            {
                int x = raw[y * (stride + 1) + 1 + i];
                int a = i >= bpp ? rows[y * stride + i - bpp] : 0, b = y > 0 ? rows[(y - 1) * stride + i] : 0, c = (y > 0 && i >= bpp) ? rows[(y - 1) * stride + i - bpp] : 0;
                int value;
                switch (filter)
                {
                    case 1: value = x + a; break;
                    case 2: value = x + b; break;
                    case 3: value = x + ((a + b) >> 1); break;
                    case 4: { int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c); value = x + (pa <= pb && pa <= pc ? a : pb <= pc ? b : c); break; }
                    default: value = x; break;
                }
                rows[y * stride + i] = (byte)value;
            }
        }
        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * stride + x * bpp;
                pixels[(height - 1 - y) * width + x] = new Color32(rows[i], rows[i + 1], rows[i + 2], bpp == 4 ? rows[i + 3] : (byte)255);
            }
        return new Image { Width = width, Height = height, Pixels = pixels };
    }

    // The Region assets in the folder
    public static List<Region> ReadRegions()
    {
        List<Region> regions = new List<Region>();
        foreach (string file in Directory.GetFiles(Folder, "*.asset").OrderBy(f => f))
        {
            string text = File.ReadAllText(file);
            string Field(string name) => Regex.Match(text, "(?m)^  " + name + ": (.*)$").Groups[1].Value.Trim().Trim('"');
            Region r = ScriptableObject.CreateInstance<Region>();
            r.name = Path.GetFileNameWithoutExtension(file);
            r.id = Field("id"); r.displayName = Field("displayName");
            r.terrain = (RegionTerrain)int.Parse(Field("terrain"));
            Match c = Regex.Match(text, @"color: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+), a: ([\d.]+)\}");
            r.color = new Color(float.Parse(c.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(c.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(c.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture), 1f);
            r.startsCleared = Field("startsCleared") == "1";
            r.goldCost = int.Parse(Field("goldCost")); r.charactersNeeded = int.Parse(Field("charactersNeeded")); r.clearSeconds = float.Parse(Field("clearSeconds"), System.Globalization.CultureInfo.InvariantCulture);
            foreach (Match y in Regex.Matches(text, @"- item: (\w+)\r?\n    amount: (\d+)")) r.yields.Add(new RegionYield { item = y.Groups[1].Value, amount = int.Parse(y.Groups[2].Value) });
            regions.Add(r);
        }
        return regions;
    }
}
