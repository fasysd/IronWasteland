using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IronWasteland.Tanks;

namespace IronWasteland.UI
{
    /// <summary>
    /// Thanh trang thai cua tank nguoi choi: mau, nang luong, va 4 o ky nang
    /// (mo dan khi dang hoi chieu).
    ///
    /// Vi sao tach khoi StageHUD: StageHUD lo "tien do man choi" (dot quai, thoi gian,
    /// ket qua), con component nay lo "trang thai nhan vat". Hai thu thay doi vi ly do
    /// khac nhau nen de rieng de sua cai nay khong lam vo cai kia.
    ///
    /// Cach dung: gan component nay len Canvas, keo GameController + cac Image/TMP vao.
    /// </summary>
    [DisallowMultipleComponent]
    public class TankStatusHUD : MonoBehaviour
    {
        [Header("Nguồn dữ liệu")]
        [SerializeField] private GameController gameController;

        [Header("Máu")]
        [SerializeField] private Image healthFill;
        [SerializeField] private TMP_Text healthText;

        [Tooltip("Tên xe hiện ở góc trên-trái. Bỏ trống thì HUD không đổi tên.")]
        [SerializeField] private TMP_Text tankNameText;

        [Header("Năng lượng")]
        [SerializeField] private Image energyFill;
        [SerializeField] private TMP_Text energyText;

        [Header("Kỹ năng (tối đa 4)")]
        [SerializeField] private Image[] skillFills = new Image[0];
        [SerializeField] private TMP_Text[] skillLabels = new TMP_Text[0];
        [Tooltip("Chữ số giây hồi chiêu hiện giữa ô (để trống nếu không dùng).")]
        [SerializeField] private TMP_Text[] skillCdTexts = new TMP_Text[0];

        [Header("Icon kỹ năng (lấy từ TankDefinition)")]
        [Tooltip("Ảnh icon của từng ô. Bỏ trống thì HUD quay về tô màu ô như bản cũ.")]
        [SerializeField] private Image[] skillIconImages = new Image[0];

        [Tooltip("Lớp phủ tối hiện khi kỹ năng đang hồi chiêu.")]
        [SerializeField] private GameObject[] skillCooldownOverlays = new GameObject[0];

        [Tooltip("Huy hiệu phím J/K/L/I. Sẽ bị ẩn nếu bộ art của xe đã in sẵn nhãn phím.")]
        [SerializeField] private GameObject[] skillKeyBadges = new GameObject[0];

        [Tooltip("Nhãn phím của từng ô. Theo tài liệu: J = thường, K = chuyên biệt, L = tuỳ chọn 1, I = tuỳ chọn 2.")]
        [SerializeField] private string[] skillKeyLabels = { "J", "K", "L", "I" };

        [Header("Màu hiển thị")]
        [Tooltip("Màu chữ \"SẴN SÀNG\" và màu tô dự phòng khi xe chưa có icon riêng.")]
        [SerializeField] private Color readyColor = new Color32(0xE6, 0xED, 0xF5, 0xFF);
        [SerializeField] private Color cooldownColor = new Color(0.35f, 0.38f, 0.45f, 1f);

        [Header("Panel người chơi (art tách từ Figma)")]
        [Tooltip("Ảnh chân dung trong khung panel góc trên-trái.")]
        [SerializeField] private Image portraitImage;

        [Tooltip("Chữ \"CẤP 80\" cạnh tên xe.")]
        [SerializeField] private TMP_Text levelText;

        [Tooltip("Dòng chỉ số dưới cùng: \"20 ATK / 12 DEF / TỐC ĐỘ 110\".")]
        [SerializeField] private TMP_Text statsText;

        [Tooltip("Tên kỹ năng in dưới mỗi ô (lấy từ TankDefinition.skillNames).")]
        [SerializeField] private TMP_Text[] skillNameTexts = new TMP_Text[0];

        [Tooltip("Trạng thái dưới tên: SẴN SÀNG / HỒI: 3,2 GIÂY / ĐANG CƯỜNG HOÁ.")]
        [SerializeField] private TMP_Text[] skillStateTexts = new TMP_Text[0];

        [Tooltip("Biểu tượng trong suốt đặt trong khung ô kỹ năng (khung do art vẽ).")]
        [SerializeField] private Image[] skillGlyphImages = new Image[0];

        private TankBrain m_Tank;

        private void Awake()
        {
            if (gameController == null) gameController = FindAnyObjectByType<GameController>();
        }

        private void Update()
        {
            if (m_Tank == null)
            {
                m_Tank = ResolveTank();
                if (m_Tank == null) return;
            }

            if (!m_Tank.IsAlive)
            {
                SetBar(healthFill, 0f);
                if (healthText != null) healthText.text = "0 / 0";
                SetBar(energyFill, 0f);
                if (energyText != null) energyText.text = "0 / 0";
                BlankSkills();
                return;
            }

            ApplyDefinitionUi();

            float maxHealth = Mathf.Max(1f, m_Tank.Stats.MaxHealth);
            float health = Mathf.Clamp(m_Tank.CurrentHealth, 0f, maxHealth);
            SetBar(healthFill, health / maxHealth);
            if (healthText != null) healthText.text = $"{Mathf.CeilToInt(health)} / {Mathf.CeilToInt(maxHealth)}";

            float maxEnergy = Mathf.Max(1f, m_Tank.Stats.MaxEnergy);
            float energy = Mathf.Clamp(m_Tank.CurrentEnergy, 0f, maxEnergy);
            SetBar(energyFill, energy / maxEnergy);
            if (energyText != null) energyText.text = $"{Mathf.FloorToInt(energy)} / {Mathf.FloorToInt(maxEnergy)}";

            RefreshStatsLine();

            int count = Mathf.Min(skillFills != null ? skillFills.Length : 0, TankBrain.SkillCount);
            int iconCount = Mathf.Min(skillIconImages != null ? skillIconImages.Length : 0, TankBrain.SkillCount);
            int glyphCount = Mathf.Min(skillGlyphImages != null ? skillGlyphImages.Length : 0, TankBrain.SkillCount);
            int stateCount = Mathf.Min(skillStateTexts != null ? skillStateTexts.Length : 0, TankBrain.SkillCount);
            count = Mathf.Max(count, Mathf.Max(iconCount, Mathf.Max(glyphCount, stateCount)));

            for (int i = 0; i < count; i++)
            {
                float remaining = m_Tank.GetSkillCooldown(i);
                bool cooling = remaining > 0.001f;

                if (skillFills != null && i < skillFills.Length && skillFills[i] != null)
                    skillFills[i].color = cooling ? cooldownColor : readyColor;

                // Icon rieng cua xe: doi anh ready <-> cooldown, lop phu toi bat/tat.
                if (skillIconImages != null && i < skillIconImages.Length && skillIconImages[i] != null)
                {
                    Sprite ready = GetIcon(i, false);
                    Sprite cool = GetIcon(i, true);
                    Sprite target = cooling ? (cool != null ? cool : ready) : ready;

                    if (target != null)
                    {
                        skillIconImages[i].sprite = target;
                        skillIconImages[i].color = Color.white;
                    }
                    else
                    {
                        // Xe chua co art rieng: giu nguyen icon va chi doi mau cho de nhin.
                        skillIconImages[i].color = cooling ? cooldownColor : readyColor;
                    }
                }

                // Bieu tuong trong suot dat trong khung art: giu mau goc, lam toi khi hoi chieu.
                if (skillGlyphImages != null && i < skillGlyphImages.Length && skillGlyphImages[i] != null)
                {
                    Sprite glyph = GetGlyph(i);
                    if (glyph != null)
                    {
                        if (skillGlyphImages[i].sprite != glyph) skillGlyphImages[i].sprite = glyph;
                        if (!skillGlyphImages[i].enabled) skillGlyphImages[i].enabled = true;
                        skillGlyphImages[i].color = cooling ? new Color(0.45f, 0.47f, 0.52f, 0.75f) : Color.white;
                    }
                    else if (skillGlyphImages[i].enabled)
                    {
                        // Icon the art day du: an lop glyph de khong ve trung hinh len tren.
                        skillGlyphImages[i].enabled = false;
                    }
                }

                if (skillCooldownOverlays != null && i < skillCooldownOverlays.Length && skillCooldownOverlays[i] != null)
                {
                    if (skillCooldownOverlays[i].activeSelf != cooling) skillCooldownOverlays[i].SetActive(cooling);
                }

                // Nhan phim luon hien (J/K/L/I), so giay hoi chieu hien o giua neu con.
                if (skillLabels != null && i < skillLabels.Length && skillLabels[i] != null)
                {
                    skillLabels[i].text = skillKeyLabels != null && i < skillKeyLabels.Length
                        ? skillKeyLabels[i]
                        : $"{i + 1}";
                }

                if (skillCdTexts != null && i < skillCdTexts.Length && skillCdTexts[i] != null)
                {
                    skillCdTexts[i].text = cooling ? $"{remaining:0.0}" : string.Empty;
                }

                if (skillNameTexts != null && i < skillNameTexts.Length && skillNameTexts[i] != null)
                {
                    skillNameTexts[i].text = GetSkillName(i);
                }

                if (skillStateTexts != null && i < skillStateTexts.Length && skillStateTexts[i] != null)
                {
                    skillStateTexts[i].text = cooling
                        ? "HỒI: " + remaining.ToString("0.0").Replace('.', ',') + " GIÂY"
                        : "SẴN SÀNG";

                    skillStateTexts[i].color = cooling ? new Color(1f, 0.62f, 0.25f, 1f) : readyColor;
                }
            }
        }

        /// <summary>Tên kỹ năng của slot, lấy từ TankDefinition; thiếu thì dùng "KỸ NĂNG n".</summary>
        private string GetSkillName(int index)
        {
            TankDefinition definition = gameController != null ? gameController.TankDefinition : null;
            string name = definition != null ? definition.GetSkillName(index) : string.Empty;
            return string.IsNullOrEmpty(name) ? $"KỸ NĂNG {index + 1}" : name;
        }

        /// <summary>
        /// Biểu tượng trong suốt đặt trong khung ô. Không gắn thì trả null và lớp glyph bị ẩn,
        /// vì bộ icon mới đã là thẻ art đầy đủ (không cần vẽ thêm biểu tượng lên trên).
        /// </summary>
        private Sprite GetGlyph(int index)
        {
            TankDefinition definition = gameController != null ? gameController.TankDefinition : null;
            return definition != null ? definition.GetSkillGlyph(index) : null;
        }

        /// <summary>Dòng "20 ATK / 12 DEF / TỐC ĐỘ 110" + cấp của xe.</summary>
        private void RefreshStatsLine()
        {
            if (statsText != null && m_Tank.Stats != null)
            {
                statsText.text = $"{Mathf.RoundToInt(m_Tank.Stats.Attack)} ATK   /   " +
                                 $"{Mathf.RoundToInt(m_Tank.Stats.Defense)} DEF   /   " +
                                 $"TỐC ĐỘ {Mathf.RoundToInt(m_Tank.Stats.MoveSpeed)}";
            }

            if (levelText != null && gameController != null)
            {
                levelText.text = $"CẤP {gameController.PlayerLevel}";
            }
        }

        /// <summary>Icon "san sang" hoac "dang hoi chieu" cua slot, lay tu TankDefinition dang choi.</summary>
        private Sprite GetIcon(int index, bool cooldown)
        {
            TankDefinition definition = gameController != null ? gameController.TankDefinition : null;
            if (definition == null) return null;
            return cooldown ? definition.GetSkillIconCooldown(index) : definition.GetSkillIcon(index);
        }

        private TankDefinition m_UiDefinition;

        /// <summary>An huy hieu phim khi bo art cua xe da in san "J/K/L/I" trong anh.</summary>
        private void ApplyDefinitionUi()
        {
            TankDefinition definition = gameController != null ? gameController.TankDefinition : null;
            if (definition == m_UiDefinition) return;
            m_UiDefinition = definition;

            bool showBadges = definition == null || !definition.SkillIconsIncludeLabels;
            if (skillKeyBadges != null)
            {
                for (int i = 0; i < skillKeyBadges.Length; i++)
                {
                    if (skillKeyBadges[i] != null && skillKeyBadges[i].activeSelf != showBadges)
                        skillKeyBadges[i].SetActive(showBadges);
                }
            }

            if (tankNameText != null && definition != null && !string.IsNullOrEmpty(definition.TankName))
            {
                tankNameText.text = definition.TankName.ToUpperInvariant();
            }
        }

        private TankBrain ResolveTank()
        {
            if (gameController != null && gameController.PlayerTank != null) return gameController.PlayerTank;

            if (TankBrain.NetworkSessionActive)
            {
                var nm = Unity.Netcode.NetworkManager.Singleton;
                var playerObj = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
                return playerObj != null ? playerObj.GetComponent<TankBrain>() : null;
            }

            return FindAnyObjectByType<TankBrain>();
        }

        /// <summary>Tank da chet: xam het cac o ky nang va bo nhan phim (tranh hien "LMB/J/K/L" nhu con dung duoc).</summary>
        private void BlankSkills()
        {
            int count = skillFills != null ? skillFills.Length : 0;
            for (int i = 0; i < count; i++)
            {
                if (skillFills[i] != null) skillFills[i].color = cooldownColor;
                if (skillLabels != null && i < skillLabels.Length && skillLabels[i] != null)
                    skillLabels[i].text = string.Empty;
                if (skillCdTexts != null && i < skillCdTexts.Length && skillCdTexts[i] != null)
                    skillCdTexts[i].text = string.Empty;
            }

            if (skillIconImages != null)
            {
                for (int i = 0; i < skillIconImages.Length; i++)
                {
                    if (skillIconImages[i] != null) skillIconImages[i].color = cooldownColor;
                }
            }

            if (skillCooldownOverlays != null)
            {
                for (int i = 0; i < skillCooldownOverlays.Length; i++)
                {
                    if (skillCooldownOverlays[i] != null && !skillCooldownOverlays[i].activeSelf)
                        skillCooldownOverlays[i].SetActive(true);
                }
            }
        }

        private static void SetBar(Image image, float normalized)
        {
            if (image == null) return;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = Mathf.Clamp01(normalized);
        }
    }
}
