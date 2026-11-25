using IronWasteland.Core;
using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Lop co so cua mot Phu kien (Accessory / Equipment) cho xe Tank.
    /// Theo Tai lieu thiet ke Game (GDD):
    /// - Moi xe Tank co 1 o de mang Phu kien.
    /// - Cung cap Chi so (cong phang va/hoac cong phan tram tinh theo BaseStats).
    /// - Cung cap Ky nang bi dong (Passive Skill) tu dong kich hoat khi thoa man dieu kien.
    /// </summary>
    [DisallowMultipleComponent]
    public class EquipmentBrain : MonoBehaviour
    {
        [Header("Info")]
        [Tooltip("ID tra cuu trong EquipmentDatabase.")]
        [SerializeField] private string equipmentId;

        [Tooltip("Ten hien thi cho UI.")]
        [SerializeField] private string displayName;

        [TextArea(2, 4)]
        [Tooltip("Mo ta hieu qua cua phu kien.")]
        [SerializeField] private string description;

        [Tooltip("Anh bieu tuong cho UI.")]
        [SerializeField] private Sprite icon;

        // Runtime
        private TankBrain m_Owner;
        private bool m_IsEquipped;

        public string EquipmentId => equipmentId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public TankBrain Owner => m_Owner;
        public bool IsEquipped => m_IsEquipped;

        /// <summary>
        /// Gan phu kien vao TankBrain (logic + chi so + passive).
        /// </summary>
        public virtual bool Equip(TankBrain tank)
        {
            if (tank == null)
            {
                Debug.LogError($"[{nameof(EquipmentBrain)}] Equip null Tank.", this);
                return false;
            }

            if (m_IsEquipped && m_Owner == tank) return true;

            m_Owner = tank;
            m_IsEquipped = true;

            OnEquipped(tank);
            return true;
        }

        /// <summary>
        /// Thao phu kien khoi TankBrain.
        /// </summary>
        public virtual bool UnEquip(TankBrain tank)
        {
            if (!m_IsEquipped || m_Owner == null) return false;

            TankBrain prevOwner = m_Owner;
            m_IsEquipped = false;
            m_Owner = null;

            OnUnequipped(prevOwner);
            return true;
        }

        #region Passive Hooks (Lop con ghi de de cai dat ky nang bi dong)

        /// <summary>Goi ngay sau khi phu kien duoc trang bi cho xe.</summary>
        protected virtual void OnEquipped(TankBrain owner) { }

        /// <summary>Goi ngay khi phu kien bi thao ra khoi xe.</summary>
        protected virtual void OnUnequipped(TankBrain owner) { }

        /// <summary>Goi moi frame tu TankBrain (neu can tick passive timer).</summary>
        public virtual void OnBrainTick(float deltaTime) { }

        /// <summary>Goi khi Tank gay sat thuong cho mot muc tieu.</summary>
        public virtual void OnOwnerDealtDamage(DamageInfo info) { }

        /// <summary>Goi khi Tank nhan sat thuong tu nguoi khac.</summary>
        public virtual void OnOwnerTookDamage(DamageInfo info) { }

        /// <summary>Goi khi Tank thi trien thanh cong 1 ky nang (0..3).</summary>
        public virtual void OnOwnerUsedSkill(int skillIndex) { }

        /// <summary>Gọi khi Tank được hồi máu (Heal).</summary>
        public virtual void OnOwnerHealed(float amount) { }

        /// <summary>Gọi khi Tank nhận năng lượng từ kỹ năng (skillIndex = 0 là Skill thường).</summary>
        public virtual void OnOwnerGainedEnergy(float amount, int skillIndex) { }

        /// <summary>Hệ số nhân sát thương nhận vào (dùng cho phụ kiện giảm sát thương theo tầng).</summary>
        public virtual float GetIncomingDamageMultiplier(DamageInfo info) => 1f;

        /// <summary>Gọi khi Tank hồi máu hoặc tạo khiên cho một đồng minh.</summary>
        public virtual void OnOwnerSupportedAlly(TankBrain ally) { }

        #endregion
    }
}
