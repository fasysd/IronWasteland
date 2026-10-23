using IronWasteland;
using IronWasteland.Enemies;
using IronWasteland.Tanks;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.Leviathan
{
    /// <summary>
    /// Kỹ năng Tùy Chọn 2 của Leviathan: Tidal Return (Thủy Triều Nghịch Chuyển).
    /// Hiệu quả:
    /// - Tạo ra một vùng xoáy nước tại vị trí con trỏ chuột.
    /// - Sau một khoảng thời gian trì hoãn ngắn (delayDuration), một đợt thủy triều dội ngược cưỡng chế
    ///   kéo toàn bộ kẻ địch trong vùng ảnh hưởng về vị trí tâm xoáy (vị trí con trỏ chuột ban đầu) và gây sát thương.
    /// Hiển thị trực quan:
    /// - Vùng nước xoáy cuộn trào xuất hiện dưới chân nhóm kẻ địch (VisualZone).
    /// - Sóng nước bùng nổ dội ngược kéo các mục tiêu về tâm xoáy (VisualBurst).
    /// </summary>
    [DisallowMultipleComponent]
    public class OptionalSkill2_Leviathan : MonoBehaviour
    {
        public event Action<Vector2> OnTideZonePlaced;
        public event Action<Vector2, int> OnTidalReturnTriggered;

        [Header("Thông Số Vùng & Thời Gian")]
        [Tooltip("Thời gian nước xoáy trì hoãn trước khi kéo toàn bộ kẻ địch về tâm (giây).")]
        [SerializeField, Min(0.1f)] private float delayDuration = 1.2f;

        [Tooltip("Bán kính vùng xoáy nước quét và kéo kẻ địch (đơn vị).")]
        [SerializeField, Min(0.5f)] private float zoneRadius = 3.8f;

        [Tooltip("Thời gian cưỡng chế kéo kẻ địch trượt nhanh về tâm xoáy (giây).")]
        [SerializeField, Min(0.1f)] private float pullDuration = 0.35f;

        [Header("Chỉ Số Chiến Đấu (Combat)")]
        [Tooltip("Sát thương gây ra lên toàn bộ kẻ địch bị kéo về tâm xoáy.")]
        [SerializeField, Min(0f)] private float damage = 65.0f;

        [Header("Hình Ảnh & Hiệu Ứng (Visuals)")]
        [Tooltip("GameObject hiển thị vùng nước xoáy (VisualZone).")]
        [SerializeField] private GameObject visualZone;

        [Tooltip("GameObject hiển thị sóng nước bùng nổ dội ngược khi kéo địch (VisualBurst).")]
        [SerializeField] private GameObject visualBurst;

        [Tooltip("Tốc độ xoay của vùng nước xoáy (độ/giây).")]
        [SerializeField] private float rotationSpeed = 160.0f;

        [Tooltip("Thời gian hiển thị hiệu ứng sóng nước bùng nổ (giây).")]
        [SerializeField, Min(0.05f)] private float burstDuration = 0.4f;

        private enum SkillState
        {
            Idle,
            Swirling,
            Pulling,
            Finishing
        }

        // Trạng thái nội bộ (Runtime State)
        private TankBrain ownerTank;
        private SkillState currentState = SkillState.Idle;
        private Vector2 centerPosition;
        private float remainingDelay = 0f;

        private Transform originalParent;
        private Coroutine activeSkillCoroutine;

        private SpriteRenderer burstRenderer;
        private Vector3 burstInitialScale = Vector3.one;
        private Color burstInitialColor = Color.white;

        // Thuộc tính công khai (Read-only Getters & UI)
        public bool IsActive => currentState != SkillState.Idle;
        public float DelayDuration => delayDuration;
        public float ZoneRadius => zoneRadius;
        public float Damage => damage;
        public Vector2 CenterPosition => centerPosition;

        private void Awake()
        {
            originalParent = transform.parent;

            if (visualZone != null)
            {
                visualZone.SetActive(false);
            }

            if (visualBurst != null)
            {
                burstRenderer = visualBurst.GetComponent<SpriteRenderer>();
                if (burstRenderer == null)
                {
                    burstRenderer = visualBurst.GetComponentInChildren<SpriteRenderer>();
                }

                burstInitialScale = visualBurst.transform.localScale;
                if (burstRenderer != null)
                {
                    burstInitialColor = burstRenderer.color;
                }

                visualBurst.SetActive(false);
            }

            // Tắt SpriteRenderer thừa trên chính GameObject này (nếu có)
            SpriteRenderer selfSr = GetComponent<SpriteRenderer>();
            if (selfSr != null)
            {
                selfSr.enabled = false;
            }
        }

        /// <summary>
        /// Kích hoạt kỹ năng Tidal Return tại vị trí con trỏ chuột chỉ định.
        /// </summary>
        public void Initialize(TankBrain owner, Vector2 targetWorldPos, float damageAmount = -1f)
        {
            if (owner == null) return;
            this.ownerTank = owner;

            if (damageAmount >= 0f) this.damage = damageAmount;

            // Nếu đang trong đợt xoáy trước thì dọn dẹp trước khi bắt đầu đợt mới
            if (currentState != SkillState.Idle)
            {
                CancelSkill();
            }

            centerPosition = targetWorldPos;
            remainingDelay = delayDuration;
            currentState = SkillState.Swirling;

            // Tách khỏi cha để vị trí vùng nước xoáy cố định tại tọa độ thế giới của con trỏ chuột
            transform.SetParent(null);
            transform.position = new Vector3(centerPosition.x, centerPosition.y, 0f);

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (visualZone != null)
            {
                visualZone.transform.localPosition = Vector3.zero;
                visualZone.transform.localRotation = Quaternion.identity;
                visualZone.SetActive(true);
            }

            if (visualBurst != null)
            {
                visualBurst.SetActive(false);
            }

            OnTideZonePlaced?.Invoke(centerPosition);
        }

        private void Update()
        {
            if (currentState != SkillState.Swirling) return;

            // Xoay tròn vùng nước xoáy quanh tâm
            if (visualZone != null && rotationSpeed != 0f)
            {
                visualZone.transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
            }

            // Đếm ngược thời gian trì hoãn
            remainingDelay -= Time.deltaTime;
            if (remainingDelay <= 0f)
            {
                TriggerTidalReturn();
            }
        }

        /// <summary>
        /// Kích hoạt đợt thủy triều dội ngược: cưỡng chế kéo toàn bộ kẻ địch về tâm xoáy và gây sát thương.
        /// </summary>
        private void TriggerTidalReturn()
        {
            if (currentState != SkillState.Swirling) return;

            currentState = SkillState.Pulling;

            // 1. Quét tìm toàn bộ kẻ địch trong bán kính vùng xoáy thuần túy qua ColliderOwnerRef
            Collider2D[] hits = Physics2D.OverlapCircleAll(centerPosition, zoneRadius);
            var affectedEnemies = new HashSet<EnemyBrain>();

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D col = hits[i];
                if (col == null) continue;

                ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(col);
                if (targetRef != null && targetRef.TryGetOwner<EnemyBrain>(out var enemy) && enemy.IsAlive)
                {
                    affectedEnemies.Add(enemy);
                }
            }

            // 2. Gây sát thương và bắt đầu kéo từng kẻ địch về tâm xoáy
            foreach (EnemyBrain enemy in affectedEnemies)
            {
                enemy.TakeDamage(damage);
                TextDamageManager.Instance?.Show(damage, enemy.transform.position);

                StartCoroutine(PullEnemyToCenter(enemy, centerPosition));
            }

            OnTidalReturnTriggered?.Invoke(centerPosition, affectedEnemies.Count);

            // 3. Tắt vùng xoáy và kích hoạt hiệu ứng sóng nước bùng nổ dội ngược
            if (visualZone != null)
            {
                visualZone.SetActive(false);
            }

            if (activeSkillCoroutine != null)
            {
                StopCoroutine(activeSkillCoroutine);
            }
            activeSkillCoroutine = StartCoroutine(AnimateBurstAndFinish());
        }

        /// <summary>
        /// Cưỡng chế kéo kẻ địch trượt nhanh về vị trí tâm xoáy (vị trí con trỏ chuột ban đầu).
        /// Sử dụng Ease In Cubic để mô phỏng lực hút của dòng xoáy nước tăng dần khi càng gần tâm.
        /// </summary>
        private IEnumerator PullEnemyToCenter(EnemyBrain enemy, Vector2 center)
        {
            if (enemy == null) yield break;

            Vector2 startPos = enemy.transform.position;
            // Độ lệch ngẫu nhiên nhẹ quanh tâm để các kẻ địch không bị đè hoàn toàn lên nhau
            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * 0.25f;
            Vector2 targetPos = center + randomOffset;

            Rigidbody2D enemyRb = enemy.GetComponent<Rigidbody2D>();

            float elapsed = 0f;
            while (elapsed < pullDuration)
            {
                if (enemy == null || !enemy.IsAlive) yield break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / pullDuration);

                // Ease In Cubic: bắt đầu hút từ từ rồi giật mạnh về tâm
                float easeIn = t * t * t;

                Vector2 currentPos = Vector2.Lerp(startPos, targetPos, easeIn);
                enemy.transform.position = currentPos;

                if (enemyRb != null)
                {
                    enemyRb.position = currentPos;
                    enemyRb.linearVelocity = Vector2.zero;
                }

                yield return null;
            }

            if (enemy != null && enemy.IsAlive)
            {
                enemy.transform.position = targetPos;
                if (enemyRb != null)
                {
                    enemyRb.position = targetPos;
                    enemyRb.linearVelocity = Vector2.zero;
                }
            }
        }

        /// <summary>
        /// Hiển thị hiệu ứng sóng nước bùng nổ tại tâm và kết thúc kỹ năng.
        /// </summary>
        private IEnumerator AnimateBurstAndFinish()
        {
            if (visualBurst != null)
            {
                visualBurst.transform.localPosition = Vector3.zero;
                visualBurst.transform.localScale = burstInitialScale;
                if (burstRenderer != null)
                {
                    burstRenderer.color = burstInitialColor;
                }
                visualBurst.SetActive(true);

                Vector3 startScale = burstInitialScale;
                Vector3 targetScale = burstInitialScale * 1.35f;

                float elapsed = 0f;
                while (elapsed < burstDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / burstDuration);

                    visualBurst.transform.localScale = Vector3.Lerp(startScale, targetScale, t);

                    if (burstRenderer != null)
                    {
                        Color c = burstInitialColor;
                        c.a = Mathf.Lerp(burstInitialColor.a, 0f, t);
                        burstRenderer.color = c;
                    }

                    yield return null;
                }

                visualBurst.transform.localScale = burstInitialScale;
                if (burstRenderer != null)
                {
                    burstRenderer.color = burstInitialColor;
                }
                visualBurst.SetActive(false);
            }

            // Đảm bảo đợi đủ thời gian kéo kẻ địch nếu pullDuration > burstDuration
            if (pullDuration > burstDuration)
            {
                yield return new WaitForSeconds(pullDuration - burstDuration);
            }

            FinishSkill();
        }

        private void FinishSkill()
        {
            currentState = SkillState.Idle;
            remainingDelay = 0f;

            if (visualZone != null)
            {
                visualZone.SetActive(false);
            }

            if (visualBurst != null)
            {
                visualBurst.SetActive(false);
            }

            if (originalParent != null)
            {
                transform.SetParent(originalParent);
                transform.localPosition = Vector3.zero;
            }

            gameObject.SetActive(false);
            activeSkillCoroutine = null;
        }

        /// <summary>
        /// Hủy bỏ kỹ năng khi xe tăng tử trận hoặc tắt game.
        /// </summary>
        public void CancelSkill()
        {
            StopAllCoroutines();
            activeSkillCoroutine = null;

            currentState = SkillState.Idle;
            remainingDelay = 0f;

            if (visualZone != null)
            {
                visualZone.SetActive(false);
            }

            if (visualBurst != null)
            {
                visualBurst.transform.localScale = burstInitialScale;
                if (burstRenderer != null)
                {
                    burstRenderer.color = burstInitialColor;
                }
                visualBurst.SetActive(false);
            }

            if (originalParent != null)
            {
                transform.SetParent(originalParent);
                transform.localPosition = Vector3.zero;
            }

            gameObject.SetActive(false);
        }

        public void Clear()
        {
            CancelSkill();
            ownerTank = null;
            OnTideZonePlaced = null;
            OnTidalReturnTriggered = null;
        }

        private void OnDisable()
        {
            CancelSkill();
        }

        private void OnDestroy()
        {
            CancelSkill();
        }

        private void OnValidate()
        {
            delayDuration = Mathf.Max(0.1f, delayDuration);
            zoneRadius = Mathf.Max(0.5f, zoneRadius);
            pullDuration = Mathf.Max(0.1f, pullDuration);
            damage = Mathf.Max(0f, damage);
            burstDuration = Mathf.Max(0.05f, burstDuration);
        }
    }
}
