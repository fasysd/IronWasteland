#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bo mau + sprite dung chung cho toan bo UI, lay tu wireframe Figma
/// (nen #1A1F26 / #282C35, vien #253545, xanh #4C82FF, la #00E676, vang #FFD54F, do #FF5252).
///
/// Sprite bo goc duoc sinh tu dong vao Assets/Art/UI/ va dat 9-slice
/// de khung to nho tuy y ma goc van tron.
/// </summary>
public static class UITheme
{
    public const string SpriteFolder = "Assets/Art/UI";

    // Nen / vien
    public static readonly Color PanelBg = new Color32(0x14, 0x1A, 0x22, 0xEE);
    public static readonly Color PanelSoft = new Color32(0x1B, 0x23, 0x30, 0xE6);
    public static readonly Color PanelDeep = new Color32(0x0E, 0x13, 0x1A, 0xF2);
    public static readonly Color Border = new Color32(0x2C, 0x3D, 0x52, 0xFF);
    public static readonly Color BorderHot = new Color32(0x4C, 0x82, 0xFF, 0xFF);

    // Chu
    public static readonly Color TextMain = new Color32(0xEA, 0xF2, 0xFF, 0xFF);
    public static readonly Color TextMuted = new Color32(0x8F, 0xA6, 0xBF, 0xFF);
    public static readonly Color TextDim = new Color32(0x5C, 0x6E, 0x82, 0xFF);

    // Mau nhan
    public static readonly Color Accent = new Color32(0x4C, 0x82, 0xFF, 0xFF);
    public static readonly Color Green = new Color32(0x00, 0xE6, 0x76, 0xFF);
    public static readonly Color Amber = new Color32(0xFF, 0xD5, 0x4F, 0xFF);
    public static readonly Color Red = new Color32(0xFF, 0x52, 0x52, 0xFF);
    public static readonly Color Purple = new Color32(0xB3, 0x88, 0xFF, 0xFF);

    private static readonly System.Collections.Generic.Dictionary<int, Sprite> Cache =
        new System.Collections.Generic.Dictionary<int, Sprite>();

    /// <summary>Sprite vuong bo goc ban kinh `radius`, dung dang Sliced.</summary>
    public static Sprite Rounded(int radius)
    {
        if (Cache.TryGetValue(radius, out Sprite cached) && cached != null) return cached;

        string path = SpriteFolder + "/round_" + radius + ".png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) sprite = CreateRounded(radius, path);

        Cache[radius] = sprite;
        return sprite;
    }

    private static Sprite CreateRounded(int radius, string path)
    {
        Directory.CreateDirectory(SpriteFolder);

        int size = radius * 2 + 4;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        float r = radius;
        float max = size - 1;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;

                float cx = Mathf.Clamp(px, r, max - r);
                float cy = Mathf.Clamp(py, r, max - r);
                float d = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));

                float alpha = Mathf.Clamp01(r - d + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = new Vector4(radius, radius, radius, radius);
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>Dat sprite bo goc cho mot Image co san (giu nguyen mau).</summary>
    public static Image RoundedImage(Image image, int radius)
    {
        if (image == null) return null;
        image.sprite = Rounded(radius);
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
        return image;
    }

    /// <summary>
    /// Tao khung nen: 1 Image vien (to hon) + 1 Image nen ben trong.
    /// Tra ve Image nen de chinh mau sau nay.
    /// </summary>
    public static Image Panel(RectTransform host, Color fill, Color border, int radius, float borderWidth = 2f)
    {
        var frameGo = new GameObject("BgFrame", typeof(RectTransform));
        frameGo.transform.SetParent(host, false);
        frameGo.transform.SetAsFirstSibling();

        var frameRect = (RectTransform)frameGo.transform;
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;

        var frame = frameGo.AddComponent<Image>();
        RoundedImage(frame, radius);
        frame.color = border;
        frame.raycastTarget = false;

        var fillGo = new GameObject("BgFill", typeof(RectTransform));
        fillGo.transform.SetParent(host, false);
        fillGo.transform.SetSiblingIndex(frameGo.transform.GetSiblingIndex() + 1);

        var fillRect = (RectTransform)fillGo.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(borderWidth, borderWidth);
        fillRect.offsetMax = new Vector2(-borderWidth, -borderWidth);

        var fillImage = fillGo.AddComponent<Image>();
        RoundedImage(fillImage, Mathf.Max(2, radius - 1));
        fillImage.color = fill;
        fillImage.raycastTarget = false;

        return fillImage;
    }

    /// <summary>Dat lai co chu / mau / can le cho mot TMP_Text.</summary>
    public static void StyleText(TMP_Text text, float size, Color color, TextAlignmentOptions align,
        FontStyles style = FontStyles.Normal)
    {
        if (text == null) return;
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.fontStyle = style;
        text.characterSpacing = 0f;
    }

    /// <summary>Thanh tien do: nen mo + phan to mau, deu bo goc.</summary>
    public static Image StyleBar(RectTransform track, Color fillColor, int radius)
    {
        var trackImage = track.GetComponent<Image>();
        if (trackImage != null)
        {
            RoundedImage(trackImage, radius);
            trackImage.color = new Color(1f, 1f, 1f, 0.10f);
        }

        Transform fill = track.Find("Fill");
        Image fillImage = fill != null ? fill.GetComponent<Image>() : null;

        if (fillImage != null)
        {
            RoundedImage(fillImage, radius);
            fillImage.color = fillColor;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
        }

        return fillImage;
    }
}
#endif
