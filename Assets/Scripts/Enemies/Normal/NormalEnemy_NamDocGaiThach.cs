using System.Collections;
using System.Collections.Generic;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Nấm Độc Gai Thạch - Kẻ địch thường LUÔN ĐỨNG YÊN (kế thừa NormalEnemy_BaseStationary).
    /// Kỹ năng đặc trưng:
    /// - Luôn ở trạng thái tàng hình (color.Alpha = 0.1f).
    /// - Khi phát hiện mục tiêu trong bán kính lãnh thổ C và kỹ năng hồi chiêu xong:
    ///   + Hiện hình: chỉnh alpha của GameObject Visual nằm trong EnemyModel lên 1.0f.
    ///   + Thi triển một vùng độc (PoisonZone_NamDocGaiThach) xung quanh bản thân.
    ///   + Sát thương của vùng độc được quản lý và kích hoạt bởi chính EnemyBrain.
    ///   + Sau khoảng thời gian tồn tại của vùng độc, nấm tiếp tục tàng hình (Alpha = 0.1f).
    /// </summary>
    [DisallowMultipleComponent]
    public class NormalEnemy_NamDocGaiThach : NormalEnemy_BaseStationary
    {
        [Header("Visual References")]
        [Tooltip("SpriteRenderer của GameObject Visual (nằm trong EnemyModel) để điều chỉnh Alpha tàng hình/hiện hình.")]
        [SerializeField] private SpriteRenderer visualRenderer;

        [Header("Stealth Settings")]
        [Tooltip("Độ trong suốt khi ở trạng thái tàng hình (mặc định 0.1 = 10%).")]
        [SerializeField, Range(0.01f, 1f)] private float stealthAlpha = 0.1f;

        [Tooltip("Độ trong suốt khi hiện hình thi triển chiêu (mặc định 1.0 = 100%).")]
        [SerializeField, Range(0.1f, 1f)] private float visibleAlpha = 1.0f;

        [Tooltip("Thời gian chuyển đổi độ mờ giữa tàng hình và hiện hình (giây).")]
        [SerializeField, Min(0f)] private float fadeDuration = 0.25f;

        [Header("Poison Skill Settings")]
        [Tooltip("Template vùng độc (nằm trong Children của NamDocGaiThach Prefab). Cấu hình kích thước qua Transform Scale của GameObject.")]
        [SerializeField] private PoisonZone_NamDocGaiThach zoneTemplate;

        [Tooltip("Thời gian hồi kỹ năng sau khi vùng độc kết thúc (giây).")]
        [SerializeField, Min(1f)] private float skillCooldown = 6.0f;

        [Tooltip("Tỷ lệ sát thương dựa trên Stats.Attack ở mỗi nhịp tick của vùng độc (0.5 = 50% Attack).")]
        [SerializeField, Min(0.1f)] private float damageMultiplierPerTick = 0.5f;

        [Header("Runtime Info")]
        [SerializeField, ReadOnly] private float currentCooldownTimer;
        [SerializeField, ReadOnly] private bool isCasting;
        [SerializeField, ReadOnly] private float currentAlpha = 0.1f;
        [SerializeField, ReadOnly] private int activeZoneCount;
        [SerializeField, ReadOnly] private int pooledZoneCount;

        private Coroutine m_CastCoroutine;
        private Coroutine m_FadeCoroutine;
        private readonly Queue<PoisonZone_NamDocGaiThach> m_ZonePool = new Queue<PoisonZone_NamDocGaiThach>();
        private readonly List<PoisonZone_NamDocGaiThach> m_ActiveZones = new List<PoisonZone_NamDocGaiThach>();

        public SpriteRenderer VisualRenderer { get => visualRenderer; set => visualRenderer = value; }
        public PoisonZone_NamDocGaiThach ZoneTemplate { get => zoneTemplate; set => zoneTemplate = value; }
        public float StealthAlpha { get => stealthAlpha; set => stealthAlpha = value; }
        public float VisibleAlpha { get => visibleAlpha; set => visibleAlpha = value; }
        public float SkillCooldown { get => skillCooldown; set => skillCooldown = value; }
        public float CurrentCooldownTimer => currentCooldownTimer;
        public bool IsCasting => isCasting;
        public int ActiveZoneCount => m_ActiveZones.Count;
        public int PooledZoneCount => m_ZonePool.Count;

        protected override void Awake()
        {
            base.Awake();

            ResolveVisualRenderer();

            if (zoneTemplate == null)
            {
                zoneTemplate = GetComponentInChildren<PoisonZone_NamDocGaiThach>(true);
            }

            if (zoneTemplate != null)
            {
                zoneTemplate.gameObject.SetActive(false);
            }

            ApplyAlphaImmediate(stealthAlpha);
        }

        private void ResolveVisualRenderer()
        {
            if (visualRenderer != null) return;

            // 1. Tìm chính xác GameObject Visual nằm trong EnemyModel
            Transform enemyModelTransform = transform.Find("EnemyModel");
            if (enemyModelTransform != null)
            {
                Transform visualTransform = enemyModelTransform.Find("Visual");
                if (visualTransform != null)
                {
                    visualRenderer = visualTransform.GetComponent<SpriteRenderer>();
                }
            }

            // 2. Fallback từ Model.SpriteRenderer
            if (visualRenderer == null && Model != null)
            {
                visualRenderer = Model.SpriteRenderer;
            }

            // 3. Fallback tìm bất kỳ child có tên "Visual"
            if (visualRenderer == null)
            {
                SpriteRenderer[] all = GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].gameObject.name == "Visual")
                    {
                        visualRenderer = all[i];
                        break;
                    }
                }
            }
        }

        protected override void OnBrainTick(float deltaTime)
        {
            base.OnBrainTick(deltaTime);

            if (currentCooldownTimer > 0f)
            {
                currentCooldownTimer = Mathf.Max(0f, currentCooldownTimer - deltaTime);
            }

            if (!isCasting && currentCooldownTimer <= 0f && currentTarget != null)
            {
                StartCastSkill();
            }

            activeZoneCount = m_ActiveZones.Count;
            pooledZoneCount = m_ZonePool.Count;
        }

        private void LateUpdate()
        {
            if (visualRenderer == null)
            {
                ResolveVisualRenderer();
            }

            // Luôn đảm bảo Alpha trên Visual được cập nhật ở LateUpdate (chạy SAU khi Animator cập nhật pose)
            if (visualRenderer != null)
            {
                Color c = visualRenderer.color;
                if (!Mathf.Approximately(c.a, currentAlpha))
                {
                    c.a = currentAlpha;
                    visualRenderer.color = c;
                }
            }
        }

        public void StartCastSkill()
        {
            if (isCasting) return;

            if (m_CastCoroutine != null)
            {
                StopCoroutine(m_CastCoroutine);
            }
            m_CastCoroutine = StartCoroutine(SkillCastRoutine());
        }

        private IEnumerator SkillCastRoutine()
        {
            isCasting = true;

            // 1. Hiện hình (chuyển Alpha của Visual lên 1.0)
            yield return StartFadeAlpha(visibleAlpha);
            ApplyAlphaImmediate(visibleAlpha);

            // 2. Thi triển vùng độc xung quanh bản thân nấm
            PoisonZone_NamDocGaiThach activeZone = SpawnPoisonZone(transform.position);

            // 3. Đợi vùng độc hoàn thành thời gian tồn tại và biến mất
            if (activeZone != null)
            {
                while (activeZone != null && activeZone.IsActive)
                {
                    yield return null;
                }
            }
            else
            {
                // Fallback nếu không có template
                yield return new WaitForSeconds(3.0f);
            }

            // 4. Tiếp tục tàng hình (chuyển Alpha của Visual về 0.1)
            yield return StartFadeAlpha(stealthAlpha);
            ApplyAlphaImmediate(stealthAlpha);

            // 5. Kết thúc thi triển và bắt đầu hồi chiêu
            currentCooldownTimer = skillCooldown;
            isCasting = false;
            m_CastCoroutine = null;
        }

        private PoisonZone_NamDocGaiThach SpawnPoisonZone(Vector2 position)
        {
            if (zoneTemplate == null) return null;

            PoisonZone_NamDocGaiThach zone = null;

            // Lấy từ Object Pool nếu có
            while (m_ZonePool.Count > 0)
            {
                PoisonZone_NamDocGaiThach candidate = m_ZonePool.Dequeue();
                if (candidate != null)
                {
                    zone = candidate;
                    break;
                }
            }

            // Nếu Pool trống, khởi tạo instance mới từ template
            if (zone == null)
            {
                zone = Instantiate(zoneTemplate, null);
                // Giữ nguyên localScale cấu hình từ template
                zone.transform.localScale = zoneTemplate.transform.localScale;
                zone.OnTargetsDetected += HandleZoneTargetsDetected;
            }

            zone.SpawnAt(position, ReturnZoneToPool);
            m_ActiveZones.Add(zone);
            return zone;
        }

        private void ReturnZoneToPool(PoisonZone_NamDocGaiThach zone)
        {
            if (zone == null) return;

            m_ActiveZones.Remove(zone);
            m_ZonePool.Enqueue(zone);
        }

        /// <summary>
        /// Xử lý sát thương khi vùng độc phát hiện mục tiêu theo từng chu kỳ tick.
        /// EnemyBrain chịu trách nhiệm tính toán và kích hoạt sát thương theo chỉ số Attack của quái.
        /// </summary>
        private void HandleZoneTargetsDetected(PoisonZone_NamDocGaiThach zone, HashSet<TankBrain> targets)
        {
            if (targets == null || targets.Count == 0) return;

            float baseAtk = Stats != null ? Stats.Attack : 10f;
            float damage = baseAtk * damageMultiplierPerTick;

            foreach (TankBrain tank in targets)
            {
                if (tank != null && tank.IsAlive)
                {
                    tank.TakeDamage(damage);
                    TextDamageManager.Instance?.Show(damage, tank.transform.position);
                }
            }
        }

        #region Alpha Control

        private Coroutine StartFadeAlpha(float targetAlpha)
        {
            if (m_FadeCoroutine != null)
            {
                StopCoroutine(m_FadeCoroutine);
            }
            m_FadeCoroutine = StartCoroutine(FadeAlphaRoutine(targetAlpha));
            return m_FadeCoroutine;
        }

        private IEnumerator FadeAlphaRoutine(float targetAlpha)
        {
            float startAlpha = currentAlpha;
            if (fadeDuration <= 0f)
            {
                ApplyAlphaImmediate(targetAlpha);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                float alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                ApplyAlphaImmediate(alpha);
                yield return null;
            }

            ApplyAlphaImmediate(targetAlpha);
            m_FadeCoroutine = null;
        }

        public void ApplyAlphaImmediate(float alpha)
        {
            currentAlpha = alpha;

            if (visualRenderer == null)
            {
                ResolveVisualRenderer();
            }

            if (visualRenderer != null)
            {
                Color c = visualRenderer.color;
                c.a = alpha;
                visualRenderer.color = c;
            }
        }

        #endregion

        #region Debug / Context Menu

        [ContextMenu("Test/Cast Skill")]
        public void TestCastSkill()
        {
            StartCastSkill();
        }

        [ContextMenu("Test/Set Stealth (Alpha 0.1)")]
        public void TestSetStealth()
        {
            ApplyAlphaImmediate(stealthAlpha);
        }

        [ContextMenu("Test/Set Visible (Alpha 1.0)")]
        public void TestSetVisible()
        {
            ApplyAlphaImmediate(visibleAlpha);
        }

        #endregion

        protected override void OnDeath()
        {
            if (m_CastCoroutine != null)
            {
                StopCoroutine(m_CastCoroutine);
                m_CastCoroutine = null;
            }

            if (m_FadeCoroutine != null)
            {
                StopCoroutine(m_FadeCoroutine);
                m_FadeCoroutine = null;
            }

            // Hiện rõ khi chết
            ApplyAlphaImmediate(1.0f);

            base.OnDeath();
        }

        protected virtual void OnDestroy()
        {
            while (m_ZonePool.Count > 0)
            {
                PoisonZone_NamDocGaiThach pooled = m_ZonePool.Dequeue();
                if (pooled != null && pooled != zoneTemplate)
                {
                    pooled.OnTargetsDetected -= HandleZoneTargetsDetected;
                    Destroy(pooled.gameObject);
                }
            }

            for (int i = 0; i < m_ActiveZones.Count; i++)
            {
                if (m_ActiveZones[i] != null && m_ActiveZones[i] != zoneTemplate)
                {
                    m_ActiveZones[i].OnTargetsDetected -= HandleZoneTargetsDetected;
                    Destroy(m_ActiveZones[i].gameObject);
                }
            }
            m_ActiveZones.Clear();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            ResolveVisualRenderer();
        }
    }
}
