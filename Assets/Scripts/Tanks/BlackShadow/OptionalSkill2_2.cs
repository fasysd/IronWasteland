using IronWasteland;
using IronWasteland.Tanks;
using System;
using System.Collections;
using UnityEngine;

namespace IronWasteland.Tanks.BlackShadow
{
    /// <summary>
    /// Kỹ năng Tùy Chọn 2 - Biến thể 3: Shadow Exchange (Hoán Đổi Bóng Tối).
    /// Giai đoạn 1: Tạo ra một bản sao bóng tối của đồng minh đứng ở trước đầu xe để hấp thụ đòn đánh (tối đa 3 đòn).
    /// Giai đoạn 2: Tái kích hoạt kỹ năng để Black Shadow lập tức hoán đổi vị trí trực tiếp tới bản sao,
    /// bản sao tan biến trong làn khói đen và kỹ năng bắt đầu tính hồi chiêu.
    /// </summary>
    [DisallowMultipleComponent]
    public class OptionalSkill2_2 : MonoBehaviour
    {
        public event Action<TankBrain> OnCloneCreated;
        public event Action<Vector2, Vector2> OnSwapped;
        public event Action OnCloneExpired;

        [Header("Bản Sao & Mục Tiêu")]
        [Tooltip("Tham chiếu tới đối tượng bản sao bóng tối (ShadowExchangeClone).")]
        [SerializeField] private ShadowExchangeClone cloneInstance;

        [Tooltip("Bán kính tìm kiếm đồng minh xung quanh vị trí con trỏ chuột.")]
        [SerializeField, Min(0.1f)] private float selectRadius = 1.5f;

        [Tooltip("Khoảng cách đặt bản sao ở phía trước đầu xe đồng minh.")]
        [SerializeField, Min(0.2f)] private float frontOffsetDistance = 1.3f;

        [Tooltip("Thời gian tồn tại tối đa của bản sao bóng tối (giây).")]
        [SerializeField, Min(0.5f)] private float duration = 6.0f;

        [Tooltip("Số đòn đánh tối đa mà bản sao có thể hấp thụ trước khi tan biến.")]
        [SerializeField, Min(1)] private int maxHits = 3;

        [Header("Hiển Thị Hình Ảnh (Visuals)")]
        [Tooltip("Kéo thả GameObject hiển thị khói đen bóng tối bùng tan vào đây (VisualBurst).")]
        [SerializeField] private GameObject visualBurst;

        [Tooltip("Kéo thả GameObject khói đen phụ tại vị trí xuất phát khi hoán đổi (tùy chọn).")]
        [SerializeField] private GameObject visualBurstSwap;

        [Tooltip("Thời gian hiển thị làn khói bóng tối (giây).")]
        [SerializeField, Min(0.05f)] private float burstDuration = 0.4f;

        // Trạng thái nội bộ (Runtime State)
        private TankBrain ownerTank;
        private int skillState = 0; // 0: Chờ tạo bản sao | 1: Đã có bản sao (chờ hoán đổi)
        private Coroutine burstCoroutine;
        private Coroutine burstSwapCoroutine;

        // Thuộc tính công khai (Read-only Getters)
        public int SkillState => skillState;
        public bool HasActiveClone => skillState == 1 && cloneInstance != null && cloneInstance.IsAlive;
        public ShadowExchangeClone CloneInstance => cloneInstance;
        public float Duration => duration;
        public int MaxHits => maxHits;
        public TankBrain OwnerTank => ownerTank;

        private void Awake()
        {
            if (visualBurst != null) visualBurst.SetActive(false);
            if (visualBurstSwap != null) visualBurstSwap.SetActive(false);
            if (cloneInstance != null) cloneInstance.gameObject.SetActive(false);
        }

        /// <summary>
        /// Thực thi kỹ năng Shadow Exchange (tự động phân nhánh theo giai đoạn).
        /// </summary>
        public bool Execute(TankBrain owner, Vector2 mouseWorldPos)
        {
            if (owner == null) return false;

            // GIAI ĐOẠN 2: Bản sao đang tồn tại -> Hoán đổi vị trí trực tiếp
            if (skillState == 1 && HasActiveClone)
            {
                this.ownerTank = owner;
                SwapWithClone();
                return true;
            }

            // Dọn dẹp trạng thái của lần kích hoạt trước trước khi bắt đầu giai đoạn 1
            Clear();

            this.ownerTank = owner;

            // GIAI ĐOẠN 1: Chưa có bản sao -> Quét tìm đồng minh và tạo bản sao ở đầu xe
            if (mouseWorldPos == Vector2.zero && owner.HasLookTarget)
            {
                mouseWorldPos = owner.LookTarget;
            }

            if (!TryFindAllyAt(mouseWorldPos, owner, out TankBrain foundAlly))
            {
                return false;
            }

            SpawnCloneAtAlly(foundAlly);
            return true;
        }

        private void SpawnCloneAtAlly(TankBrain ally)
        {
            if (cloneInstance == null || ally == null) return;

            // Xác định hướng phía trước đầu xe đồng minh
            Vector2 allyPos = ally.transform.position;
            Vector2 forward = Vector2.right;

            if (ally.View != null && ally.View.BodyPivot != null)
            {
                forward = ally.View.BodyPivot.right;
            }
            else
            {
                forward = ally.transform.right;
            }

            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector2.right;
            }

            // Đảm bảo khoảng cách đặt bản sao ở phía ngoài collider của đồng minh, không bị đè lên nhau
            float effectiveOffset = frontOffsetDistance;
            Collider2D allyCol = ally.GetComponentInChildren<Collider2D>();
            if (allyCol != null)
            {
                float maxExtent = Mathf.Max(allyCol.bounds.extents.x, allyCol.bounds.extents.y);
                effectiveOffset = Mathf.Max(effectiveOffset, maxExtent + 0.8f);
            }

            Vector2 spawnPos = allyPos + forward.normalized * effectiveOffset;
            Quaternion bodyRot = (ally.View != null && ally.View.BodyPivot != null)
                ? ally.View.BodyPivot.rotation
                : ally.transform.rotation;

            // Tách bản sao ra khỏi thân xe Black Shadow để bản sao độc lập tọa độ trong thế giới,
            // không bị kéo lê đi theo khi Black Shadow di chuyển
            cloneInstance.transform.SetParent(null);

            cloneInstance.Initialize(ownerTank, ally, spawnPos, bodyRot, maxHits, duration);
            cloneInstance.OnCloneDestroyed -= OnCloneExpiredOrDestroyed;
            cloneInstance.OnCloneDestroyed += OnCloneExpiredOrDestroyed;

            PlayBurst(spawnPos);
            skillState = 1;

            OnCloneCreated?.Invoke(ally);
        }

        private void SwapWithClone()
        {
            if (ownerTank == null || cloneInstance == null) return;

            Vector2 oldPos = ownerTank.transform.position;
            Vector2 targetPos = cloneInstance.transform.position;

            // 1. Dịch chuyển Black Shadow tới vị trí bản sao
            ownerTank.transform.position = targetPos;
            Rigidbody2D rb = ownerTank.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.position = targetPos;
                rb.linearVelocity = Vector2.zero;
            }

            // 2. Phát hiệu ứng khói đen tại cả 2 vị trí
            PlayBurst(targetPos);
            if (visualBurstSwap != null)
            {
                PlayBurstSwap(oldPos);
            }

            // 3. Tiêu hủy bản sao bóng tối
            cloneInstance.OnCloneDestroyed -= OnCloneExpiredOrDestroyed;
            cloneInstance.DestroyClone();
            skillState = 0;

            OnSwapped?.Invoke(oldPos, targetPos);
        }

        private void OnCloneExpiredOrDestroyed(ShadowExchangeClone clone)
        {
            if (skillState == 1)
            {
                skillState = 0;

                if (clone != null)
                {
                    clone.OnCloneDestroyed -= OnCloneExpiredOrDestroyed;
                    PlayBurst(clone.transform.position);
                }

                // Phát sự kiện bản sao hết hạn/vỡ mà người chơi chưa hoán đổi -> TankBrain lắng nghe để tự kích hoạt hồi chiêu
                OnCloneExpired?.Invoke();
            }
        }

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

        private void PlayBurst(Vector2 pos)
        {
            if (visualBurst != null)
            {
                visualBurst.transform.position = pos;
                visualBurst.SetActive(true);

                if (burstCoroutine != null)
                {
                    StopCoroutine(burstCoroutine);
                }

                burstCoroutine = StartCoroutine(HideBurstCoroutine(visualBurst));
            }
        }

        private void PlayBurstSwap(Vector2 pos)
        {
            if (visualBurstSwap != null)
            {
                visualBurstSwap.transform.position = pos;
                visualBurstSwap.SetActive(true);

                if (burstSwapCoroutine != null)
                {
                    StopCoroutine(burstSwapCoroutine);
                }

                burstSwapCoroutine = StartCoroutine(HideBurstCoroutine(visualBurstSwap));
            }
        }

        private IEnumerator HideBurstCoroutine(GameObject burstGo)
        {
            yield return new WaitForSeconds(burstDuration);
            if (burstGo != null)
            {
                burstGo.SetActive(false);
            }
        }

        /// <summary>
        /// Đặt lại toàn bộ trạng thái runtime của Shadow Exchange về mặc định.
        /// CHỈ reset state: không phát event, không tắt GameObject, không ghi đè trường cấu hình.
        /// Gỡ đăng ký OnCloneDestroyed trên bản sao để tránh rò rỉ, tiêu hủy bản sao còn sống
        /// và ẩn toàn bộ hiệu ứng khói đen.
        /// </summary>
        private void ResetRuntimeState()
        {
            // Dừng coroutine ẩn khói đen đang chạy (nếu có) rồi xoá tham chiếu
            if (burstCoroutine != null)
            {
                StopCoroutine(burstCoroutine);
            }

            burstCoroutine = null;

            if (burstSwapCoroutine != null)
            {
                StopCoroutine(burstSwapCoroutine);
            }

            burstSwapCoroutine = null;

            // Gỡ đăng ký sự kiện trên bản sao trước khi tiêu hủy để không nhận lại callback
            if (cloneInstance != null)
            {
                cloneInstance.OnCloneDestroyed -= OnCloneExpiredOrDestroyed;

                // Tiêu hủy bản sao còn sống (DestroyClone tự tắt GameObject của bản sao)
                if (cloneInstance.IsAlive)
                {
                    cloneInstance.DestroyClone();
                }
            }

            skillState = 0;

            // Ẩn toàn bộ hiệu ứng hình ảnh
            if (visualBurst != null)
            {
                visualBurst.SetActive(false);
            }

            if (visualBurstSwap != null)
            {
                visualBurstSwap.SetActive(false);
            }
        }

        /// <summary>
        /// Dọn dẹp để tái sử dụng: reset state và gỡ owner.
        /// Không phát event, không tắt GameObject, KHÔNG xoá subscriber —
        /// người nghe tự gỡ đăng ký của mình trong OnDisable/OnDestroy.
        /// Execute gọi hàm này trước khi bắt đầu giai đoạn 1.
        /// </summary>
        public void Clear()
        {
            ResetRuntimeState();
            ownerTank = null;
        }

        /// <summary>
        /// Hủy kỹ năng Shadow Exchange: dọn state và tiêu hủy bản sao đang tồn tại.
        /// Cố ý KHÔNG phát OnCloneExpired vì sự kiện đó báo "bản sao tự hết hạn" để TankBrain
        /// bắt đầu hồi chiêu; hủy kỹ năng không phải là hết hạn tự nhiên.
        /// </summary>
        public void CancelClone()
        {
            ResetRuntimeState();
        }

        private void OnDisable()
        {
            CancelClone();
        }

        private void OnDestroy()
        {
            if (cloneInstance != null)
            {
                cloneInstance.OnCloneDestroyed -= OnCloneExpiredOrDestroyed;
                if (cloneInstance.transform.parent == null)
                {
                    Destroy(cloneInstance.gameObject);
                }
            }
        }
    }
}
