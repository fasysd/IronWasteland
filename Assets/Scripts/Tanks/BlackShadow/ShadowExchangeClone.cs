using IronWasteland;
using IronWasteland.Enemies;
using IronWasteland.Tanks;
using System;
using UnityEngine;

namespace IronWasteland.Tanks.BlackShadow
{
    /// <summary>
    /// Thực thể Bản Sao Bóng Tối (Shadow Clone) của kỹ năng Shadow Exchange (Hoán Đổi Bóng Tối).
    /// Xuất hiện ở phía trước đầu xe đồng minh, sao chép hình dáng của đồng minh nhưng mang màu xám đen mờ.
    /// Có khả năng hấp thụ tối đa một lượng đòn đánh nhất định (mặc định 3 đòn) thông qua va chạm vật lý để bảo vệ đồng minh,
    /// đồng thời là vị trí neo để Black Shadow dịch chuyển hoán đổi vị trí đến.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ColliderOwnerRef))]
    public class ShadowExchangeClone : MonoBehaviour
    {
        public event Action<ShadowExchangeClone> OnCloneDestroyed;
        public event Action<int, int> OnHitAbsorbed;

        [Header("Hiển Thị Hình Ảnh (Visuals)")]
        [Tooltip("SpriteRenderer hiển thị thân xe của bản sao.")]
        [SerializeField] private SpriteRenderer hullRenderer;

        [Tooltip("SpriteRenderer hiển thị nòng pháo của bản sao.")]
        [SerializeField] private SpriteRenderer weaponRenderer;

        [Tooltip("Màu sắc xám đen bóng tối áp dụng cho bản sao.")]
        [SerializeField] private Color shadowColor = new Color(0.2f, 0.2f, 0.2f, 0.75f);

        [Header("Chỉ Số Đỡ Đòn & Thời Gian")]
        [Tooltip("Số đòn đánh tối đa mà bản sao có thể hấp thụ trước khi tan biến.")]
        [SerializeField, Min(1)] private int maxHits = 3;

        [Tooltip("Thời gian tồn tại tối đa của bản sao nếu không bị đánh tan hoặc không hoán đổi (giây).")]
        [SerializeField, Min(0.5f)] private float duration = 6.0f;

        // Trạng thái nội bộ (Runtime State)
        private TankBrain ownerTank;
        private TankBrain protectedAlly;
        private ColliderOwnerRef colliderOwnerRef;
        private Rigidbody2D body;
        private int currentHits = 0;
        private float remainingTime = 0f;
        private bool isAlive = false;

        // Thuộc tính công khai (Read-only Getters)
        public bool IsActive => isAlive && remainingTime > 0f;
        public bool IsAlive => isAlive && remainingTime > 0f;
        public int CurrentHits => currentHits;
        public int MaxHits => maxHits;
        public float RemainingTime => remainingTime;
        public TankBrain ProtectedAlly => protectedAlly;
        public TankBrain OwnerTank => ownerTank;

        private void Awake()
        {
            colliderOwnerRef = GetComponent<ColliderOwnerRef>();
            body = GetComponent<Rigidbody2D>();

            if (hullRenderer == null)
            {
                Transform hullTr = transform.Find("Hull");
                if (hullTr != null) hullRenderer = hullTr.GetComponent<SpriteRenderer>();
            }

            if (weaponRenderer == null)
            {
                Transform weaponTr = transform.Find("Weapon");
                if (weaponTr != null) weaponRenderer = weaponTr.GetComponent<SpriteRenderer>();
            }
        }

        private void OnEnable()
        {
            if (colliderOwnerRef != null)
            {
                colliderOwnerRef.TriggerEnter += OnColliderTriggerEnter;
            }
        }

        private void OnDisable()
        {
            if (colliderOwnerRef != null)
            {
                colliderOwnerRef.TriggerEnter -= OnColliderTriggerEnter;
            }
        }

        /// <summary>
        /// Khởi tạo và kích hoạt bản sao bóng tối bên cạnh đồng minh.
        /// Sao chép sprite từ xe đồng minh và nhuộm màu xám đen.
        /// </summary>
        public void Initialize(TankBrain owner, TankBrain ally, Vector2 spawnPos, Quaternion bodyRot, int overrideMaxHits = -1, float overrideDuration = -1f)
        {
            // Dọn dẹp trạng thái của lần sử dụng trước trước khi nhận dữ liệu mới
            Clear();

            this.ownerTank = owner;
            this.protectedAlly = ally;
            this.maxHits = overrideMaxHits > 0 ? overrideMaxHits : this.maxHits;
            this.duration = overrideDuration > 0f ? overrideDuration : this.duration;
            this.remainingTime = this.duration;
            this.currentHits = 0;
            this.isAlive = true;

            transform.position = spawnPos;
            transform.rotation = bodyRot;

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            // Sao chép hình dáng từ xe đồng minh
            CopyAllyVisuals(ally);

            gameObject.SetActive(true);
        }

        private void CopyAllyVisuals(TankBrain ally)
        {
            if (ally == null) return;

            SpriteRenderer allyHull = null;
            SpriteRenderer allyWeapon = null;

            // 1. Ưu tiên quét tìm SpriteRenderer từ TankView (nếu xe có TankView)
            if (ally.View != null)
            {
                SpriteRenderer[] srs = ally.View.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < srs.Length; i++)
                {
                    SpriteRenderer sr = srs[i];
                    if (sr == null || sr.sprite == null) continue;

                    string objName = sr.gameObject.name.ToLower();
                    if (objName.Contains("weapon") || objName.Contains("turret"))
                    {
                        allyWeapon = sr;
                    }
                    else if (objName.Contains("hull") || objName.Contains("body"))
                    {
                        allyHull = sr;
                    }
                }
            }

            // 2. Nếu không có View hoặc chưa tìm thấy, quét trực tiếp trên toàn bộ GameObject đồng minh
            if (allyHull == null)
            {
                SpriteRenderer[] rootSrs = ally.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < rootSrs.Length; i++)
                {
                    SpriteRenderer sr = rootSrs[i];
                    if (sr != null && sr.sprite != null)
                    {
                        string objName = sr.gameObject.name.ToLower();
                        if (objName.Contains("weapon") || objName.Contains("turret"))
                        {
                            if (allyWeapon == null) allyWeapon = sr;
                        }
                        else
                        {
                            if (allyHull == null) allyHull = sr;
                        }
                    }
                }
            }

            // Cập nhật Sprite và màu xám đen mờ cho thân xe bản sao
            if (hullRenderer != null)
            {
                if (allyHull != null && allyHull.sprite != null)
                {
                    hullRenderer.sprite = allyHull.sprite;
                }
                hullRenderer.color = shadowColor;
            }

            // Cập nhật Sprite và góc xoay cho nòng pháo bản sao
            if (weaponRenderer != null)
            {
                if (allyWeapon != null && allyWeapon.sprite != null)
                {
                    weaponRenderer.sprite = allyWeapon.sprite;
                    weaponRenderer.transform.rotation = allyWeapon.transform.rotation;
                    weaponRenderer.gameObject.SetActive(true);
                }
                else
                {
                    // Nếu đồng minh chỉ có 1 sprite duy nhất (không có nòng pháo rời) -> Ẩn weaponRenderer
                    weaponRenderer.gameObject.SetActive(false);
                }
                weaponRenderer.color = shadowColor;
            }
        }

        private void Update()
        {
            if (!isAlive) return;

            // Nếu xe đồng minh hoặc Black Shadow chết -> Bản sao tự tan biến
            if (protectedAlly == null || !protectedAlly.IsAlive || ownerTank == null || !ownerTank.IsAlive)
            {
                DestroyClone();
                return;
            }

            // Đồng bộ góc quay nòng pháo theo nòng pháo của đồng minh (nếu có)
            if (weaponRenderer != null && protectedAlly.View != null && protectedAlly.View.FirepowerPivot != null)
            {
                weaponRenderer.transform.rotation = protectedAlly.View.FirepowerPivot.rotation;
            }

            // Đếm ngược thời gian tồn tại
            remainingTime -= Time.deltaTime;
            if (remainingTime <= 0f)
            {
                DestroyClone();
            }
        }

        /// <summary>
        /// Hấp thụ một đòn đánh bảo vệ đồng minh.
        /// Khi hấp thụ đủ số đòn tối đa, bản sao sẽ vỡ tan.
        /// </summary>
        public void AbsorbHit()
        {
            if (!isAlive) return;

            currentHits++;
            OnHitAbsorbed?.Invoke(currentHits, maxHits);

            if (currentHits >= maxHits)
            {
                DestroyClone();
            }
        }

        /// <summary>
        /// Tiêu hủy bản sao bóng tối.
        /// </summary>
        public void DestroyClone()
        {
            if (!isAlive) return;

            ResetRuntimeState();

            OnCloneDestroyed?.Invoke(this);

            gameObject.SetActive(false);
        }

        /// <summary>
        /// Đặt lại toàn bộ trạng thái runtime của bản sao về mặc định.
        /// CHỈ reset state: không phát event, không tắt GameObject, không ghi đè trường cấu hình.
        /// </summary>
        private void ResetRuntimeState()
        {
            isAlive = false;
            remainingTime = 0f;
            currentHits = 0;
            protectedAlly = null;
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

        private void OnColliderTriggerEnter(Collider2D other)
        {
            if (!isAlive || other == null) return;

            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(other);
            if (targetRef != null && targetRef.TryGetOwner<EnemyBrain>(out var enemy) && enemy.IsAlive)
            {
                AbsorbHit();
            }
        }
    }
}
