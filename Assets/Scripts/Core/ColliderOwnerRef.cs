using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland
{
    /// <summary>
    /// OwnerRef dành cho COLLIDER.
    ///
    /// Ngoài việc giữ Owner, component này còn:
    /// - Phát event Trigger / Collision.
    /// - Theo dõi các Collider2D đang nằm bên trong Trigger.
    /// - Theo dõi các Collider2D đang tiếp xúc Collision.
    /// - Tự cleanup các Collider đã bị Destroy / Disable.
    ///
    /// Trigger:
    ///     Enter = bắt đầu nằm trong / tiếp xúc Trigger.
    ///     Stay  = vẫn đang nằm trong / tiếp xúc Trigger.
    ///     Exit  = không còn nằm trong / tiếp xúc Trigger.
    ///
    /// Collision:
    ///     Enter = bắt đầu tiếp xúc.
    ///     Stay  = vẫn đang tiếp xúc.
    ///     Exit  = không còn tiếp xúc.
    ///
    /// BAT BUOC phải gắn cùng với một Collider2D trên cùng GameObject.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ColliderOwnerRef : OwnerRef
    {
        // ============================================================
        // EVENTS
        // ============================================================

        // --- Trigger ---
        public event Action<Collider2D> TriggerEnter;
        public event Action<Collider2D> TriggerStay;
        public event Action<Collider2D> TriggerExit;

        // --- Collision ---
        public event Action<Collision2D> CollisionEnter;
        public event Action<Collision2D> CollisionStay;
        public event Action<Collision2D> CollisionExit;

        // --- Event gộp ---
        public event Action<ColliderOwnerRef> Contact;

        // ============================================================
        // COLLIDER
        // ============================================================

        /// <summary>
        /// Collider2D gắn cùng trên GameObject này.
        /// </summary>
        public Collider2D Collider { get; private set; }

        /// <summary>
        /// True khi component hoạt động và có Collider2D.
        /// </summary>
        public bool IsActive { get; private set; }

        // ============================================================
        // CONTACT TRACKING
        // ============================================================

        /// <summary>
        /// Các Collider2D hiện đang nằm trong / tiếp xúc Trigger.
        ///
        /// Enter -> Add
        /// Stay  -> Không thay đổi
        /// Exit  -> Remove
        /// FixedUpdate -> Cleanup collider không còn hợp lệ
        /// </summary>
        private readonly HashSet<Collider2D> triggerColliders = new();

        /// <summary>
        /// Các Collider2D hiện đang tiếp xúc Collision.
        ///
        /// Enter -> Add
        /// Stay  -> Không thay đổi
        /// Exit  -> Remove
        /// FixedUpdate -> Cleanup collider không còn hợp lệ
        /// </summary>
        private readonly HashSet<Collider2D> collisionColliders = new();

        // ============================================================
        // UNITY LIFECYCLE
        // ============================================================

        private void Awake()
        {
            Collider = GetComponent<Collider2D>();
            IsActive = Collider != null;

            if (!IsActive)
            {
                Debug.LogWarning(
                    $"[{nameof(ColliderOwnerRef)}] Thieu Collider2D tren cung GameObject " +
                    "-> cac event se khong hoat dong.",
                    this
                );
            }
        }

        private void FixedUpdate()
        {
            if (!IsActive)
                return;

            CleanupInvalidColliders();
        }

        // ============================================================
        // TRIGGER
        // ============================================================

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsActive || other == null)
                return;

            triggerColliders.Add(other);

            TriggerEnter?.Invoke(other);

            var otherRef = FromCollider(other);

            if (otherRef != null)
                Contact?.Invoke(otherRef);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!IsActive || other == null)
                return;

            // Không Add lại vào HashSet.
            // Enter đã chịu trách nhiệm tracking.
            TriggerStay?.Invoke(other);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!IsActive || other == null)
                return;

            triggerColliders.Remove(other);

            TriggerExit?.Invoke(other);
        }

        // ============================================================
        // COLLISION
        // ============================================================

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!IsActive || collision == null)
                return;

            var other = collision.collider;

            if (other == null)
                return;

            collisionColliders.Add(other);

            CollisionEnter?.Invoke(collision);

            var otherRef = FromCollider(other);

            if (otherRef != null)
                Contact?.Invoke(otherRef);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (!IsActive || collision == null)
                return;

            // Không Add lại vào HashSet.
            CollisionStay?.Invoke(collision);
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (!IsActive || collision == null)
                return;

            var other = collision.collider;

            if (other == null)
                return;

            collisionColliders.Remove(other);

            CollisionExit?.Invoke(collision);
        }

        // ============================================================
        // CLEANUP
        // ============================================================

        /// <summary>
        /// Loại bỏ các Collider không còn hợp lệ.
        ///
        /// Xảy ra khi:
        /// - Collider bị Destroy.
        /// - GameObject bị Disable.
        /// - Collider bị Disable.
        ///
        /// Chạy trong FixedUpdate để đồng bộ với Physics2D.
        /// </summary>
        private void CleanupInvalidColliders()
        {
            triggerColliders.RemoveWhere(IsInvalidCollider);
            collisionColliders.RemoveWhere(IsInvalidCollider);
        }

        private static bool IsInvalidCollider(Collider2D collider)
        {
            if (collider == null)
                return true;

            if (!collider.enabled)
                return true;

            if (!collider.gameObject.activeInHierarchy)
                return true;

            return false;
        }

        // ============================================================
        // TRIGGER QUERY
        // ============================================================

        /// <summary>
        /// True nếu Collider2D hiện đang được tracking trong Trigger.
        /// </summary>
        public bool IsTriggerInside(Collider2D other)
        {
            return other != null &&
                   triggerColliders.Contains(other);
        }

        /// <summary>
        /// Lấy toàn bộ Collider2D hiện đang được tracking trong Trigger.
        /// </summary>
        public IEnumerable<Collider2D> GetTriggerColliders()
        {
            return triggerColliders;
        }

        /// <summary>
        /// Số Collider2D hiện đang được tracking trong Trigger.
        /// </summary>
        public int TriggerCount => triggerColliders.Count;

        // ============================================================
        // COLLISION QUERY
        // ============================================================

        /// <summary>
        /// True nếu Collider2D hiện đang được tracking là Collision.
        /// </summary>
        public bool IsColliding(Collider2D other)
        {
            return other != null &&
                   collisionColliders.Contains(other);
        }

        /// <summary>
        /// Lấy toàn bộ Collider2D hiện đang Collision.
        /// </summary>
        public IEnumerable<Collider2D> GetCollisionColliders()
        {
            return collisionColliders;
        }

        /// <summary>
        /// Số Collider2D hiện đang Collision.
        /// </summary>
        public int CollisionCount => collisionColliders.Count;

        // ============================================================
        // ALL CONTACT
        // ============================================================

        /// <summary>
        /// True nếu Collider2D đang:
        /// - nằm trong Trigger
        /// HOẶC
        /// - tiếp xúc Collision.
        /// </summary>
        public bool IsContacting(Collider2D other)
        {
            if (other == null)
                return false;

            return triggerColliders.Contains(other) ||
                   collisionColliders.Contains(other);
        }

        /// <summary>
        /// Lấy ColliderOwnerRef của Collider đang contact.
        /// </summary>
        public ColliderOwnerRef GetContactOwner(Collider2D other)
        {
            if (!IsContacting(other))
                return null;

            return FromCollider(other);
        }

        /// <summary>
        /// Lấy Owner của Collider đang contact.
        /// </summary>
        public T GetContactOwner<T>(Collider2D other)
            where T : UnityEngine.Object
        {
            var ownerRef = GetContactOwner(other);

            if (ownerRef == null)
                return null;

            return ownerRef.GetOwner<T>();
        }

        // ============================================================
        // OWNER HELPERS
        // ============================================================

        /// <summary>
        /// Lấy Owner của đối phương từ Collider2D.
        /// </summary>
        public bool TryGetOtherOwner<T>(
            Collider2D other,
            out T result
        ) where T : UnityEngine.Object
        {
            result = null;

            var otherRef = FromCollider(other);

            return otherRef != null &&
                   otherRef.TryGetOwner(out result);
        }

        // ============================================================
        // CLEAR
        // ============================================================

        /// <summary>
        /// Xóa toàn bộ listener và trạng thái contact.
        /// </summary>
        public void ClearEvents()
        {
            TriggerEnter = null;
            TriggerStay = null;
            TriggerExit = null;

            CollisionEnter = null;
            CollisionStay = null;
            CollisionExit = null;

            Contact = null;

            triggerColliders.Clear();
            collisionColliders.Clear();
        }

        private void OnDisable()
        {
            triggerColliders.Clear();
            collisionColliders.Clear();
        }

        private void OnDestroy()
        {
            ClearEvents();
        }

        // ============================================================
        // VALIDATION
        // ============================================================

        private void OnValidate()
        {
            if (GetComponent<Collider2D>() == null)
            {
                Debug.LogWarning(
                    $"[{nameof(ColliderOwnerRef)}] Canh bao: thieu Collider2D tren cung GameObject, " +
                    "component se khong hoat dong.",
                    this
                );
            }
        }

        // ============================================================
        // STATIC LOOKUP
        // ============================================================

        /// <summary>
        /// Lấy ColliderOwnerRef từ Collider2D.
        /// </summary>
        public static ColliderOwnerRef FromCollider(Collider2D collider)
        {
            if (collider == null)
                return null;

            return collider.GetComponent<ColliderOwnerRef>();
        }

        /// <summary>
        /// Lấy ColliderOwnerRef từ RaycastHit2D.
        /// </summary>
        public static ColliderOwnerRef FromHit(RaycastHit2D hit)
        {
            return FromCollider(hit.collider);
        }
    }
}