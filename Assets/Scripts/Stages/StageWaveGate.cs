using IronWasteland.Enemies.Navigation;
using IronWasteland.Net;
using IronWasteland.Tanks;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Stages
{
    [AddComponentMenu("Iron Wasteland/Stages/Stage Wave Gate")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class StageWaveGate : MonoBehaviour
    {
        [Tooltip("Số đợt phải dọn hết để mở cửa. W1 = 1.")]
        [SerializeField, Min(1)] private int completedWaveNumber = 1;
        [Tooltip("Số đợt bắt đầu khi xe đi qua cửa. W2 = 2.")]
        [SerializeField, Min(2)] private int nextWaveNumber = 2;
        [Tooltip("Collider đặc của GameObject cửa. Dùng layer Default để lưới tìm đường xử lý cửa như vật cản động.")]
        [SerializeField] private Collider2D barrier;
        [Tooltip("SpriteRenderer của cửa cần ẩn khi mở. Để trống nếu nó nằm cùng GameObject với Barrier.")]
        [SerializeField] private SpriteRenderer closedVisual;

        private EnemySpawner m_Spawner;
        private GameController m_GameController;
        private PathfindingGrid2D m_Grid;
        private bool m_Registered;
        private bool m_RegisteredObstacle;
        private bool m_Entered;

        internal int CompletedWaveIndex => completedWaveNumber - 1;
        internal int NextWaveIndex => nextWaveNumber - 1;
        public bool IsOpen { get; private set; }

        private static bool HasAuthority =>
            !TankBrain.NetworkSessionActive || NetworkManager.Singleton.IsServer;

        private void OnEnable()
        {
            if (!GetComponent<BoxCollider2D>().isTrigger)
            {
                DisableWithError("Bật Is Trigger trên BoxCollider2D của GameObject này.");
                return;
            }

            if (barrier == null || barrier.isTrigger || barrier.gameObject.scene != gameObject.scene)
            {
                DisableWithError("Kéo collider của cửa trong cùng scene vào ô Barrier và tắt Is Trigger trên collider đó.");
                return;
            }

            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                if (m_Spawner == null) m_Spawner = root.GetComponentInChildren<EnemySpawner>();
                if (m_GameController == null) m_GameController = root.GetComponentInChildren<GameController>();
                if (m_Grid == null) m_Grid = root.GetComponentInChildren<PathfindingGrid2D>();
            }

            int terrain = m_Grid != null && m_Grid.TerrainLayer.value != 0
                ? m_Grid.TerrainLayer.value
                : LayerMask.GetMask("Terrain");
            if ((terrain & (1 << barrier.gameObject.layer)) != 0)
            {
                DisableWithError("Đưa GameObject cửa ra khỏi layer địa hình tĩnh (ví dụ đặt Layer = Default).");
                return;
            }

            if (m_Spawner == null || !m_Spawner.RegisterWaveGate(this))
            {
                DisableWithError("Kiểm tra EnemySpawner và số wave. Mỗi wave chỉ được gắn với một cửa.");
                return;
            }

            if (closedVisual == null) closedVisual = barrier.GetComponent<SpriteRenderer>();
            m_Registered = true;
            m_Spawner.WaveCleared += OnWaveCleared;
        }

        private void DisableWithError(string message)
        {
            Debug.LogError($"[StageWaveGate] {message}", this);
            enabled = false;
        }

        private void Start()
        {
            if (HasAuthority && m_Grid == null) m_Grid = PathfindingGrid2D.Instance;
            RefreshBarrier();
        }

        private void OnDisable()
        {
            if (m_Registered && m_Spawner != null)
            {
                m_Spawner.WaveCleared -= OnWaveCleared;
                m_Spawner.UnregisterWaveGate(this);
            }

            m_Registered = false;
            RemovePathfindingObstacle();
        }

        internal void ResetGate()
        {
            m_Entered = false;
            IsOpen = false;
            RefreshBarrier();
        }

        private void OnWaveCleared(int waveIndex)
        {
            if (!HasAuthority || waveIndex != CompletedWaveIndex || IsOpen) return;

            IsOpen = true;
            RefreshBarrier();
            PublishState();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!m_Registered || !HasAuthority || !IsOpen || m_Entered) return;

            TankBrain tank = OwnerRef.From(other)?.GetOwner<TankBrain>();
            if (tank == null) tank = other.GetComponentInParent<TankBrain>();
            if (tank == null || !tank.IsAlive) return;

            if (TankBrain.NetworkSessionActive)
            {
                if (!tank.TryGetComponent(out NetworkObject networkObject) ||
                    !networkObject.IsSpawned || !networkObject.IsPlayerObject) return;
            }
            else if (m_GameController == null || m_GameController.PlayerTank != tank)
            {
                return;
            }

            m_Entered = m_Spawner.TryStartGatedWave(this);
        }

        internal void PublishState(ulong? targetClientId = null)
        {
            if (!m_Registered || !TankBrain.NetworkSessionActive || !HasAuthority) return;

            NetStageEvents.Instance?.SendWaveGateState(gameObject.scene.name, NextWaveIndex,
                IsOpen, m_Spawner.WavesFired, targetClientId);
        }

        internal void ApplyRemoteState(bool opened, int firedWaves)
        {
            if (!m_Registered) return;

            m_Spawner.ApplyRemoteWaveCount(firedWaves);
            IsOpen = opened;
            RefreshBarrier();
        }

        private void RefreshBarrier()
        {
            if (IsOpen) RemovePathfindingObstacle();
            barrier.enabled = !IsOpen;
            if (closedVisual != null) closedVisual.enabled = !IsOpen;

            // Cửa dùng layer Default, nằm ngoài lượt quét Terrain tĩnh của lưới.
            if (!IsOpen && HasAuthority && m_Grid != null && m_Grid.IsInitialized && !m_RegisteredObstacle)
            {
                m_Grid.AddObstacle(barrier);
                m_RegisteredObstacle = true;
            }
        }

        private void RemovePathfindingObstacle()
        {
            if (m_RegisteredObstacle && m_Grid != null) m_Grid.RemoveObstacle(barrier);
            m_RegisteredObstacle = false;
        }
    }
}
