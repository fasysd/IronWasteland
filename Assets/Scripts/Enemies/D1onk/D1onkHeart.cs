using System;
using System.Collections;
using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Tanks;
using IronWasteland.Utilities;
using UnityEngine;

namespace IronWasteland.Enemies.D1onk
{
    /// <summary>
    /// Trái Tim Boss ở trung tâm buồng lái chiến hạm D1onk:
    /// - Sở hữu 10% HP tối đa của Boss gốc.
    /// - Sử dụng toàn bộ kỹ năng và bị động của Boss (bị động bắn 2 đạn mỗi 5s, 5 đạn khi dùng kỹ năng,
    ///   kỹ năng Trừng Phạt, Laze Nguyên Tố, Tỏa Nhiệt Diện Rộng).
    /// - Được bảo vệ bởi lớp khiên Bất Hoại khiến không thể nhận sát thương.
    /// - Khi hoàn thành 3 thử thách (hoặc phá 3/4 cột ở Solo): Xóa bỏ khiên Bất Hoại.
    /// - Khi bị tiêu diệt: Thoát trạng thái Bùng nổ năng lượng và kích hoạt phần thưởng!
    /// 
    /// RÀNG BUỘC COLLIDER:
    /// Tuyệt đối không gắn ColliderOwnerRef trỏ về Boss Brain trên Trái Tim!
    /// </summary>
    [DisallowMultipleComponent]
    public class D1onkHeart : EnemyBrain
    {
        [Header("Heart State & Shield")]
        [Tooltip("Trạng thái khiên Bất Hoại. Khi bật, chặn 100% sát thương nhận vào.")]
        [SerializeField] private bool invincibleShieldActive = true;

        [Header("Skill & Passive Settings")]
        [Tooltip("Chu kỳ bắn đạn bị động (giây).")]
        [SerializeField, Min(1f)] private float passiveFireInterval = 5.0f;

        [Tooltip("Hồi chiêu Trừng Phạt (giây).")]
        [SerializeField, Min(0.1f)] private float trungPhatCooldown = 8.0f;

        [Tooltip("Hồi chiêu Laze Nguyên Tố (giây).")]
        [SerializeField, Min(0.1f)] private float lazeNguyenToCooldown = 12.0f;

        [Tooltip("Hồi chiêu Tỏa Nhiệt Diện Rộng (giây).")]
        [SerializeField, Min(0.1f)] private float toaNhietDienRongCooldown = 15.0f;

        [Tooltip("Hồi chiêu Viện Binh Tinh Linh (giây).")]
        [SerializeField, Min(0.1f)] private float vienBinhTinhLinhCooldown = 20.0f;

        [Tooltip("Giãn cách dùng kỹ năng toàn cục (giây).")]
        [SerializeField, Min(0f)] private float globalSkillInterval = 2.0f;

        private D1onkBrain m_Boss;
        private D1onkMothershipSubArena m_Arena;

        private float m_PassiveMissileTimer;
        private float m_TrungPhatTimer;
        private float m_LazeTimer;
        private float m_ToaNhietTimer;
        private float m_VienBinhTimer;
        private float m_GlobalSkillTimer;

        private Coroutine m_LaserCoroutine;

        public bool InvincibleShieldActive => invincibleShieldActive;
        public D1onkBrain Boss => m_Boss;
        public D1onkMothershipSubArena Arena => m_Arena;

        public float TrungPhatTimer => m_TrungPhatTimer;
        public float LazeTimer => m_LazeTimer;
        public float ToaNhietTimer => m_ToaNhietTimer;
        public float VienBinhTimer => m_VienBinhTimer;
        public float GlobalSkillTimer => m_GlobalSkillTimer;
        public bool CanCastAnySkill => m_GlobalSkillTimer <= 0f;

        public event Action<D1onkHeart> OnHeartDestroyed;

        public void InitializeHeart(D1onkBrain boss, D1onkMothershipSubArena arena, float hp)
        {
            m_Boss = boss;
            m_Arena = arena;
            invincibleShieldActive = true;

            m_PassiveMissileTimer = 0f;
            m_TrungPhatTimer = 0f;
            m_LazeTimer = 0f;
            m_ToaNhietTimer = 0f;
            m_VienBinhTimer = 0f;
            m_GlobalSkillTimer = 0f;

            float bossAtk = 500f;
            float bossDef = 60f;
            if (boss != null && boss.Stats != null)
            {
                bossAtk = boss.IsBurstActive ? Mathf.Round(boss.Stats.Attack / boss.BurstAttackMultiplier) : boss.Stats.Attack;
                bossDef = boss.IsBurstActive ? Mathf.Round(boss.Stats.Defense / boss.BurstDefenseMultiplier) : boss.Stats.Defense;
            }

            EnemyStats heartStats = new EnemyStats
            {
                MaxHealth = hp,
                Attack = bossAtk,
                Defense = bossDef,
                MoveSpeed = 0f
            };

            Initialize(heartStats);
            IsCrowdControlImmune = true;

            // Ràng buộc Collider: Đảm bảo có Collider2D và ColliderOwnerRef riêng trên Prefab, TUYỆT ĐỐI không trỏ về Boss Brain!
            if (gameObject.layer == 0) gameObject.layer = 7; // Layer Enemy nếu chưa đặt

            var col = GetComponent<Collider2D>();
            if (col == null)
            {
                Debug.LogError($"[D1onkHeart] {gameObject.name} thiếu Collider2D trên Prefab! Vui lòng cấu hình trên Prefab thay vì code.", this);
            }

            var ownerRef = GetComponent<ColliderOwnerRef>();
            if (ownerRef == null)
            {
                Debug.LogError($"[D1onkHeart] {gameObject.name} thiếu ColliderOwnerRef trên Prefab! Vui lòng cấu hình trên Prefab thay vì code.", this);
            }
            else
            {
                ownerRef.SetOwner(this);
            }

            var sr = GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                Debug.LogError($"[D1onkHeart] {gameObject.name} thiếu SpriteRenderer trên Prefab! Vui lòng cấu hình SpriteRenderer trên Prefab thay vì code.", this);
            }
        }

        /// <summary>
        /// Xóa bỏ lớp khiên Bất Hoại khi hoàn thành 2 thử thách hoặc phá 2 cột.
        /// </summary>
        public void RemoveInvincibleShield()
        {
            invincibleShieldActive = false;
            Debug.Log("[D1onkHeart] Khiên Bất Hoại đã bị xóa bỏ! Trái Tim Boss giờ đây có thể nhận sát thương!");
        }

        public override void TakeDamage(DamageInfo info)
        {
            if (invincibleShieldActive)
            {
                // Khiên Bất Hoại chặn đứng mọi nguồn sát thương
                return;
            }
            base.TakeDamage(info);
        }

        public override void TakeDamage(float rawDamage)
        {
            if (invincibleShieldActive)
            {
                return;
            }
            base.TakeDamage(rawDamage);
        }

        public void ManualTick(float deltaTime)
        {
            OnBrainTick(deltaTime);
        }

        protected override void OnBrainTick(float deltaTime)
        {
            if (!IsAlive) return;

            // Đếm lùi hồi chiêu
            if (m_GlobalSkillTimer > 0f) m_GlobalSkillTimer = Mathf.Max(0f, m_GlobalSkillTimer - deltaTime);
            if (m_TrungPhatTimer > 0f) m_TrungPhatTimer = Mathf.Max(0f, m_TrungPhatTimer - deltaTime);
            if (m_LazeTimer > 0f) m_LazeTimer = Mathf.Max(0f, m_LazeTimer - deltaTime);
            if (m_ToaNhietTimer > 0f) m_ToaNhietTimer = Mathf.Max(0f, m_ToaNhietTimer - deltaTime);
            if (m_VienBinhTimer > 0f) m_VienBinhTimer = Mathf.Max(0f, m_VienBinhTimer - deltaTime);

            // Bị động — Pháo Hạm Đa Nòng: Định kỳ mỗi 5s tự tạo 2 đạn truy đuổi (20% ATK/viên)
            m_PassiveMissileTimer += deltaTime;
            if (m_PassiveMissileTimer >= passiveFireInterval)
            {
                m_PassiveMissileTimer = 0f;
                FirePassiveMissiles(2);
            }

            // Thi triển kỹ năng nếu đủ điều kiện (tuân thủ giãn cách toàn cục 2.0s)
            if (CanCastAnySkill && CanUseSkill)
            {
                TryCastAvailableSkill();
            }
        }

        private void TryCastAvailableSkill()
        {
            var trappedTanks = GetAliveTrappedTanks();
            if (trappedTanks.Count == 0) return;

            if (m_TrungPhatTimer <= 0f)
            {
                CastTrungPhat();
            }
            else if (m_LazeTimer <= 0f)
            {
                CastLazeNguyenTo();
            }
            else if (m_ToaNhietTimer <= 0f)
            {
                CastToaNhietDienRong();
            }
            else if (m_VienBinhTimer <= 0f)
            {
                CastVienBinhTinhLinh();
            }
        }

        /// <summary>
        /// Kỹ năng 4: Viện Binh Tinh Linh (VienBinhTinhLinh) bên trong buồng lái.
        /// Sinh quái thường hỗ trợ Trái Tim và kích hoạt bị động 5 đạn truy đuổi.
        /// </summary>
        public void CastVienBinhTinhLinh()
        {
            m_VienBinhTimer = vienBinhTinhLinhCooldown;
            m_GlobalSkillTimer = globalSkillInterval;

            if (m_Arena != null)
            {
                m_Arena.SpawnInsideMonster();
            }

            FirePassiveMissiles(5);
        }

        /// <summary>
        /// Bị động: Tạo đạn truy đuổi gây 20% ATK/viên lên các Tank bị giam bên trong buồng lái.
        /// </summary>
        public void FirePassiveMissiles(int count)
        {
            if (count <= 0) return;

            var aliveTanks = GetAliveTrappedTanks();
            if (aliveTanks.Count == 0) return;

            float missileDmg = (Stats != null ? Stats.Attack : 500f) * 0.20f;

            for (int i = 0; i < count; i++)
            {
                TankBrain target = aliveTanks[i % aliveTanks.Count];
                if (target != null && target.IsAlive)
                {
                    DamageInfo info = new DamageInfo(this, target, missileDmg, 0f, target.transform.position, sourceType: DamageSourceType.EnemyAttack);
                    target.TakeDamage(info);
                    TextDamageManager.Instance?.Show(info.FinalDamage, target.transform.position);
                }
            }
        }

        /// <summary>
        /// Kỹ năng 1: Trừng Phạt (TrungPhat):
        /// - Tank gần nhất: 150% ATK + giảm 20% DEF trong 5s.
        /// - Tank xa nhất (nếu có 2 Tank): giảm 30% ATK, 10% SPD trong 5s.
        /// - Kích hoạt bị động: Tạo 5 đạn truy đuổi.
        /// </summary>
        public void CastTrungPhat()
        {
            m_TrungPhatTimer = trungPhatCooldown;
            m_GlobalSkillTimer = globalSkillInterval;

            var (nearest, furthest) = FindNearestAndFurthestTrappedTanks();
            float atk = Stats != null ? Stats.Attack : 500f;

            if (nearest != null)
            {
                DamageInfo dmg = new DamageInfo(this, nearest, atk * 1.50f, 0f, nearest.transform.position, sourceType: DamageSourceType.EnemyAttack);
                nearest.TakeDamage(dmg);
                TextDamageManager.Instance?.Show(dmg.FinalDamage, nearest.transform.position);

                nearest.AddBonusStats("D1onkHeart_TrungPhat_Def", null, new TankStats { Defense = -0.20f });
                StartCoroutine(RemoveBonusAfterDelay(nearest, "D1onkHeart_TrungPhat_Def", 5.0f));
            }

            if (furthest != null && furthest != nearest)
            {
                furthest.AddBonusStats("D1onkHeart_TrungPhat_Debuff", null, new TankStats { Attack = -0.30f, MoveSpeed = -0.10f });
                StartCoroutine(RemoveBonusAfterDelay(furthest, "D1onkHeart_TrungPhat_Debuff", 5.0f));
            }

            FirePassiveMissiles(5);
        }

        /// <summary>
        /// Kỹ năng 2: Laze Nguyên Tố (LazeNguyenTo):
        /// - Thiêu đốt mục tiêu xa nhất 5s (40% ATK mỗi 0.5s = 10 ticks).
        /// - Kích hoạt bị động: Tạo 5 đạn truy đuổi.
        /// </summary>
        public void CastLazeNguyenTo()
        {
            m_LazeTimer = lazeNguyenToCooldown;
            m_GlobalSkillTimer = globalSkillInterval;

            var (_, furthest) = FindNearestAndFurthestTrappedTanks();
            TankBrain target = furthest;
            if (target == null)
            {
                var alive = GetAliveTrappedTanks();
                if (alive.Count > 0) target = alive[0];
            }

            if (target != null)
            {
                if (m_LaserCoroutine != null) StopCoroutine(m_LaserCoroutine);
                m_LaserCoroutine = StartCoroutine(LaserDamageTicks(target, 5.0f, 0.5f));
            }

            FirePassiveMissiles(5);
        }

        private IEnumerator LaserDamageTicks(TankBrain target, float duration, float interval)
        {
            float elapsed = 0f;
            float atk = Stats != null ? Stats.Attack : 500f;
            float tickDmg = atk * 0.40f;

            while (elapsed < duration)
            {
                yield return new WaitForSeconds(interval);
                elapsed += interval;

                if (target != null && target.IsAlive)
                {
                    DamageInfo info = new DamageInfo(this, target, tickDmg, 0f, target.transform.position, sourceType: DamageSourceType.EnemyAttack);
                    target.TakeDamage(info);
                    TextDamageManager.Instance?.Show(info.FinalDamage, target.transform.position);
                }
            }
            m_LaserCoroutine = null;
        }

        /// <summary>
        /// Kỹ năng 3: Tỏa Nhiệt Diện Rộng (ToaNhietDienRong):
        /// - Nổ sóng nhiệt toàn buồng lái gây 100% ATK (Tank gần x2 sát thương, các Tank sau giảm dần).
        /// - Kích hoạt bị động: Tạo 5 đạn truy đuổi.
        /// </summary>
        public void CastToaNhietDienRong()
        {
            m_ToaNhietTimer = toaNhietDienRongCooldown;
            m_GlobalSkillTimer = globalSkillInterval;

            float atk = Stats != null ? Stats.Attack : 500f;
            var sortedTanks = GetSortedTrappedTanksByDistance();
            float[] multipliers = { 2.0f, 1.7f, 1.3f, 1.0f };

            for (int i = 0; i < sortedTanks.Count; i++)
            {
                TankBrain t = sortedTanks[i];
                if (t != null && t.IsAlive)
                {
                    float mult = i < multipliers.Length ? multipliers[i] : 1.0f;
                    DamageInfo info = new DamageInfo(this, t, atk * mult, 0f, t.transform.position, sourceType: DamageSourceType.EnemyAttack);
                    t.TakeDamage(info);
                    TextDamageManager.Instance?.Show(info.FinalDamage, t.transform.position);
                }
            }

            FirePassiveMissiles(5);
        }

        #region Helpers & Targeting

        private List<TankBrain> GetAliveTrappedTanks()
        {
            var list = new List<TankBrain>();
            if (m_Arena == null) return list;

            var trapped = m_Arena.TrappedTanks;
            if (trapped != null)
            {
                for (int i = 0; i < trapped.Count; i++)
                {
                    if (trapped[i] != null && trapped[i].IsAlive)
                    {
                        list.Add(trapped[i]);
                    }
                }
            }
            return list;
        }

        private (TankBrain nearest, TankBrain furthest) FindNearestAndFurthestTrappedTanks()
        {
            var tanks = GetAliveTrappedTanks();
            if (tanks.Count == 0) return (null, null);
            if (tanks.Count == 1) return (tanks[0], tanks[0]);

            TankBrain nearest = null;
            TankBrain furthest = null;
            float minDist = float.MaxValue;
            float maxDist = float.MinValue;

            for (int i = 0; i < tanks.Count; i++)
            {
                TankBrain t = tanks[i];
                float d = Vector2.Distance(transform.position, t.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = t;
                }
                if (d > maxDist)
                {
                    maxDist = d;
                    furthest = t;
                }
            }

            // Nếu 2 tank ở khoảng cách đối xứng (bằng nhau), đảm bảo chọn 2 tank khác nhau
            if (furthest == nearest && tanks.Count >= 2)
            {
                furthest = (nearest == tanks[0]) ? tanks[1] : tanks[0];
            }

            return (nearest, furthest);
        }

        private List<TankBrain> GetSortedTrappedTanksByDistance()
        {
            var tanks = GetAliveTrappedTanks();
            tanks.Sort((a, b) =>
                Vector2.Distance(transform.position, a.transform.position).CompareTo(
                Vector2.Distance(transform.position, b.transform.position)));
            return tanks;
        }

        private IEnumerator RemoveBonusAfterDelay(TankBrain tank, string key, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (tank != null)
            {
                tank.RemoveBonusStats(key);
            }
        }

        #endregion

        protected override void OnDeath()
        {
            if (m_LaserCoroutine != null)
            {
                StopCoroutine(m_LaserCoroutine);
                m_LaserCoroutine = null;
            }

            OnHeartDestroyed?.Invoke(this);
            if (m_Arena != null)
            {
                m_Arena.OnHeartKilled();
            }
        }
    }
}
