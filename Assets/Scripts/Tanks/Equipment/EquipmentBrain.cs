using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Lop co so cua moi trang bi Tank.
    ///
    /// Goc cua he thong: trang bi KHONG tu "no" gi vao TankView, ma noi:
    ///   1. TankBrain tao instance tu <see cref="EquipmentDatabase"/>.
    ///   2. TankBrain goi <see cref="Equip"/> - trang bi tu cong stat + noi Owner.
    ///   3. Trang bi goi TankView.AttachEquipment - TankView lo vi tri.
    ///   4. <see cref="UnEquip"/> lam nguoc lai: tru stat, goi ClearPassive, tra vi tri.
    ///
    /// Diem chung cua moi EquipmentBrain: them chi so (statBonus) + co kha nang bi dong.
    /// Kha nang bi dong la TRUU TUONG - lop con ghi de de dinh nghia:
    ///   - <see cref="BuildPassiveBonus"/>: cong them chi so (tinh % tu BaseStats).
    ///   - <see cref="ClearPassive"/>: xoa ky nang bi dong khi trang bi bi thoa.
    ///
    /// Prefab trang bi chi can 1 <see cref="SpriteRenderer"/> de hien thi hinh anh.
    /// </summary>
    [DisallowMultipleComponent]
    public class EquipmentBrain : MonoBehaviour
    {
        [Header("Info")]
        [Tooltip("ID tra cuu trong EquipmentDatabase. De trong -> prefab nay chi dung tay.")]
        [SerializeField] private string equipmentId;

        [Tooltip("Ten hien thi cho UI.")]
        [SerializeField] private string displayName;

        [Tooltip("Anh nho cho UI (khong bat buoc).")]
        [SerializeField] private Sprite icon;

        [Header("Visual")]
        [Tooltip("SpriteRenderer hien thi hinh anh cua trang bi trong TankView.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Stats")]
        [Tooltip("Chi so cong them khi trang bi duoc trang bi. Mac dinh moi chi so = 0.")]
        [SerializeField] private TankStats statBonus = TankStats.Zeroed();

        // --- Runtime ---
        private TankBrain m_Owner;
        private bool m_IsEquipped;
        private EquipmentSlot m_Slot = EquipmentSlot.None;

        // Tong chi so da thuc su cong vao tank luc Equip (statBonus + passive).
        // De UnEquip tru dung chinh so da cong (khong tinh lai).
        private TankStats m_AppliedBonus;

        /// <summary>ID tra cuu trong EquipmentDatabase.</summary>
        public string EquipmentId => equipmentId;

        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public TankStats StatBonus => statBonus;

        /// <summary>Tank dang so huu trang bi nay (null neu chua Equip).</summary>
        public TankBrain Owner => m_Owner;

        public bool IsEquipped => m_IsEquipped;

        /// <summary>
        /// Loai trang bi -> slot se gan vao TankView.
        /// Xac dinh theo LOP dang chay (WeaponBrain -> Weapon...), nen 3 lop con
        /// khong can viet gi them. Them loai moi = them 1 lop con + 1 nhanh trong ResolveSlot().
        /// </summary>
        public EquipmentSlot Slot
        {
            get
            {
                if (m_Slot == EquipmentSlot.None) m_Slot = ResolveSlot();
                return m_Slot;
            }
        }

        #region Equip / UnEquip

        /// <summary>
        /// TankBrain goi khi trang bi duoc gan. Gan owner, cong stat, noi TankView.
        /// KHONG tu gan vi tri - TankView moi la ben quyet dinh trang bi nam o dau.
        /// </summary>
        public bool Equip(TankBrain tank)
        {
            if (tank == null)
            {
                Debug.LogError($"[{nameof(EquipmentBrain)}] Equip null Tank.", this);
                return false;
            }

            if (m_IsEquipped && m_Owner == tank) return true;

            m_Owner = tank;
            m_IsEquipped = true;

            // 1) Cong chi so: statBonus + phan dong tu ky nang bi dong cua lop con.
            // Luon tinh % tu BaseStats (khong dung bonus lam base).
            // Cache lai de UnEquip tru dung chinh so da cong.
            m_AppliedBonus = BuildBonus(tank);
            tank.ModifyBonusStats(m_AppliedBonus);

            // 2) Cho TankView quyet dinh vi tri hien thi.
            TankView view = tank.View;
            if (view == null)
            {
                Debug.LogWarning($"[{nameof(EquipmentBrain)}] Tank '{tank.name}' chua co View, "
                    + "trang bi se khong hien thi.", this);
            }
            else if (!view.AttachEquipment(this))
            {
                Debug.LogWarning($"[{nameof(EquipmentBrain)}] TankView cua Tank '{tank.name}' "
                    + $"khong co slot cho {Slot} -> trang bi khong hien thi.", this);
            }

            return true;
        }

        /// <summary>
        /// TankBrain goi khi thoa trang bi. Tru stat, goi ClearPassive, tra vi tri, bo owner.
        /// Neu <paramref name="tank"/> khong phai owner thi bo qua (tranh thoa nham tren Tank khac).
        /// </summary>
        public bool UnEquip(TankBrain tank)
        {
            if (!m_IsEquipped) return false;

            if (tank != null && tank != m_Owner)
            {
                Debug.LogWarning($"[{nameof(EquipmentBrain)}] '{displayName}' dang thoa tren Tank khac, bo qua.", this);
                return false;
            }

            TankBrain owner = m_Owner;

            m_IsEquipped = false;
            m_Owner = null;

            if (owner != null)
            {
                // Tru dung chinh so da cong (cache tai Equip -> doi xung tuyet doi).
                if (m_AppliedBonus != null)
                {
                    owner.ModifyBonusStats(m_AppliedBonus.Negated());
                    m_AppliedBonus = null;
                }

                owner.View?.DetachEquipment(this);

                // Xoa ky nang bi dong (lop con ghi de de don sach trang thai/event).
                ClearPassive(owner);
            }

            return true;
        }

        /// <summary>Thoa trang bi, su dung Owner da ghi nho (giam do chi so doc).</summary>
        public bool UnEquip() => UnEquip(m_Owner);

        #endregion

        #region Passive

        /// <summary>Chi so thuc su cong khi Equip = statBonus + phan dong tu ky nang bi dong.</summary>
        private TankStats BuildBonus(TankBrain owner)
        {
            TankStats bonus = statBonus != null ? statBonus.Clone() : TankStats.Zeroed();

            TankStats passiveBonus = BuildPassiveBonus(owner);
            if (passiveBonus != null) bonus.AddTo(passiveBonus);

            return bonus;
        }

        /// <summary>
        /// Kha nang bi dong - TRUU TUONG, lop con ghi de de dinh nghia.
        /// Phan chi so tinh % PHAI lay tu owner.BaseStats,
        /// KHONG dung Stats (da cong bonus) hay statBonus lam base.
        /// Mac dinh: khong cong gi.
        /// </summary>
        protected virtual TankStats BuildPassiveBonus(TankBrain owner) => TankStats.Zeroed();

        /// <summary>
        /// Xoa ky nang bi dong khi trang bi bi thoa (doi xung voi BuildPassiveBonus).
        /// Chi so da duoc cache va tu tru o UnEquip - khong can xoa o day.
        /// Lop con ghi de de don sach trang thai / huy dang ky su kien
        /// (vi du dang ky event cua TankBrain khi co them cho ben ngoai dang ky).
        /// </summary>
        protected virtual void ClearPassive(TankBrain owner) { }

        /// <summary>
        /// TankBrain goi khi Tank vua gay sat thuong cho doi phuong.
        /// Hook rong - lop con ghi de de trien khai ky nang bi dong lien quan
        /// (vi du hoi mau khi danh trung).
        /// </summary>
        public virtual void OnOwnerDamageDealt(float damage) { }

        #endregion

        /// <summary>Lop con -> slot. Do chay 1 lan roi ghi nho.</summary>
        private EquipmentSlot ResolveSlot()
        {
            // GetType() de phan biet chinh xac cac lop con theo loai trang bi
            // (lop con ke thua them van dung duoc).
            if (this is WeaponBrain) return EquipmentSlot.Weapon;
            if (this is HullBrain) return EquipmentSlot.Hull;
            if (this is TrackBrain) return EquipmentSlot.Track;

            Debug.LogError($"[{nameof(EquipmentBrain)}] Lop '{GetType().Name}' chua khai bao slot. "
                + "Hay khai bao trong ResolveSlot().", this);

            return EquipmentSlot.None;
        }

        private void Awake()
        {
            // Resolve som de Slot luon hop le ke ca khi chua co Tank nao gan.
            _ = Slot;
        }

        private void OnValidate()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (string.IsNullOrEmpty(displayName)) displayName = name;
            if (statBonus == null) statBonus = TankStats.Zeroed();
        }
    }
}