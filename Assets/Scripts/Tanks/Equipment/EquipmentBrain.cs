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
    ///   4. <see cref="UnEquip"/> lam nguoc lai: tru stat, tra lai vi tri.
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

        [Header("Passive")]
        [Tooltip("Hieu ung bi dong dac biet.")]
        [SerializeField] private EquipmentPassive passive = EquipmentPassive.None;

        [Tooltip("Doi luong cua passive (xem mo ta trong EquipmentPassive).")]
        [SerializeField] private float passiveMagnitude;

        // --- Runtime ---
        private TankBrain m_Owner;
        private bool m_IsEquipped;
        private EquipmentSlot m_Slot = EquipmentSlot.None;

        /// <summary>ID tra cuu trong EquipmentDatabase.</summary>
        public string EquipmentId => equipmentId;

        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public TankStats StatBonus => statBonus;
        public EquipmentPassive Passive => passive;
        public float PassiveMagnitude => passiveMagnitude;

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

            // 1) Cong chi so (ModifyRuntimeStats tu keo lai HP/Energy).
            tank.ModifyRuntimeStats(BuildBonus());

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

            // 3) Hieu ung mot lan khi gan.
            ApplyPassiveOnEquip(tank);

            return true;
        }

        /// <summary>
        /// TankBrain goi khi thoa trang bi. Tru stat, tra vi tri cho TankView, bo owner.
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
                // Tru dung bang chieu stat da cong.
                owner.ModifyRuntimeStats(BuildBonus().Negated());
                owner.View?.DetachEquipment(this);
            }

            return true;
        }

        /// <summary>Thoa trang bi, su dung Owner da ghi nho (giam do chi so doc).</summary>
        public bool UnEquip() => UnEquip(m_Owner);

        #endregion

        #region Passive

        /// <summary>Chi so thuc su cong khi Equip = statBonus + phan cong 1 lan cua passive.</summary>
        private TankStats BuildBonus()
        {
            TankStats bonus = statBonus != null ? statBonus.Clone() : TankStats.Zeroed();

            // CooldownSurge chi cong 1 lan luc Equip -> phai nam trong chinh bonus
            // de khi UnEquip thi bi tru sach theo dung chieu.
            if (passive == EquipmentPassive.CooldownSurge) bonus.CooldownReduction += passiveMagnitude;

            return bonus;
        }

        private void ApplyPassiveOnEquip(TankBrain tank)
        {
            if (passive != EquipmentPassive.ShieldOnEquip) return;

            float pct = passiveMagnitude * 0.01f;
            tank.Heal(tank.Stats.MaxHealth * pct);
            tank.GainEnergy(tank.Stats.MaxEnergy * pct);
        }

        /// <summary>
        /// TankBrain goi khi Tank vua gay sat thuong cho doi phuong.
        /// Chi dung cho <see cref="EquipmentPassive.DamageLifesteal"/>.
        /// </summary>
        public void OnOwnerDamageDealt(float damage)
        {
            if (!m_IsEquipped || m_Owner == null) return;
            if (passive != EquipmentPassive.DamageLifesteal) return;
            if (damage <= 0f) return;

            m_Owner.Heal(damage * passiveMagnitude * 0.01f);
        }

        private void Update()
        {
            // Chi tick khi dang that su duoc trang bi (khong dung component.enabled
            // vi TankView co the tam tat GameObject).
            if (!m_IsEquipped || m_Owner == null) return;

            switch (passive)
            {
                case EquipmentPassive.SelfRepair:
                    m_Owner.Heal(m_Owner.Stats.MaxHealth * passiveMagnitude * 0.01f * Time.deltaTime);
                    break;

                case EquipmentPassive.EnergyRegen:
                    m_Owner.GainEnergy(m_Owner.Stats.MaxEnergy * passiveMagnitude * 0.01f * Time.deltaTime);
                    break;
            }
        }

        #endregion

        /// <summary>Lop con -> slot. Do chay 1 lan roi ghi nho.</summary>
        private EquipmentSlot ResolveSlot()
        {
            // GetType() de phan biet chinh xac 3 lop con, khong dung is WeaponBrain
            // vi 3 lop nay dang duoc viet giong het nhau.
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