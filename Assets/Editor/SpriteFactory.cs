using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lockdown.EditorTools {

/// <summary>Generates every sprite the game needs as PNGs in Assets/Sprites.
/// No external art is used.</summary>
public static class SpriteFactory {
    const string Dir = "Assets/Sprites";

    public static void GenerateAll() {
        Directory.CreateDirectory(Dir);
        Write("square",   Square(32));
        Write("circle",   Circle(64, filled: true));
        Write("ring",     Circle(64, filled: false));
        Write("triangle", Triangle(64));
        AssetDatabase.Refresh();
        foreach (var n in new[] { "square", "circle", "ring", "triangle" }) Import(n);
    }

    public static Sprite Load(string name) =>
        AssetDatabase.LoadAssetAtPath<Sprite>($"{Dir}/{name}.png");

    static void Write(string name, Texture2D tex) {
        File.WriteAllBytes($"{Dir}/{name}.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    static void Import(string name) {
        string path = $"{Dir}/{name}.png";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType         = TextureImporterType.Sprite;
        ti.spriteImportMode    = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = name == "square" ? 32 : 64;   // every sprite = 1 world unit
        ti.filterMode          = FilterMode.Bilinear;
        ti.textureCompression  = TextureImporterCompression.Uncompressed;
        ti.alphaIsTransparency = true;
        ti.SaveAndReimport();
    }

    static Texture2D New(int n) {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var clear = new Color[n * n];
        t.SetPixels(clear);
        return t;
    }

    static Texture2D Square(int n) {
        var t = New(n);
        var px = new Color[n * n];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        t.SetPixels(px); t.Apply();
        return t;
    }

    static Texture2D Circle(int n, bool filled) {
        var t = New(n);
        float r = n / 2f - 1f, inner = r * 0.78f, c = n / 2f;
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++) {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
            bool on = filled ? d <= r : (d <= r && d >= inner);
            t.SetPixel(x, y, on ? Color.white : Color.clear);
        }
        t.Apply();
        return t;
    }

    static Texture2D Triangle(int n) {
        var t = New(n);
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++) {
            float ty = y / (float)(n - 1);              // apex at top
            float halfWidth = (1f - ty) * (n / 2f);
            bool on = Mathf.Abs(x - n / 2f) <= halfWidth;
            t.SetPixel(x, y, on ? Color.white : Color.clear);
        }
        t.Apply();
        return t;
    }
}
}
