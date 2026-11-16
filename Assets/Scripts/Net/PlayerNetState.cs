using IronWasteland.Tanks;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Đồng bộ trạng thái cơ bản của xe từ host xuống client: máu, năng lượng, hồi chiêu.
    /// Giai đoạn 3 của plan: chỉ host ghi giá trị; client chỉ đọc để hiển thị và dự đoán thanh hồi chiêu.
    /// </summary>
    [RequireComponent(typeof(TankBrain))]
    public class PlayerNetState : NetworkBehaviour
    {
        private const float SyncInterval = 0.1f;
        private const float ValueEpsilon = 0.1f;
        private const float CooldownEpsilon = 0.05f;
        private const float AimEpsilon = 1f;

        private readonly NetworkVariable<float> m_Health = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> m_Energy = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<CooldownState> m_Cooldowns = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<NetLoadout> m_Loadout = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Góc nòng (độ) host ghi xuống để proxy xoay nòng theo đúng hướng ngắm.</summary>
        private readonly NetworkVariable<float> m_AimAngle = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly float[] m_ClientCooldownRemaining = new float[TankBrain.SkillCount];
        private readonly float[] m_ClientCooldownReceivedAt = new float[TankBrain.SkillCount];

        private TankBrain m_Brain;
        private bool m_LoadoutApplied;
        private float m_SyncTimer;
        private float m_SentHealth;
        private float m_SentEnergy;
        private float m_SentAimAngle;
        private CooldownState m_SentCooldowns;

        public override void OnNetworkSpawn()
        {
            m_Brain = GetComponent<TankBrain>();

            m_Brain.IsLocallyControlled = IsOwner;
            m_Brain.IsServerSimulated = IsServer;

            m_Health.OnValueChanged += HandleHealthChanged;
            m_Energy.OnValueChanged += HandleEnergyChanged;
            m_Cooldowns.OnValueChanged += HandleCooldownsChanged;
            m_Loadout.OnValueChanged += HandleLoadoutChanged;

            if (IsServer)
            {
                m_LoadoutApplied = true;
                m_SentHealth = m_Brain.CurrentHealth;
                m_SentEnergy = m_Brain.CurrentEnergy;
                m_Health.Value = m_SentHealth;
                m_Energy.Value = m_SentEnergy;
                m_SentCooldowns = ReadBrainCooldowns();
                m_Cooldowns.Value = m_SentCooldowns;
                m_SentAimAngle = ReadAimAngle();
                m_AimAngle.Value = m_SentAimAngle;
            }
            else
            {
                ApplyLoadout(m_Loadout.Value);
                ApplyHealth(m_Health.Value);
                ApplyEnergy(m_Energy.Value);
                ApplyCooldowns(m_Cooldowns.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            m_Health.OnValueChanged -= HandleHealthChanged;
            m_Energy.OnValueChanged -= HandleEnergyChanged;
            m_Cooldowns.OnValueChanged -= HandleCooldownsChanged;
            m_Loadout.OnValueChanged -= HandleLoadoutChanged;
        }

        /// <summary>Xe mà state này gắn vào (null khi chưa spawn).</summary>
        public TankBrain Brain => m_Brain;

        /// <summary>Máu hiện tại: host đọc trực tiếp từ brain, client đọc bản đã áp từ host.</summary>
        public float Health => m_Brain != null ? m_Brain.CurrentHealth : m_Health.Value;

        /// <summary>Máu tối đa theo chỉ số đã khởi tạo.</summary>
        public float MaxHealth => m_Brain != null && m_Brain.Stats != null ? m_Brain.Stats.MaxHealth : 0f;

        /// <summary>Năng lượng hiện tại.</summary>
        public float Energy => m_Brain != null ? m_Brain.CurrentEnergy : m_Energy.Value;

        /// <summary>Năng lượng tối đa.</summary>
        public float MaxEnergy => m_Brain != null && m_Brain.Stats != null ? m_Brain.Stats.MaxEnergy : 0f;

        /// <summary>Góc nòng hiện tại (độ) — host đọc từ look target, client đọc bản đã nhận.</summary>
        public float AimAngle => m_AimAngle.Value;

        /// <summary>Client đã áp xong loadout chưa (host luôn true).</summary>
        public bool LoadoutApplied => m_LoadoutApplied;

        /// <summary>Host ghi lựa chọn xe xuống mọi máy; client dùng để khởi tạo đúng chỉ số.</summary>
        public void SetServerLoadout(NetLoadout loadout)
        {
            if (!IsServer || !IsSpawned) return;
            m_Loadout.Value = loadout;
        }

        private void Update()
        {
            if (!IsServer || m_Brain == null) return;

            m_SyncTimer += Time.deltaTime;
            if (m_SyncTimer < SyncInterval) return;
            m_SyncTimer = 0f;

            float health = m_Brain.CurrentHealth;
            if (Mathf.Abs(health - m_SentHealth) >= ValueEpsilon)
            {
                m_SentHealth = health;
                m_Health.Value = health;
            }

            float energy = m_Brain.CurrentEnergy;
            if (Mathf.Abs(energy - m_SentEnergy) >= ValueEpsilon)
            {
                m_SentEnergy = energy;
                m_Energy.Value = energy;
            }

            CooldownState current = ReadBrainCooldowns();
            if (HasCooldownIncreased(current, m_SentCooldowns))
            {
                m_SentCooldowns = current;
                m_Cooldowns.Value = current;
            }

            float aim = ReadAimAngle();
            if (Mathf.Abs(Mathf.DeltaAngle(m_SentAimAngle, aim)) >= AimEpsilon)
            {
                m_SentAimAngle = aim;
                m_AimAngle.Value = aim;
            }
        }

        /// <summary>Hướng nòng hiện tại của xe trên host, quy về độ.</summary>
        private float ReadAimAngle()
        {
            if (m_Brain == null) return 0f;

            if (m_Brain.HasLookTarget)
            {
                Vector2 direction = m_Brain.LookTarget - (Vector2)m_Brain.transform.position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                }
            }

            // Chưa có look target: giữ hướng thân hiện tại để proxy không giật về 0 độ.
            return m_Brain.View != null ? m_Brain.View.BodyPivot.eulerAngles.z : 0f;
        }

        /// <summary>
        /// Thanh hồi chiêu phía client: host trả số thật, client tự trừ thời gian đã trôi qua.
        /// </summary>
        public float GetRemainingCooldown(int index)
        {
            if (index < 0 || index >= TankBrain.SkillCount) return 0f;
            if (m_Brain == null) return 0f;
            if (IsServer) return m_Brain.GetSkillCooldown(index);

            float elapsed = Time.time - m_ClientCooldownReceivedAt[index];
            return Mathf.Max(0f, m_ClientCooldownRemaining[index] - elapsed);
        }

        private CooldownState ReadBrainCooldowns()
        {
            CooldownState state = default;
            state.Normal = m_Brain.GetSkillCooldown(0);
            state.Specialized = m_Brain.GetSkillCooldown(1);
            state.Optional1 = m_Brain.GetSkillCooldown(2);
            state.Optional2 = m_Brain.GetSkillCooldown(3);
            return state;
        }

        private static bool HasCooldownIncreased(in CooldownState current, in CooldownState sent)
        {
            return current.Normal > sent.Normal + CooldownEpsilon
                || current.Specialized > sent.Specialized + CooldownEpsilon
                || current.Optional1 > sent.Optional1 + CooldownEpsilon
                || current.Optional2 > sent.Optional2 + CooldownEpsilon;
        }

        private void HandleHealthChanged(float previous, float current) => ApplyHealth(current);

        private void HandleEnergyChanged(float previous, float current) => ApplyEnergy(current);

        private void HandleCooldownsChanged(CooldownState previous, CooldownState current) => ApplyCooldowns(current);

        private void HandleLoadoutChanged(NetLoadout previous, NetLoadout current) => ApplyLoadout(current);

        /// <summary>
        /// Client khởi tạo lại xe theo đúng lựa chọn của chủ xe để chỉ số khớp host.
        /// Phải chạy trước khi áp máu/năng lượng, nếu không <c>Initialize</c> sẽ ghi đè giá trị vừa nhận.
        /// </summary>
        private void ApplyLoadout(NetLoadout loadout)
        {
            if (IsServer || m_Brain == null) return;

            NetSpawnService spawnService = FindAnyObjectByType<NetSpawnService>();
            TankDefinition definition = spawnService != null ? spawnService.GetDefinition(loadout.TankIndex) : null;
            if (definition == null) return;

            definition.InitializeTank(
                m_Brain,
                new TankBrainContext(loadout.Level, loadout.EquipmentId, loadout.OptionSkill1, loadout.OptionSkill2));

            m_LoadoutApplied = true;
            ApplyHealth(m_Health.Value);
            ApplyEnergy(m_Energy.Value);
        }

        private void ApplyHealth(float value)
        {
            if (m_Brain == null || IsServer || !m_LoadoutApplied) return;
            m_Brain.SetHealth(value);
        }

        private void ApplyEnergy(float value)
        {
            if (m_Brain == null || IsServer || !m_LoadoutApplied) return;
            m_Brain.SetEnergy(value);
        }

        private void ApplyCooldowns(CooldownState state)
        {
            if (IsServer) return;

            for (int i = 0; i < TankBrain.SkillCount; i++)
            {
                m_ClientCooldownRemaining[i] = state.Get(i);
                m_ClientCooldownReceivedAt[i] = Time.time;
            }
        }
    }
}
