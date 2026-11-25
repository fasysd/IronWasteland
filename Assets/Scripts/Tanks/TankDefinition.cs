using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Mo ta 1 loai Tank: ten, mo ta, 2 moc chi so co ban (Cap 1 va Cap 90), va tankPrefab chua san TankView.
    /// Theo GDD: moi xe co 2 moc chi so co ban o cap 1 va cap 90, cac cap con lai noi suy tuyen tinh.
    /// Tank Prefab asset chua luon TankView trong hierarchy con, khong can ViewPrefabs hay IdView rieng.
    /// </summary>
    [CreateAssetMenu(menuName = "IronWasteland/Tank/Tank Definition", fileName = "TankDefinition")]
    public class TankDefinition : ScriptableObject
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 90;

        [Header("Info")]
        [SerializeField] private string tankName = "Tank";
        [TextArea(2, 4)]
        [SerializeField] private string description;

        [Header("Roster (Garage)")]
        [Tooltip("Vai trò chính hiển thị dưới tên xe trong Garage (vd TANKER, HỖ TRỢ).")]
        [SerializeField] private string rolePrimary = string.Empty;

        [Tooltip("Vai trò phụ; để trống nếu xe chỉ có một vai trò.")]
        [SerializeField] private string roleSecondary = string.Empty;

        [Tooltip("Mã ải cần mở để sử dụng xe (vd 2-5). Để trống nghĩa là mở sẵn từ đầu.")]
        [SerializeField] private string unlockStageCode = string.Empty;

        [Tooltip("Ảnh đại diện dùng trong danh sách xe của Garage (để trống thì hàng chỉ hiện chữ).")]
        [SerializeField] private Sprite rosterIcon;

        [Tooltip("Màu chủ đề của xe trong danh sách Garage.")]
        [SerializeField] private Color rosterAccent = new Color32(0xFF, 0x7A, 0x18, 0xFF);

        [Tooltip("Ảnh chân dung lớn của xe trong khu trưng bày Garage.")]
        [SerializeField] private Sprite portrait;

        [Tooltip("Độ dời ảnh chân dung trong khung tròn Garage để khối tank cân giữa khung (đơn vị pixel thiết kế, dương = dịch sang phải / lên trên).")]
        [SerializeField] private Vector2 portraitOffset = Vector2.zero;

        [Tooltip("Ảnh trưng bày của xe ở màn hình chính (khung 518x518, nền trong suốt).")]
        [SerializeField] private Sprite menuPortrait;

        [Tooltip("Mô tả ngắn 4 kỹ năng, đúng thứ tự slot 1..4.")]
        [SerializeField] private string[] skillDescriptions = new string[4];

        [Tooltip("Cấp khởi điểm của xe khi người chơi chưa có tiến trình riêng (mặc định 1).")]
        [SerializeField] private int startingLevel = 1;

        [Header("Stats Milestones (Level 1 & 90)")]
        [Tooltip("Chi so co ban tai Cap 1 (Level 1).")]
        [SerializeField] private TankStats baseStatsLevel1 = new TankStats();

        [Tooltip("Chi so co ban tai Cap 90 (Level 90).")]
        [SerializeField] private TankStats baseStatsLevel90 = new TankStats();

        [Header("Prefabs")]
        [Tooltip("Prefab chua TankBrain va TankView con ben trong.")]
        [SerializeField] private TankBrain tankPrefab;

        [Header("Equipment")]
        [Tooltip("Kho du lieu dung de tra cuu prefab trang bi / phu kien.")]
        [SerializeField] private EquipmentDatabase equipmentDatabase;

        [Header("Context")]
        [Tooltip("Context khoi tao cho TankBrain khi CreateTank.")]
        [SerializeField] private TankBrainContext context;

        [Header("UI - Skill Icons")]
        [Tooltip("Icon 4 ky nang theo dung thu tu slot: 0 = thuong (J), 1 = chuyen biet (K), 2 = tuy chon 1 (L), 3 = tuy chon 2 (I).")]
        [SerializeField] private Sprite[] skillIcons = new Sprite[0];

        [Tooltip("Anh bien the khi ky nang dang hoi chieu (cung thu tu). Bo trong thi HUD chi lam toi icon thuong.")]
        [SerializeField] private Sprite[] skillIconsCooldown = new Sprite[0];

        [Tooltip("Bat khi anh icon da co san nhan phim + ten ky nang (bo art Black Shadow); HUD se khong ve them huy hieu phim nua.")]
        [SerializeField] private bool skillIconsIncludeLabels;

        [Header("UI - Ten + bieu tuong ky nang (HUD moi)")]
        [Tooltip("Ten 4 ky nang theo dung thu tu slot, hien duoi bieu tuong trong panel KY NANG TRANG BI.")]
        [SerializeField] private string[] skillNames = new string[0];

        [Tooltip("Bieu tuong trong suot (chi con hinh) de HUD tu ve khung + ten + trang thai ben duoi.")]
        [SerializeField] private Sprite[] skillGlyphs = new Sprite[0];

        public bool SkillIconsIncludeLabels => skillIconsIncludeLabels;

        /// <summary>Ten ky nang cua slot (rong neu chua khai bao).</summary>
        public string GetSkillName(int index)
        {
            if (skillNames == null || index < 0 || index >= skillNames.Length) return string.Empty;
            return skillNames[index];
        }

        /// <summary>Bieu tuong trong suot (khong khung, khong chu) cua slot ky nang.</summary>
        public Sprite GetSkillGlyph(int index)
        {
            if (skillGlyphs == null || index < 0 || index >= skillGlyphs.Length) return null;
            return skillGlyphs[index];
        }

        public string TankName => tankName;
        public string Description => description;

        /// <summary>Vai trò chính hiển thị trong danh sách Garage.</summary>
        public string RolePrimary => rolePrimary;

        /// <summary>Vai trò phụ (rỗng nếu không có).</summary>
        public string RoleSecondary => roleSecondary;

        /// <summary>Mã ải mở khoá xe (rỗng nếu mở sẵn).</summary>
        public string UnlockStageCode => unlockStageCode;

        /// <summary>Ảnh đại diện của xe trong danh sách Garage (có thể null).</summary>
        public Sprite RosterIcon => rosterIcon;

        /// <summary>Màu chủ đề của xe, dùng cho vạch nhấn và thanh cấp trong danh sách Garage.</summary>
        public Color RosterAccent => rosterAccent;

        /// <summary>Ảnh chân dung lớn của xe ở khu trưng bày Garage (có thể null).</summary>
        public Sprite Portrait => portrait;

        /// <summary>Độ dời ảnh chân dung để khối tank cân giữa khung tròn (pixel thiết kế).</summary>
        public Vector2 PortraitOffset => portraitOffset;

        /// <summary>Ảnh trưng bày dùng riêng cho Main Menu.</summary>
        public Sprite MenuPortrait => menuPortrait;

        /// <summary>Mô tả ngắn của kỹ năng theo slot (rỗng nếu chưa khai báo).</summary>
        public string GetSkillDescription(int index)
        {
            if (skillDescriptions == null || index < 0 || index >= skillDescriptions.Length) return string.Empty;
            return skillDescriptions[index];
        }

        /// <summary>Cấp khởi điểm của xe (đã kẹp trong khoảng 1..90).</summary>
        public int StartingLevel => Mathf.Clamp(startingLevel, MinLevel, MaxLevel);
        public TankStats BaseStatsLevel1 => baseStatsLevel1;
        public TankStats BaseStatsLevel90 => baseStatsLevel90;
        public TankBrain TankPrefab => tankPrefab;
        public EquipmentDatabase EquipmentDatabase => equipmentDatabase;
        public int SkillIconCount => skillIcons != null ? skillIcons.Length : 0;

        /// <summary>Icon "san sang" cua slot ky nang (null neu chua gan).</summary>
        public Sprite GetSkillIcon(int index)
        {
            if (skillIcons == null || index < 0 || index >= skillIcons.Length) return null;
            return skillIcons[index];
        }

        /// <summary>Icon "dang hoi chieu" cua slot ky nang (null neu chua gan).</summary>
        public Sprite GetSkillIconCooldown(int index)
        {
            if (skillIconsCooldown == null || index < 0 || index >= skillIconsCooldown.Length) return null;
            return skillIconsCooldown[index];
        }

        /// <summary>
        /// Tao Tank moi voi context chi dinh.
        /// </summary>
        public TankBrain CreateTank(TankBrainContext context)
        {
            if (tankPrefab == null)
            {
                Debug.LogError($"[{nameof(TankDefinition)}] Chua gan TankPrefab.", this);
                return null;
            }

            TankBrain brain = Instantiate(tankPrefab, Vector3.zero, Quaternion.identity, null);
            InitializeTank(brain, context);
            return brain;
        }

        /// <summary>
        /// Khởi tạo đầy đủ chỉ số và phụ kiện cho một brain đã được tạo sẵn.
        /// Dùng cho spawn qua NGO: prefab do mạng sinh ra trước, sau đó mới Initialize.
        /// </summary>
        public void InitializeTank(TankBrain brain, TankBrainContext context)
        {
            if (brain == null) return;

            brain.Initialize(context, GetStatsForLevel(context.Level));

            // Trang bi 1 phu kien duy nhat theo context.EquipmentId
            if (context.EquipmentId >= 0 && equipmentDatabase != null)
            {
                var accessoryPrefab = equipmentDatabase.GetPrefab(context.EquipmentId);
                if (accessoryPrefab != null)
                {
                    brain.EquipAccessory(accessoryPrefab);
                }
            }
        }

        /// <summary>
        /// Tinh chi so co ban tai mot level (1..90) bang noi suy tuyen tinh tu Cap 1 va Cap 90:
        /// Stat(L) = Stat(1) + (Stat(90) - Stat(1)) * (L - 1) / (90 - 1).
        /// </summary>
        public TankStats GetStatsForLevel(int level)
        {
            level = Mathf.Clamp(level, MinLevel, MaxLevel);

            if (baseStatsLevel1 == null || baseStatsLevel90 == null)
            {
                Debug.LogWarning($"[{nameof(TankDefinition)}] Thieu du lieu chi so Cap 1 hoac Cap 90.", this);
                return new TankStats();
            }

            float t = (float)(level - MinLevel) / (MaxLevel - MinLevel); // (level - 1) / 89f
            return Interpolate(baseStatsLevel1, baseStatsLevel90, t);
        }

        private static TankStats Interpolate(TankStats a, TankStats b, float t)
        {
            TankStats r = a.Clone();
            r.Attack = Mathf.Lerp(a.Attack, b.Attack, t);
            r.MaxHealth = Mathf.Lerp(a.MaxHealth, b.MaxHealth, t);
            r.MaxEnergy = Mathf.Lerp(a.MaxEnergy, b.MaxEnergy, t);
            r.Defense = Mathf.Lerp(a.Defense, b.Defense, t);
            r.MoveSpeed = Mathf.Lerp(a.MoveSpeed, b.MoveSpeed, t);
            r.CooldownReduction = Mathf.Clamp(Mathf.Lerp(a.CooldownReduction, b.CooldownReduction, t), 0f, TankStats.MaxCooldownReduction);
            r.DamageRate = Mathf.Lerp(a.DamageRate, b.DamageRate, t);
            r.ArmorPenetration = Mathf.Clamp(Mathf.Lerp(a.ArmorPenetration, b.ArmorPenetration, t), 0f, TankStats.MaxArmorPenetration);
            return r;
        }
    }
}
