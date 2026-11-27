using System;
using IronWasteland.Net;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Enemies.DeadlandPhantom
{
    /// <summary>
    /// Master Controller gắn trên Boss Bóng Ma Tử Địa (BongMaTuDia),
    /// quản lý việc kích hoạt và điều phối toàn bộ 6 hiệu ứng VFX đặc trưng:
    /// 1. Vết Chém Tử Địa (DeadlandSlash)
    /// 2. Sóng Xung Kích Triệu Hồi (SoulSummon Pulse)
    /// 3. Đạn Linh Hồn Ma Hỏa (DeadlandSoul Projectile)
    /// 4. Vết Nứt Không Gian (SpatialRift)
    /// 5. Vòm Khiên Tứ Hướng (CardinalShield)
    /// 6. Vận Sức Bùng Nổ (Burst Charging Aura)
    /// </summary>
    [DisallowMultipleComponent]
    public class BongMaTuDiaVFXController : MonoBehaviour
    {
        [Header("VFX Prefabs")]
        [Tooltip("Prefab VFX 1: Vết chém xoay 360 độ (bán kính 4m).")]
        [SerializeField] private GameObject deadlandSlashPrefab;

        [Tooltip("Prefab VFX 2: Sóng xung kích lan 10m kèm sương mù.")]
        [SerializeField] private GameObject soulSummonPulsePrefab;

        [Tooltip("Prefab VFX 3: Đạn linh hồn ma hỏa tự tìm mục tiêu.")]
        [SerializeField] private GameObject deadlandSoulProjectilePrefab;

        [Tooltip("Prefab VFX 4: Vết nứt không gian.")]
        [SerializeField] private GameObject spatialRiftPrefab;

        [Tooltip("Prefab VFX 5: Vòm khiên 4 hướng.")]
        [SerializeField] private GameObject cardinalShieldPrefab;

        [Tooltip("Prefab VFX 6: Vận sức bùng nổ.")]
        [SerializeField] private GameObject burstChargingAuraPrefab;

        // Active instances
        private CardinalShieldVFX m_ActiveCardinalShield;
        private BurstChargingAuraVFX m_ActiveBurstChargingAura;
        private Animator m_Animator;

        public CardinalShieldVFX ActiveCardinalShield => m_ActiveCardinalShield;
        public BurstChargingAuraVFX ActiveBurstChargingAura => m_ActiveBurstChargingAura;
        public Animator BossAnimator => m_Animator;

        private void Awake()
        {
            m_Animator = GetComponentInChildren<Animator>();
        }

        #region VFX 1: Vết Chém Tử Địa (DeadlandSlash)

        /// <summary>
        /// Kích hoạt vung chém tử địa 360 độ (bán kính 4m).
        /// </summary>
        public DeadlandSlashVFX PlayDeadlandSlash(Vector3? centerPosition = null, Action onComplete = null)
        {
            Vector3 spawnPos = centerPosition ?? transform.position;
            if (deadlandSlashPrefab == null)
            {
                Debug.LogWarning("[BongMaTuDiaVFX] deadlandSlashPrefab is missing!");
                return null;
            }

            GameObject go = SpawnVfx(deadlandSlashPrefab, spawnPos, Quaternion.identity);
            DeadlandSlashVFX slash = go.GetComponent<DeadlandSlashVFX>();
            if (slash != null)
            {
                slash.Play(onComplete);
            }
            return slash;
        }

        #endregion

        #region VFX 2: Sóng Xung Kích Triệu Hồi (SoulSummon Pulse)

        /// <summary>
        /// Kích hoạt sóng xung kích ma pháp 10m dưới chân Boss kèm sương mù bao phủ mặt đất.
        /// </summary>
        public SoulSummonPulseVFX PlaySoulSummonPulse(Vector3? centerPosition = null, Action onComplete = null)
        {
            Vector3 spawnPos = centerPosition ?? transform.position;
            if (soulSummonPulsePrefab == null)
            {
                Debug.LogWarning("[BongMaTuDiaVFX] soulSummonPulsePrefab is missing!");
                return null;
            }

            GameObject go = SpawnVfx(soulSummonPulsePrefab, spawnPos, Quaternion.identity);
            SoulSummonPulseVFX pulse = go.GetComponent<SoulSummonPulseVFX>();
            if (pulse != null)
            {
                pulse.Play(onComplete);
            }
            return pulse;
        }

        #endregion

        #region VFX 3: Đạn Linh Hồn Ma Hỏa (DeadlandSoul Projectile)

        /// <summary>
        /// Bắn ra quả cầu ma hỏa tím xanh tự tìm mục tiêu Tank.
        /// </summary>
        public DeadlandSoulProjectile SpawnSoulProjectile(Vector3 startPosition, Transform target, Vector2 initialDirection, float speed = 8f, float damage = 0f, EnemyBrain owner = null)
        {
            if (deadlandSoulProjectilePrefab == null)
            {
                Debug.LogWarning("[BongMaTuDiaVFX] deadlandSoulProjectilePrefab is missing!");
                return null;
            }

            Vector2 direction = initialDirection.sqrMagnitude > 0.0001f ? initialDirection.normalized : Vector2.down;
            GameObject go = SpawnProjectileVfx(deadlandSoulProjectilePrefab, startPosition, Quaternion.identity, direction * speed, target, 180f);
            DeadlandSoulProjectile proj = go.GetComponent<DeadlandSoulProjectile>();
            if (proj != null)
            {
                proj.Initialize(target, initialDirection, speed, damage, owner);
            }
            return proj;
        }

        #endregion

        #region VFX 4: Vết Nứt Không Gian (SpatialRift)

        /// <summary>
        /// Tạo vết nứt không gian tại tọa độ chỉ định trên mặt đất.
        /// </summary>
        public SpatialRiftVFX SpawnSpatialRift(Vector3 position, Action<SpatialRiftVFX, Tanks.TankBrain> onTankEntered = null)
        {
            if (spatialRiftPrefab == null)
            {
                Debug.LogWarning("[BongMaTuDiaVFX] spatialRiftPrefab is missing!");
                return null;
            }

            GameObject go = SpawnVfx(spatialRiftPrefab, position, Quaternion.identity);
            SpatialRiftVFX rift = go.GetComponent<SpatialRiftVFX>();
            if (rift != null && onTankEntered != null)
            {
                rift.TankEntered += onTankEntered;
            }
            return rift;
        }

        #endregion

        #region VFX 5: Vòm Khiên Tứ Hướng (CardinalShield)

        /// <summary>
        /// Bật vòm khiên tứ hướng bao quanh Boss.
        /// </summary>
        public CardinalShieldVFX ActivateCardinalShield(CardinalDirection initialOpenDirection = CardinalDirection.North)
        {
            if (m_ActiveCardinalShield != null)
            {
                m_ActiveCardinalShield.SetOpenDirection(initialOpenDirection);
                return m_ActiveCardinalShield;
            }

            if (cardinalShieldPrefab == null)
            {
                Debug.LogWarning("[BongMaTuDiaVFX] cardinalShieldPrefab is missing!");
                return null;
            }

            GameObject go = SpawnVfx(cardinalShieldPrefab, transform);
            go.transform.localPosition = Vector3.zero;
            m_ActiveCardinalShield = go.GetComponent<CardinalShieldVFX>();
            if (m_ActiveCardinalShield != null)
            {
                m_ActiveCardinalShield.SetOpenDirection(initialOpenDirection);
            }
            return m_ActiveCardinalShield;
        }

        /// <summary>Chuyển hướng mở của khiên tứ hướng.</summary>
        public void SetCardinalShieldDirection(CardinalDirection dir)
        {
            if (m_ActiveCardinalShield != null)
            {
                m_ActiveCardinalShield.SetOpenDirection(dir);
            }
        }

        /// <summary>Phản đòn khi bị bắn vào hướng đóng.</summary>
        public void PlayCardinalReflect(Vector2 hitPoint)
        {
            if (m_ActiveCardinalShield != null)
            {
                m_ActiveCardinalShield.PlayReflectFlash(hitPoint);
            }
        }

        /// <summary>
        /// Kiểm tra đòn tấn công va vào khiên tứ hướng.
        /// Trả về true nếu bị cản (góc đóng) và tự động kích hoạt hiệu ứng phản đòn.
        /// Trả về false nếu trúng góc mở hoặc khiên chưa bật.
        /// </summary>
        public bool CheckCardinalShieldDeflect(Vector2 hitPoint, Vector2 attackDirection)
        {
            if (m_ActiveCardinalShield == null) return false;
            return m_ActiveCardinalShield.HandleIncomingAttack(hitPoint, attackDirection);
        }

        /// <summary>Tắt vòm khiên tứ hướng.</summary>
        public void DeactivateCardinalShield()
        {
            if (m_ActiveCardinalShield != null)
            {
                if (Application.isPlaying)
                    Destroy(m_ActiveCardinalShield.gameObject);
                else
                    DestroyImmediate(m_ActiveCardinalShield.gameObject);
                m_ActiveCardinalShield = null;
            }
        }

        #endregion

        #region VFX 6: Vận Sức Bùng Nổ (Burst Charging Aura)

        /// <summary>
        /// Bắt đầu vận sức bùng nổ (vòng cảnh báo đỏ tím lan rộng + năng lượng tụ về Boss).
        /// </summary>
        public BurstChargingAuraVFX StartBurstCharging(float duration = 2f, float maxRadius = 8f, Action onComplete = null)
        {
            if (m_ActiveBurstChargingAura == null)
            {
                if (burstChargingAuraPrefab == null)
                {
                    Debug.LogWarning("[BongMaTuDiaVFX] burstChargingAuraPrefab is missing!");
                    return null;
                }

                GameObject go = SpawnVfx(burstChargingAuraPrefab, transform);
                go.transform.localPosition = Vector3.zero;
                m_ActiveBurstChargingAura = go.GetComponent<BurstChargingAuraVFX>();
            }

            if (m_ActiveBurstChargingAura != null)
            {
                m_ActiveBurstChargingAura.StartCharging(onComplete);
            }
            return m_ActiveBurstChargingAura;
        }

        /// <summary>Dừng vận sức bùng nổ.</summary>
        public void StopBurstCharging()
        {
            if (m_ActiveBurstChargingAura != null)
            {
                m_ActiveBurstChargingAura.StopCharging();
            }
        }

        #endregion

        /// <summary>Dựng VFX tại vị trí thế giới và phát bản sao visual xuống client qua catalog.</summary>
        private GameObject SpawnVfx(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            GameObject go = Instantiate(prefab, position, rotation);
            NetVisualSpawn.PlayVfx(prefab, go, GetComponentInParent<NetworkObject>(true));
            return go;
        }

        /// <summary>Dựng VFX gắn vào một transform (aura bám boss) và phát bản sao visual xuống client.</summary>
        private GameObject SpawnVfx(GameObject prefab, Transform parent)
        {
            GameObject go = Instantiate(prefab, parent);
            go.transform.localPosition = Vector3.zero;
            NetVisualSpawn.PlayVfx(prefab, go, GetComponentInParent<NetworkObject>(true), attachToRoot: true);
            return go;
        }

        /// <summary>Dựng đạn VFX và gửi vận tốc/bám mục tiêu để client tự bay giống host.</summary>
        private GameObject SpawnProjectileVfx(GameObject prefab, Vector3 position, Quaternion rotation,
            Vector2 velocity, Transform target, float turnSpeed)
        {
            GameObject go = Instantiate(prefab, position, rotation);
            NetworkObject targetNetwork = target != null ? target.GetComponentInParent<NetworkObject>() : null;
            NetVisualSpawn.PlayVfx(
                prefab,
                go,
                GetComponentInParent<NetworkObject>(true),
                attachToRoot: false,
                velocity: velocity,
                homingTargetId: targetNetwork != null ? targetNetwork.NetworkObjectId : 0UL,
                turnSpeed: turnSpeed);
            return go;
        }

        private void OnDestroy()
        {
            DeactivateCardinalShield();
            StopBurstCharging();
        }
    }
}
