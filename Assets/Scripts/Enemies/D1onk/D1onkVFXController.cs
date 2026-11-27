using System;
using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Enemies;
using IronWasteland.Enemies.D1onk.VFX;
using IronWasteland.Net;
using UnityEngine;

namespace IronWasteland.Enemies.D1onk
{
    /// <summary>
    /// Master VFX & Animation Controller gắn trên Boss Trưởng Quản Ngục D1onk:
    /// Quản lý việc kích hoạt và điều phối toàn bộ hệ thống VFX kỹ năng, bị động và diễn hoạt procedural xoay trục Z:
    /// 1. Bị động — Pháo Hạm Đa Nòng: 14 nòng súng bắn tên lửa đạn đạo ra ngoài theo hướng -gunTransform.right.
    /// 2. Kỹ năng — Trừng Phạt (TrungPhat): Sấm sét trừng phạt giáng từ trên trời kèm vòng cảnh báo và nổ hồ quang.
    /// 3. Kỹ năng — Laze Nguyên Tố (LazeNguyenTo): Tia laser năng lượng tập trung quét mục tiêu.
    /// 4. Kỹ năng — Tỏa Nhiệt Diện Rộng (ToaNhietDienRong): Sóng nhiệt áp suất cao bung tỏa dạng vòng tròn hình xuyến.
    /// 5. Kỹ năng — Viện Binh Tinh Linh (VienBinhTinhLinh): Vòng ma pháp triệu hồi 3-5 quái thường ngẫu nhiên từ danh sách normalEnemy.
    /// 6. Trạng thái Tụ Lực (Charging / BurstCharging): Vòng xoáy năng lượng cao thế bao quanh thân tàu.
    /// 7. Trạng thái Choáng (Stun): Hồ quang sao năng lượng xoay quanh đầu D1onk.
    /// 
    /// RÀNG BUỘC KIẾN TRÚC:
    /// - Không hardcode, cấu hình tường minh trên Inspector.
    /// - Khi thiếu component/ref thì ghi log cảnh báo/lỗi rõ ràng, TUYỆT ĐỐI không tự ý AddComponent ngầm.
    /// - ColliderRef ở VFX prefab hoặc đạn prefab TUYỆT ĐỐI KHÔNG được lấy EliteEnemyBrain của D1onk làm owner.
    /// </summary>
    [DisallowMultipleComponent]
    public class D1onkVFXController : MonoBehaviour, INetVfxReceiver
    {
        [Header("VFX Prefabs")]
        [Tooltip("Prefab tên lửa bắn từ 14 nòng súng.")]
        [SerializeField] private GameObject missileProjectilePrefab;

        [Tooltip("Prefab tia lửa đầu nòng súng (Muzzle Flash).")]
        [SerializeField] private GameObject muzzleFlashPrefab;

        [Tooltip("Prefab vụ nổ khi tên lửa va chạm.")]
        [SerializeField] private GameObject explosionVFXPrefab;

        [Tooltip("Prefab sấm sét trừng phạt từ trên trời giáng xuống.")]
        [SerializeField] private GameObject trungPhatLightningPrefab;

        [Tooltip("Prefab tia laser nguyên tố.")]
        [SerializeField] private GameObject lazeNguyenToPrefab;

        [Tooltip("Prefab sóng nhiệt tỏa rộng hình xuyến.")]
        [SerializeField] private GameObject toaNhietDienRongPrefab;

        [Tooltip("Prefab vòng ma pháp triệu hồi viện binh tinh linh.")]
        [SerializeField] private GameObject vienBinhSummonVFXPrefab;

        [Tooltip("Prefab hào quang tụ năng lượng (Charging Aura).")]
        [SerializeField] private GameObject chargingAuraPrefab;

        [Tooltip("Prefab hiệu ứng choáng váng (Stun).")]
        [SerializeField] private GameObject stunVFXPrefab;

        [Header("Gun Battery & Emitter Points")]
        [Tooltip("Danh sách toàn bộ 14 nòng súng của D1onk (10 từ Súng 2 nòng, 4 từ Súng 3 nòng).")]
        [SerializeField] private Transform[] gunBarrels;

        [Tooltip("Khoảng cách đẩy điểm spawn đạn ra khỏi pivot nòng súng theo hướng -gun.right (m).")]
        [SerializeField, Min(0.1f)] private float gunMuzzleOffset = 0.8f;

        [Tooltip("Điểm phát tia laser nguyên tố (thường ở ngực / lõi thân / trước mũi tàu).")]
        [SerializeField] private Transform laserEmitterPoint;

        [Tooltip("Vị trí đỉnh đầu để gắn hiệu ứng choáng.")]
        [SerializeField] private Transform headPoint;

        [Header("Reinforcements Pool (Normal Enemies)")]
        [Tooltip("Danh sách toàn bộ Prefab quái thường (normalEnemy) trong game để triệu hồi ngẫu nhiên.")]
        [SerializeField] private GameObject[] normalEnemyPrefabs;

        // Active runtime instances
        private D1onkChargingAuraVFX m_ActiveChargingAura;
        private D1onkStunVFX m_ActiveStunVFX;
        private D1onkLazeNguyenToVFX m_ActiveLaserVFX;
        private EnemyBrain m_OwnerBrain;
        private EnemyNetState m_NetState;

        // Public getters for Inspector wiring verification
        public GameObject MissileProjectilePrefab => missileProjectilePrefab;
        public GameObject MuzzleFlashPrefab => muzzleFlashPrefab;
        public GameObject ExplosionVFXPrefab => explosionVFXPrefab;
        public GameObject TrungPhatLightningPrefab => trungPhatLightningPrefab;
        public GameObject LazeNguyenToPrefab => lazeNguyenToPrefab;
        public GameObject ToaNhietDienRongPrefab => toaNhietDienRongPrefab;
        public GameObject VienBinhSummonVFXPrefab => vienBinhSummonVFXPrefab;
        public GameObject ChargingAuraPrefab => chargingAuraPrefab;
        public GameObject StunVFXPrefab => stunVFXPrefab;
        public Transform[] GunBarrels => gunBarrels;
        public float GunMuzzleOffset => gunMuzzleOffset;
        public Transform LaserEmitterPoint => laserEmitterPoint;
        public Transform HeadPoint => headPoint;
        public GameObject[] NormalEnemyPrefabs => normalEnemyPrefabs;

        public D1onkChargingAuraVFX ActiveChargingAura => m_ActiveChargingAura;
        public D1onkStunVFX ActiveStunVFX => m_ActiveStunVFX;
        public D1onkLazeNguyenToVFX ActiveLaserVFX => m_ActiveLaserVFX;

        private void Awake()
        {
            m_OwnerBrain = GetComponent<EnemyBrain>();
            m_NetState = GetComponent<EnemyNetState>();
        }

        private void Start()
        {
            ValidateReferences();
        }

        /// <summary>
        /// Kiểm tra tường minh các tham chiếu Inspector, ghi log cảnh báo rõ ràng nếu thiếu.
        /// TUYỆT ĐỐI không tự ý AddComponent ngầm.
        /// </summary>
        public void ValidateReferences()
        {
            if (missileProjectilePrefab == null) Debug.LogWarning("[D1onkVFXController] Missing reference: 'missileProjectilePrefab' is not assigned!");
            if (trungPhatLightningPrefab == null) Debug.LogWarning("[D1onkVFXController] Missing reference: 'trungPhatLightningPrefab' is not assigned!");
            if (lazeNguyenToPrefab == null) Debug.LogWarning("[D1onkVFXController] Missing reference: 'lazeNguyenToPrefab' is not assigned!");
            if (toaNhietDienRongPrefab == null) Debug.LogWarning("[D1onkVFXController] Missing reference: 'toaNhietDienRongPrefab' is not assigned!");
            if (vienBinhSummonVFXPrefab == null) Debug.LogWarning("[D1onkVFXController] Missing reference: 'vienBinhSummonVFXPrefab' is not assigned!");
            if (chargingAuraPrefab == null) Debug.LogWarning("[D1onkVFXController] Missing reference: 'chargingAuraPrefab' is not assigned!");
            if (stunVFXPrefab == null) Debug.LogWarning("[D1onkVFXController] Missing reference: 'stunVFXPrefab' is not assigned!");

            if (gunBarrels == null || gunBarrels.Length < 14)
                Debug.LogWarning($"[D1onkVFXController] Missing gun barrel references: expected 14, got {gunBarrels?.Length ?? 0}!");

            if (normalEnemyPrefabs == null || normalEnemyPrefabs.Length == 0)
                Debug.LogWarning("[D1onkVFXController] Missing reference: 'normalEnemyPrefabs' array is empty!");
        }

        #region 1. Bị động — Pháo Hạm Đa Nòng (14 Guns Battery)

        /// <summary>
        /// Bắn một loạt đạn tên lửa từ toàn bộ 14 nòng súng hướng ra ngoài theo vectơ -gunTransform.right.
        /// Kích hoạt diễn hoạt phản lực đại bác (PhaoHamDaNong) trên thân và tay Boss.
        /// Đồng thời tạo hiệu ứng lửa nòng (Muzzle Flash) tại từng đầu nòng súng.
        /// </summary>
        public List<D1onkMissileProjectile> FireMultiBarrelSalvo(Transform target = null, float damage = 25f)
        {
            var spawnedMissiles = new List<D1onkMissileProjectile>();

            if (gunBarrels == null || gunBarrels.Length == 0)
            {
                Debug.LogWarning("[D1onkVFXController] Cannot fire salvo: gunBarrels list is empty!");
                return spawnedMissiles;
            }

            for (int i = 0; i < gunBarrels.Length; i++)
            {
                var missile = FireSingleBarrel(i, target, damage);
                if (missile != null) spawnedMissiles.Add(missile);
            }

            return spawnedMissiles;
        }

        /// <summary>
        /// Bắn ngẫu nhiên count nòng súng (ví dụ 5 viên khi thi triển skill, hoặc 2 viên định kỳ mỗi 5s theo GDD).
        /// Kích hoạt diễn hoạt phản lực bắn đại bác trên cơ thể Boss.
        /// </summary>
        public List<D1onkMissileProjectile> FireRandomBarrels(int count = 5, Transform target = null, float damage = 25f)
        {
            var spawnedMissiles = new List<D1onkMissileProjectile>();

            if (gunBarrels == null || gunBarrels.Length == 0)
            {
                Debug.LogWarning("[D1onkVFXController] Cannot fire random barrels: gunBarrels list is empty!");
                return spawnedMissiles;
            }

            count = Mathf.Clamp(count, 1, gunBarrels.Length);

            // Xáo trộn ngẫu nhiên thứ tự nòng súng
            var indices = new List<int>(gunBarrels.Length);
            for (int i = 0; i < gunBarrels.Length; i++) indices.Add(i);
            for (int i = 0; i < indices.Count; i++)
            {
                int r = UnityEngine.Random.Range(i, indices.Count);
                int temp = indices[i];
                indices[i] = indices[r];
                indices[r] = temp;
            }

            for (int i = 0; i < count; i++)
            {
                var missile = FireSingleBarrel(indices[i], target, damage);
                if (missile != null) spawnedMissiles.Add(missile);
            }

            return spawnedMissiles;
        }

        /// <summary>
        /// Bắn một tên lửa từ nòng súng chỉ định theo chỉ số barrelIndex (0 đến 13).
        /// Vectơ bắn ra khỏi đầu nòng là -gunTransform.right.
        /// </summary>
        public D1onkMissileProjectile FireSingleBarrel(int barrelIndex, Transform target = null, float damage = 25f)
        {
            if (gunBarrels == null || barrelIndex < 0 || barrelIndex >= gunBarrels.Length)
            {
                Debug.LogWarning($"[D1onkVFXController] Invalid gun barrel index {barrelIndex}!");
                return null;
            }

            Transform gun = gunBarrels[barrelIndex];
            if (gun == null)
            {
                Debug.LogWarning($"[D1onkVFXController] Gun barrel at index {barrelIndex} is null!");
                return null;
            }

            if (m_OwnerBrain == null)
            {
                m_OwnerBrain = GetComponent<EnemyBrain>() ?? GetComponentInParent<EnemyBrain>();
            }

            // Vectơ bắn ra khỏi đầu nòng: -gunTransform.right
            Vector2 fireDir = (Vector2)(-gun.right);
            Vector3 spawnPos = gun.position + (Vector3)(fireDir * gunMuzzleOffset);

            // Tạo hiệu ứng lửa nòng súng (Muzzle Flash)
            if (muzzleFlashPrefab != null)
            {
                float angle = Mathf.Atan2(fireDir.y, fireDir.x) * Mathf.Rad2Deg;
                Instantiate(muzzleFlashPrefab, spawnPos, Quaternion.Euler(0f, 0f, angle));
                m_NetState?.BroadcastVfx("MuzzleFlash", spawnPos, angle);
            }

            if (missileProjectilePrefab == null)
            {
                Debug.LogWarning("[D1onkVFXController] missileProjectilePrefab is missing!");
                return null;
            }

            GameObject go = Instantiate(missileProjectilePrefab, spawnPos, Quaternion.identity);
            D1onkMissileProjectile projectile = go.GetComponent<D1onkMissileProjectile>();
            if (projectile != null)
            {
                projectile.Initialize(fireDir, target, 12f, damage, m_OwnerBrain);
            }
            m_NetState?.BroadcastVfx("Missile", spawnPos, 0f, fireDir, 12f, 0f);
            return projectile;
        }

        #endregion

        #region 2. Kỹ năng — Trừng Phạt (TrungPhat)

        /// <summary>
        /// Kích hoạt kỹ năng Trừng Phạt: Diễn hoạt giơ tay tụ điện và giáng một tia sét từ trời xuống vị trí targetPosition.
        /// </summary>
        public D1onkTrungPhatVFX PlayTrungPhat(Vector3 targetPosition, float damage = 60f, Action onComplete = null)
        {
            if (trungPhatLightningPrefab == null)
            {
                Debug.LogWarning("[D1onkVFXController] trungPhatLightningPrefab is missing!");
                onComplete?.Invoke();
                return null;
            }

            GameObject go = Instantiate(trungPhatLightningPrefab, targetPosition, Quaternion.identity);
            D1onkTrungPhatVFX vfx = go.GetComponent<D1onkTrungPhatVFX>();
            if (vfx != null)
            {
                vfx.Initialize(targetPosition, damage, m_OwnerBrain, onComplete);
            }
            m_NetState?.BroadcastVfx("TrungPhat", targetPosition);
            return vfx;
        }

        /// <summary>
        /// Kích hoạt chuỗi sấm sét trừng phạt liên hoàn xung quanh mục tiêu hoặc trung tâm.
        /// </summary>
        public List<D1onkTrungPhatVFX> PlayTrungPhatBarrage(Vector3 center, int count = 3, float radius = 5f, float damage = 50f, Action onComplete = null)
        {
            var list = new List<D1onkTrungPhatVFX>();
            if (trungPhatLightningPrefab == null)
            {
                Debug.LogWarning("[D1onkVFXController] trungPhatLightningPrefab is missing!");
                onComplete?.Invoke();
                return list;
            }

            for (int i = 0; i < count; i++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * radius;
                Vector3 pos = center + new Vector3(offset.x, offset.y, 0f);
                GameObject go = Instantiate(trungPhatLightningPrefab, pos, Quaternion.identity);
                D1onkTrungPhatVFX vfx = go.GetComponent<D1onkTrungPhatVFX>();
                if (vfx != null)
                {
                    vfx.Initialize(pos, damage, m_OwnerBrain, i == count - 1 ? onComplete : null);
                    list.Add(vfx);
                }
                m_NetState?.BroadcastVfx("TrungPhat", pos);
            }

            return list;
        }

        #endregion

        #region 3. Kỹ năng — Laze Nguyên Tố (LazeNguyenTo)

        /// <summary>
        /// Kích hoạt kỹ năng Laze Nguyên Tố: Diễn hoạt dồn người về trước, phóng tia laser năng lượng cao tần theo hướng direction.
        /// </summary>
        public D1onkLazeNguyenToVFX PlayLazeNguyenTo(Vector3 direction, float duration = 2.5f, float damagePerSecond = 50f, Action onComplete = null)
        {
            if (lazeNguyenToPrefab == null)
            {
                Debug.LogWarning("[D1onkVFXController] lazeNguyenToPrefab is missing!");
                onComplete?.Invoke();
                return null;
            }

            Vector3 spawnPos = laserEmitterPoint != null ? laserEmitterPoint.position : transform.position;
            GameObject go = Instantiate(lazeNguyenToPrefab, spawnPos, Quaternion.identity);
            if (laserEmitterPoint != null)
            {
                go.transform.SetParent(laserEmitterPoint);
                go.transform.localPosition = Vector3.zero;
            }

            D1onkLazeNguyenToVFX vfx = go.GetComponent<D1onkLazeNguyenToVFX>();
            if (vfx != null)
            {
                m_ActiveLaserVFX = vfx;
                vfx.Initialize(direction, duration, damagePerSecond, m_OwnerBrain, () =>
                {
                    m_ActiveLaserVFX = null;
                    onComplete?.Invoke();
                });
            }
            m_NetState?.BroadcastVfx("LazeNguyenTo", spawnPos, 0f, direction, duration, 0f);
            return vfx;
        }

        #endregion

        #region 4. Kỹ năng — Tỏa Nhiệt Diện Rộng (ToaNhietDienRong)

        /// <summary>
        /// Kích hoạt kỹ năng Tỏa Nhiệt Diện Rộng: Diễn hoạt dang rộng 2 tay xả áp suất nhiệt, bung sóng nhiệt tròn hình xuyến.
        /// </summary>
        public D1onkToaNhietDienRongVFX PlayToaNhietDienRong(float damage = 40f, Action onComplete = null)
        {
            if (toaNhietDienRongPrefab == null)
            {
                Debug.LogWarning("[D1onkVFXController] toaNhietDienRongPrefab is missing!");
                onComplete?.Invoke();
                return null;
            }

            GameObject go = Instantiate(toaNhietDienRongPrefab, transform.position, Quaternion.identity);
            D1onkToaNhietDienRongVFX vfx = go.GetComponent<D1onkToaNhietDienRongVFX>();
            if (vfx != null)
            {
                vfx.Initialize(damage, m_OwnerBrain, onComplete);
            }
            m_NetState?.BroadcastVfx("ToaNhietDienRong", transform.position);
            return vfx;
        }

        #endregion

        #region 5. Kỹ năng — Viện Binh Tinh Linh (VienBinhTinhLinh)

        /// <summary>
        /// Kích hoạt kỹ năng Viện Binh Tinh Linh:
        /// Diễn hoạt ma pháp triệu hồi, sinh ra các vòng ma pháp và triệu hồi ngẫu nhiên từ 3 đến 5 quái thường trong danh sách.
        /// </summary>
        public List<D1onkVienBinhSummonVFX> SummonReinforcements(int count = -1, Action onComplete = null, Action<GameObject> onEnemySpawned = null)
        {
            // Nếu không truyền count thì chọn ngẫu nhiên 3 đến 5 theo yêu cầu kiến trúc
            if (count < 0)
            {
                count = UnityEngine.Random.Range(3, 6);
            }
            count = Mathf.Clamp(count, 3, 5);

            var spawnedSummonVFXList = new List<D1onkVienBinhSummonVFX>();

            if (vienBinhSummonVFXPrefab == null)
            {
                Debug.LogWarning("[D1onkVFXController] vienBinhSummonVFXPrefab is missing!");
                onComplete?.Invoke();
                return spawnedSummonVFXList;
            }

            if (normalEnemyPrefabs == null || normalEnemyPrefabs.Length == 0)
            {
                Debug.LogWarning("[D1onkVFXController] normalEnemyPrefabs list is empty, cannot summon reinforcements!");
                onComplete?.Invoke();
                return spawnedSummonVFXList;
            }

            // Sinh các vòng ma pháp ở các góc phân tán quanh Boss
            float baseAngle = UnityEngine.Random.Range(0f, 360f);
            float angleStep = 360f / count;
            for (int i = 0; i < count; i++)
            {
                float a = (baseAngle + i * angleStep + UnityEngine.Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
                float dist = UnityEngine.Random.Range(4.5f, 7.5f);
                Vector3 spawnPos = transform.position + new Vector3(Mathf.Cos(a) * dist, Mathf.Sin(a) * dist, 0f);

                // Chọn quái thường ngẫu nhiên trong danh sách
                int rndIdx = UnityEngine.Random.Range(0, normalEnemyPrefabs.Length);
                GameObject enemyPrefab = normalEnemyPrefabs[rndIdx];

                GameObject summonGo = Instantiate(vienBinhSummonVFXPrefab, spawnPos, Quaternion.identity);
                D1onkVienBinhSummonVFX summonVFX = summonGo.GetComponent<D1onkVienBinhSummonVFX>();
                if (summonVFX != null)
                {
                    summonVFX.Initialize(enemyPrefab, onEnemySpawned);
                    spawnedSummonVFXList.Add(summonVFX);
                }
                m_NetState?.BroadcastVfx("VienBinhSummon", spawnPos);
            }

            return spawnedSummonVFXList;
        }

        #endregion

        #region 6. Tụ Năng Lượng (Charging / BurstCharging)

        /// <summary>
        /// Bật hào quang tụ năng lượng và chuyển diễn hoạt sang tư thế Charging.
        /// </summary>
        public D1onkChargingAuraVFX StartCharging()
        {
            if (m_ActiveChargingAura == null && chargingAuraPrefab != null)
            {
                GameObject go = Instantiate(chargingAuraPrefab, transform);
                go.transform.localPosition = Vector3.zero;
                m_ActiveChargingAura = go.GetComponent<D1onkChargingAuraVFX>();
            }

            if (m_ActiveChargingAura != null)
            {
                m_ActiveChargingAura.Play();
            }
            m_NetState?.BroadcastVfx("StartCharging", transform.position);
            return m_ActiveChargingAura;
        }

        /// <summary>
        /// Tắt hào quang tụ năng lượng và hồi diễn hoạt về Idle.
        /// </summary>
        public void StopCharging()
        {
            if (m_ActiveChargingAura != null)
            {
                m_ActiveChargingAura.Stop();
                m_ActiveChargingAura = null;
            }
            m_NetState?.BroadcastVfx("StopCharging", transform.position);
        }

        #endregion

        #region 7. Choáng Váng (Stun) & Tiêu Diệt (Die)

        /// <summary>
        /// Kích hoạt trạng thái Choáng Váng (Stun): Diễn hoạt gục đầu buông thõng tay và hồ quang sao quay quanh đầu.
        /// onComplete chỉ được gọi duy nhất 1 lần khi trạng thái choáng kết thúc.
        /// </summary>
        public D1onkStunVFX PlayStun(float duration = 3f, Action onComplete = null)
        {
            if (m_ActiveStunVFX == null && stunVFXPrefab != null)
            {
                Vector3 headPos = headPoint != null ? headPoint.position : transform.position + new Vector3(0f, 2.5f, 0f);
                GameObject go = Instantiate(stunVFXPrefab, headPos, Quaternion.identity);
                if (headPoint != null) go.transform.SetParent(headPoint);
                m_ActiveStunVFX = go.GetComponent<D1onkStunVFX>();
            }

            if (m_ActiveStunVFX != null)
            {
                m_ActiveStunVFX.Play(duration, () =>
                {
                    m_ActiveStunVFX = null;
                    onComplete?.Invoke();
                });
            }
            else
            {
                onComplete?.Invoke();
            }
            m_NetState?.BroadcastVfx("StartStun", transform.position, duration: duration);
            return m_ActiveStunVFX;
        }

        /// <summary>
        /// Tắt hiệu ứng choáng váng sớm.
        /// </summary>
        public void StopStun()
        {
            if (m_ActiveStunVFX != null)
            {
                m_ActiveStunVFX.Stop();
                m_ActiveStunVFX = null;
            }
            m_NetState?.BroadcastVfx("StopStun", transform.position);
        }

        /// <summary>
        /// Kích hoạt diễn hoạt sập nguồn / tiêu diệt (Die).
        /// </summary>
        public void PlayDie()
        {
        }

        private void OnDestroy()
        {
            if (m_ActiveChargingAura != null)
            {
                if (Application.isPlaying) Destroy(m_ActiveChargingAura.gameObject);
                else DestroyImmediate(m_ActiveChargingAura.gameObject);
                m_ActiveChargingAura = null;
            }

            if (m_ActiveStunVFX != null)
            {
                if (Application.isPlaying) Destroy(m_ActiveStunVFX.gameObject);
                else DestroyImmediate(m_ActiveStunVFX.gameObject);
                m_ActiveStunVFX = null;
            }

            if (m_ActiveLaserVFX != null)
            {
                if (Application.isPlaying) Destroy(m_ActiveLaserVFX.gameObject);
                else DestroyImmediate(m_ActiveLaserVFX.gameObject);
                m_ActiveLaserVFX = null;
            }
        }

        #endregion

        public void OnClientNetVfx(string key, Vector2 position, float rotationZ, Vector2 dir, float duration, float value)
        {
            switch (key)
            {
                case "MuzzleFlash":
                    if (muzzleFlashPrefab != null)
                    {
                        Instantiate(muzzleFlashPrefab, position, Quaternion.Euler(0f, 0f, rotationZ));
                    }
                    break;
                case "Missile":
                    if (missileProjectilePrefab != null)
                    {
                        GameObject go = Instantiate(missileProjectilePrefab, position, Quaternion.identity);
                        D1onkMissileProjectile projectile = go.GetComponent<D1onkMissileProjectile>();
                        projectile?.Initialize(dir, null, duration > 0f ? duration : 12f, 0f, null);
                    }
                    break;
                case "TrungPhat":
                    if (trungPhatLightningPrefab != null)
                    {
                        GameObject go = Instantiate(trungPhatLightningPrefab, position, Quaternion.identity);
                        D1onkTrungPhatVFX vfx = go.GetComponent<D1onkTrungPhatVFX>();
                        vfx?.Initialize(position, 0f, null, null);
                    }
                    break;
                case "LazeNguyenTo":
                    if (lazeNguyenToPrefab != null)
                    {
                        Vector3 spawnPos = laserEmitterPoint != null ? laserEmitterPoint.position : (Vector3)position;
                        GameObject go = Instantiate(lazeNguyenToPrefab, spawnPos, Quaternion.identity);
                        if (laserEmitterPoint != null)
                        {
                            go.transform.SetParent(laserEmitterPoint);
                            go.transform.localPosition = Vector3.zero;
                        }
                        D1onkLazeNguyenToVFX vfx = go.GetComponent<D1onkLazeNguyenToVFX>();
                        vfx?.Initialize(dir, duration > 0f ? duration : 2.5f, 0f, null, null);
                    }
                    break;
                case "ToaNhietDienRong":
                    if (toaNhietDienRongPrefab != null)
                    {
                        GameObject go = Instantiate(toaNhietDienRongPrefab, position, Quaternion.identity);
                        D1onkToaNhietDienRongVFX vfx = go.GetComponent<D1onkToaNhietDienRongVFX>();
                        vfx?.Initialize(0f, null, null);
                    }
                    break;
                case "VienBinhSummon":
                    if (vienBinhSummonVFXPrefab != null)
                    {
                        GameObject go = Instantiate(vienBinhSummonVFXPrefab, position, Quaternion.identity);
                        D1onkVienBinhSummonVFX vfx = go.GetComponent<D1onkVienBinhSummonVFX>();
                        // Client chỉ diễn hoạt vòng ma pháp, không tự spawn quái (Host sẽ spawn qua NetSpawnService).
                        vfx?.Initialize(null, null);
                    }
                    break;
                case "StartCharging":
                    if (m_ActiveChargingAura == null && chargingAuraPrefab != null)
                    {
                        GameObject go = Instantiate(chargingAuraPrefab, transform);
                        go.transform.localPosition = Vector3.zero;
                        m_ActiveChargingAura = go.GetComponent<D1onkChargingAuraVFX>();
                    }
                    m_ActiveChargingAura?.Play();
                    break;
                case "StopCharging":
                    if (m_ActiveChargingAura != null)
                    {
                        m_ActiveChargingAura.Stop();
                        m_ActiveChargingAura = null;
                    }
                    break;
                case "StartStun":
                    if (m_ActiveStunVFX == null && stunVFXPrefab != null)
                    {
                        Vector3 headPos = headPoint != null ? headPoint.position : transform.position + new Vector3(0f, 2.5f, 0f);
                        GameObject go = Instantiate(stunVFXPrefab, headPos, Quaternion.identity);
                        if (headPoint != null) go.transform.SetParent(headPoint);
                        m_ActiveStunVFX = go.GetComponent<D1onkStunVFX>();
                    }
                    m_ActiveStunVFX?.Play(duration > 0f ? duration : 3f, () => { m_ActiveStunVFX = null; });
                    break;
                case "StopStun":
                    if (m_ActiveStunVFX != null)
                    {
                        m_ActiveStunVFX.Stop();
                        m_ActiveStunVFX = null;
                    }
                    break;
                case "MothershipFloor":
                    EnsureClientMothershipFloor(position, duration > 0f ? duration : 15f);
                    break;
                case "MothershipCleanup":
                    CleanupClientMothershipFloor();
                    break;
            }
        }

        private GameObject m_ClientMothershipFloor;

        private void EnsureClientMothershipFloor(Vector2 center, float radius)
        {
            if (m_ClientMothershipFloor != null) return;
            GameObject floorGo = new GameObject("Mothership_Floor_Client");
            floorGo.transform.position = center;
            SpriteRenderer sr = floorGo.AddComponent<SpriteRenderer>();
            sr.sprite = IronWasteland.Utilities.SpriteFactory.SoftCircle;
            sr.color = new Color(0.08f, 0.04f, 0.14f, 0.92f);
            sr.transform.localScale = Vector3.one * (radius * 2f);
            sr.sortingOrder = -10;
            m_ClientMothershipFloor = floorGo;
        }

        private void CleanupClientMothershipFloor()
        {
            if (m_ClientMothershipFloor != null)
            {
                Destroy(m_ClientMothershipFloor);
                m_ClientMothershipFloor = null;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            AutoWireIfEmpty();
        }

        public void AutoWireIfEmpty()
        {
            Transform root = transform;
            Transform model = root.Find("EnemyModel") ?? root;
            Transform ship = model.Find("Chiến hạm");
            Transform than = ship != null ? ship.Find("Thân") : null;

            if (headPoint == null && than != null)
            {
                headPoint = than.Find("Đầu");
            }

            if (laserEmitterPoint == null)
            {
                laserEmitterPoint = than ?? ship ?? root;
            }

            if ((gunBarrels == null || gunBarrels.Length < 14) && ship != null)
            {
                var s2 = ship.Find("Súng 2 nòng");
                var s3 = ship.Find("Súng 3 nòng");
                if (s2 != null && s3 != null)
                {
                    var gunList = new List<Transform>();
                    foreach (Transform c in s2) gunList.Add(c);
                    foreach (Transform c in s3) gunList.Add(c);
                    gunBarrels = gunList.ToArray();
                }
            }
        }
#endif
    }
}
