using IronWasteland.Core.Effects;
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
        private const float CooldownDriftEpsilon = 0.2f;
        private const float AimEpsilon = 0.5f;
        private const float AimSendInterval = 1f / 15f;
        private const float StatEpsilon = 0.5f;

        private readonly NetworkVariable<float> m_Health = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> m_Energy = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<CooldownState> m_Cooldowns = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<NetLoadout> m_Loadout = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Góc nòng (độ). Chủ xe đọc trực tiếp look target để không trễ; máy khác đọc bản đã nhận.</summary>
        private readonly NetworkVariable<float> m_AimAngle = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>Hệ số tốc độ do Slow (0..1). Client áp vào TankBrain vì client không tick CC.</summary>
        private readonly NetworkVariable<float> m_SlowSpeedFactor = new(
            1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Host báo Stun/Root đang chặn di chuyển để client dừng ngay.</summary>
        private readonly NetworkVariable<bool> m_MovementBlocked = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Tổng khiên hiện tại do host ghi; client chỉ hiển thị (không tự tạo khiên trên brain).</summary>
        private readonly NetworkVariable<float> m_Shield = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Máu tối đa thực tế của host (đã gồm buff như Leviathan Form +50%) để client kẹp máu đúng.</summary>
        private readonly NetworkVariable<float> m_MaxHealth = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Tốc độ di chuyển thực tế của host để client di chuyển khớp buff/debuff.</summary>
        private readonly NetworkVariable<float> m_MoveSpeed = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly float[] m_ClientCooldownRemaining = new float[TankBrain.SkillCount];
        private readonly float[] m_ClientCooldownReceivedAt = new float[TankBrain.SkillCount];

        private TankBrain m_Brain;
        private bool m_LoadoutApplied;
        private float m_SyncTimer;
        private float m_SentHealth;
        private float m_SentEnergy;
        private float m_SentAimAngle;
        private float m_SentSlowFactor = 1f;
        private bool m_SentBlocked;
        private float m_SentShield;
        private float m_SentMaxHealth;
        private float m_SentMoveSpeed;
        private float m_AimSendTimer;
        private bool m_LoadoutMissingLogged;
        private CooldownState m_SentCooldowns;
        private float m_SentCooldownsAt;

        public override void OnNetworkSpawn()
        {
            m_Brain = GetComponent<TankBrain>();

            m_Brain.IsLocallyControlled = IsOwner;
            m_Brain.IsServerSimulated = IsServer;

            m_Health.OnValueChanged += HandleHealthChanged;
            m_Energy.OnValueChanged += HandleEnergyChanged;
            m_Cooldowns.OnValueChanged += HandleCooldownsChanged;
            m_Loadout.OnValueChanged += HandleLoadoutChanged;
            m_SlowSpeedFactor.OnValueChanged += HandleSlowChanged;
            m_MovementBlocked.OnValueChanged += HandleBlockedChanged;
            m_MaxHealth.OnValueChanged += HandleMaxHealthChanged;
            m_MoveSpeed.OnValueChanged += HandleMoveSpeedChanged;

            if (IsServer)
            {
                m_LoadoutApplied = true;
                m_SentHealth = m_Brain.CurrentHealth;
                m_SentEnergy = m_Brain.CurrentEnergy;
                m_Health.Value = m_SentHealth;
                m_Energy.Value = m_SentEnergy;
                m_SentCooldowns = ReadBrainCooldowns();
                m_SentCooldownsAt = Time.time;
                m_Cooldowns.Value = m_SentCooldowns;
                m_SentSlowFactor = m_Brain.CurrentSlowSpeedFactor;
                m_SlowSpeedFactor.Value = m_SentSlowFactor;
                m_SentBlocked = m_Brain.IsMovementBlocked;
                m_MovementBlocked.Value = m_SentBlocked;
                m_SentShield = m_Brain.CurrentShield;
                m_Shield.Value = m_SentShield;

                // Client không tự tính stats theo buff của host, nên gửi máu tối đa + tốc độ cuối.
                m_SentMaxHealth = m_Brain.Stats != null ? m_Brain.Stats.MaxHealth : 0f;
                m_SentMoveSpeed = m_Brain.Stats != null ? m_Brain.Stats.MoveSpeed : 0f;
                m_MaxHealth.Value = m_SentMaxHealth;
                m_MoveSpeed.Value = m_SentMoveSpeed;

                // Chỉ host phát hiệu ứng xuống client; client không bao giờ áp hiệu ứng lên brain.
                m_Brain.OnEffectAppliedEvent += HandleEffectApplied;
                m_Brain.OnEffectRemovedEvent += HandleEffectRemoved;

                // Người vào giữa trận không nhận lại các hiệu ứng đang có (named message chỉ phát lúc áp/gỡ),
                // nên gửi snapshot riêng cho chính client sở hữu xe này.
                StartCoroutine(SendEffectSnapshotRoutine());
            }
            else
            {
                ApplyLoadout(m_Loadout.Value);
                ApplyNetworkStats(m_MaxHealth.Value, m_MoveSpeed.Value);
                ApplyHealth(m_Health.Value);
                ApplyEnergy(m_Energy.Value);
                ApplyCooldowns(m_Cooldowns.Value);
                ApplySlow(m_SlowSpeedFactor.Value);
                ApplyBlocked(m_MovementBlocked.Value);
            }

            FindAnyObjectByType<GameController>()?.ApplySpawnFacing(m_Brain);

            // AimAngle do chủ xe ghi (owner-write) để mọi máy thấy nòng bám chuột liên tục.
            if (IsOwner)
            {
                m_SentAimAngle = ReadAimAngle();
                m_AimAngle.Value = m_SentAimAngle;
            }
        }

        public override void OnNetworkDespawn()
        {
            m_Health.OnValueChanged -= HandleHealthChanged;
            m_Energy.OnValueChanged -= HandleEnergyChanged;
            m_Cooldowns.OnValueChanged -= HandleCooldownsChanged;
            m_Loadout.OnValueChanged -= HandleLoadoutChanged;
            m_SlowSpeedFactor.OnValueChanged -= HandleSlowChanged;
            m_MovementBlocked.OnValueChanged -= HandleBlockedChanged;
            m_MaxHealth.OnValueChanged -= HandleMaxHealthChanged;
            m_MoveSpeed.OnValueChanged -= HandleMoveSpeedChanged;

            if (IsServer && m_Brain != null)
            {
                m_Brain.OnEffectAppliedEvent -= HandleEffectApplied;
                m_Brain.OnEffectRemovedEvent -= HandleEffectRemoved;
            }
            else if (!IsServer)
            {
                // (cache hiệu ứng được dọn chung ở dưới, cho cả host lẫn client)
            }

            // Cả host lẫn client đều phải xoá cache khi xe despawn (Replay/rời phòng):
            // host cũng nhận named message của chính mình nên cache host cũng bị nhiễm.
            NetEffectSync.Instance?.ClearOwner(OwnerClientId);
        }

        /// <summary>Xe mà state này gắn vào (null khi chưa spawn).</summary>
        public TankBrain Brain => m_Brain;

        /// <summary>Máu hiện tại: host đọc trực tiếp từ brain, client đọc bản đã áp từ host.</summary>
        public float Health => m_Brain != null ? m_Brain.CurrentHealth : m_Health.Value;

        /// <summary>Máu tối đa: host đọc stats thật (đã gồm buff), client đọc giá trị host gửi.</summary>
        public float MaxHealth => IsServer && m_Brain != null && m_Brain.Stats != null
            ? m_Brain.Stats.MaxHealth
            : m_MaxHealth.Value;

        /// <summary>Năng lượng hiện tại.</summary>
        public float Energy => m_Brain != null ? m_Brain.CurrentEnergy : m_Energy.Value;

        /// <summary>Năng lượng tối đa.</summary>
        public float MaxEnergy => m_Brain != null && m_Brain.Stats != null ? m_Brain.Stats.MaxEnergy : 0f;

        /// <summary>Khiên hiện tại: host đọc trực tiếp brain, client đọc bản host gửi (chỉ để hiển thị).</summary>
        public float ShieldAmount => IsServer && m_Brain != null ? m_Brain.CurrentShield : m_Shield.Value;

        /// <summary>Góc nòng hiện tại (độ) — chủ xe đọc look target, máy khác đọc bản đã nhận.</summary>
        public float AimAngle => IsOwner ? ReadAimAngle() : m_AimAngle.Value;

        /// <summary>Client đã áp xong loadout chưa (host luôn true).</summary>
        public bool LoadoutApplied => m_LoadoutApplied;

        /// <summary>Host ghi lựa chọn xe xuống mọi máy; client dùng để khởi tạo đúng chỉ số.
        /// Gọi được cả TRƯỚC khi spawn (giá trị đi kèm payload spawn).</summary>
        public void SetServerLoadout(NetLoadout loadout)
        {
            // Không dùng IsServer ở đây: NetworkBehaviour.IsServer chỉ được gán trong lúc spawn,
            // nên khi host ghi loadout TRƯỚC SpawnAsPlayerObject thì IsServer còn false và lệnh ghi
            // sẽ bị bỏ qua ⇒ client nhận loadout rỗng (0/0) và hiển thị sai biến thể kỹ năng.
            // Chỉ cần kiểm tra phía NetworkManager đang là server; NetworkVariable sẽ mang giá trị
            // này trong gói spawn đầu tiên.
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer) return;
            m_Loadout.Value = loadout;
        }

        private void Update()
        {
            if (!IsSpawned || m_Brain == null) return;

            // Aim phải chạy mỗi frame: nếu đặt sau nhịp 0,1 s của các NetworkVariable khác thì
            // khi HP/EN/cooldown đứng yên, góc nòng cũng không được gửi và nòng xe người khác kẹt.
            if (IsOwner) UpdateAim();

            m_SyncTimer += Time.deltaTime;
            if (m_SyncTimer < SyncInterval) return;
            m_SyncTimer = 0f;

            if (!IsServer)
            {
                // Client: loadout có thể tới trước khi NetSpawnService kịp sẵn sàng — thử lại
                // cho tới khi áp được, nếu không máu/năng lượng sẽ không bao giờ cập nhật.
                if (!m_LoadoutApplied) ApplyLoadout(m_Loadout.Value);
                return;
            }

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
            float cooldownElapsed = Time.time - m_SentCooldownsAt;
            if (HasCooldownDrifted(current, m_SentCooldowns, cooldownElapsed))
            {
                m_SentCooldowns = current;
                m_SentCooldownsAt = Time.time;
                m_Cooldowns.Value = current;
            }

            float slow = m_Brain.CurrentSlowSpeedFactor;
            if (Mathf.Abs(slow - m_SentSlowFactor) >= 0.05f)
            {
                m_SentSlowFactor = slow;
                m_SlowSpeedFactor.Value = slow;
            }

            bool blocked = m_Brain.IsMovementBlocked;
            if (blocked != m_SentBlocked)
            {
                m_SentBlocked = blocked;
                m_MovementBlocked.Value = blocked;
            }

            float shield = m_Brain.CurrentShield;
            if (Mathf.Abs(shield - m_SentShield) >= 0.5f)
            {
                m_SentShield = shield;
                m_Shield.Value = shield;
            }

            float maxHealth = m_Brain.Stats != null ? m_Brain.Stats.MaxHealth : 0f;
            if (Mathf.Abs(maxHealth - m_SentMaxHealth) >= StatEpsilon)
            {
                m_SentMaxHealth = maxHealth;
                m_MaxHealth.Value = maxHealth;
            }

            float moveSpeed = m_Brain.Stats != null ? m_Brain.Stats.MoveSpeed : 0f;
            if (Mathf.Abs(moveSpeed - m_SentMoveSpeed) >= StatEpsilon)
            {
                m_SentMoveSpeed = moveSpeed;
                m_MoveSpeed.Value = moveSpeed;
            }
        }

        /// <summary>Gửi lại toàn bộ hiệu ứng đang có cho client vừa nhận xe (người vào giữa trận).</summary>
        private System.Collections.IEnumerator SendEffectSnapshotRoutine()
        {
            // NetEffectSync đăng ký handler trong Update nên chờ tối đa vài frame trước khi gửi.
            for (int i = 0; i < 120 && NetEffectSync.Instance == null; i++)
            {
                yield return null;
            }

            NetEffectSync sync = NetEffectSync.Instance;
            if (sync == null || m_Brain == null) yield break;

            sync.SendSnapshotToClient(OwnerClientId, OwnerClientId, m_Brain.ActiveEffects);
        }

        private void HandleEffectApplied(StatusEffect effect)
        {
            if (!IsServer || effect == null) return;
            NetEffectSync.Instance?.Broadcast(OwnerClientId, NetEffectSync.OpApply, effect);
        }

        private void HandleEffectRemoved(StatusEffect effect)
        {
            if (!IsServer || effect == null) return;
            NetEffectSync.Instance?.Broadcast(OwnerClientId, NetEffectSync.OpRemove, effect);
        }

        /// <summary>Chủ xe ghi hướng nòng: tính mỗi frame nhưng chỉ gửi tối đa 15 Hz và khi lệch ≥ 0,5°.</summary>
        private void UpdateAim()
        {
            m_AimSendTimer -= Time.deltaTime;
            if (m_AimSendTimer > 0f) return;
            m_AimSendTimer = AimSendInterval;

            float aim = ReadAimAngle();
            if (Mathf.Abs(Mathf.DeltaAngle(m_SentAimAngle, aim)) < AimEpsilon) return;

            m_SentAimAngle = aim;
            m_AimAngle.Value = aim;
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

        /// <summary>
        /// Host so hồi chiêu hiện tại với giá trị client sẽ suy ra (giá trị đã gửi trừ thời gian trôi qua).
        /// Lệch nhiều nghĩa là vừa dùng chiêu (tăng vọt) hoặc vừa được giảm/hoàn hồi chiêu (giảm vọt) — cả hai đều phải gửi.
        /// </summary>
        private static bool HasCooldownDrifted(in CooldownState current, in CooldownState sent, float elapsed)
        {
            return HasSlotDrifted(current.Normal, sent.Normal, elapsed)
                || HasSlotDrifted(current.Specialized, sent.Specialized, elapsed)
                || HasSlotDrifted(current.Optional1, sent.Optional1, elapsed)
                || HasSlotDrifted(current.Optional2, sent.Optional2, elapsed);
        }

        private static bool HasSlotDrifted(float current, float sent, float elapsed)
        {
            float expected = Mathf.Max(0f, sent - Mathf.Max(0f, elapsed));
            return Mathf.Abs(current - expected) >= CooldownDriftEpsilon;
        }

        private void HandleHealthChanged(float previous, float current) => ApplyHealth(current);

        private void HandleEnergyChanged(float previous, float current) => ApplyEnergy(current);

        private void HandleCooldownsChanged(CooldownState previous, CooldownState current) => ApplyCooldowns(current);

        private void HandleLoadoutChanged(NetLoadout previous, NetLoadout current) => ApplyLoadout(current);

        private void HandleSlowChanged(float previous, float current) => ApplySlow(current);

        private void HandleBlockedChanged(bool previous, bool current) => ApplyBlocked(current);

        private void HandleMaxHealthChanged(float previous, float current)
        {
            if (IsServer) return;
            ApplyNetworkStats(current, m_MoveSpeed.Value);
            ApplyHealth(m_Health.Value);
        }

        private void HandleMoveSpeedChanged(float previous, float current)
        {
            if (IsServer) return;
            ApplyNetworkStats(m_MaxHealth.Value, current);
        }

        /// <summary>Client áp hệ số Slow do host gửi — client không tick CC nên không tự tính được.</summary>
        private void ApplySlow(float value)
        {
            if (m_Brain == null || IsServer) return;
            m_Brain.RemoteSlowSpeedFactor = Mathf.Clamp01(value);
        }

        /// <summary>Client dừng di chuyển theo cờ Stun/Root của host.</summary>
        private void ApplyBlocked(bool blocked)
        {
            if (m_Brain == null || IsServer) return;
            m_Brain.RemoteMovementBlocked = blocked;
        }

        /// <summary>
        /// Client khởi tạo lại xe theo đúng lựa chọn của chủ xe để chỉ số khớp host.
        /// Phải chạy trước khi áp máu/năng lượng, nếu không <c>Initialize</c> sẽ ghi đè giá trị vừa nhận.
        /// </summary>
        private void ApplyLoadout(NetLoadout loadout)
        {
            if (IsServer || m_Brain == null) return;

            NetSpawnService spawnService = FindAnyObjectByType<NetSpawnService>();
            TankDefinition definition = spawnService != null ? spawnService.GetDefinition(loadout.TankIndex) : null;
            if (definition == null)
            {
                if (!m_LoadoutMissingLogged)
                {
                    m_LoadoutMissingLogged = true;
                    Debug.LogWarning(
                        $"[PlayerNetState] Chưa tìm được TankDefinition cho tankIndex={loadout.TankIndex}; sẽ thử lại mỗi 0,1 giây.",
                        this);
                }

                return;
            }

            definition.InitializeTank(
                m_Brain,
                new TankBrainContext(loadout.Level, loadout.EquipmentId, loadout.OptionSkill1, loadout.OptionSkill2));

            // Áp chỉ số cuối của host trước khi kẹp máu/năng lượng, nếu không máu sẽ bị chặn theo stats cũ.
            ApplyNetworkStats(m_MaxHealth.Value, m_MoveSpeed.Value);

            m_LoadoutApplied = true;
            ApplyHealth(m_Health.Value);
            ApplyEnergy(m_Energy.Value);

            if (IsOwner)
            {
                GameController gc = FindAnyObjectByType<GameController>();
                if (gc != null && gc.PlayerTank == m_Brain)
                {
                    gc.BindTank(m_Brain);
                }
            }
        }

        /// <summary>Client áp máu tối đa/tốc độ host gửi để kẹp máu và di chuyển khớp host.</summary>
        private void ApplyNetworkStats(float maxHealth, float moveSpeed)
        {
            if (m_Brain == null || IsServer) return;
            m_Brain.ApplyNetworkStats(maxHealth, moveSpeed);
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
