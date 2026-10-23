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
    /// Kỹ năng Tùy Chọn 1 của Leviathan: Water Armor (Giáp Nước).
    /// Tạo ra một màng nước bao phủ liên tục xoay quanh thân xe, tăng mạnh chỉ số phòng thủ (Defense)
    /// nhưng làm giảm nhẹ sát thương gây ra (DamageMultiplier).
    /// Khi hết thời gian duy trì, màng nước vỡ tung phát nổ gây sát thương diện rộng lên toàn bộ kẻ địch xung quanh.
    /// </summary>
    [DisallowMultipleComponent]
    public class OptionalSkill1_Leviathan : MonoBehaviour
    {
        public event Action<bool> OnArmorStateChange;
        public event Action<Vector2> OnExploded;

        [Header("Chỉ Số Hiệu Ứng (Stats)")]
        [Tooltip("Phần trăm phòng thủ tăng thêm khi màng nước bao phủ (0.4 = giảm 40% sát thương nhận vào).")]
        [SerializeField, Range(0f, 1f)] private float bonusDefense = 0.4f;

        [Tooltip("Phần trăm sát thương gây ra bị giảm trong khi giáp duy trì (0.2 = giảm 20% sát thương gây ra).")]
        [SerializeField, Range(0f, 1f)] private float damageReductionPenalty = 0.2f;

        [Tooltip("Thời gian duy trì giáp nước trước khi tự phát nổ (giây).")]
        [SerializeField, Min(0.5f)] private float duration = 5.0f;

        [Header("Sát Thương Phát Nổ (Explosion)")]
        [Tooltip("Sát thương diện rộng gây ra khi màng nước nổ tung.")]
        [SerializeField, Min(0f)] private float explosionDamage = 55f;

        [Tooltip("Bán kính nổ diện rộng xung quanh xe tăng (đơn vị).")]
        [SerializeField, Min(0.5f)] private float explosionRadius = 3.2f;

        [Tooltip("Thời gian hiển thị hiệu ứng sóng nước bùng nổ trước khi biến mất (giây).")]
        [SerializeField, Min(0.05f)] private float burstDuration = 0.35f;

        [Header("Hình Ảnh & Hiệu Ứng (Visuals)")]
        [Tooltip("GameObject hiển thị màng nước xoay quanh thân xe (VisualArmor).")]
        [SerializeField] private GameObject visualArmor;

        [Tooltip("GameObject hiển thị vụ nổ sóng nước lan tỏa (VisualBurst).")]
        [SerializeField] private GameObject visualBurst;

        [Tooltip("Tốc độ xoay của màng nước quanh thân xe (độ/giây).")]
        [SerializeField] private float rotationSpeed = 120f;

        private const string WaterArmorBonusKey = "Leviathan_WaterArmor";

        private TankBrain ownerTank;
        private bool isArmorActive = false;
        private float remainingTime = 0f;
        private Coroutine burstCoroutine;

        private SpriteRenderer burstRenderer;
        private Vector3 burstInitialScale = Vector3.one;
        private Color burstInitialColor = Color.white;

        public TankBrain OwnerTank => ownerTank;
        public bool IsActive => isArmorActive && remainingTime > 0f;
        public float RemainingTime => remainingTime;
        public float Duration => duration;

        private void Awake()
        {
            if (visualArmor != null)
            {
                visualArmor.SetActive(false);
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
        }

        /// <summary>
        /// Kích hoạt kỹ năng Giáp Nước cho xe tăng Leviathan.
        /// </summary>
        public void Initialize(TankBrain owner, float explosionDmg = -1f, float defBonus = -1f, float dmgPenalty = -1f, float dur = -1f)
        {
            if (owner == null) return;

            // Dọn dẹp trạng thái của lần kích hoạt trước trước khi áp cấu hình mới
            Clear();

            this.ownerTank = owner;

            if (explosionDmg >= 0f) this.explosionDamage = explosionDmg;
            if (defBonus >= 0f) this.bonusDefense = defBonus;
            if (dmgPenalty >= 0f) this.damageReductionPenalty = dmgPenalty;
            if (dur >= 0f) this.duration = dur;

            remainingTime = duration;
            isArmorActive = true;

            // Áp dụng chỉ số buff: Tăng Defense, giảm nhẹ DamageMultiplier
            ApplyArmorBonus();

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (visualArmor != null)
            {
                visualArmor.transform.position = ownerTank.transform.position;
                visualArmor.SetActive(true);
            }

            if (visualBurst != null)
            {
                visualBurst.SetActive(false);
            }

            OnArmorStateChange?.Invoke(true);
        }

        private void ApplyArmorBonus()
        {
            if (ownerTank == null) return;

            TankStats bonus = TankStats.Zeroed();
            bonus.Defense = bonusDefense;
            bonus.DamageMultiplier = -damageReductionPenalty;

            ownerTank.AddBonusStats(WaterArmorBonusKey, bonus);
        }

        private void RemoveArmorBonus()
        {
            if (ownerTank != null)
            {
                ownerTank.RemoveBonusStats(WaterArmorBonusKey);
            }
        }

        private void Update()
        {
            if (!isArmorActive) return;

            // Nếu xe tăng chủ thể bị tiêu diệt hoặc biến mất -> hủy giáp ngay lập tức
            if (ownerTank == null || !ownerTank.IsAlive)
            {
                CancelArmor();
                return;
            }

            remainingTime -= Time.deltaTime;
            if (remainingTime <= 0f)
            {
                Explode();
            }
        }

        private void LateUpdate()
        {
            // Đồng bộ vị trí màng nước theo thân xe và xoay tròn liên tục quanh tâm
            if (isArmorActive && ownerTank != null && visualArmor != null)
            {
                visualArmor.transform.position = ownerTank.transform.position;
                visualArmor.transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
            }
        }

        /// <summary>
        /// Kích nổ giáp nước: gây sát thương diện rộng và phát hiệu ứng sóng nước bùng nổ.
        /// </summary>
        public void Explode()
        {
            if (!isArmorActive) return;

            isArmorActive = false;
            remainingTime = 0f;

            // Hoàn trả chỉ số về bình thường
            RemoveArmorBonus();

            Vector2 center = ownerTank != null ? (Vector2)ownerTank.transform.position : (Vector2)transform.position;

            // Ẩn màng nước xoay quanh thân xe
            if (visualArmor != null)
            {
                visualArmor.SetActive(false);
            }

            // Quét tìm toàn bộ kẻ địch trong bán kính nổ bằng ColliderOwnerRef
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, explosionRadius);
            var affectedEnemies = new HashSet<EnemyBrain>();

            for (int i = 0; i < hits.Length; i++)
            {
                ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(hits[i]);
                if (targetRef != null && targetRef.TryGetOwner<EnemyBrain>(out var enemy) && enemy.IsAlive)
                {
                    affectedEnemies.Add(enemy);
                }
            }

            // Gây sát thương lên tất cả kẻ địch trúng vụ nổ
            foreach (EnemyBrain enemy in affectedEnemies)
            {
                enemy.TakeDamage(explosionDamage);
                TextDamageManager.Instance?.Show(explosionDamage, enemy.transform.position);
            }

            OnArmorStateChange?.Invoke(false);
            OnExploded?.Invoke(center);

            // Kích hoạt hiệu ứng bùng nổ sóng nước
            if (visualBurst != null)
            {
                visualBurst.transform.position = center;
                visualBurst.SetActive(true);

                if (burstCoroutine != null)
                {
                    StopCoroutine(burstCoroutine);
                }
                burstCoroutine = StartCoroutine(AnimateBurst());
            }
        }

        private IEnumerator AnimateBurst()
        {
            if (visualBurst == null) yield break;

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

            // Reset trạng thái ban đầu của burst
            visualBurst.transform.localScale = burstInitialScale;
            if (burstRenderer != null)
            {
                burstRenderer.color = burstInitialColor;
            }

            visualBurst.SetActive(false);
            burstCoroutine = null;
        }

        /// <summary>
        /// Đặt lại toàn bộ trạng thái runtime của giáp nước về mặc định.
        /// CHỈ reset state: không phát event, không tắt GameObject.
        /// Dùng chung cho CancelArmor (kết thúc trong trận) và Clear (dọn dẹp khi tái sử dụng).
        /// </summary>
        private void ResetRuntimeState()
        {
            if (burstCoroutine != null)
            {
                StopCoroutine(burstCoroutine);
                burstCoroutine = null;
            }

            isArmorActive = false;
            remainingTime = 0f;

            RemoveArmorBonus();

            if (visualArmor != null)
            {
                visualArmor.SetActive(false);
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
        }

        /// <summary>
        /// Hủy giáp nước mà không kích nổ (dùng khi xe chết hoặc tắt component).
        /// </summary>
        public void CancelArmor()
        {
            ResetRuntimeState();
            OnArmorStateChange?.Invoke(false);
        }

        /// <summary>
        /// Dọn dẹp để tái sử dụng: reset state và gỡ owner.
        /// Không phát event, không tắt GameObject, KHÔNG xoá subscriber —
        /// người nghe tự gỡ đăng ký của mình trong OnDisable/OnDestroy.
        /// Initialize gọi hàm này trước tiên.
        /// </summary>
        public void Clear()
        {
            ResetRuntimeState();
            ownerTank = null;
        }

        private void OnDisable()
        {
            CancelArmor();
        }

        private void OnValidate()
        {
            duration = Mathf.Max(0.5f, duration);
            explosionDamage = Mathf.Max(0f, explosionDamage);
            explosionRadius = Mathf.Max(0.5f, explosionRadius);
            burstDuration = Mathf.Max(0.05f, burstDuration);
        }
    }
}
