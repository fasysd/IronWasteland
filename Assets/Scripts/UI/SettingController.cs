using System;
using TMPro;
using IronWasteland.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IronWasteland.UI
{
    /// <summary>
    /// Điều khiển màn hình Cài đặt hệ thống: bốn thanh âm lượng lưu qua SaveSystem (JSON),
    /// vòng âm lượng tổng vẽ bằng texture sinh tại runtime, các nút quay lại / mặc định / áp dụng.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingController : MonoBehaviour
    {
        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;

        private const string MasterKey = "iw.setting.master";
        private const string SfxKey = "iw.setting.sfx";
        private const string BgmKey = "iw.setting.bgm";
        private const string AnnouncerKey = "iw.setting.announcer";

        private const float RingCenterX = 356f;
        private const float RingCenterY = 492f;
        private const float RingRadius = 115f;
        private const float RingThickness = 11.5f;

        /// <summary>Một thanh trượt âm lượng gồm phần tô màu, núm kéo và số phần trăm.</summary>
        [Serializable]
        private sealed class SliderView
        {
            public Image Fill;
            public RectTransform Knob;
            public TextMeshProUGUI Value;
        }

        [Header("Tham chiếu giao diện")]
        [Tooltip("Canvas của màn hình, dùng để đổi toạ độ chuột sang hệ thiết kế 1920x1080.")]
        [SerializeField] private Canvas m_Canvas;

        [Tooltip("Số phần trăm âm lượng tổng ở giữa vòng tròn.")]
        [SerializeField] private TextMeshProUGUI m_MasterValue;

        [Tooltip("Dấu phần trăm màu cam cạnh số âm lượng tổng.")]
        [SerializeField] private TextMeshProUGUI m_MasterPercent;

        [Tooltip("Ảnh vẽ bốn cung cam của vòng âm lượng tổng.")]
        [SerializeField] private Image m_RingArc;

        [Tooltip("Ba thanh trượt Hiệu ứng âm thanh / Nhạc nền / Giọng thông báo.")]
        [SerializeField] private SliderView[] m_Sliders = new SliderView[3];

        [Header("Điều hướng")]
        [Tooltip("Scene sẽ mở khi bấm QUAY LẠI, MẶC ĐỊNH giữ nguyên màn hình.")]
        [SerializeField] private string m_BackSceneName = "MainMenu";

        private static readonly int[] DefaultValues = { 78, 65, 45, 80 };

        private static readonly Color32[] SliderColors =
        {
            new Color32(0xFF, 0x7A, 0x18, 0xFF),
            new Color32(0x2F, 0xE8, 0xE0, 0xFF),
            new Color32(0x8A, 0x4B, 0xF0, 0xFF)
        };

        private readonly int[] m_Values = new int[3];
        private readonly float[] m_SegmentStarts = { 55f, 145f, 235f, 325f };

        private int m_MasterValueNumber;
        private int m_LastRingValue = -1;
        private Texture2D m_RingTexture;
        private Sprite m_RingSprite;

        private Vector2 m_PointerScreen;
        private bool m_HasGuiPointer;
        private bool m_GuiPointerDown;
        private bool m_GuiPointerUp;
        private bool m_DraggingRing;
        private int m_DraggingSlider = -1;
        private bool m_IsLeaving;

        private Rect BackArea => new Rect(72f, 100f, 158f, 48f);
        private Rect ResetArea => new Rect(1292f, 890f, 200f, 86f);
        private Rect ApplyArea => new Rect(1504f, 890f, 344f, 86f);

        private void Awake()
        {
            LoadValues();
            RefreshAll();
        }

        private void OnDestroy()
        {
            if (m_RingSprite != null)
            {
                Destroy(m_RingSprite);
            }

            if (m_RingTexture != null)
            {
                Destroy(m_RingTexture);
            }
        }

        private void Update()
        {
            if (m_IsLeaving)
            {
                return;
            }

            Vector2 designPointer = DesignPointerPosition(PointerPosition);

            if (m_GuiPointerDown)
            {
                m_GuiPointerDown = false;
                BeginDrag(designPointer);
            }

            if (m_GuiPointerUp)
            {
                m_GuiPointerUp = false;
                bool wasDragging = m_DraggingRing || m_DraggingSlider >= 0;
                m_DraggingRing = false;
                m_DraggingSlider = -1;
                if (wasDragging)
                {
                    SaveValues();
                }
            }

            if (m_DraggingRing)
            {
                SetMasterFromPoint(designPointer);
            }
            else if (m_DraggingSlider >= 0)
            {
                SetSliderFromPoint(m_DraggingSlider, designPointer);
            }
        }

        private void OnGUI()
        {
            Event current = Event.current;
            if (current == null)
            {
                return;
            }

            switch (current.type)
            {
                case EventType.MouseMove:
                case EventType.MouseDrag:
                    m_PointerScreen = ToScreenPoint(current.mousePosition);
                    m_HasGuiPointer = true;
                    break;
                case EventType.MouseDown:
                    if (current.button == 0)
                    {
                        m_PointerScreen = ToScreenPoint(current.mousePosition);
                        m_HasGuiPointer = true;
                        m_GuiPointerDown = true;
                    }

                    break;
                case EventType.MouseUp:
                    if (current.button == 0)
                    {
                        m_PointerScreen = ToScreenPoint(current.mousePosition);
                        m_HasGuiPointer = true;
                        m_GuiPointerUp = true;
                    }

                    break;
                case EventType.KeyDown:
                    if (current.keyCode == KeyCode.Escape && !m_IsLeaving)
                    {
                        SaveValues();
                        LoadScene(m_BackSceneName);
                    }

                    break;
            }
        }

        private static Vector2 ToScreenPoint(Vector2 guiPosition)
        {
            return new Vector2(guiPosition.x, Screen.height - guiPosition.y);
        }

        private Vector2 DesignPointerPosition(Vector2 screenPosition)
        {
            float scale = m_Canvas != null ? m_Canvas.scaleFactor : 1f;
            if (scale <= 0f)
            {
                scale = 1f;
            }

            return new Vector2(screenPosition.x / scale, (Screen.height - screenPosition.y) / scale);
        }

        private Vector2 PointerPosition
        {
            get
            {
                if (m_HasGuiPointer)
                {
                    return m_PointerScreen;
                }

#if ENABLE_INPUT_SYSTEM
                UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
                return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#else
                return Input.mousePosition;
#endif
            }
        }

        /// <summary>Bắt đầu kéo vòng âm lượng, một thanh trượt hoặc bấm nút.</summary>
        private void BeginDrag(Vector2 point)
        {
            float ringDistance = Vector2.Distance(point, new Vector2(RingCenterX, RingCenterY));
            if (ringDistance <= RingRadius + 32f && ringDistance >= 60f)
            {
                m_DraggingRing = true;
                m_DraggingSlider = -1;
                SetMasterFromPoint(point);
                return;
            }

            for (int i = 0; i < m_Sliders.Length; i++)
            {
                float top = 411f + 122f * i;
                Rect track = new Rect(680f, top - 22f, 570f, 44f);
                if (track.Contains(point))
                {
                    m_DraggingSlider = i;
                    m_DraggingRing = false;
                    SetSliderFromPoint(i, point);
                    return;
                }
            }

            if (BackArea.Contains(point) || ApplyArea.Contains(point))
            {
                SaveValues();
                LoadScene(m_BackSceneName);
                return;
            }

            if (ResetArea.Contains(point))
            {
                ResetToDefaults();
            }
        }

        /// <summary>Đổi âm lượng tổng theo góc chuột tính từ đỉnh vòng tròn, chiều kim đồng hồ.</summary>
        private void SetMasterFromPoint(Vector2 point)
        {
            float dx = point.x - RingCenterX;
            float dyUp = RingCenterY - point.y;
            float angle = Mathf.Atan2(dyUp, dx) * Mathf.Rad2Deg;
            angle = Mathf.Repeat(angle, 360f);
            float fraction = Mathf.Repeat(90f - angle, 360f) / 360f;
            int value = Mathf.Clamp(Mathf.RoundToInt(fraction * 100f), 0, 100);
            if (value == m_MasterValueNumber)
            {
                return;
            }

            m_MasterValueNumber = value;
            RefreshMaster();
        }

        private void SetSliderFromPoint(int index, Vector2 point)
        {
            if (index < 0 || index >= m_Values.Length || index >= m_Sliders.Length)
            {
                return;
            }

            float fraction = Mathf.Clamp01((point.x - 700f) / 530f);
            int value = Mathf.Clamp(Mathf.RoundToInt(fraction * 100f), 0, 100);
            if (value == m_Values[index])
            {
                return;
            }

            m_Values[index] = value;
            RefreshSlider(index);
        }

        private void ResetToDefaults()
        {
            m_MasterValueNumber = DefaultValues[0];
            for (int i = 0; i < m_Values.Length; i++)
            {
                m_Values[i] = DefaultValues[i + 1];
            }

            SaveValues();
            RefreshAll();
        }

        /// <summary>Nạp bốn giá trị âm lượng đã lưu từ SaveSystem, thiếu thì dùng mặc định của thiết kế.</summary>
        private void LoadValues()
        {
            if (SaveSystem.Instance != null)
            {
                var settings = SaveSystem.Instance.Settings;
                m_MasterValueNumber = Mathf.Clamp(settings.MasterVolume, 0, 100);
                m_Values[0] = Mathf.Clamp(settings.SfxVolume, 0, 100);
                m_Values[1] = Mathf.Clamp(settings.BgmVolume, 0, 100);
                m_Values[2] = Mathf.Clamp(settings.AnnouncerVolume, 0, 100);
            }
            else
            {
                m_MasterValueNumber = DefaultValues[0];
                m_Values[0] = DefaultValues[1];
                m_Values[1] = DefaultValues[2];
                m_Values[2] = DefaultValues[3];
            }
            ApplyVolumeToAudio();
        }

        private void SaveValues()
        {
            if (SaveSystem.Instance != null)
            {
                SaveSystem.Instance.SetSettings(m_MasterValueNumber, m_Values[0], m_Values[1], m_Values[2]);
            }
            ApplyVolumeToAudio();
        }

        /// <summary>Chỉ âm lượng tổng tác động thật lên AudioListener, ba kênh còn lại chờ hệ âm thanh.</summary>
        private void ApplyVolumeToAudio()
        {
            AudioListener.volume = m_MasterValueNumber / 100f;
        }

        private void RefreshAll()
        {
            RefreshMaster();
            for (int i = 0; i < m_Values.Length; i++)
            {
                RefreshSlider(i);
            }
        }

        private void RefreshMaster()
        {
            if (m_MasterValue != null)
            {
                m_MasterValue.text = m_MasterValueNumber.ToString();
            }

            if (m_MasterPercent != null)
            {
                m_MasterPercent.text = "%";
            }

            ApplyVolumeToAudio();
            if (m_MasterValueNumber != m_LastRingValue)
            {
                m_LastRingValue = m_MasterValueNumber;
                RebuildRing();
            }
        }

        private void RefreshSlider(int index)
        {
            if (index < 0 || index >= m_Sliders.Length || m_Sliders[index] == null)
            {
                return;
            }

            SliderView slider = m_Sliders[index];
            float fraction = m_Values[index] / 100f;
            if (slider.Fill != null)
            {
                slider.Fill.fillAmount = fraction;
                slider.Fill.color = SliderColors[index];
            }

            if (slider.Knob != null)
            {
                slider.Knob.anchoredPosition = new Vector2(700f + 530f * fraction, DesignHeight - (415f + 122f * index));
            }

            if (slider.Value != null)
            {
                slider.Value.text = m_Values[index].ToString();
            }
        }

        /// <summary>Vẽ lại bốn cung của vòng âm lượng tổng theo giá trị hiện tại.</summary>
        private void RebuildRing()
        {
            const int size = 240;
            if (m_RingTexture == null)
            {
                m_RingTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                m_RingTexture.name = "SettingMasterRing";
                m_RingTexture.filterMode = FilterMode.Bilinear;
                m_RingTexture.wrapMode = TextureWrapMode.Clamp;
            }

            Color32 bottom = SliderColors[0];
            Color32 top = new Color32(0xFF, 0xB3, 0x47, 0xFF);
            Color32[] pixels = new Color32[size * size];
            float center = size * 0.5f;
            float halfThickness = RingThickness * 0.5f;
            float span = 0.9f * m_MasterValueNumber;
            float anglePerPixel = 180f / (Mathf.PI * RingRadius);

            for (int y = 0; y < size; y++)
            {
                float dy = y + 0.5f - center;
                float gradient = Mathf.Clamp01((y - 5f) / 230f);
                Color color = Color32.Lerp(bottom, top, gradient);
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float band = Mathf.Clamp01(halfThickness + 0.75f - Mathf.Abs(distance - RingRadius));
                    if (band <= 0f || span <= 0f)
                    {
                        continue;
                    }

                    float angle = Mathf.Repeat(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg, 360f);
                    float coverage = 0f;
                    for (int s = 0; s < m_SegmentStarts.Length; s++)
                    {
                        float relative = Mathf.Repeat(angle - m_SegmentStarts[s], 360f);
                        if (relative > span)
                        {
                            continue;
                        }

                        float edge = Mathf.Min(relative, span - relative) * anglePerPixel;
                        coverage = Mathf.Max(coverage, Mathf.Clamp01(edge + 0.5f));
                    }

                    if (coverage <= 0f)
                    {
                        continue;
                    }

                    color.a = band * coverage;
                    pixels[y * size + x] = color;
                }
            }

            m_RingTexture.SetPixels32(pixels);
            m_RingTexture.Apply(false);
            if (m_RingArc != null)
            {
                if (m_RingSprite != null)
                {
                    Destroy(m_RingSprite);
                }

                m_RingSprite = Sprite.Create(m_RingTexture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
                m_RingArc.sprite = m_RingSprite;
                m_RingArc.color = Color.white;
            }
        }

        private void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning("[Setting] Scene chưa có trong Build Settings: " + sceneName);
                m_IsLeaving = false;
                return;
            }

            m_IsLeaving = true;
            SceneManager.LoadScene(sceneName);
        }
    }
}
