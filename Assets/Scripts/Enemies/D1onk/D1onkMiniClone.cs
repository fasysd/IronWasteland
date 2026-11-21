using IronWasteland.Core;
using IronWasteland.Tanks;
using IronWasteland.Utilities;
using UnityEngine;

namespace IronWasteland.Enemies.D1onk
{
    /// <summary>
    /// Bản sao thu nhỏ của Trưởng Quản Ngục D1onk (triệu hồi bởi kỹ năng Viện Binh Tinh Linh):
    /// - Kế thừa 20% chỉ số của Boss gốc (HP, ATK, DEF, SPD).
    /// - Tự động tiếp cận và tấn công các Tank người chơi.
    /// 
    /// RÀNG BUỘC COLLIDER:
    /// Tuyệt đối không gắn ColliderOwnerRef trỏ về Boss Brain trên bản sao này!
    /// </summary>
    [DisallowMultipleComponent]
    public class D1onkMiniClone : EnemyBrain
    {
        [Header("Combat Settings")]
        [Tooltip("Khoảng cách tiếp cận mục tiêu (m).")]
        [SerializeField, Min(0.5f)] private float attackRange = 3.0f;

        [Tooltip("Hồi chiêu đòn đánh cơ bản (giây).")]
        [SerializeField, Min(0.5f)] private float attackCooldown = 2.5f;

        [Tooltip("Hệ số chia tốc độ di chuyển.")]
        [SerializeField, Min(1f)] private float moveSpeedDivisor = 100f;

        private D1onkBrain m_Boss;
        private float m_AttackTimer;

        public D1onkBrain Boss => m_Boss;
        public float AttackRange => attackRange;
        public float AttackCooldown => attackCooldown;

        public void InitializeClone(D1onkBrain boss, Vector3 spawnPosition)
        {
            m_Boss = boss;
            transform.position = spawnPosition;

            float bossHp = (boss != null && boss.MaxHealth > 0f) ? boss.MaxHealth : 27000f;
            float bossAtk = 500f;
            float bossDef = 60f;
            float bossSpd = 70f;
            if (boss != null && boss.Stats != null)
            {
                bossAtk = boss.IsBurstActive ? Mathf.Round(boss.Stats.Attack / boss.BurstAttackMultiplier) : boss.Stats.Attack;
                bossDef = boss.IsBurstActive ? Mathf.Round(boss.Stats.Defense / boss.BurstDefenseMultiplier) : boss.Stats.Defense;
                bossSpd = boss.Stats.MoveSpeed;
            }

            EnemyStats cloneStats = new EnemyStats
            {
                MaxHealth = bossHp * 0.2f,
                Attack = bossAtk * 0.2f,
                Defense = bossDef * 0.2f,
                MoveSpeed = bossSpd
            };

            Initialize(cloneStats);
            m_AttackTimer = 0f;

            // Ràng buộc Collider: Đảm bảo có Collider2D và ColliderOwnerRef riêng, TUYỆT ĐỐI không trỏ về Boss Brain!
            gameObject.layer = 7; // Layer Enemy

            var col = GetComponent<CircleCollider2D>();
            if (col == null)
            {
                col = gameObject.AddComponent<CircleCollider2D>();
                col.radius = 0.8f;
            }

            var ownerRef = GetComponent<ColliderOwnerRef>();
            if (ownerRef == null)
            {
                ownerRef = gameObject.AddComponent<ColliderOwnerRef>();
            }
            ownerRef.SetOwner(this.gameObject);

            var sr = GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Circle;
                sr.color = new Color(0.6f, 0.2f, 0.7f, 0.95f);
                sr.transform.localScale = Vector3.one * 1.6f;
                sr.sortingOrder = 5;
            }
        }

        protected override void OnBrainTick(float deltaTime)
        {
            if (!IsAlive) return;

            m_AttackTimer += deltaTime;

            // Tìm Tank gần nhất
            TankBrain target = FindClosestTank();
            if (target == null)
            {
                Model?.SetMoveSpeed(0f);
                return;
            }

            Vector2 toTarget = (Vector2)target.transform.position - (Vector2)transform.position;
            float dist = toTarget.magnitude;

            if (dist > attackRange && CanMove)
            {
                float speed = (Stats != null ? Stats.MoveSpeed : 70f) / moveSpeedDivisor;
                Vector2 moveDir = toTarget.normalized;
                transform.position += (Vector3)(moveDir * (speed * deltaTime));
                Model?.SetMoveSpeed(speed);
            }
            else
            {
                Model?.SetMoveSpeed(0f);
                if (m_AttackTimer >= attackCooldown && CanUseSkill)
                {
                    m_AttackTimer = 0f;
                    PerformAttack(target);
                }
            }
        }

        private void PerformAttack(TankBrain target)
        {
            if (target == null || !target.IsAlive) return;

            float damage = (Stats != null ? Stats.Attack : 100f);
            DamageInfo info = new DamageInfo(this, target, damage, 0f, target.transform.position, sourceType: DamageSourceType.EnemyAttack);
            target.TakeDamage(info);
            TextDamageManager.Instance?.Show(info.FinalDamage, target.transform.position);
        }

        private TankBrain FindClosestTank()
        {
            var tanks = UnityEngine.Object.FindObjectsByType<TankBrain>(FindObjectsSortMode.None);
            TankBrain closest = null;
            float minDist = float.MaxValue;

            for (int i = 0; i < tanks.Length; i++)
            {
                TankBrain t = tanks[i];
                if (t != null && t.IsAlive)
                {
                    float d = Vector2.Distance(transform.position, t.transform.position);
                    if (d < minDist)
                    {
                        minDist = d;
                        closest = t;
                    }
                }
            }

            return closest;
        }

        protected override void OnDeath()
        {
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }
}
