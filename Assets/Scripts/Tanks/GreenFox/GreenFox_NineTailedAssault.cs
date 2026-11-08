using System.Collections;
using System.Collections.Generic;
using IronWasteland.Enemies;
using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    /// <summary>
    /// Nine-Tailed Assault (GDD): 6s, 9 đuôi, mỗi đuôi đánh 1 lần mỗi 1s với 15% ATK lên kẻ địch trong vùng.
    /// Chỉ làm phần GÂY SÁT THƯƠNG: vùng = CircleCollider2D (Is Trigger) trên prefab này, bám theo xe.
    /// Hình ảnh 9 đuôi là prefab riêng (GreenFox_NineTailsVisual), không nằm ở đây.
    /// Mỗi nhịp, mỗi đuôi đánh 1 kẻ địch ngẫu nhiên trong vùng và đính kèm thiêu đốt nếu đang bật Burning Fox.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    public class GreenFox_NineTailedAssault : MonoBehaviour
    {
        private TankBrain_GreenFox m_OwnerTank;
        private float m_Duration;
        private float m_Interval;
        private int m_TailCount;
        private float m_TailDamage;

        private readonly Dictionary<EnemyBrain, int> m_EnemiesInRange = new Dictionary<EnemyBrain, int>();
        private readonly List<EnemyBrain> m_Targets = new List<EnemyBrain>();

        private void Awake()
        {
            CircleCollider2D circle = GetComponent<CircleCollider2D>();
            if (circle != null) circle.isTrigger = true;
        }

        /// <param name="tailDamage">Sát thương cuối mỗi đuôi (đã tính 15% ATK và tỉ lệ sát thương).</param>
        public void Initialize(TankBrain_GreenFox owner, Transform tankTransform, float duration, float interval, int tailCount, float tailDamage)
        {
            m_OwnerTank = owner;
            m_Duration = duration;
            m_Interval = Mathf.Max(0.05f, interval);
            m_TailCount = Mathf.Max(1, tailCount);
            m_TailDamage = tailDamage;

            if (tankTransform != null)
            {
                transform.position = tankTransform.position;
                transform.SetParent(tankTransform);
            }

            StartCoroutine(AssaultRoutine());
        }

        private IEnumerator AssaultRoutine()
        {
            float endTime = Time.time + m_Duration;
            var wait = new WaitForSeconds(m_Interval);

            while (Time.time < endTime)
            {
                yield return wait;
                StrikeWithAllTails();
            }

            Destroy(gameObject);
        }

        private void StrikeWithAllTails()
        {
            if (m_OwnerTank == null) return;

            m_Targets.Clear();
            foreach (var kv in m_EnemiesInRange)
            {
                if (kv.Key != null && kv.Key.IsAlive) m_Targets.Add(kv.Key);
            }
            if (m_Targets.Count == 0) return;

            for (int i = 0; i < m_TailCount; i++)
            {
                EnemyBrain enemy = m_Targets[Random.Range(0, m_Targets.Count)];
                if (enemy == null) continue;

                m_OwnerTank.DamageEnemy(enemy, m_TailDamage, enemy.transform.position);
                m_OwnerTank.TryApplyBurn(enemy);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null) return;
            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(other);
            if (targetRef == null) return;

            EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
            if (enemy == null) return;

            m_EnemiesInRange.TryGetValue(enemy, out int count);
            m_EnemiesInRange[enemy] = count + 1;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other == null) return;
            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(other);
            if (targetRef == null) return;

            EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
            if (enemy == null || !m_EnemiesInRange.TryGetValue(enemy, out int count)) return;

            count--;
            if (count <= 0) m_EnemiesInRange.Remove(enemy);
            else m_EnemiesInRange[enemy] = count;
        }
    }
}
