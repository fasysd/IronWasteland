using System.Collections;
using IronWasteland.Enemies;
using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    /// <summary>
    /// Fox Trap (GDD): tồn tại 15s. Kẻ địch dẫm phải bị trói chân 2s, HẾT TRÓI mới nổ (150% ATK).
    /// Tinh anh không bị trói chân mà nhận sát thương lớn hơn (230% ATK), bẫy nổ sau 1 nhịp ngắn (hàm nanh khép lại).
    /// Vùng kích hoạt = Collider2D (Is Trigger) trên prefab.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class GreenFox_MagicTrap : MonoBehaviour
    {
        private const string RootKey = "GreenFox_TrapRoot";

        [Header("Explosion Effect")]
        [SerializeField] private GameObject explosionEffectPrefab;                 // Prefab vụ nổ (quầng lửa xanh)
        [SerializeField, Min(0.1f)] private float explosionEffectLifetime = 1.5f;  // Tự hủy prefab nổ sau khoảng này
        [SerializeField, Min(0f)] private float eliteTriggerDelay = 0.2f;          // Tinh anh không bị trói: nổ sau nhịp ngắn này

        private TankBrain_GreenFox m_Owner;
        private float m_NormalDamage;
        private float m_EliteDamage;
        private float m_RootDuration;
        private bool m_Triggered;
        private Collider2D m_HitCollider;
        private void Awake()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        /// <param name="normalDamage">Sát thương cuối lên kẻ địch thường (đã tính 150% ATK và tỉ lệ sát thương).</param>
        /// <param name="eliteDamage">Sát thương cuối lên Tinh anh (230% ATK).</param>
        public void Initialize(TankBrain_GreenFox owner, float normalDamage, float eliteDamage, float rootDuration, float trapDuration)
        {
            m_Owner = owner;
            m_NormalDamage = normalDamage;
            m_EliteDamage = eliteDamage;
            m_RootDuration = rootDuration;
            m_Triggered = false;

            // Tự hủy nếu không ai dẫm phải. Đã kích hoạt thì bẫy tự quản lý vòng đời (nổ xong mới hủy).
            StartCoroutine(ExpireRoutine(trapDuration));
        }

        private IEnumerator ExpireRoutine(float trapDuration)
        {
            yield return new WaitForSeconds(trapDuration);
            if (!m_Triggered) Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (m_Triggered) return;

            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(other);
            if (targetRef == null) return;

            EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
            if (enemy == null) return;

            m_Triggered = true;
            m_HitCollider = other;   // <- thêm
            StartCoroutine(TrapTriggerRoutine(enemy));
        }
        /// <summary>Tâm của kẻ địch (tâm collider thân), chỉ dùng cho hiệu ứng nổ.</summary>
        private Vector3 GetEffectPosition(Vector3 fallback)
        {
            if (m_HitCollider == null || !m_HitCollider.enabled) return fallback;

            Vector3 c = m_HitCollider.bounds.center;
            return new Vector3(c.x, c.y, fallback.z);
        }
        private IEnumerator TrapTriggerRoutine(EnemyBrain enemy)
        {
            bool isElite = enemy is EliteEnemyBrain;
            Vector3 explosionPos = enemy.transform.position;

            if (isElite)
            {
                // Tinh anh: không trói chân, nổ sau nhịp ngắn
                yield return new WaitForSeconds(eliteTriggerDelay);
            }
            else
            {
                // Thường: trói chân cố định đủ thời gian rồi mới nổ
                GreenFox_EnemyMoveModifier.Apply(enemy, RootKey, 0f, m_RootDuration);
                yield return new WaitForSeconds(m_RootDuration);
            }

            // Kẻ địch còn sống thì lấy đúng vị trí hiện tại, nếu đã chết thì nổ ở vị trí cuối đã biết
            bool enemyAlive = enemy != null && enemy.gameObject.activeInHierarchy;
            if (enemyAlive) explosionPos = enemy.transform.position;

            SpawnExplosionEffect(GetEffectPosition(explosionPos));

            if (enemyAlive)
            {
                float damage = isElite ? m_EliteDamage : m_NormalDamage;

                if (m_Owner != null) m_Owner.DamageEnemy(enemy, damage, explosionPos);
                else
                {
                    enemy.TakeDamage(damage);
                    TextDamageManager.Instance?.Show(damage, explosionPos);
                }
            }

            Destroy(gameObject);
        }

        private void SpawnExplosionEffect(Vector3 position)
        {
            if (explosionEffectPrefab == null) return;

            GameObject fx = Instantiate(explosionEffectPrefab, position, Quaternion.identity);
            Destroy(fx, explosionEffectLifetime);
        }
    }
}