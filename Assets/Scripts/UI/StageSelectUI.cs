using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using IronWasteland.Stages;

namespace IronWasteland.UI
{
    /// <summary>
    /// Man chon ai — dung theo wireframe 02_StageSelect_Wireframe.svg:
    /// tieu de chien dich, 2 chuong, the man choi 3 cot (ten ai, so dot quai, do kho),
    /// the BOSS / DAI BOSS mau do, va trang thai Da vuot qua / San sang / Khoa.
    ///
    /// The duoc sinh bang code de them/bot man chi can keo them asset vao danh sach `stages`.
    /// Cach dung: Canvas -> object con "StageListUI" gan component nay, keo cac StageDefinition vao `stages`.
    /// </summary>
    [DisallowMultipleComponent]
    public class StageSelectUI : MonoBehaviour
    {
        [Header("Dữ liệu")]
        [Tooltip("Các màn sẽ hiện ra, theo thứ tự.")]
        [SerializeField] private List<StageDefinition> stages = new List<StageDefinition>();

        [Header("UI (có thể để trống)")]
        [SerializeField] private RectTransform listRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text gemsText;

        [Header("Kích thước thẻ")]
        [SerializeField, Min(120f)] private float cardWidth = 540f;
        [SerializeField, Min(80f)] private float cardHeight = 128f;
        [SerializeField, Min(0f)] private float gapX = 30f;
        [SerializeField, Min(0f)] private float gapY = 20f;
        [SerializeField, Min(0f)] private float chapterHeight = 52f;
        [SerializeField, Min(0f)] private float leftPad = 120f;
        [SerializeField, Min(0f)] private float topPad = 30f;

        [Header("Màu")]
        [SerializeField] private Color availableColor = new Color(0.14f, 0.18f, 0.27f, 1f);
        [SerializeField] private Color clearedColor = new Color(0.12f, 0.28f, 0.20f, 1f);
        [SerializeField] private Color lockedColor = new Color(0.11f, 0.11f, 0.13f, 1f);
        [SerializeField] private Color bossColor = new Color(0.32f, 0.13f, 0.15f, 1f);

        private readonly List<GameObject> m_Spawned = new List<GameObject>();
        private RectTransform m_Content;
        private GameObject m_BackButton;

        private static readonly HashSet<string> BossCodes = new HashSet<string> { "1-4", "2-4" };
        private const string FinalBossCode = "2-5";

        private void Start()
        {
            Rebuild();
        }

        /// <summary>Dung lai toan bo man hinh (goi khi tien do thay doi).</summary>
        public void Rebuild()
        {
            Clear();

            if (titleText != null)
            {
                titleText.text = "CHỌN MÀN CHƠI • CHIẾN DỊCH GIẢI CỨU ĐỊA CẦU";
                titleText.fontSize = 34f;
                titleText.alignment = TextAlignmentOptions.Center;

                var titleRect = titleText.rectTransform;
                titleRect.anchorMin = new Vector2(0.5f, 1f);
                titleRect.anchorMax = new Vector2(0.5f, 1f);
                titleRect.pivot = new Vector2(0.5f, 1f);
                titleRect.anchoredPosition = new Vector2(0f, -24f);
                titleRect.sizeDelta = new Vector2(1700f, 52f);
            }
            if (gemsText != null)
                gemsText.text = $"Ngọc: {StageProgress.Gems}";

            EnsureContent();
            EnsureBackButton();

            float y = -96f;
            string chapter = null;
            int column = 0;

            for (int i = 0; i < stages.Count; i++)
            {
                StageDefinition def = stages[i];
                if (def == null) continue;

                string defChapter = ChapterOf(def.StageCode);
                if (defChapter != chapter)
                {
                    if (chapter != null)
                    {
                        // Dang do dang mot hang thi xuong hang truoc khi chen tieu de chuong moi.
                        if (column != 0)
                        {
                            y -= cardHeight + gapY;
                            column = 0;
                        }
                        y -= gapY;
                    }

                    CreateChapter(defChapter, y);
                    y -= chapterHeight;
                    chapter = defChapter;
                    column = 0;
                }

                CreateCard(def, column, y);

                column++;
                if (column >= 3)
                {
                    column = 0;
                    y -= cardHeight + gapY;
                }
            }
        }

        private void EnsureContent()
        {
            if (m_Content != null) return;

            // Luoi the rong hon o "listRoot" cu trong scene, nen gan vao Canvas full-screen
            // de the khong bi cat; padding trai/tren do leftPad/topPad quyet dinh.
            Canvas canvas = GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : transform;

            var go = new GameObject("StageCards", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            m_Content = (RectTransform)go.transform;
            m_Content.anchorMin = Vector2.zero;
            m_Content.anchorMax = Vector2.one;
            m_Content.pivot = new Vector2(0.5f, 1f);
            m_Content.offsetMin = Vector2.zero;
            m_Content.offsetMax = Vector2.zero;

            // Nam tren cung de khong bi cac object cu trong scene che.
            go.transform.SetAsLastSibling();
        }

        private void CreateChapter(string label, float y)
        {
            var go = new GameObject("Chapter", typeof(RectTransform));
            go.transform.SetParent(m_Content, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(leftPad, y);
            rect.sizeDelta = new Vector2(1200f, chapterHeight - 8f);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 28f;
            text.color = new Color(1f, 0.82f, 0.42f);
            text.alignment = TextAlignmentOptions.Left;
            text.font = TMP_Settings.defaultFontAsset;
            text.raycastTarget = false;

            m_Spawned.Add(go);
        }

        private void CreateCard(StageDefinition def, int column, float y)
        {
            bool unlocked = StageProgress.IsUnlocked(def.StageCode);
            bool cleared = StageProgress.IsCleared(def.StageCode);
            bool isBoss = BossCodes.Contains(def.StageCode) || def.StageCode == FinalBossCode;

            var go = new GameObject($"Card_{def.StageCode}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(m_Content, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(leftPad + column * (cardWidth + gapX), y);
            rect.sizeDelta = new Vector2(cardWidth, cardHeight);

            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);

            Color fillColor = !unlocked ? lockedColor : (isBoss ? bossColor : (cleared ? clearedColor : availableColor));
            Color borderColor = !unlocked
                ? new Color(0.20f, 0.22f, 0.26f, 1f)
                : (isBoss ? new Color(0.55f, 0.20f, 0.22f, 1f)
                    : (cleared ? new Color(0.10f, 0.45f, 0.24f, 1f) : new Color(0.22f, 0.32f, 0.48f, 1f)));

            AddFrame(rect, fillColor, borderColor, 12);
            AddStripe(rect, StatusColor(unlocked, cleared));

            // Dong 1: ma ai + trang thai
            MakeText(go.transform, "Code", def.StageCode == FinalBossCode ? "ĐẠI BOSS ẢI " + def.StageCode : "ẢI " + def.StageCode,
                18f, new Vector2(18f, -12f), new Vector2(220f, 26f), new Color(0.75f, 0.8f, 0.9f), TextAlignmentOptions.Left);

            MakeText(go.transform, "Status", StatusLabel(unlocked, cleared), 16f,
                new Vector2(cardWidth - 210f, -12f), new Vector2(192f, 26f), StatusColor(unlocked, cleared), TextAlignmentOptions.Right);

            // Dong 2: ten ai
            MakeText(go.transform, "Name", def.StageName, 26f, new Vector2(18f, -42f), new Vector2(cardWidth - 36f, 34f),
                unlocked ? Color.white : new Color(0.55f, 0.55f, 0.6f), TextAlignmentOptions.Left);

            // Dong 3: so dot quai + do kho
            int waves = def.Waves != null ? def.Waves.Count : 0;
            MakeText(go.transform, "Info", $"{waves} đợt quái • Độ khó: {Difficulty(def.StageCode)}", 16f,
                new Vector2(18f, -88f), new Vector2(cardWidth - 36f, 24f), new Color(0.72f, 0.76f, 0.84f), TextAlignmentOptions.Left);

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.interactable = unlocked;

            string code = def.StageCode;
            button.onClick.AddListener(() => OnStageClicked(code));

            m_Spawned.Add(go);
        }

        private static RectTransform AddFrame(RectTransform host, Color fill, Color border, int radius)
        {
            var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            frameGo.transform.SetParent(host, false);

            var frameRect = (RectTransform)frameGo.transform;
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;

            var frameImage = frameGo.GetComponent<Image>();
            frameImage.sprite = UISprites.Rounded(radius);
            frameImage.type = Image.Type.Sliced;
            frameImage.color = border;
            frameImage.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(host, false);

            var fillRect = (RectTransform)fillGo.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);

            var fillImage = fillGo.GetComponent<Image>();
            fillImage.sprite = UISprites.Rounded(Mathf.Max(2, radius - 1));
            fillImage.type = Image.Type.Sliced;
            fillImage.color = fill;
            fillImage.raycastTarget = false;

            frameRect.SetAsFirstSibling();
            fillRect.SetSiblingIndex(1);
            return fillRect;
        }

        private static void AddStripe(RectTransform host, Color color)
        {
            var go = new GameObject("Stripe", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(host, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = new Vector2(0f, 10f);
            rect.offsetMax = new Vector2(5f, -10f);

            var image = go.GetComponent<Image>();
            image.sprite = UISprites.Rounded(2);
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
        }

        private static TMP_Text MakeText(Transform parent, string name, string text, float size, Vector2 position, Vector2 size2, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size2;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.font = TMP_Settings.defaultFontAsset;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static string ChapterOf(string code)
        {
            return code != null && code.StartsWith("2")
                ? "CHƯƠNG 2: VÙNG ĐẤT NGUYÊN TỐ"
                : "CHƯƠNG 1: LỤC ĐỊA ĐEN";
        }

        private static string Difficulty(string code)
        {
            switch (code)
            {
                case "1-1": return "Dễ";
                case "1-2": return "Dễ";
                case "1-3": return "Trung bình";
                case "1-4": return "Boss";
                case "2-1": return "Trung bình";
                case "2-2": return "Khó";
                case "2-3": return "Khó";
                case "2-4": return "Boss";
                case "2-5": return "Đại Boss";
                default: return "Trung bình";
            }
        }

        private static string StatusLabel(bool unlocked, bool cleared)
        {
            if (!unlocked) return "KHÓA";
            return cleared ? "ĐÃ VƯỢT QUA" : "SẴN SÀNG";
        }

        private static Color StatusColor(bool unlocked, bool cleared)
        {
            if (!unlocked) return new Color(0.6f, 0.6f, 0.66f);
            return cleared ? new Color(0.45f, 0.95f, 0.55f) : new Color(1f, 0.85f, 0.35f);
        }

        private void OnStageClicked(string stageCode)
        {
            string sceneName = StageProgress.SceneNameFor(stageCode);
            Debug.Log($"[StageSelectUI] Mo man {stageCode} -> scene '{sceneName}'");

            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(sceneName);
            }
            else
            {
                Debug.LogWarning($"[StageSelectUI] Scene '{sceneName}' chua co trong Build Settings.");
            }
        }

        /// <summary>Nut "< VỀ SẢNH" o goc tren-trai (wireframe 02). Chua co scene sanh thi chi ghi log.</summary>
        private void EnsureBackButton()
        {
            if (m_BackButton != null) return;

            Canvas canvas = GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : transform;

            var go = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -20f);
            rect.sizeDelta = new Vector2(190f, 46f);

            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);

            AddFrame(rect, new Color(0.10f, 0.13f, 0.18f, 1f), new Color(0.20f, 0.28f, 0.38f, 1f), 12);

            var label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(go.transform, false);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var text = label.AddComponent<TextMeshProUGUI>();
            text.text = "< VỀ SẢNH";
            text.fontSize = 20f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.font = TMP_Settings.defaultFontAsset;
            text.raycastTarget = false;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(OnBackClicked);

            m_BackButton = go;
        }

        private void OnBackClicked()
        {
            if (Application.CanStreamedLevelBeLoaded("MainMenu"))
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene("MainMenu");
            }
            else
            {
                Debug.Log("[StageSelectUI] Chua co scene 'MainMenu' — nut VE SANH chua noi vao dau.");
            }
        }

        private void Clear()
        {
            for (int i = 0; i < m_Spawned.Count; i++)
            {
                if (m_Spawned[i] != null) Destroy(m_Spawned[i]);
            }
            m_Spawned.Clear();
        }
    }
}
