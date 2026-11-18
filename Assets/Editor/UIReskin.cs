#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lam lai giao dien GameplayHub theo ngon ngu thiet ke cua wireframe Figma:
/// nen toi, khung bo goc co vien, chu phan cap ro rang, thanh mau co nhan.
///
/// Chay: Tools > Iron Wasteland > Reskin UI.
/// Script chi sua hinh thuc (sprite, mau, co chu, khoang cach), khong doi logic.
/// </summary>
public static class UIReskin
{
    private const string PrefabPath = "Assets/Prefabs/UI/StageHUD.prefab";

    [MenuItem("Tools/Iron Wasteland/Reskin UI")]
    public static void Reskin()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        Transform t = root.transform;

        StyleTopLeft(t);
        StyleTopCenter(t);
        StyleTopRight(t);
        StyleSkills(t);
        StyleCoop(t);
        StyleHint(t);
        StyleResult(t);
        StyleSettings(t);

        // Lop phu hoi chieu cua o ky nang: de trong suot de doc duoc phim/ten ky nang.
        var status = root.GetComponent<IronWasteland.UI.TankStatusHUD>();
        if (status != null)
        {
            var so = new SerializedObject(status);
            so.FindProperty("readyColor").colorValue = new Color(0f, 0.9f, 0.46f, 0.16f);
            so.FindProperty("cooldownColor").colorValue = new Color(0.04f, 0.06f, 0.09f, 0.88f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        Debug.Log("[RESKIN] da lam lai giao dien GameplayHub");
    }

    // ---------------------------------------------------------------- cum goc

    private static void StyleTopLeft(Transform t)
    {
        RectTransform box = R(t, "TopLeft");
        NodeBg(box, UITheme.PanelBg, UITheme.Border, 14);

        RectTransform avatar = R(t, "TopLeft/Avatar");
        if (avatar != null)
        {
            Image img = avatar.GetComponent<Image>();
            if (img == null) img = avatar.gameObject.AddComponent<Image>();
            img.sprite = UITheme.Rounded(52);
            img.type = Image.Type.Sliced;
            img.color = new Color(0.13f, 0.20f, 0.30f, 1f);
            img.raycastTarget = false;
        }

        Txt(t, "TopLeft/TankName", 20f, UITheme.TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
        Txt(t, "TopLeft/HPBg/HPText", 12f, UITheme.TextMain, TextAlignmentOptions.Center, FontStyles.Bold);

        // So lieu nam de len thanh mau nen can vien toi cho doc duoc.
        Outline(t, "TopLeft/HPBg/HPText");
        Outline(t, "TopLeft/ENBg/ENText");

        Bar(R(t, "TopLeft/HPBg"), UITheme.Green, 12);
        Bar(R(t, "TopLeft/ENBg"), UITheme.Accent, 9);

        Txt(t, "TopLeft/ENBg/ENText", 11f, UITheme.TextDim, TextAlignmentOptions.Center, FontStyles.Normal);

        RectTransform passive = R(t, "TopLeft/PassiveIcon");
        NodeBg(passive, UITheme.PanelSoft, UITheme.Border, 8);
    }

    private static void StyleTopCenter(Transform t)
    {
        Txt(t, "TopCenter/StageTitle", 30f, UITheme.TextMain, TextAlignmentOptions.Center, FontStyles.Bold);
        Txt(t, "TopCenter/WaveText", 16f, UITheme.Amber, TextAlignmentOptions.Center, FontStyles.Bold);
        Txt(t, "TopCenter/ObjectiveText", 16f, UITheme.TextMuted, TextAlignmentOptions.Center, FontStyles.Normal);

        RectTransform boss = R(t, "TopCenter/BossPanel");
        if (boss != null)
        {
            NodeBg(boss, new Color(0.08f, 0.05f, 0.06f, 0.92f), new Color(1f, 0.32f, 0.32f, 0.55f), 10);
            boss.gameObject.SetActive(false); // StageHUD bat lai khi quet thay boss
            Txt(t, "TopCenter/BossPanel/BossName", 13f, UITheme.TextMain, TextAlignmentOptions.Left, FontStyles.Bold);

            RectTransform bar = R(t, "TopCenter/BossPanel/BossBar");
            if (bar != null)
            {
                Image track = bar.GetComponent<Image>();
                if (track != null)
                {
                    track.sprite = UITheme.Rounded(4);
                    track.type = Image.Type.Sliced;
                    track.color = new Color(1f, 1f, 1f, 0.12f);
                }

                Image fill = bar.Find("Fill") != null ? bar.Find("Fill").GetComponent<Image>() : null;
                if (fill != null)
                {
                    fill.sprite = UITheme.Rounded(4);
                    fill.type = Image.Type.Filled;
                    fill.fillMethod = Image.FillMethod.Horizontal;
                    fill.color = UITheme.Red;
                }
            }
        }
    }

    private static void StyleTopRight(Transform t)
    {
        Txt(t, "TopRight/GemsText", 18f, UITheme.Amber, TextAlignmentOptions.Right, FontStyles.Bold);
        Txt(t, "TopRight/TimerText", 34f, UITheme.TextMain, TextAlignmentOptions.Right, FontStyles.Bold);
        Txt(t, "TopRight/KillsText", 15f, UITheme.TextMuted, TextAlignmentOptions.Right, FontStyles.Normal);

        RectTransform radar = R(t, "TopRight/Radar");
        if (radar != null)
        {
            // Hinh tron: ban kinh phai <= nua kich thuoc de 9-slice khong bi meo.
            NodeBg(radar, new Color(0f, 0f, 0f, 0.45f), new Color(0.3f, 0.51f, 1f, 0.55f), 50);
        }
        Txt(t, "TopRight/Radar/RadarLabel", 11f, UITheme.TextDim, TextAlignmentOptions.Center, FontStyles.Normal);

        RectTransform set = R(t, "TopRight/SettingsButton");
        NodeBg(set, UITheme.PanelSoft, UITheme.Border, 14);
        Txt(t, "TopRight/SettingsButton/Label", 16f, UITheme.TextMain, TextAlignmentOptions.Center, FontStyles.Bold);
        Txt(t, "TopRight/SettingsButton/EscLabel", 11f, UITheme.TextDim, TextAlignmentOptions.Center, FontStyles.Normal);
    }

    private static void StyleSkills(Transform t)
    {
        for (int i = 0; i < 4; i++)
        {
            string path = "BottomRight/Skill" + i;
            RectTransform slot = R(t, path);
            if (slot == null) continue;

            // Image cua o ky nang la lop "hoi chieu" (TankStatusHUD doi mau + fillAmount),
            // nen phai boc no trong mot khung nen rieng thay vi to mau len chinh no.
            Transform parent = slot.parent;
            Transform existing = parent.Find("Slot" + i);
            RectTransform wrapper;

            if (existing == null)
            {
                var go = new GameObject("Slot" + i, typeof(RectTransform));
                wrapper = (RectTransform)go.transform;
                wrapper.SetParent(parent, false);
                wrapper.anchorMin = slot.anchorMin;
                wrapper.anchorMax = slot.anchorMax;
                wrapper.pivot = slot.pivot;
                wrapper.sizeDelta = slot.sizeDelta;
                wrapper.anchoredPosition = slot.anchoredPosition;
                wrapper.SetSiblingIndex(slot.GetSiblingIndex());

                slot.SetParent(wrapper, false);
            }
            else
            {
                wrapper = (RectTransform)existing;
            }

            NodeBg(wrapper, UITheme.PanelBg, UITheme.Border, 16);

            slot.anchorMin = Vector2.zero;
            slot.anchorMax = Vector2.one;
            slot.offsetMin = new Vector2(4f, 4f);
            slot.offsetMax = new Vector2(-4f, -4f);

            Image fill = slot.GetComponent<Image>();
            if (fill != null)
            {
                fill.sprite = UITheme.Rounded(13);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Vertical;
                fill.fillOrigin = 1;
            }

            Txt(t, path + "/Key", 18f, UITheme.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
            Txt(t, path + "/Cd", 24f, UITheme.TextMain, TextAlignmentOptions.Center, FontStyles.Bold);
            Txt(t, path + "/Name", 11f, UITheme.TextMuted, TextAlignmentOptions.Center, FontStyles.Normal);
        }
    }

    private static void StyleCoop(Transform t)
    {
        NodeBg(R(t, "BottomLeft"), new Color(0.06f, 0.09f, 0.12f, 0.72f), UITheme.Border, 12);
        Txt(t, "BottomLeft/CoopTitle", 12f, UITheme.TextDim, TextAlignmentOptions.Left, FontStyles.Bold);
        Txt(t, "BottomLeft/CoopBody", 12f, UITheme.TextMuted, TextAlignmentOptions.Left, FontStyles.Normal);
    }

    private static void StyleHint(Transform t)
    {
        Txt(t, "ControlHint", 13f, UITheme.TextDim, TextAlignmentOptions.Center, FontStyles.Normal);
    }

    // ---------------------------------------------------------------- ket qua

    private static void StyleResult(Transform t)
    {
        RectTransform dim = R(t, "ResultPanel");
        if (dim != null)
        {
            Image img = dim.GetComponent<Image>();
            if (img != null) img.color = new Color(0f, 0f, 0f, 0.82f);
        }

        RectTransform box = R(t, "ResultPanel/ResultBox");
        NodeBg(box, UITheme.PanelDeep, new Color(0.3f, 0.51f, 1f, 0.55f), 20);

        Txt(t, "ResultPanel/ResultBox/ResultTitle", 40f, UITheme.TextMain, TextAlignmentOptions.Center, FontStyles.Bold);
        Txt(t, "ResultPanel/ResultBox/ResultDetail", 20f, UITheme.TextMuted, TextAlignmentOptions.Center, FontStyles.Normal);

        StyleButton(t, "ResultPanel/ResultBox/NextButton", new Color(0.05f, 0.32f, 0.19f, 1f), UITheme.Green);
        StyleButton(t, "ResultPanel/ResultBox/RetryButton", UITheme.PanelSoft, UITheme.Border);
        StyleButton(t, "ResultPanel/ResultBox/SelectButton", UITheme.PanelSoft, UITheme.Border);
    }

    // ---------------------------------------------------------------- cai dat

    private static void StyleSettings(Transform t)
    {
        RectTransform dim = R(t, "SettingsPanel");
        if (dim != null)
        {
            Image img = dim.GetComponent<Image>();
            if (img != null) img.color = new Color(0f, 0f, 0f, 0.80f);
        }

        RectTransform box = R(t, "SettingsPanel/Box");
        NodeBg(box, UITheme.PanelDeep, new Color(0.3f, 0.51f, 1f, 0.5f), 20);

        Txt(t, "SettingsPanel/Box/Title", 36f, UITheme.TextMain, TextAlignmentOptions.Center, FontStyles.Bold);
        Txt(t, "SettingsPanel/Box/Warning", 17f, UITheme.Amber, TextAlignmentOptions.Center, FontStyles.Bold);
        Txt(t, "SettingsPanel/Box/MasterLabel", 15f, UITheme.TextMuted, TextAlignmentOptions.Left, FontStyles.Normal);
        Txt(t, "SettingsPanel/Box/SfxLabel", 15f, UITheme.TextMuted, TextAlignmentOptions.Left, FontStyles.Normal);
        Txt(t, "SettingsPanel/Box/ControlHeader", 17f, UITheme.Amber, TextAlignmentOptions.Center, FontStyles.Bold);
        Txt(t, "SettingsPanel/Box/KeyTable", 17f, UITheme.TextMuted, TextAlignmentOptions.Center, FontStyles.Normal);
        Txt(t, "SettingsPanel/Box/CoopNote", 13f, UITheme.TextDim, TextAlignmentOptions.Center, FontStyles.Normal);

        StyleSlider(t, "SettingsPanel/Box/MasterSlider");
        StyleSlider(t, "SettingsPanel/Box/SfxSlider");

        StyleButton(t, "SettingsPanel/Box/ResumeButton", new Color(0.06f, 0.30f, 0.35f, 1f), UITheme.Accent);
        StyleButton(t, "SettingsPanel/Box/LeaveButton", new Color(0.28f, 0.12f, 0.14f, 1f), new Color(1f, 0.42f, 0.42f, 0.7f));
    }

    private static void StyleSlider(Transform t, string path)
    {
        RectTransform slider = R(t, path);
        if (slider == null) return;

        RectTransform bg = slider.Find("Background") as RectTransform;
        if (bg != null)
        {
            Image img = bg.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = UITheme.Rounded(12);
                img.type = Image.Type.Sliced;
                img.color = new Color(1f, 1f, 1f, 0.12f);
            }
        }

        Transform fillArea = slider.Find("Fill Area");
        Transform fill = fillArea != null ? fillArea.Find("Fill") : null;
        if (fill != null)
        {
            Image img = fill.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = UITheme.Rounded(12);
                img.type = Image.Type.Sliced;
                img.color = UITheme.Accent;
            }
        }

        Transform handleArea = slider.Find("Handle Slide Area");
        Transform handle = handleArea != null ? handleArea.Find("Handle") : null;
        if (handle != null)
        {
            Image img = handle.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = UITheme.Rounded(10);
                img.type = Image.Type.Sliced;
                img.color = UITheme.TextMain;
            }
        }
    }

    // ---------------------------------------------------------------- helpers

    private static RectTransform R(Transform root, string path)
    {
        Transform found = root.Find(path);
        return found as RectTransform;
    }

    private static void Txt(Transform t, string path, float size, Color color, TextAlignmentOptions align, FontStyles style)
    {
        RectTransform rt = R(t, path);
        if (rt == null) return;
        var text = rt.GetComponent<TMP_Text>();
        if (text == null) return;
        UITheme.StyleText(text, size, color, align, style);
    }

    /// <summary>Vien toi quanh chu de doc duoc khi nam tren thanh mau sang.</summary>
    private static void Outline(Transform t, string path)
    {
        RectTransform rt = R(t, path);
        if (rt == null) return;
        var text = rt.GetComponent<TMP_Text>();
        if (text == null) return;
        text.outlineWidth = 0.18f;
        text.outlineColor = new Color(0f, 0f, 0f, 0.9f);
    }

    /// <summary>
    /// Nen cho mot node: dung UITheme.Panel de khung la CON cua node
    /// (an/hien cung node, khong de lai vien mo côi khi node bi SetActive(false)),
    /// con Image rieng cua node (neu co) chi giu vai tro nhan chuot.
    /// </summary>
    private static void NodeBg(RectTransform rt, Color fill, Color border, int radius, float borderWidth = 2f)
    {
        if (rt == null) return;

        Image own = rt.GetComponent<Image>();
        if (own != null) own.color = new Color(0f, 0f, 0f, 0f);

        // Don vien cu (ban reskin truoc tao ra dang sibling).
        Transform stale = rt.parent.Find(rt.name + "_Border");
        if (stale != null) Object.DestroyImmediate(stale.gameObject);
        Transform staleSelf = rt.Find("BgFrame");
        if (staleSelf != null) Object.DestroyImmediate(staleSelf.gameObject);
        Transform staleFill = rt.Find("BgFill");
        if (staleFill != null) Object.DestroyImmediate(staleFill.gameObject);

        UITheme.Panel(rt, fill, border, radius, borderWidth);
    }

    private static void AddBorderBehind(RectTransform target, Color color, int radius, float width)
    {
        string name = target.name + "_Border";
        Transform existing = target.parent.Find(name);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(target.parent, false);

        rt.anchorMin = target.anchorMin;
        rt.anchorMax = target.anchorMax;
        rt.pivot = target.pivot;

        bool stretched = target.anchorMin != target.anchorMax;
        if (stretched)
        {
            // Node keo gian: mo rong bang offset, khong dung sizeDelta (sizeDelta la phan bu).
            rt.offsetMin = target.offsetMin - new Vector2(width, width);
            rt.offsetMax = target.offsetMax + new Vector2(width, width);
        }
        else
        {
            rt.sizeDelta = target.sizeDelta + new Vector2(width * 2f, width * 2f);
            rt.anchoredPosition = target.anchoredPosition;
        }

        rt.SetSiblingIndex(target.GetSiblingIndex());

        Image img = go.GetComponent<Image>();
        img.sprite = UITheme.Rounded(radius + Mathf.RoundToInt(width));
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
    }

    private static void Bar(RectTransform bg, Color fillColor, int radius)
    {
        if (bg == null) return;

        Image track = bg.GetComponent<Image>();
        if (track != null)
        {
            track.sprite = UITheme.Rounded(radius);
            track.type = Image.Type.Sliced;
            track.color = new Color(1f, 1f, 1f, 0.10f);
        }

        Transform fill = bg.Find("Fill");
        Image fillImage = fill != null ? fill.GetComponent<Image>() : null;
        if (fillImage == null)
        {
            // HPFill/ENFill nam truc tiep trong HPBg/ENBg (khong co node "Fill").
            for (int i = 0; i < bg.childCount; i++)
            {
                Image child = bg.GetChild(i).GetComponent<Image>();
                if (child != null && child.name.EndsWith("Fill")) { fillImage = child; break; }
            }
        }

        if (fillImage != null)
        {
            fillImage.sprite = UITheme.Rounded(radius);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.color = fillColor;
        }
    }

    private static void StyleButton(Transform t, string path, Color fill, Color border)
    {
        RectTransform rt = R(t, path);
        if (rt == null) return;

        NodeBg(rt, fill, border, 12);

        Transform label = rt.Find("Label");
        if (label != null)
        {
            var text = label.GetComponent<TMP_Text>();
            if (text != null) UITheme.StyleText(text, 20f, UITheme.TextMain, TextAlignmentOptions.Center, FontStyles.Bold);
        }
    }
}
#endif
