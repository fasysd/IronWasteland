using System;
using UnityEngine;

namespace IronWasteland
{
    /// <summary>
    /// OwnerRef danh cho COLLIDER: ngoai ra giu Owner, con cho phep ben ngoai
    /// dang ky event phan ung cua collider (trigger / collision) ma khong can subclass them.
    ///
    ///   var r = ColliderOwnerRef.FromHit(hit);
    ///   if (r != null && r.TryGetOwner<TankBrain>(out var brain)) brain.TakeDamage(10f);
    ///   r.TriggerEnter += other => { ... };
    ///
    /// BAT BUOC phai gan cung voi mot Collider2D tren cung GameObject.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ColliderOwnerRef : OwnerRef
    {
        // --- Trigger (Unity tra Collider2D) ---
        public event Action<Collider2D> TriggerEnter;
        public event Action<Collider2D> TriggerStay;
        public event Action<Collider2D> TriggerExit;

        // --- Collision (Unity tra Collision2D) ---
        public event Action<Collision2D> CollisionEnter;
        public event Action<Collision2D> CollisionStay;
        public event Action<Collision2D> CollisionExit;

        // --- Event gop: moi lan cham, tra ve OwnerRef cua doi phuong (neu co) ---
        public event Action<ColliderOwnerRef> Contact;

        /// <summary>Collider2D gan cung tren GameObject nay.</summary>
        public Collider2D Collider { get; private set; }

        /// <summary>True khi component hoat dong (co Collider2D).</summary>
        public bool IsActive { get; private set; }

        private void Awake()
        {
            Collider = GetComponent<Collider2D>();
            IsActive = Collider != null;

            if (!IsActive)
            {
                Debug.LogWarning($"[{nameof(ColliderOwnerRef)}] Thieu Collider2D tren cung GameObject "
                    + $"-> cac event se khong hoat dong.", this);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsActive) return;
            TriggerEnter?.Invoke(other);
            Contact?.Invoke(FromCollider(other));
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!IsActive) return;
            TriggerStay?.Invoke(other);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!IsActive) return;
            TriggerExit?.Invoke(other);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!IsActive) return;
            CollisionEnter?.Invoke(collision);
            Contact?.Invoke(FromCollider(collision.collider));
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (!IsActive) return;
            CollisionStay?.Invoke(collision);
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (!IsActive) return;
            CollisionExit?.Invoke(collision);
        }

        /// <summary>Bo listener de tranh ro ri khi object bi huy / pool.</summary>
        public void ClearEvents()
        {
            TriggerEnter = null;
            TriggerStay = null;
            TriggerExit = null;
            CollisionEnter = null;
            CollisionStay = null;
            CollisionExit = null;
            Contact = null;
        }

        private void OnDestroy() => ClearEvents();

        private void OnValidate()
        {
            if (GetComponent<Collider2D>() == null)
            {
                Debug.LogWarning($"[{nameof(ColliderOwnerRef)}] Canh bao: thieu Collider2D tren cung GameObject, "
                    + $"component se khong hoat dong.", this);
            }
        }

        // ---------- Tien ich tra cuu nhanh ----------

        public static ColliderOwnerRef FromCollider(Collider2D collider)
        {
            if (collider == null) return null;
            return collider.GetComponent<ColliderOwnerRef>();
        }

        public static ColliderOwnerRef FromHit(RaycastHit2D hit) => FromCollider(hit.collider);

        /// <summary>Lay Owner cu doi phuong neu no cung la mot OwnerRef hop le.</summary>
        public bool TryGetOtherOwner<T>(Collider2D other, out T result) where T : UnityEngine.Object
        {
            result = null;
            var otherRef = FromCollider(other);
            return otherRef != null && otherRef.TryGetOwner(out result);
        }
    }
}
