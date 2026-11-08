using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies
{
    /// <summary>
    /// Ky sinh thien thach (Chuong 1): lao cuc nhanh vao tank va tu no khi cham,
    /// gay sat thuong dien nho + lam cham 30% trong 5s. Bi tieu diet cung no lan.
    ///
    /// Chi so theo tai lieu thiet ke: HP 100 / ATK 150 / Giap 0 / SPD 125.
    /// </summary>
    public class MeteorParasiteBrain : ChasingEnemyBrain
    {
        [Header("Tu no")]
        [Tooltip("Ban kinh vu no (met, tinh tu be mat collider).")]
        [SerializeField, Min(0f)] private float explosionRadius = 2.5f;

        [Tooltip("He so sat thuong cua vu no so voi Attack (1 = 100% ATK).")]
        [SerializeField, Range(0f, 5f)] private float explosionAttackRatio = 1f;

        [Tooltip("Khoang cach dem them (met) khi kiem tra cham tank - du phong khi va cham khong ban ra event.")]
        [SerializeField, Min(0f)] private float detonationPadding = 0.25f;

        [Header("Hieu ung lam cham")]
        [Tooltip("Phan tram toc do bi giam khi trung vu no (0.3 = 30%).")]
        [SerializeField, Range(0f, 1f)] private float slowPercent = 0.3f;

        [Tooltip("Thoi gian lam cham (giay).")]
        [SerializeField, Min(0f)] private float slowDuration = 5f;

        [Header("Debug")]
        [SerializeField] private bool logExplosion = true;

        private bool m_Exploded;

        protected override void OnBrainTick(float deltaTime)
        {
            base.OnBrainTick(deltaTime);

            // Tu no xong thi bien mat, tranh xac quai con song tiep tuc lao theo tank.
            if (m_Exploded)
            {
                Destroy(gameObject);
                return;
            }

            if (HasReachedTank()) Detonate();
        }

        /// <summary>
        /// True khi collider cua quai da cham (hoac cach <= padding) collider cua tank.
        /// Tinh khoang ho giua 2 bounds (AABB) - on dinh o ca Edit mode lan Play mode,
        /// khong phu thuoc vao physics query (Collider2D.Distance tra ve sai khi chua simulate).
        /// </summary>
        private bool HasReachedTank()
        {
            TankBrain target = Target;
            if (target == null) return false;

            Collider2D self = Model != null ? Model.BodyCollider : null;
            Collider2D other = FindSolidCollider(target);
            if (self == null || other == null) return false;

            return BoundsGap(self.bounds, other.bounds) <= detonationPadding;
        }

        /// <summary>Khoang cach nho nhat giua 2 hop AABB (0 khi chong len nhau).</summary>
        private static float BoundsGap(Bounds a, Bounds b)
        {
            float dx = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
            float dy = Mathf.Max(0f, Mathf.Max(a.min.y - b.max.y, b.min.y - a.max.y));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Tim collider "than" cua tank: bo qua cac trigger rong (collider ky nang co size 0
        /// nam tren tank) va chi lay collider vat ly co kich thuoc that.
        /// </summary>
        private static Collider2D FindSolidCollider(TankBrain tank)
        {
            Collider2D[] colliders = tank.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                if (collider.bounds.size.sqrMagnitude <= 0.0001f) continue;
                return collider;
            }

            return null;
        }

        protected override void OnContact(ColliderOwnerRef other)
        {
            if (m_Exploded || other == null) return;

            // Cham tank -> kich no ngay.
            if (other.GetOwner<TankBrain>() != null) Detonate();
        }

        protected override void OnDeath()
        {
            // "Khi bi tieu diet cung no lan tuong tu tai vi tri chet."
            Detonate();
            if (this != null) Destroy(gameObject);
        }

        /// <summary>Phat no: gay sat thuong dien + lam cham cac tank trong ban kinh.</summary>
        private void Detonate()
        {
            if (m_Exploded) return;
            m_Exploded = true;

            float damage = Stats != null ? Stats.Attack * explosionAttackRatio : 0f;
            int hits = DamageTanksInRadius(transform.position, explosionRadius, damage);

            if (hits > 0 && slowPercent > 0f && slowDuration > 0f)
            {
                TankBrain[] tanks = FindObjectsByType<TankBrain>(FindObjectsSortMode.None);
                for (int i = 0; i < tanks.Length; i++)
                {
                    TankBrain tank = tanks[i];
                    if (tank == null || !tank.IsAlive) continue;
                    if (Vector2.Distance(transform.position, tank.transform.position) > explosionRadius) continue;
                    TankSlowDebuff.Apply(tank, slowPercent, slowDuration);
                }
            }

            if (logExplosion)
            {
                Debug.Log($"[MeteorParasite] No tai {transform.position:0.0}: {damage:0} sat thuong, trung {hits} tank.");
            }
        }
    }
}
