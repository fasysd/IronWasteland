using System;
using IronWasteland.Tanks;
using IronWasteland.Utilities;
using UnityEngine;

namespace IronWasteland.Enemies.DeadlandPhantom
{
    /// <summary>
    /// Không gian biệt lập Vùng Tăm Tối (DarkZone Sub-Arena):
    /// - Tọa lạc tại tọa độ arenaCenter trên cùng Scene (mặc định 100, 100).
    /// - Giai đoạn Bùng nổ: Giam giữ 1 Tank gần nhất và sinh Bản sao Bóng Ma Tử Địa.
    /// - Vị trí của Tank và Bản sao được tùy chỉnh qua tankSpawnOffset và cloneSpawnOffset trên Inspector.
    /// - Cung cấp cơ chế hồi máu cứu trợ cho Tank bị nhốt mỗi khi Khiên của Boss ngoài sân bị suy giảm tích lũy 10%.
    /// - Tự động phát hiện nếu Tank bị nhốt tử trận để chuyển sang giai đoạn Phạt (PenaltyPhase).
    /// - Giải phóng và hoàn trả Tank về sân chính khi Bùng nổ kết thúc (Thưởng hoặc Phạt).
    /// </summary>
    [DisallowMultipleComponent]
    public class DarkZoneSubArena : MonoBehaviour
    {
        [Header("Arena Coordinates & Bounds")]
        [Tooltip("Tọa độ trung tâm của Vùng tăm tối trên Scene.")]
        [SerializeField] private Vector2 arenaCenter = new Vector2(100f, 100f);

        [Tooltip("Bán kính vùng không gian buồng tối (m).")]
        [SerializeField, Min(5f)] private float arenaRadius = 12f;

        [Header("Spawn Offsets")]
        [Tooltip("Độ lệch vị trí đặt Tank khi bị kéo vào buồng tối.")]
        [SerializeField] private Vector2 tankSpawnOffset = new Vector2(0f, -3f);

        [Tooltip("Độ lệch vị trí sinh Bản sao trong buồng tối.")]
        [SerializeField] private Vector2 cloneSpawnOffset = new Vector2(0f, 2f);

        [Header("Visual Styling")]
        [Tooltip("Màu sắc nền sàn không gian Vùng tăm tối.")]
        [SerializeField] private Color floorColor = new Color(0.06f, 0.01f, 0.12f, 0.95f); // #0F031F Tím đen hư không

        private DeadlandPhantomBrain m_Boss;
        private TankBrain m_TrappedTank;
        private DeadlandPhantomClone m_Clone;
        private Vector2 m_SavedTankPosition;
        private bool m_IsTankTrapped;
        private bool m_IsCleanedUp;

        public Vector2 ArenaCenter => arenaCenter;
        public float ArenaRadius => arenaRadius;
        public Vector2 TankSpawnOffset => tankSpawnOffset;
        public Vector2 CloneSpawnOffset => cloneSpawnOffset;
        public TankBrain TrappedTank => m_TrappedTank;
        public DeadlandPhantomClone Clone => m_Clone;
        public bool IsTankTrapped => m_IsTankTrapped;

        public void InitializeArena(DeadlandPhantomBrain boss, Vector2? customCenter = null)
        {
            m_Boss = boss;
            if (customCenter.HasValue)
            {
                arenaCenter = customCenter.Value;
            }
            transform.position = arenaCenter;

            BuildVisualArenaPocket();
        }

        private void BuildVisualArenaPocket()
        {
            GameObject floorGo = new GameObject("DarkZone_Floor");
            floorGo.transform.SetParent(transform);
            floorGo.transform.localPosition = Vector3.zero;

            SpriteRenderer sr = floorGo.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.SoftCircle;
            sr.color = floorColor;
            sr.transform.localScale = Vector3.one * (arenaRadius * 2f);
            sr.sortingOrder = -10;
        }

        /// <summary>
        /// Kéo Tank chỉ định vào buồng tối và triệu hồi Bản sao.
        /// </summary>
        public void TrapTank(TankBrain tank, GameObject clonePrefab = null)
        {
            if (tank == null || !tank.IsAlive) return;

            m_TrappedTank = tank;
            m_SavedTankPosition = tank.transform.position;
            m_IsTankTrapped = true;

            // 1. Dịch chuyển Tank vào buồng tối
            IronWasteland.Net.NetTeleport.Move(tank, arenaCenter + tankSpawnOffset);

            if (IronWasteland.Tanks.TankBrain.NetworkSessionActive && m_Boss != null)
            {
                m_Boss.GetComponent<IronWasteland.Net.EnemyNetState>()?.BroadcastVfx(
                    "DarkZoneFloor", arenaCenter, 0f, default, arenaRadius, 0f);
            }

            // 2. Spawn Bản sao tại vị trí chỉ định
            Vector2 clonePos = arenaCenter + cloneSpawnOffset;
            if (clonePrefab == null)
            {
                Debug.LogWarning($"[DarkZoneSubArena] clonePrefab is null on '{gameObject.name}'. Cannot spawn DeadlandPhantomClone.");
                return;
            }

            GameObject cloneObj = Instantiate(clonePrefab, clonePos, Quaternion.identity);
            if (IronWasteland.Tanks.TankBrain.NetworkSessionActive &&
                cloneObj.TryGetComponent(out Unity.Netcode.NetworkObject cloneNetObj) && !cloneNetObj.IsSpawned)
            {
                var manager = Unity.Netcode.NetworkManager.Singleton;
                if (manager != null && manager.IsServer) cloneNetObj.Spawn(true);
            }
            m_Clone = cloneObj.GetComponent<DeadlandPhantomClone>();
            if (m_Clone == null)
            {
                Debug.LogError($"[DarkZoneSubArena] Prefab '{clonePrefab.name}' is missing DeadlandPhantomClone component! Please attach DeadlandPhantomClone to the prefab.");
                return;
            }

            m_Clone.InitializeClone(m_Boss, this);

            // 3. Đăng ký theo dõi cái chết của Tank trong buồng tối
            m_TrappedTank.OnDeathEvent -= HandleTrappedTankDied;
            m_TrappedTank.OnDeathEvent += HandleTrappedTankDied;
        }

        private void HandleTrappedTankDied()
        {
            if (!m_IsTankTrapped) return;

            // Nếu Tank bị nhốt chết trong buồng tối -> Buộc thất bại Bùng nổ (chuyển sang PenaltyPhase)
            if (m_Boss != null && m_Boss.IsBurstActive)
            {
                m_Boss.ForceBurstFailure();
            }
        }

        /// <summary>
        /// Hồi máu cứu trợ cho Tank bị nhốt (tính theo % Max HP của Tank).
        /// </summary>
        public void HealTrappedTank(float percentMaxHp)
        {
            if (m_TrappedTank == null || !m_TrappedTank.IsAlive || percentMaxHp <= 0f) return;

            float healAmount = m_TrappedTank.Stats.MaxHealth * percentMaxHp;
            m_TrappedTank.Heal(healAmount);
        }

        /// <summary>
        /// Trả Tank về vị trí ban đầu trước khi bị nhốt.
        /// </summary>
        public void ReleaseTank()
        {
            if (m_TrappedTank != null)
            {
                m_TrappedTank.OnDeathEvent -= HandleTrappedTankDied;

                if (m_TrappedTank.IsAlive)
                {
                    IronWasteland.Net.NetTeleport.Move(m_TrappedTank, m_SavedTankPosition);
                }
            }

            m_IsTankTrapped = false;
        }

        /// <summary>
        /// Dọn dẹp buồng tối Sub-Arena và bản sao.
        /// </summary>
        public void CleanupArena()
        {
            if (m_IsCleanedUp) return;
            m_IsCleanedUp = true;

            ReleaseTank();

            if (IronWasteland.Tanks.TankBrain.NetworkSessionActive && m_Boss != null)
            {
                m_Boss.GetComponent<IronWasteland.Net.EnemyNetState>()?.BroadcastVfx(
                    "DarkZoneCleanup", arenaCenter);
            }

            if (m_Clone != null)
            {
                if (IronWasteland.Tanks.TankBrain.NetworkSessionActive &&
                    m_Clone.TryGetComponent(out Unity.Netcode.NetworkObject cloneNetObj) && cloneNetObj.IsSpawned)
                {
                    var manager = Unity.Netcode.NetworkManager.Singleton;
                    if (manager != null && manager.IsServer) cloneNetObj.Despawn(true);
                }
                else if (Application.isPlaying) Destroy(m_Clone.gameObject);
                else DestroyImmediate(m_Clone.gameObject);
                m_Clone = null;
            }

            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        private void OnDestroy()
        {
            if (m_TrappedTank != null)
            {
                m_TrappedTank.OnDeathEvent -= HandleTrappedTankDied;
            }
        }
    }
}
