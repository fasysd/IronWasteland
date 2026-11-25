using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Enemies;
using UnityEngine;

namespace IronWasteland.Tanks.Equipment
{
    /// <summary>
    /// Phụ kiện 13 (STT 14 GDD): Lõi Tụ Điện Ma Pháp Sấm Sét.
    /// - Chỉ số nền: +20% Tấn công.
    /// - Nội tại: Mỗi lần thi triển Kỹ năng thường thứ 3 sẽ phóng tia sét chuỗi lan qua 4 kẻ địch đứng gần nhau,
    ///   gây 65% sát thương Tấn công (DamageSourceType.Accessory) và làm chậm 20% trong 5s.
    /// </summary>
    [DisallowMultipleComponent]
    public class Accessory_13_LightningCapacitorCore : EquipmentBrain
    {
        private const string BaseStatsKey = "Accessory_13_LightningCapacitorCore_BaseStats";

        [Header("Chỉ số nền")]
        [SerializeField, Range(0f, 1f)] private float baseAttackMultiplier = 0.20f;

        [Header("Nội tại Sấm Sét Chuỗi")]
        [SerializeField, Range(1, 5)] private int normalSkillCountRequired = 3;
        [SerializeField, Range(1, 10)] private int maxChainTargets = 4;
        [SerializeField, Range(0f, 2f)] private float chainDamageAttackRatio = 0.65f;
        [SerializeField, Range(0f, 1f)] private float slowPercent = 0.20f;
        [SerializeField, Min(1f)] private float slowDuration = 5.0f;
        [SerializeField, Min(1f)] private float chainJumpRadius = 6.0f;

        private int m_NormalSkillCastCount;
        private bool m_PendingChainLightning;


        protected override void OnEquipped(TankBrain owner)
        {
            base.OnEquipped(owner);
            if (owner != null)
            {
                owner.AddBonusStats(BaseStatsKey, null, new TankStats { Attack = baseAttackMultiplier });
            }
            m_NormalSkillCastCount = 0;
            m_PendingChainLightning = false;
        }

        protected override void OnUnequipped(TankBrain owner)
        {
            if (owner != null)
            {
                owner.RemoveBonusStats(BaseStatsKey);
            }
            base.OnUnequipped(owner);
        }

        public override void OnOwnerUsedSkill(int skillIndex)
        {
            if (Owner == null || !Owner.IsAlive || skillIndex != 0) return;

            m_NormalSkillCastCount++;
            if (m_NormalSkillCastCount >= normalSkillCountRequired)
            {
                m_NormalSkillCastCount = 0;
                m_PendingChainLightning = true;
            }
        }

        public override void OnOwnerDealtDamage(DamageInfo info)
        {
            if (Owner == null || !Owner.IsAlive || !m_PendingChainLightning) return;

            // Kích hoạt khi Kỹ năng thường trúng mục tiêu sau lần thi triển thứ 3
            if (info.SourceType == DamageSourceType.NormalSkill && info.Target is EnemyBrain firstTarget && firstTarget.IsAlive)
            {
                m_PendingChainLightning = false;
                CastChainLightning(firstTarget);
            }
        }

        private void CastChainLightning(EnemyBrain startTarget)
        {
            var hitEnemies = new List<EnemyBrain> { startTarget };
            EnemyBrain current = startTarget;
            float rawDamage = Owner.FinalStats.Attack * chainDamageAttackRatio;

            // Đòn giật vào mục tiêu đầu tiên
            ApplyLightningHit(current, rawDamage);

            for (int i = 1; i < maxChainTargets; i++)
            {
                EnemyBrain nextTarget = FindNextChainTarget(current.transform.position, chainJumpRadius, hitEnemies);
                if (nextTarget == null) break;

                hitEnemies.Add(nextTarget);
                current = nextTarget;
                ApplyLightningHit(current, rawDamage);
            }
        }

        private void ApplyLightningHit(EnemyBrain target, float rawDamage)
        {
            if (target == null || !target.IsAlive) return;
            DamageInfo lightningDmg = new DamageInfo(Owner, target, rawDamage, Owner.FinalStats.ArmorPenetration, target.transform.position, sourceType: DamageSourceType.Accessory);
            target.TakeDamage(lightningDmg);
            TextDamageManager.Instance?.Show(lightningDmg.FinalDamage, target.transform.position);

            target.ApplyCrowdControl(CrowdControlType.Slow, slowDuration, slowPercent);
        }

        private EnemyBrain FindNextChainTarget(Vector2 center, float radius, List<EnemyBrain> excluded)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
            EnemyBrain nearest = null;
            float minDistance = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                ColliderOwnerRef ownerRef = ColliderOwnerRef.FromCollider(hits[i]);
                if (ownerRef == null) continue;

                EnemyBrain enemy = ownerRef.GetOwner<EnemyBrain>();
                if (enemy == null || !enemy.IsAlive || excluded.Contains(enemy)) continue;

                float dist = Vector2.Distance(center, enemy.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearest = enemy;
                }
            }
            return nearest;
        }
    }
}
