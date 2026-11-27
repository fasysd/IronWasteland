using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace IronWasteland.UI
{
    public enum BossAnnouncementType : byte
    {
        Skill = 0,
        BurstStart = 1,
        BurstSuccess = 2,
        BurstFailure = 3,
        PhaseEvent = 4
    }

    /// <summary>
    /// Singleton hiển thị thông báo hành động của Boss ở giữa màn hình cho toàn bộ người chơi
    /// (hỗ trợ cả Offline và Co-op qua NetStageEvents).
    /// Boss chỉ cần gọi <see cref="Announce"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossActionAnnouncer : MonoBehaviour
    {
        private struct AnnouncementEntry
        {
            public string Title;
            public string Subtitle;
            public BossAnnouncementType Type;
            public float Duration;
        }

        public static BossActionAnnouncer Instance { get; private set; }

        [Header("UI References (tự tạo nếu để trống)")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform bannerRect;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image accentBarImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;

        [Header("Timing & Animation")]
        [SerializeField, Min(0.05f)] private float fadeInDuration = 0.18f;
        [SerializeField, Min(0.05f)] private float fadeOutDuration = 0.28f;
        [SerializeField] private float defaultDuration = 2.4f;

        [Header("Colors")]
        [SerializeField] private Color skillAccentColor = new Color(1f, 0.72f, 0.18f, 1f);
        [SerializeField] private Color burstStartAccentColor = new Color(1f, 0.32f, 0.14f, 1f);
        [SerializeField] private Color burstSuccessAccentColor = new Color(0.22f, 0.9f, 0.45f, 1f);
        [SerializeField] private Color burstFailureAccentColor = new Color(0.92f, 0.18f, 0.24f, 1f);
        [SerializeField] private Color phaseEventAccentColor = new Color(0.25f, 0.78f, 1f, 1f);

        private readonly Queue<AnnouncementEntry> m_Queue = new Queue<AnnouncementEntry>();
        private AnnouncementEntry m_Current;
        private bool m_HasCurrent;
        private float m_StateTimer;
        private int m_State; // 0 = idle, 1 = fadeIn, 2 = hold, 3 = fadeOut

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureUiBuilt();
            HideImmediate();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Gọi từ Boss (trên Server/Host hoặc Offline) để thông báo hành động tới toàn bộ người chơi.
        /// </summary>
        public static void Announce(
            string title,
            string subtitle = "",
            BossAnnouncementType type = BossAnnouncementType.Skill,
            float duration = 2.4f)
        {
            if (string.IsNullOrWhiteSpace(title)) return;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening)
            {
                if (!manager.IsServer) return;

                if (IronWasteland.Net.NetStageEvents.Instance != null)
                {
                    IronWasteland.Net.NetStageEvents.Instance.BroadcastBossAnnouncement(
                        title, subtitle ?? string.Empty, (byte)type, duration);
                    return;
                }
            }

            EnsureInstance().ShowLocal(title, subtitle, type, duration);
        }

        /// <summary>Được NetStageEvents gọi ở cả Host và Client khi nhận thông báo Boss.</summary>
        public static void HandleNetworkAnnouncement(string title, string subtitle, byte type, float duration)
        {
            if (string.IsNullOrWhiteSpace(title)) return;

            BossAnnouncementType parsedType = System.Enum.IsDefined(typeof(BossAnnouncementType), type)
                ? (BossAnnouncementType)type
                : BossAnnouncementType.Skill;

            EnsureInstance().ShowLocal(title, subtitle, parsedType, duration);
        }

        public static BossActionAnnouncer EnsureInstance()
        {
            if (Instance != null) return Instance;

            BossActionAnnouncer existing = FindAnyObjectByType<BossActionAnnouncer>();
            if (existing != null)
            {
                Instance = existing;
                existing.EnsureUiBuilt();
                return existing;
            }

            GameObject go = new GameObject("BossActionAnnouncer");
            return go.AddComponent<BossActionAnnouncer>();
        }

        public void ShowLocal(string title, string subtitle, BossAnnouncementType type, float duration)
        {
            EnsureUiBuilt();

            var entry = new AnnouncementEntry
            {
                Title = title,
                Subtitle = subtitle ?? string.Empty,
                Type = type,
                Duration = duration > 0.1f ? duration : defaultDuration
            };

            // Các mốc Bùng nổ (bắt đầu / thành công / thất bại) được ưu tiên hiển thị ngay.
            if (type == BossAnnouncementType.BurstStart ||
                type == BossAnnouncementType.BurstSuccess ||
                type == BossAnnouncementType.BurstFailure)
            {
                m_Queue.Clear();
                StartEntry(entry);
                return;
            }

            if (type == BossAnnouncementType.PhaseEvent)
            {
                if (!m_HasCurrent || m_Current.Type == BossAnnouncementType.Skill)
                {
                    StartEntry(entry);
                    return;
                }

                if (m_Queue.Count < 8)
                {
                    m_Queue.Enqueue(entry);
                }
                return;
            }

            if (!m_HasCurrent)
            {
                StartEntry(entry);
            }
            else if (m_Queue.Count < 3)
            {
                m_Queue.Enqueue(entry);
            }
        }

        public static string FormatName(Object obj)
        {
            if (obj == null) return "Tank";
            string raw = obj.name;
            if (string.IsNullOrWhiteSpace(raw)) return "Tank";
            return raw.Replace("(Clone)", string.Empty).Trim();
        }

        private void Update()
        {
            if (!m_HasCurrent) return;

            float dt = Time.unscaledDeltaTime;
            m_StateTimer += dt;

            if (m_State == 1)
            {
                float t = Mathf.Clamp01(m_StateTimer / Mathf.Max(0.01f, fadeInDuration));
                if (canvasGroup != null) canvasGroup.alpha = t;
                if (bannerRect != null)
                {
                    float s = Mathf.Lerp(1.08f, 1f, t);
                    bannerRect.localScale = new Vector3(s, s, 1f);
                }

                if (t >= 1f)
                {
                    m_State = 2;
                    m_StateTimer = 0f;
                }
            }
            else if (m_State == 2)
            {
                if (m_StateTimer >= m_Current.Duration)
                {
                    m_State = 3;
                    m_StateTimer = 0f;
                }
            }
            else if (m_State == 3)
            {
                float t = Mathf.Clamp01(m_StateTimer / Mathf.Max(0.01f, fadeOutDuration));
                if (canvasGroup != null) canvasGroup.alpha = 1f - t;

                if (t >= 1f)
                {
                    m_HasCurrent = false;
                    m_State = 0;
                    if (m_Queue.Count > 0)
                    {
                        StartEntry(m_Queue.Dequeue());
                    }
                    else
                    {
                        HideImmediate();
                    }
                }
            }
        }

        private void StartEntry(AnnouncementEntry entry)
        {
            m_Current = entry;
            m_HasCurrent = true;
            m_State = 1;
            m_StateTimer = 0f;

            if (titleText != null) titleText.text = entry.Title;
            if (subtitleText != null)
            {
                subtitleText.text = entry.Subtitle;
                subtitleText.gameObject.SetActive(!string.IsNullOrWhiteSpace(entry.Subtitle));
            }

            Color accent = entry.Type switch
            {
                BossAnnouncementType.BurstStart => burstStartAccentColor,
                BossAnnouncementType.BurstSuccess => burstSuccessAccentColor,
                BossAnnouncementType.BurstFailure => burstFailureAccentColor,
                BossAnnouncementType.PhaseEvent => phaseEventAccentColor,
                _ => skillAccentColor
            };

            if (accentBarImage != null) accentBarImage.color = accent;
            if (titleText != null) titleText.color = accent;

            if (bannerRect != null)
            {
                bannerRect.gameObject.SetActive(true);
                bannerRect.localScale = new Vector3(1.08f, 1.08f, 1f);
            }
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        private void HideImmediate()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (bannerRect != null) bannerRect.gameObject.SetActive(false);
        }

        private void EnsureUiBuilt()
        {
            if (canvasGroup != null && bannerRect != null && titleText != null) return;

            Canvas hostCanvas = GetComponentInParent<Canvas>();
            if (hostCanvas == null)
            {
                Canvas rootCanvas = gameObject.AddComponent<Canvas>();
                rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                rootCanvas.sortingOrder = 50;
                CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            GameObject panelGo = new GameObject("BossAnnouncementBanner", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            panelGo.transform.SetParent(transform, false);

            bannerRect = panelGo.GetComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0.5f, 1f);
            bannerRect.anchorMax = new Vector2(0.5f, 1f);
            bannerRect.pivot = new Vector2(0.5f, 1f);
            bannerRect.anchoredPosition = new Vector2(0f, -96f);
            bannerRect.sizeDelta = new Vector2(720f, 108f);

            canvasGroup = panelGo.GetComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            backgroundImage = panelGo.GetComponent<Image>();
            backgroundImage.color = new Color(0.06f, 0.07f, 0.1f, 0.82f);
            backgroundImage.raycastTarget = false;

            GameObject barGo = new GameObject("AccentBar", typeof(RectTransform), typeof(Image));
            barGo.transform.SetParent(panelGo.transform, false);
            RectTransform barRect = barGo.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.anchoredPosition = Vector2.zero;
            barRect.sizeDelta = new Vector2(0f, 5f);
            accentBarImage = barGo.GetComponent<Image>();
            accentBarImage.raycastTarget = false;

            GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(panelGo.transform, false);
            RectTransform titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.45f);
            titleRect.anchorMax = new Vector2(1f, 0.92f);
            titleRect.offsetMin = new Vector2(20f, 0f);
            titleRect.offsetMax = new Vector2(-20f, 0f);
            titleText = titleGo.GetComponent<TextMeshProUGUI>();
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontSize = 34f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.raycastTarget = false;

            GameObject subGo = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            subGo.transform.SetParent(panelGo.transform, false);
            RectTransform subRect = subGo.GetComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0f, 0.08f);
            subRect.anchorMax = new Vector2(1f, 0.45f);
            subRect.offsetMin = new Vector2(20f, 0f);
            subRect.offsetMax = new Vector2(-20f, 0f);
            subtitleText = subGo.GetComponent<TextMeshProUGUI>();
            subtitleText.alignment = TextAlignmentOptions.Center;
            subtitleText.fontSize = 22f;
            subtitleText.color = new Color(0.92f, 0.94f, 0.98f, 0.95f);
            subtitleText.raycastTarget = false;
        }
    }
}
