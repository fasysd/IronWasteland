using System;
using IronWasteland.Core;
using IronWasteland.Tanks;
using IronWasteland.Utilities;
using UnityEngine;

namespace IronWasteland.Enemies.D1onk
{
    /// <summary>
    /// Bản sao của Tank người chơi bị kéo vào trong buồng lái (sinh ra ở BÊN NGOÀI pháo hạm trong chế độ Co-op):
    /// - Kế thừa toàn bộ chỉ số của bản gốc (MaxHealth, Attack, Defense, MoveSpeed).
    /// - Không kế thừa phụ kiện (sử dụng BaseStats).
    /// - Chỉ có thể sử dụng Kỹ năng thường (Skill 0) của bản gốc.
    /// - Chiến đấu với các Tank còn lại ở bên ngoài.
    /// 
    /// RÀNG BUỘC COLLIDER:
    /// Tuyệt đối không gắn ColliderOwnerRef trỏ về Boss Brain trên bản sao này!
    /// </summary>
    [DisallowMultipleComponent]
    public class D1onkTankClone : EnemyBrain
    {
        [Header("Clone Target & Attack")]
        [Tooltip("Khoảng cách tiếp cận mục tiêu để bắn (m).")]
        [SerializeField, Min(0.5f)] private float attackRange = 5.0f;

        [Tooltip("Hồi chiêu kỹ năng thường (giây).")]
        [SerializeField, Min(0.2f)] private float normalSkillCooldown = 1.5f;

        [Tooltip("Hệ số chia tốc độ di chuyển.")]
        [SerializeField, Min(1f)] private float moveSpeedDivisor = 100f;

        private TankBrain m_OriginalTank;
        private float m_SkillTimer;
        private bool m_IsDefeated;

        public TankBrain OriginalTank => m_OriginalTank;
        public bool IsDefeated => m_IsDefeated || !IsAlive;

        public event Action<D1onkTankClone> OnCloneDefeated;

        public void InitializeTankClone(TankBrain originalTank, Vector3 spawnPosition)
        {
            m_OriginalTank = originalTank;
            transform.position = spawnPosition;
            m_IsDefeated = false;
            m_SkillTimer = 0f;

            // Kế thừa toàn bộ chỉ số của bản gốc, không kế thừa phụ kiện (dùng BaseStats)
            TankStats srcStats = null;
            if (originalTank != null)
            {
                srcStats = (originalTank.BaseStats != null && originalTank.BaseStats.MaxHealth > 0f)
                    ? originalTank.BaseStats
                    : originalTank.FinalStats;
            }
            srcStats = srcStats ?? new TankStats();

            EnemyStats cloneStats = new EnemyStats
            {
                MaxHealth = srcStats.MaxHealth > 0f ? srcStats.MaxHealth : 1000f,
                Attack = srcStats.Attack > 0f ? srcStats.Attack : 100f,
                Defense = srcStats.Defense,
                MoveSpeed = srcStats.MoveSpeed > 0f ? srcStats.MoveSpeed : 60f
            };

            // Khởi tạo không kế thừa phụ kiện
            Initialize(cloneStats);

            // Ràng buộc Collider: Đảm bảo có Collider2D và ColliderOwnerRef riêng, TUYỆT ĐỐI không trỏ về Boss Brain!
            gameObject.layer = 7; // Layer Enemy

            var col = GetComponent<CircleCollider2D>();
            if (col == null)
            {
                col = gameObject.AddComponent<CircleCollider2D>();
                col.radius = 0.7f;
            }

            var ownerRef = GetComponent<ColliderOwnerRef>();
            if (ownerRef == null)
            {
                ownerRef = gameObject.AddComponent<ColliderOwnerRef>();
            }
            ownerRef.SetOwner(this);

            var sr = GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Circle;
                sr.color = new Color(0.85f, 0.25f, 0.25f, 0.95f);
                sr.transform.localScale = Vector3.one * 1.4f;
                sr.sortingOrder = 5;
            }
        }

        protected override void OnBrainTick(float deltaTime)
        {
            if (!IsAlive) return;

            m_SkillTimer += deltaTime;

            TankBrain target = FindClosestOutsideTank();
            if (target == null)
            {
                Model?.SetMoveSpeed(0f);
                return;
            }

            Vector2 toTarget = (Vector2)target.transform.position - (Vector2)transform.position;
            float dist = toTarget.magnitude;

            if (dist > attackRange && CanMove)
            {
                float speed = (Stats != null ? Stats.MoveSpeed : 60f) / moveSpeedDivisor;
                Vector2 moveDir = toTarget.normalized;
                transform.position += (Vector3)(moveDir * (speed * deltaTime));
                Model?.SetMoveSpeed(speed);
            }
            else
            {
                Model?.SetMoveSpeed(0f);
                if (m_SkillTimer >= normalSkillCooldown && CanUseSkill)
                {
                    m_SkillTimer = 0f;
                    CastNormalSkill(target);
                }
            }
        }

        private void CastNormalSkill(TankBrain target)
        {
            if (target == null || !target.IsAlive) return;

            float damage = (Stats != null ? Stats.Attack : 100f);
            DamageInfo info = new DamageInfo(this, target, damage, 0f, target.transform.position, sourceType: DamageSourceType.EnemyAttack);
            target.TakeDamage(info);
            TextDamageManager.Instance?.Show(info.FinalDamage, target.transform.position);
        }

        private TankBrain FindClosestOutsideTank()
        {
            var tanks = UnityEngine.Object.FindObjectsByType<TankBrain>(FindObjectsSortMode.None);
            TankBrain closest = null;
            float minDist = float.MaxValue;

            for (int i = 0; i < tanks.Length; i++)
            {
                TankBrain t = tanks[i];
                if (t != null && t.IsAlive)
                {
                    // Chỉ nhắm vào tank ngoài buồng lái (không phải tank bị giam)
                    if (t.transform.position.x < 150f)
                    {
                        float d = Vector2.Distance(transform.position, t.transform.position);
                        if (d < minDist)
                        {
                            minDist = d;
                            closest = t;
                        }
                    }
                }
            }

            return closest;
        }

        protected override void OnDeath()
        {
            m_IsDefeated = true;
            OnCloneDefeated?.Invoke(this);
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }
}
