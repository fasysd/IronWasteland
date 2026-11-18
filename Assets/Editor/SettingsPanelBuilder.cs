#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using IronWasteland.UI;

/// <summary>
/// Dung popup Cai dat (ESC) vao prefab StageHUD theo wireframe 05_InGame_Settings_Wireframe.svg.
/// Chay: Tools > Iron Wasteland > Build Settings Panel.
/// </summary>
public static class SettingsPanelBuilder
{
    private const string PrefabPath = "Assets/Prefabs/UI/StageHUD.prefab";

    [MenuItem("Tools/Iron Wasteland/Build Settings Panel")]
    public static void BuildAll()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        StageHUD hud = root.GetComponent<StageHUD>();

        Transform old = root.transform.Find("SettingsPanel");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        RectTransform panel = NewRect("SettingsPanel", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image dim = panel.gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.75f);

        RectTransform box = NewRect("Box", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 640f));
        Image boxImg = box.gameObject.AddComponent<Image>();
        boxImg.color = new Color(0.08f, 0.09f, 0.14f, 1f);

        MakeText("Title", box, "CÀI ĐẶT TRẬN ĐẤU", 40f, new Vector2(0f, 262f), new Vector2(840f, 56f), Color.white);
        MakeText("Warning", box, "CHÚ Ý: GAME KHÔNG TẠM DỪNG!", 20f, new Vector2(0f, 216f), new Vector2(840f, 30f), new Color(1f, 0.6f, 0.35f));

        MakeText("MasterLabel", box, "ÂM LƯỢNG TỔNG", 18f, new Vector2(-230f, 165f), new Vector2(430f, 26f), new Color(0.8f, 0.86f, 1f));
        Slider master = MakeSlider("MasterSlider", box, new Vector2(80f, 128f));

        MakeText("SfxLabel", box, "ÂM THANH CHIẾN ĐẤU & MA PHÁP", 18f, new Vector2(-230f, 78f), new Vector2(430f, 26f), new Color(0.8f, 0.86f, 1f));
        Slider sfx = MakeSlider("SfxSlider", box, new Vector2(80f, 41f));

        MakeText("ControlHeader", box, "HỆ THỐNG ĐIỀU KHIỂN & PHÍM BẤM", 20f, new Vector2(0f, -8f), new Vector2(840f, 28f), new Color(1f, 0.85f, 0.45f));
        MakeText("KeyTable", box,
            "Di chuyển xe: W - A - S - D\nHướng nòng pháo & ngắm bắn: con trỏ chuột\nMở Cài đặt: ESC",
            19f, new Vector2(0f, -78f), new Vector2(840f, 110f), new Color(0.85f, 0.9f, 1f));

        Button resume = MakeButton("ResumeButton", box, "QUAY LẠI TRẬN CHIẾN (ESC)", new Vector2(0f, -180f), new Color(0.16f, 0.45f, 0.28f));
        Button leave = MakeButton("LeaveButton", box, "RỜI TRẬN (VỀ SẢNH CHÍNH)", new Vector2(0f, -252f), new Color(0.35f, 0.18f, 0.18f));
        MakeText("CoopNote", box, "* Nếu chơi Co-op, bạn sẽ thoát khỏi phòng của đồng đội.", 16f,
            new Vector2(0f, -296f), new Vector2(840f, 24f), new Color(0.7f, 0.74f, 0.82f));

        Button settingsButton = FindButton(root.transform, "SettingsButton");

        SerializedObject so = new SerializedObject(hud);
        so.FindProperty("settingsPanel").objectReferenceValue = panel.gameObject;
        so.FindProperty("settingsButton").objectReferenceValue = settingsButton;
        so.FindProperty("masterSlider").objectReferenceValue = master;
        so.FindProperty("sfxSlider").objectReferenceValue = sfx;
        so.FindProperty("resumeButton").objectReferenceValue = resume;
        so.FindProperty("leaveButton").objectReferenceValue = leave;
        so.ApplyModifiedPropertiesWithoutUndo();

        panel.gameObject.SetActive(false);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        Debug.Log("[SETTINGS] da dung lai panel theo wireframe 05");
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        return rt;
    }

    private static TMP_Text MakeText(string name, Transform parent, string text, float size, Vector2 position, Vector2 boxSize, Color color)
    {
        RectTransform rt = NewRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, boxSize);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.font = TMP_Settings.defaultFontAsset;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Slider MakeSlider(string name, Transform parent, Vector2 position)
    {
        RectTransform rt = NewRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(420f, 24f));
        Slider slider = rt.gameObject.AddComponent<Slider>();

        RectTransform bg = NewRect("Background", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image bgImg = bg.gameObject.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.22f, 0.3f, 1f);
        slider.targetGraphic = bgImg;

        RectTransform fillArea = NewRect("Fill Area", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        RectTransform fill = NewRect("Fill", fillArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image fillImg = fill.gameObject.AddComponent<Image>();
        fillImg.color = new Color(0.25f, 0.6f, 0.9f, 1f);
        slider.fillRect = fill;

        RectTransform handleArea = NewRect("Handle Slide Area", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        RectTransform handle = NewRect("Handle", handleArea, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(18f, 30f));
        Image handleImg = handle.gameObject.AddComponent<Image>();
        handleImg.color = Color.white;
        slider.handleRect = handle;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.8f;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private static Button MakeButton(string name, Transform parent, string label, Vector2 position, Color color)
    {
        RectTransform rt = NewRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(460f, 60f));
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;

        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;

        RectTransform textRt = NewRect("Label", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        TextMeshProUGUI tmp = textRt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 22f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.font = TMP_Settings.defaultFontAsset;
        tmp.raycastTarget = false;

        return button;
    }

    private static Button FindButton(Transform root, string namePart)
    {
        Button[] all = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name.IndexOf(namePart, System.StringComparison.OrdinalIgnoreCase) >= 0) return all[i];
        }
        return null;
    }
}
#endif
