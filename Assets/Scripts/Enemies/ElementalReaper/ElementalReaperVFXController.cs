using System;
using IronWasteland.Enemies.ElementalReaper.Hitboxes;
using IronWasteland.Enemies.ElementalReaper.VFX;
using UnityEngine;

namespace IronWasteland.Enemies.ElementalReaper
{
    /// <summary>
    /// Master VFX & Animation Controller gắn trên Boss Tử Thần Nguyên Tố (TuThanNguyenTo):
    /// Quản lý việc kích hoạt và điều phối toàn bộ 10 hiệu ứng VFX kỹ năng và hoạt ảnh khung xương 2D:
    /// 1. Hào Quang Nguyên Tố (VFX_ElementalAura) — 3 hệ Thủy / Độc / Hỏa
    /// 2. Sóng Thủy Triều (VFX_ThuyTrieuHitbox) — nón 4m góc 60°
    /// 3. Thủy Liệu Trị Liệu (VFX_ThuyLieu) — vòng xoáy ngọc bích
    /// 4. Độc Quật (VFX_DocQuatHitbox) — bán nguyệt 180° 4m
    /// 5. Loạn Chuyển & Nổ Độc (VFX_LoanChuyenBurstHitbox) — nổ 3m
    /// 6. Phun Dung Nham (VFX_PhunDungNhamHitbox) — tia thẳng 16m x 1.2m
    /// 7. Lốc Xoáy Lửa (VFX_FireTornadoZone) — cột lửa 2m
    /// 8. Vệt Lửa Dung Nham (VFX_HoaTocFireTrailZone) — tồn tại 10s trên đường lướt
    /// 9. Vòng Ma Pháp Tam Nguyên Tụ Lực (VFX_BurstChargingAura)
    /// 10. Khiên Nguyên Tố 25% HP & Tinh Thể (VFX_BurstShieldAndCrystals)
    /// </summary>
    [DisallowMultipleComponent]
    public class ElementalReaperVFXController : MonoBehaviour, IronWasteland.Net.INetVfxReceiver
    {
        [Header("VFX Prefabs")]
        [Tooltip("Prefab VFX 1: Hào quang quanh Boss đổi màu 3 hệ Thủy/Độc/Hỏa.")]
        [SerializeField] private GameObject elementalAuraPrefab;

        [Tooltip("Prefab VFX 2: Sóng thủy triều hình nón 4m góc 60°.")]
        [SerializeField] private GameObject thuyTrieuHitboxPrefab;

        [Tooltip("Prefab VFX 3: Vòng xoáy nước hồi phục kèm bong bóng ngọc bích.")]
        [SerializeField] private GameObject thuyLieuVFXPrefab;

        [Tooltip("Prefab VFX 4: Độc quật bán nguyệt 180° bán kính 4m.")]
        [SerializeField] private GameObject docQuatHitboxPrefab;

        [Tooltip("Prefab VFX 5: Khói độc nổ bùng hình cầu bán kính 3m.")]
        [SerializeField] private GameObject loanChuyenBurstHitboxPrefab;

        [Tooltip("Prefab VFX 6: Tia dung nham thẳng 16m x 1.2m đỏ rực.")]
        [SerializeField] private GameObject phunDungNhamHitboxPrefab;

        [Tooltip("Prefab VFX 7: Cột lốc xoáy lửa 2m tại mục tiêu xa nhất.")]
        [SerializeField] private GameObject fireTornadoZonePrefab;

        [Tooltip("Prefab VFX 8: Vệt lửa dung nham tồn tại 10s trên đường lướt.")]
        [SerializeField] private GameObject hoaTocFireTrailZonePrefab;

        [Tooltip("Prefab VFX 9: Vòng ma pháp tam nguyên tố tụ lực dưới chân Boss.")]
        [SerializeField] private GameObject burstChargingAuraPrefab;

        [Tooltip("Prefab VFX 10: Khiên nguyên tố 25% HP kèm tinh thể xoay quanh.")]
        [SerializeField] private GameObject burstShieldAndCrystalsPrefab;

        [Header("Dash Hitbox")]
        [Tooltip("Hitbox càn quét khi Boss lướt Hỏa Tốc (đẩy dạt 2 bên 2m, 100% ATK).")]
        [SerializeField] private HoaTocDashHitbox hoaTocDashHitbox;

        // Active instances
        private ElementalAuraVFX m_ActiveElementalAura;
        private BurstChargingAuraVFX m_ActiveBurstChargingAura;
        private BurstShieldVFX m_ActiveBurstShield;
        private Animator m_Animator;
        private EnemyBrain m_OwnerBrain;
        private IronWasteland.Net.EnemyNetState m_NetState;

        public ElementalAuraVFX ActiveElementalAura => m_ActiveElementalAura;
        public BurstChargingAuraVFX ActiveBurstChargingAura => m_ActiveBurstChargingAura;
        public BurstShieldVFX ActiveBurstShield => m_ActiveBurstShield;
        public HoaTocDashHitbox HoaTocDashHitboxComponent => hoaTocDashHitbox;
        public Animator BossAnimator => m_Animator;

        public GameObject ElementalAuraPrefab => elementalAuraPrefab;
        public GameObject ThuyTrieuHitboxPrefab => thuyTrieuHitboxPrefab;
        public GameObject ThuyLieuVFXPrefab => thuyLieuVFXPrefab;
        public GameObject DocQuatHitboxPrefab => docQuatHitboxPrefab;
        public GameObject LoanChuyenBurstHitboxPrefab => loanChuyenBurstHitboxPrefab;
        public GameObject PhunDungNhamHitboxPrefab => phunDungNhamHitboxPrefab;
        public GameObject FireTornadoZonePrefab => fireTornadoZonePrefab;
        public GameObject HoaTocFireTrailZonePrefab => hoaTocFireTrailZonePrefab;
        public GameObject BurstChargingAuraPrefab => burstChargingAuraPrefab;
        public GameObject BurstShieldAndCrystalsPrefab => burstShieldAndCrystalsPrefab;

        private void Awake()
        {
            m_Animator = GetComponentInChildren<Animator>();
            m_OwnerBrain = GetComponent<EnemyBrain>();
            m_NetState = GetComponent<IronWasteland.Net.EnemyNetState>();
            InitializeAura();
        }

        private void InitializeAura()
        {
            if (m_ActiveElementalAura == null && elementalAuraPrefab != null)
            {
                GameObject auraGo = Instantiate(elementalAuraPrefab, transform);
                auraGo.transform.localPosition = Vector3.zero;
                m_ActiveElementalAura = auraGo.GetComponent<ElementalAuraVFX>();
            }
        }

        #region VFX 1: Hào Quang Nguyên Tố (Elemental Aura)

        /// <summary>
        /// Thay đổi dạng nguyên tố và hào quang phát sáng tương ứng (Thủy / Độc / Hỏa).
        /// </summary>
        public void SetElementalAura(ElementalType type)
        {
            if (m_ActiveElementalAura == null) InitializeAura();
            if (m_ActiveElementalAura != null)
            {
                m_ActiveElementalAura.SetElement(type);
            }
            m_NetState?.BroadcastVfx("ElementalAura", transform.position, value: (int)type);
        }

        #endregion

        #region VFX 2: Sóng Thủy Triều (ThuyTrieu)

        /// <summary>
        /// Kích hoạt Sóng Thủy Triều nón 4m góc 60° phía trước mặt Boss.
        /// </summary>
        public ThuyTrieuHitbox PlayThuyTrieu(Vector3? targetDirection = null, Action onComplete = null)
        {
            if (m_Animator != null) m_Animator.SetTrigger("ThuyTrieu");

            if (thuyTrieuHitboxPrefab == null)
            {
                Debug.LogWarning("[ElementalReaperVFX] thuyTrieuHitboxPrefab is missing!");
                return null;
            }

            Vector3 spawnPos = transform.position;
            Quaternion rot = transform.rotation;
            if (targetDirection.HasValue && targetDirection.Value.sqrMagnitude > 0.001f)
            {
                float angle = Mathf.Atan2(targetDirection.Value.y, targetDirection.Value.x) * Mathf.Rad2Deg;
                rot = Quaternion.Euler(0f, 0f, angle);
            }

            GameObject go = Instantiate(thuyTrieuHitboxPrefab, spawnPos, rot);
            ThuyTrieuHitbox hitbox = go.GetComponent<ThuyTrieuHitbox>();
            if (hitbox != null)
            {
                float atk = m_OwnerBrain != null ? m_OwnerBrain.Stats.Attack : 250f;
                hitbox.Initialize(m_OwnerBrain, atk);
            }
            m_NetState?.BroadcastAnimTrigger("ThuyTrieu");
            m_NetState?.BroadcastVfx("ThuyTrieu", spawnPos, rot.eulerAngles.z);

            onComplete?.Invoke();
            return hitbox;
        }

        #endregion

        #region VFX 3: Thủy Liệu Trị Liệu (ThuyLieu)

        /// <summary>
        /// Kích hoạt vòng xoáy nước bao bọc thân Boss kèm bong bóng ngọc bích.
        /// </summary>
        public ThuyLieuVFX PlayThuyLieu(Action onComplete = null)
        {
            if (m_Animator != null) m_Animator.SetTrigger("ThuyLieu");

            if (thuyLieuVFXPrefab == null)
            {
                Debug.LogWarning("[ElementalReaperVFX] thuyLieuVFXPrefab is missing!");
                return null;
            }

            GameObject go = Instantiate(thuyLieuVFXPrefab, transform);
            go.transform.localPosition = Vector3.zero;
            ThuyLieuVFX vfx = go.GetComponent<ThuyLieuVFX>();
            if (vfx != null)
            {
                vfx.Play(onComplete);
            }
            m_NetState?.BroadcastAnimTrigger("ThuyLieu");
            m_NetState?.BroadcastVfx("ThuyLieu", transform.position);
            return vfx;
        }

        #endregion

        #region VFX 4: Độc Quật (DocQuat)

        /// <summary>
        /// Kích hoạt vung Độc Quật bán nguyệt 180° bán kính 4m quét vùng theo hướng chỉ định hoặc trước mặt Boss.
        /// </summary>
        public DocQuatHitbox PlayDocQuat(Vector3? targetDirection = null, Action onComplete = null)
        {
            if (m_Animator != null) m_Animator.SetTrigger("DocQuat");

            if (docQuatHitboxPrefab == null)
            {
                Debug.LogWarning("[ElementalReaperVFX] docQuatHitboxPrefab is missing!");
                return null;
            }

            Vector3 spawnPos = transform.position;
            Quaternion rot = transform.rotation;
            if (targetDirection.HasValue && targetDirection.Value.sqrMagnitude > 0.001f)
            {
                float angle = Mathf.Atan2(targetDirection.Value.y, targetDirection.Value.x) * Mathf.Rad2Deg;
                rot = Quaternion.Euler(0f, 0f, angle);
            }
            else if (m_OwnerBrain is ElementalReaperBrain reaperBrain && reaperBrain.RotationPivot != null)
            {
                rot = reaperBrain.RotationPivot.rotation;
            }

            GameObject go = Instantiate(docQuatHitboxPrefab, spawnPos, rot);
            DocQuatHitbox hitbox = go.GetComponent<DocQuatHitbox>();
            if (hitbox != null)
            {
                float atk = m_OwnerBrain != null ? m_OwnerBrain.Stats.Attack : 250f;
                hitbox.Initialize(m_OwnerBrain, atk);
            }
            m_NetState?.BroadcastAnimTrigger("DocQuat");
            m_NetState?.BroadcastVfx("DocQuat", spawnPos, rot.eulerAngles.z);

            onComplete?.Invoke();
            return hitbox;
        }

        #endregion

        #region VFX 5: Loạn Chuyển & Nổ Độc (LoanChuyen)

        /// <summary>
        /// Kích hoạt nổ độc hình cầu bán kính 3m tại vị trí chỉ định khi Boss teleport.
        /// </summary>
        public LoanChuyenBurstHitbox PlayLoanChuyenBurst(Vector3 position, Action onComplete = null)
        {
            if (m_Animator != null) m_Animator.SetTrigger("LoanChuyen");

            if (loanChuyenBurstHitboxPrefab == null)
            {
                Debug.LogWarning("[ElementalReaperVFX] loanChuyenBurstHitboxPrefab is missing!");
                return null;
            }

            GameObject go = Instantiate(loanChuyenBurstHitboxPrefab, position, Quaternion.identity);
            LoanChuyenBurstHitbox hitbox = go.GetComponent<LoanChuyenBurstHitbox>();
            if (hitbox != null)
            {
                float atk = m_OwnerBrain != null ? m_OwnerBrain.Stats.Attack : 250f;
                hitbox.Initialize(m_OwnerBrain, atk);
            }
            m_NetState?.BroadcastAnimTrigger("LoanChuyen");
            m_NetState?.BroadcastVfx("LoanChuyen", position);

            onComplete?.Invoke();
            return hitbox;
        }

        #endregion

        #region VFX 6: Phun Dung Nham (PhunDungNham)

        /// <summary>
        /// Phóng tia dung nham 16m x 1.2m theo hướng chỉ định.
        /// </summary>
        public PhunDungNhamHitbox PlayPhunDungNham(Vector3 direction, Action onComplete = null)
        {
            if (m_Animator != null) m_Animator.SetTrigger("PhunDungNham");

            if (phunDungNhamHitboxPrefab == null)
            {
                Debug.LogWarning("[ElementalReaperVFX] phunDungNhamHitboxPrefab is missing!");
                return null;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion rot = Quaternion.Euler(0f, 0f, angle);

            GameObject go = Instantiate(phunDungNhamHitboxPrefab, transform.position, rot);
            PhunDungNhamHitbox hitbox = go.GetComponent<PhunDungNhamHitbox>();
            if (hitbox != null)
            {
                float atk = m_OwnerBrain != null ? m_OwnerBrain.Stats.Attack : 250f;
                hitbox.Initialize(m_OwnerBrain, atk);
            }
            m_NetState?.BroadcastAnimTrigger("PhunDungNham");
            m_NetState?.BroadcastVfx("PhunDungNham", transform.position, angle);

            onComplete?.Invoke();
            return hitbox;
        }

        #endregion

        #region VFX 7: Cột Lốc Xoáy Lửa (FireTornado)

        /// <summary>
        /// Sinh cột lốc xoáy lửa bán kính 2m tại mục tiêu xa nhất.
        /// </summary>
        public FireTornadoZone SpawnFireTornado(Vector3 targetPosition, Action onComplete = null)
        {
            if (m_Animator != null) m_Animator.SetTrigger("HoaToc_Charge");

            if (fireTornadoZonePrefab == null)
            {
                Debug.LogWarning("[ElementalReaperVFX] fireTornadoZonePrefab is missing!");
                return null;
            }

            GameObject go = Instantiate(fireTornadoZonePrefab, targetPosition, Quaternion.identity);
            FireTornadoZone zone = go.GetComponent<FireTornadoZone>();
            if (zone != null)
            {
                float atk = m_OwnerBrain != null ? m_OwnerBrain.Stats.Attack : 250f;
                zone.Initialize(m_OwnerBrain, atk);
            }
            m_NetState?.BroadcastAnimTrigger("HoaToc_Charge");
            m_NetState?.BroadcastVfx("FireTornado", targetPosition);

            onComplete?.Invoke();
            return zone;
        }

        #endregion

        #region VFX 8: Vệt Lửa Dung Nham (HoaTocFireTrail)

        /// <summary>
        /// Sinh vệt lửa dung nham tồn tại 10s trên quãng đường lướt giữa startPos và endPos.
        /// </summary>
        public HoaTocFireTrailZone SpawnHoaTocFireTrail(Vector3 startPos, Vector3 endPos)
        {
            if (hoaTocFireTrailZonePrefab == null)
            {
                Debug.LogWarning("[ElementalReaperVFX] hoaTocFireTrailZonePrefab is missing!");
                return null;
            }

            GameObject go = Instantiate(hoaTocFireTrailZonePrefab, Vector3.zero, Quaternion.identity);
            HoaTocFireTrailZone trail = go.GetComponent<HoaTocFireTrailZone>();
            if (trail != null)
            {
                float atk = m_OwnerBrain != null ? m_OwnerBrain.Stats.Attack : 250f;
                trail.SetupTrail(startPos, endPos, m_OwnerBrain, atk);
            }
            m_NetState?.BroadcastVfx("HoaTocFireTrail", startPos, 0f, endPos);
            return trail;
        }

        #endregion

        #region VFX 9: Vòng Ma Pháp Tam Nguyên Tụ Lực (BurstCharging)

        /// <summary>
        /// Bắt đầu vận sức bùng nổ (vòng ma pháp tam nguyên xoay tròn hội tụ dưới chân Boss).
        /// </summary>
        public BurstChargingAuraVFX StartBurstCharging(float duration = 2.0f, Action onComplete = null)
        {
            if (m_Animator != null) m_Animator.SetTrigger("BurstCharging");

            if (m_ActiveBurstChargingAura == null && burstChargingAuraPrefab != null)
            {
                GameObject go = Instantiate(burstChargingAuraPrefab, transform);
                go.transform.localPosition = Vector3.zero;
                m_ActiveBurstChargingAura = go.GetComponent<BurstChargingAuraVFX>();
            }

            if (m_ActiveBurstChargingAura != null)
            {
                m_ActiveBurstChargingAura.StartCharging(duration, onComplete);
            }
            m_NetState?.BroadcastAnimTrigger("BurstCharging");
            m_NetState?.BroadcastVfx("BurstChargingStart", transform.position, duration: duration);
            return m_ActiveBurstChargingAura;
        }

        /// <summary>
        /// Dừng vận sức bùng nổ.
        /// </summary>
        public void StopBurstCharging()
        {
            if (m_ActiveBurstChargingAura != null)
            {
                m_ActiveBurstChargingAura.StopCharging();
            }
            m_NetState?.BroadcastVfx("BurstChargingStop", transform.position);
        }

        #endregion

        #region VFX 10: Khiên Nguyên Tố 25% HP & Tinh Thể (BurstShield)

        /// <summary>
        /// Kích hoạt khiên nguyên tố tam sắc và 3 tinh thể quay quanh Boss.
        /// </summary>
        public BurstShieldVFX ActivateBurstShield()
        {
            if (m_ActiveBurstShield == null && burstShieldAndCrystalsPrefab != null)
            {
                GameObject go = Instantiate(burstShieldAndCrystalsPrefab, transform);
                go.transform.localPosition = Vector3.zero;
                m_ActiveBurstShield = go.GetComponent<BurstShieldVFX>();
            }

            if (m_ActiveBurstShield != null)
            {
                m_ActiveBurstShield.SetActiveState(true);
            }
            m_NetState?.BroadcastVfx("BurstShieldActivate", transform.position);
            return m_ActiveBurstShield;
        }

        /// <summary>
        /// Hủy / tắt khiên nguyên tố.
        /// </summary>
        public void DeactivateBurstShield()
        {
            if (m_ActiveBurstShield != null)
            {
                m_ActiveBurstShield.SetActiveState(false);
                if (Application.isPlaying) Destroy(m_ActiveBurstShield.gameObject);
                else DestroyImmediate(m_ActiveBurstShield.gameObject);
                m_ActiveBurstShield = null;
            }
            m_NetState?.BroadcastVfx("BurstShieldDeactivate", transform.position);
        }

        private readonly System.Collections.Generic.List<GameObject> m_ClientAltarFloors =
            new System.Collections.Generic.List<GameObject>();

        public void OnClientNetVfx(string key, Vector2 position, float rotationZ, Vector2 dir, float duration, float value)
        {
            Quaternion rot = Quaternion.Euler(0f, 0f, rotationZ);
            switch (key)
            {
                case "ThuyTrieu":
                    if (thuyTrieuHitboxPrefab != null)
                    {
                        var go = Instantiate(thuyTrieuHitboxPrefab, position, rot);
                        go.GetComponent<ThuyTrieuHitbox>()?.Initialize(null, 0f);
                    }
                    break;
                case "ThuyLieu":
                    if (thuyLieuVFXPrefab != null)
                    {
                        var go = Instantiate(thuyLieuVFXPrefab, transform);
                        go.transform.localPosition = Vector3.zero;
                        go.GetComponent<ThuyLieuVFX>()?.Play(null);
                    }
                    break;
                case "DocQuat":
                    if (docQuatHitboxPrefab != null)
                    {
                        var go = Instantiate(docQuatHitboxPrefab, position, rot);
                        go.GetComponent<DocQuatHitbox>()?.Initialize(null, 0f);
                    }
                    break;
                case "LoanChuyen":
                    if (loanChuyenBurstHitboxPrefab != null)
                    {
                        var go = Instantiate(loanChuyenBurstHitboxPrefab, position, Quaternion.identity);
                        go.GetComponent<LoanChuyenBurstHitbox>()?.Initialize(null, 0f);
                    }
                    break;
                case "PhunDungNham":
                    if (phunDungNhamHitboxPrefab != null)
                    {
                        var go = Instantiate(phunDungNhamHitboxPrefab, position, rot);
                        go.GetComponent<PhunDungNhamHitbox>()?.Initialize(null, 0f);
                    }
                    break;
                case "FireTornado":
                    if (fireTornadoZonePrefab != null)
                    {
                        var go = Instantiate(fireTornadoZonePrefab, position, Quaternion.identity);
                        go.GetComponent<FireTornadoZone>()?.Initialize(null, 0f);
                    }
                    break;
                case "HoaTocFireTrail":
                    if (hoaTocFireTrailZonePrefab != null)
                    {
                        var go = Instantiate(hoaTocFireTrailZonePrefab, Vector3.zero, Quaternion.identity);
                        go.GetComponent<HoaTocFireTrailZone>()?.SetupTrail(position, dir, null, 0f);
                    }
                    break;
                case "BurstChargingStart":
                    if (m_ActiveBurstChargingAura == null && burstChargingAuraPrefab != null)
                    {
                        var go = Instantiate(burstChargingAuraPrefab, transform);
                        go.transform.localPosition = Vector3.zero;
                        m_ActiveBurstChargingAura = go.GetComponent<BurstChargingAuraVFX>();
                    }
                    m_ActiveBurstChargingAura?.StartCharging(duration > 0f ? duration : 2f, null);
                    break;
                case "BurstChargingStop":
                    m_ActiveBurstChargingAura?.StopCharging();
                    break;
                case "BurstShieldActivate":
                    if (m_ActiveBurstShield == null && burstShieldAndCrystalsPrefab != null)
                    {
                        var go = Instantiate(burstShieldAndCrystalsPrefab, transform);
                        go.transform.localPosition = Vector3.zero;
                        m_ActiveBurstShield = go.GetComponent<BurstShieldVFX>();
                    }
                    m_ActiveBurstShield?.SetActiveState(true);
                    break;
                case "BurstShieldDeactivate":
                    if (m_ActiveBurstShield != null)
                    {
                        m_ActiveBurstShield.SetActiveState(false);
                        Destroy(m_ActiveBurstShield.gameObject);
                        m_ActiveBurstShield = null;
                    }
                    break;
                case "AltarFloor":
                    var floorGo = new GameObject("AltarFloor_Client");
                    floorGo.transform.position = position;
                    var sr = floorGo.AddComponent<SpriteRenderer>();
                    sr.sprite = IronWasteland.Utilities.SpriteFactory.SoftCircle;
                    sr.color = new Color(0.12f, 0.06f, 0.16f, 0.9f);
                    sr.transform.localScale = Vector3.one * ((duration > 0f ? duration : 10f) * 2f);
                    sr.sortingOrder = -10;
                    m_ClientAltarFloors.Add(floorGo);
                    break;
                case "AltarCleanup":
                    for (int i = 0; i < m_ClientAltarFloors.Count; i++)
                    {
                        if (m_ClientAltarFloors[i] != null) Destroy(m_ClientAltarFloors[i]);
                    }
                    m_ClientAltarFloors.Clear();
                    break;
                case "ElementalAura":
                    if (m_ActiveElementalAura == null) InitializeAura();
                    m_ActiveElementalAura?.SetElement((ElementalType)(int)value);
                    break;
            }
        }

        #endregion

        #region Hoạt ảnh phụ trợ (Stun, Die, Dash)

        public void PlayHoaTocDashAnimation()
        {
            if (m_Animator != null) m_Animator.SetTrigger("HoaToc_Dash");
            m_NetState?.BroadcastAnimTrigger("HoaToc_Dash");
        }

        /// <summary>
        /// Kích hoạt càn quét khi Boss lướt Hỏa Tốc: bật hitbox và hoạt ảnh lướt.
        /// </summary>
        public void ActivateHoaTocDash(Vector2 dashDirection)
        {
            PlayHoaTocDashAnimation();
            if (hoaTocDashHitbox != null)
            {
                hoaTocDashHitbox.gameObject.SetActive(true);
                float atk = m_OwnerBrain != null ? m_OwnerBrain.Stats.Attack : 250f;
                hoaTocDashHitbox.Activate(m_OwnerBrain, atk, dashDirection);
            }
        }

        /// <summary>
        /// Tắt hitbox càn quét khi kết thúc lướt Hỏa Tốc.
        /// </summary>
        public void DeactivateHoaTocDash()
        {
            if (hoaTocDashHitbox != null)
            {
                hoaTocDashHitbox.Deactivate();
                hoaTocDashHitbox.gameObject.SetActive(false);
            }
        }

        public void PlayStunAnimation()
        {
            if (m_Animator != null) m_Animator.SetTrigger("Stun");
            m_NetState?.BroadcastAnimTrigger("Stun");
        }

        public void PlayDieAnimation()
        {
            if (m_Animator != null) m_Animator.SetTrigger("Die");
            m_NetState?.BroadcastAnimTrigger("Die");
        }

        #endregion

        private void OnDestroy()
        {
            DeactivateBurstShield();
            StopBurstCharging();
        }
    }
}
