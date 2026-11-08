using IronWasteland;
using IronWasteland.Core;
using IronWasteland.Core.Effects;
using IronWasteland.Tanks;
using System;
using System.Collections;
using UnityEngine;

namespace IronWasteland.Tanks.BlackShadow
{
    /// <summary>
    /// Kỹ năng Tùy Chọn 1 - Biến thể 3: Shadow Guard (Bóng Ma Bảo Vệ).
    /// Bao phủ đồng minh (hoặc bản thân) bằng một bóng ma bảo vệ xoay quanh trong 3 giây.
    /// Khi chịu đòn tấn công từ kẻ địch, bóng này sẽ lập tức triệt tiêu hoàn toàn
    /// đòn đánh đó, hoàn lại 20 năng lượng cho người niệm, bùng tan thành làn khói đen rồi biến mất.
    /// <para>
    /// Cơ chế chặn đòn đi qua <see cref="DamageInterceptEffect"/> của Brain (chặng chặn đòn trong
    /// <c>TankBrain.TakeDamage(DamageInfo)</c>), KHÔNG còn tự giữ khiên riêng: hiệu ứng chỉ được cấp
    /// đúng một lượt chặn, nên đòn bị triệt tiêu sạch sẽ không trừ giáp, không trừ khiên của mục tiêu.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class OptionalSkill1_3 : MonoBehaviour
    {
        public event Action<bool> OnShieldChange;
        public event Action OnShieldBreak;

        [Header("Cài Đặt Thời Gian & Mục Tiêu")]
        [Tooltip("Thời gian duy trì khiên bảo vệ (giây).")]
        [SerializeField, Min(0.1f)] private float duration = 3f;

        [Tooltip("Bán kính tìm kiếm đồng minh xung quanh vị trí con trỏ chuột.")]
        [SerializeField, Min(0.1f)] private float selectRadius = 1.0f;

        [Tooltip("Cho phép tự tạo khiên cho bản thân nếu không trỏ vào đồng minh nào.")]
        [SerializeField] private bool canTargetSelf = true;

        [Header("Năng Lượng")]
        [Tooltip("Năng lượng hoàn lại cho người niệm khi bóng ma đỡ thành công một đòn.")]
        [SerializeField, Min(0f)] private float energyRefundOnBlock = 20f;

        [Header("Hiển Thị Hình Ảnh (Visuals)")]
        [Tooltip("Kéo thả GameObject hiển thị bóng ma bảo vệ vào đây (VisualGuard).")]
        [SerializeField] private GameObject visualGuard;

        [Tooltip("Kéo thả GameObject hiển thị khói đen bùng tan vào đây (VisualBurst).")]
        [SerializeField] private GameObject visualBurst;

        [Tooltip("Bán kính bóng ma xoay quanh thân xe.")]
        [SerializeField, Min(0.1f)] private float orbitRadius = 1.6f;

        [Tooltip("Tốc độ xoay quanh thân xe (độ/giây).")]
        [SerializeField] private float orbitSpeed = 150f;

        [Tooltip("Thời gian hiển thị làn khói bùng tan khi đỡ đòn (giây).")]
        [SerializeField, Min(0.05f)] private float burstDuration = 0.4f;

        // Trạng thái nội bộ (Runtime State)
        private static int s_effectIdCounter;
        private TankBrain casterTank;
        private TankBrain targetTank;
        private DamageInterceptEffect guardEffect;
        private string guardEffectId;
        private bool isGuarding;
        private float currentAngle;
        private Coroutine burstCoroutine;

        // Thuộc tính công khai (Read-only Getters)
        public bool IsActive => isGuarding && guardEffect != null && !guardEffect.IsExpired;
        public float RemainingTime => guardEffect != null ? guardEffect.RemainingTime : 0f;
        public float Duration => duration;
        public TankBrain TargetTank => targetTank;

        /// <summary>
        /// Mã hiệu ứng cố định theo từng instance skill: lần niệm lại sẽ làm mới đúng hiệu ứng này
        /// thay vì chồng thêm một lớp bảo vệ thứ hai (Brain gộp theo <c>EffectId</c>).
        /// </summary>
        private string GuardEffectId
        {
            get
            {
                if (string.IsNullOrEmpty(guardEffectId))
                {
                    guardEffectId = $"ShadowGuard_{++s_effectIdCounter}";
                }
                return guardEffectId;
            }
        }

        private void Awake()
        {
            if (visualGuard != null)
            {
                visualGuard.SetActive(false);
            }

            if (visualBurst != null)
            {
                visualBurst.SetActive(false);
            }
        }

        /// <summary>
        /// Kích hoạt khiên bảo vệ cho xe mục tiêu chỉ định.
        /// Không truyền người niệm nên mặc định người niệm chính là mục tiêu (tự bảo vệ bản thân).
        /// </summary>
        public void Initialize(TankBrain target)
        {
            InitializeInternal(null, target);
        }

        /// <summary>
        /// Kích hoạt khiên bảo vệ, tách biệt người niệm (<paramref name="caster"/>) và người được bảo vệ
        /// để năng lượng hoàn lại khi đỡ đòn luôn về đúng người đã thi triển kỹ năng.
        /// </summary>
        private void InitializeInternal(TankBrain caster, TankBrain target)
        {
            if (target == null) return;

            // Dọn dẹp trạng thái của lần kích hoạt trước (gỡ hiệu ứng cũ nếu mục tiêu trước đó còn được bảo vệ)
            Clear();

            casterTank = caster != null ? caster : target;
            targetTank = target;

            // Đăng ký hiệu ứng chặn đòn vào Brain: đỡ đúng 1 đòn rồi tự tan.
            // Nếu mục tiêu đã có hiệu ứng cùng mã, Brain làm mới nó (đặt lại thời gian và lượt chặn).
            target.ApplyEffect(new DamageInterceptEffect(
                GuardEffectId,
                "Bóng Ma Bảo Vệ",
                duration,
                this,
                maxNegateHits: 1,
                blockDamageOverTime: false,
                blockRedirected: true));

            guardEffect = target.GetEffect(GuardEffectId) as DamageInterceptEffect;
            if (guardEffect != null)
            {
                guardEffect.OnNegated -= HandleNegated;
                guardEffect.OnNegated += HandleNegated;
            }
            else
            {
                // Brain từ chối hiệu ứng (ví dụ mục tiêu đã chết trong frame này) → không giữ trạng thái bảo vệ.
                targetTank = null;
                casterTank = null;
                return;
            }

            isGuarding = true;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (visualGuard != null)
            {
                currentAngle = 0f;
                Vector3 offset = new Vector3(orbitRadius, 0f, 0f);
                visualGuard.transform.position = targetTank.transform.position + offset;
                visualGuard.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
                visualGuard.SetActive(true);
            }

            OnShieldChange?.Invoke(true);
        }

        /// <summary>
        /// Kích hoạt khiên bằng vị trí con trỏ chuột.
        /// Nếu trỏ vào đồng minh -> tạo khiên cho đồng minh.
        /// Nếu không trỏ vào đồng minh và canTargetSelf = true -> tự tạo khiên cho bản thân.
        /// </summary>
        public bool Initialize(TankBrain owner, Vector2 targetWorldPos)
        {
            if (owner == null) return false;

            if (targetWorldPos == Vector2.zero && owner.HasLookTarget)
            {
                targetWorldPos = owner.LookTarget;
            }

            // 1. Ưu tiên tìm xe đồng minh dưới con trỏ chuột
            if (TryFindAllyAt(targetWorldPos, owner, out TankBrain foundAlly))
            {
                InitializeInternal(owner, foundAlly);
                return true;
            }

            // 2. Nếu không trỏ vào đồng minh nào: tự tạo khiên cho bản thân
            if (canTargetSelf && owner.IsAlive)
            {
                InitializeInternal(owner, owner);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tìm xe tăng đồng minh (TankBrain khác chủ sở hữu và còn sống) xung quanh vị trí chuột.
        /// </summary>
        public bool TryFindAllyAt(Vector2 targetPos, TankBrain owner, out TankBrain ally)
        {
            ally = null;
            Collider2D[] colliders = Physics2D.OverlapCircleAll(targetPos, selectRadius);

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D col = colliders[i];
                if (col == null) continue;

                ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(col);
                if (targetRef != null && targetRef.TryGetOwner<TankBrain>(out var target))
                {
                    if (target != owner && target.IsAlive)
                    {
                        ally = target;
                        return true;
                    }
                }
            }

            return false;
        }

        private void Update()
        {
            if (!isGuarding) return;

            // Hiệu ứng có thể biến mất vì hết thời gian, bị gỡ bởi hiệu ứng khác (Dispel/ClearAllEffects)
            // hoặc vì mục tiêu đã chết — khi đó dọn luôn bóng ma thay vì tự đếm giờ song song.
            if (targetTank == null || !targetTank.IsAlive || targetTank.GetEffect(GuardEffectId) == null)
            {
                EndGuard(hideBurst: true);
            }
        }

        private void LateUpdate()
        {
            // Cập nhật vị trí bóng ma xoay quanh mục tiêu
            if (!isGuarding || visualGuard == null || targetTank == null || !targetTank.IsAlive) return;

            currentAngle = (currentAngle + orbitSpeed * Time.deltaTime) % 360f;
            float rad = currentAngle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * orbitRadius;
            visualGuard.transform.position = targetTank.transform.position + offset;
            visualGuard.transform.rotation = Quaternion.Euler(0f, 0f, currentAngle - 90f);
        }

        /// <summary>
        /// Được hiệu ứng chặn đòn gọi ngay khi một đòn vừa bị triệt tiêu hoàn toàn:
        /// hoàn lại năng lượng cho người niệm, dọn bóng ma, bật làn khói bùng tan tại vị trí mục tiêu
        /// và phát sự kiện vỡ khiên.
        /// </summary>
        private void HandleNegated(DamageInfo info)
        {
            if (!isGuarding) return;

            // Ghi nhớ vị trí mục tiêu và người niệm trước khi dọn dẹp, vì EndGuard sẽ xoá các tham chiếu này.
            Vector3 burstPosition = targetTank != null ? targetTank.transform.position : transform.position;
            TankBrain caster = casterTank;

            // Dọn khiên trước rồi mới phát hiệu ứng bùng tan, để không dập tắt làn khói vừa hiện.
            EndGuard(hideBurst: false);

            // Đỡ đòn thành công -> hoàn lại năng lượng cho người đã thi triển kỹ năng (kể cả tự bảo vệ bản thân).
            if (caster != null && caster.IsAlive && energyRefundOnBlock > 0f)
            {
                caster.GainEnergy(energyRefundOnBlock);
            }

            PlayBurstEffect(burstPosition);
            OnShieldBreak?.Invoke();
        }

        /// <summary>
        /// Kết thúc trạng thái bảo vệ: gỡ hiệu ứng khỏi mục tiêu, ẩn bóng ma và (tuỳ chọn) dọn làn khói.
        /// Chỉ làm việc khi thực sự đang bảo vệ, tránh phát <see cref="OnShieldChange"/> lặp lại
        /// từ OnDisable/OnDestroy hoặc từ frame đầu tiên khi chưa kích hoạt lần nào.
        /// </summary>
        private void EndGuard(bool hideBurst)
        {
            if (!isGuarding) return;

            if (guardEffect != null)
            {
                guardEffect.OnNegated -= HandleNegated;
                guardEffect = null;
            }

            if (targetTank != null)
            {
                targetTank.RemoveEffect(GuardEffectId);
                targetTank = null;
            }

            casterTank = null;
            isGuarding = false;

            if (visualGuard != null)
            {
                visualGuard.SetActive(false);
            }

            if (hideBurst)
            {
                if (burstCoroutine != null)
                {
                    StopCoroutine(burstCoroutine);
                    burstCoroutine = null;
                }

                if (visualBurst != null)
                {
                    visualBurst.SetActive(false);
                }
            }

            OnShieldChange?.Invoke(false);
        }

        /// <summary>
        /// Gỡ bỏ khiên, ẩn bóng ma bảo vệ và phát sự kiện kết thúc khiên.
        /// </summary>
        public void RemoveShield()
        {
            EndGuard(hideBurst: true);
        }

        /// <summary>
        /// Phát hiệu ứng khói đen bùng tan tại vị trí chỉ định rồi tự ẩn sau burstDuration giây.
        /// </summary>
        private void PlayBurstEffect(Vector3 position)
        {
            if (visualBurst == null) return;

            visualBurst.transform.position = position;
            visualBurst.SetActive(true);

            // Bắt buộc GameObject phải đang active trước khi gọi StartCoroutine
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
            }

            if (burstCoroutine != null)
            {
                StopCoroutine(burstCoroutine);
            }
            burstCoroutine = StartCoroutine(HideBurstCoroutine());
        }

        /// <summary>
        /// Coroutine chờ thời gian burstDuration rồi ẩn visual khói bùng nổ.
        /// </summary>
        private IEnumerator HideBurstCoroutine()
        {
            yield return new WaitForSeconds(burstDuration);
            if (visualBurst != null)
            {
                visualBurst.SetActive(false);
            }
            burstCoroutine = null;
        }

        /// <summary>
        /// Dọn dẹp để tái sử dụng: gỡ hiệu ứng khỏi mục tiêu cũ và reset toàn bộ visual.
        /// Không phát event, không tắt GameObject, KHÔNG xoá subscriber —
        /// người nghe tự gỡ đăng ký của mình trong OnDisable/OnDestroy.
        /// Initialize gọi hàm này trước tiên.
        /// </summary>
        public void Clear()
        {
            if (guardEffect != null)
            {
                guardEffect.OnNegated -= HandleNegated;
                guardEffect = null;
            }

            if (targetTank != null)
            {
                targetTank.RemoveEffect(GuardEffectId);
                targetTank = null;
            }

            casterTank = null;
            isGuarding = false;

            if (burstCoroutine != null)
            {
                StopCoroutine(burstCoroutine);
                burstCoroutine = null;
            }

            if (visualGuard != null)
            {
                visualGuard.SetActive(false);
            }

            if (visualBurst != null)
            {
                visualBurst.SetActive(false);
            }
        }

        private void OnDisable()
        {
            RemoveShield();
        }

        private void OnDestroy()
        {
            RemoveShield();
        }
    }
}
