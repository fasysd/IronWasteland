using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Enemies;
using UnityEngine;

namespace IronWasteland.Tanks.Equipment
{
    /// <summary>
    /// Phụ kiện 5 (STT 6 GDD): Lõi Ma Pháp Hắc Nguyệt.
    /// - Chỉ số nền: +25% Giới hạn máu.
    /// - Nội tại: Phát trường bức xạ xung quanh xe bán kính 2.5m làm kẻ địch bị giảm 20% Tốc độ di chuyển và giảm 15% Tấn công.
    /// </summary>
    [DisallowMultipleComponent]
    public class Accessory_05_BlackMoonCore : EquipmentBrain
    {
        private const string BaseStatsKey = "Accessory_05_BlackMoonCore_BaseStats";

        [Header("Chỉ số nền")]
        [SerializeField, Range(0f, 1f)] private float baseMaxHealthMultiplier = 0.25f;

        [Header("Nội tại Trường Bức Xạ")]
        [SerializeField, Min(1f)] private float auraRadius = 2.5f;
        [SerializeField, Range(0f, 1f)] private float slowPercent = 0.20f;
        [SerializeField, Range(0f, 1f)] private float attackDebuffPercent = 0.15f;
        [SerializeField, Min(0.1f)] private float pulseInterval = 0.3f;

        private float m_PulseTimer;
        private readonly HashSet<EnemyBrain> m_AffectedEnemies = new HashSet<EnemyBrain>();
        private readonly HashSet<EnemyBrain> m_CurrentEnemies = new HashSet<EnemyBrain>();


                protected override void OnEquipped(TankBrain owner)
        {
            base.OnEquipped(owner);
            if (owner != null)
            {
                owner.AddBonusStats(BaseStatsKey, null, new TankStats { MaxHealth = baseMaxHealthMultiplier });
            }
        }

        protected override void OnUnequipped(TankBrain owner)
        {
            if (owner != null)
            {
                owner.RemoveBonusStats(BaseStatsKey);
            }
            ClearAllDebuffs();
            base.OnUnequipped(owner);
        }

        public override void OnBrainTick(float deltaTime)
        {
            if (Owner == null || !Owner.IsAlive)
            {
                ClearAllDebuffs();
                return;
            }

            m_PulseTimer += deltaTime;
            if (m_PulseTimer < pulseInterval) return;
            m_PulseTimer = 0f;

            m_CurrentEnemies.Clear();
            Collider2D[] hits = Physics2D.OverlapCircleAll(Owner.transform.position, auraRadius);
            for (int i = 0; i < hits.Length; i++)
            {
                ColliderOwnerRef ownerRef = ColliderOwnerRef.FromCollider(hits[i]);
                if (ownerRef == null) continue;

                EnemyBrain enemy = ownerRef.GetOwner<EnemyBrain>();
                if (enemy != null && enemy.IsAlive)
                {
                    m_CurrentEnemies.Add(enemy);
                    enemy.ApplyCrowdControl(CrowdControlType.Slow, pulseInterval + 0.25f, slowPercent);
                    if (m_AffectedEnemies.Add(enemy))
                    {
                        ApplyDebuff(enemy);
                    }
                }
            }

            // Gỡ bỏ debuff cho những quái đã ra khỏi vùng
            var toRemove = new List<EnemyBrain>();
            foreach (var enemy in m_AffectedEnemies)
            {
                if (enemy == null || !enemy.IsAlive || !m_CurrentEnemies.Contains(enemy))
                {
                    toRemove.Add(enemy);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                RemoveDebuff(toRemove[i]);
                m_AffectedEnemies.Remove(toRemove[i]);
            }
        }

        private void ApplyDebuff(EnemyBrain enemy)
        {
            if (enemy == null) return;
            enemy.ApplyCrowdControl(CrowdControlType.Slow, pulseInterval + 0.2f, slowPercent);
            if (enemy.Stats != null)
            {
                enemy.Stats.Attack = Mathf.Max(0f, enemy.Stats.Attack * (1f - attackDebuffPercent));
            }
        }

        private void RemoveDebuff(EnemyBrain enemy)
        {
            if (enemy == null || enemy.Stats == null) return;
            enemy.Stats.Attack /= (1f - attackDebuffPercent);
        }

        private void ClearAllDebuffs()
        {
            foreach (var enemy in m_AffectedEnemies)
            {
                RemoveDebuff(enemy);
            }
            m_AffectedEnemies.Clear();
            m_CurrentEnemies.Clear();
        }
    }
}
