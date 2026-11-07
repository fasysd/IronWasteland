using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Enemies;
using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    /// <summary>
    /// Fox Territory (GDD): 6s, kẻ địch trong vùng giảm 30% tốc độ, đồng minh (kể cả Green Fox) tăng 30% tốc độ.
    /// Đồng minh gây sát thương trong vùng giúp Green Fox hồi 3 năng lượng, tối đa 30.
    ///
    /// Vùng ảnh hưởng = CircleCollider2D (Is Trigger) trên prefab.
    /// - Tốc độ đồng minh: AddBonusStats (percent MoveSpeed).
    /// - Tốc độ kẻ địch: GreenFox_EnemyMoveModifier (hệ số nhân, tự hoàn trả đúng).
    /// - Mỗi xe/kẻ địch được đếm theo số collider để không gỡ buff nhầm khi chỉ rời 1 collider.
    ///
    /// Hồi năng lượng: MỌI nơi xe tăng (Green Fox hoặc đồng minh) gây sát thương lên kẻ địch phải gọi
    /// <see cref="ReportDamage"/> (khuyến nghị gọi 1 chỗ duy nhất trong EnemyBrain.TakeDamage).
    /// Hợp lệ khi người gây sát thương (Green Fox hoặc đồng minh) đang đứng trong vùng, bất kể kẻ địch bị đánh ở đâu.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class GreenFox_FoxTerritory : MonoBehaviour
    {
        [Header("Energy Rule")]
        [Tooltip("Bật: người gây sát thương phải đang đứng trong vùng mới hồi năng lượng (kẻ địch ở đâu cũng được). Tắt: mọi sát thương đều hồi.")]
        [SerializeField] private bool requireAttackerInside = true;

        [Tooltip("Khi bật requireAttackerInside: nguồn sát thương không khai báo người đánh (Attacker = null, code cũ của cả đội) có được tính không. " +
                 "Bật = tính nếu đang có ít nhất 1 xe nằm trong vòng (xấp xỉ, không phân biệt được ai đánh). Tắt = không tính.")]
        [SerializeField] private bool allowUnknownAttacker = true;

        // Danh sách Territory đang hoạt động, để báo sát thương từ bất kỳ nguồn nào.
        private static readonly List<GreenFox_FoxTerritory> s_Active = new List<GreenFox_FoxTerritory>();

        private TankBrain m_Owner;
        private float m_SlowAmount;
        private float m_SpeedBoostPercent;
        private float m_EnergyPerHit;
        private float m_MaxEnergyGain;
        private float m_TotalEnergyGained;
        private string m_Key;
        private CircleCollider2D m_Zone;

        private readonly Dictionary<EnemyBrain, int> m_EnemiesInside = new Dictionary<EnemyBrain, int>();
        private readonly Dictionary<TankBrain, int> m_TanksInside = new Dictionary<TankBrain, int>();

        private void OnEnable()
        {
            if (!s_Active.Contains(this)) s_Active.Add(this);
        }

        private void OnDisable()
        {
            s_Active.Remove(this);
        }

        public void Initialize(TankBrain owner, float duration, float slowAmount, float speedBoostPercent, float energyPerHit, float maxEnergyGain)
        {
            m_Owner = owner;
            m_SlowAmount = Mathf.Clamp01(slowAmount);
            m_SpeedBoostPercent = speedBoostPercent;
            m_EnergyPerHit = energyPerHit;
            m_MaxEnergyGain = maxEnergyGain;
            m_TotalEnergyGained = 0f;
            m_Key = $"GreenFox_Territory_{System.Guid.NewGuid():N}";

            Destroy(gameObject, duration);
        }

        /// <summary>
        /// Báo có xe tăng gây sát thương lên kẻ địch. Gọi từ MỌI nguồn sát thương của xe tăng
        /// (đạn, skill, DoT, đòn đánh thường, DamageEnemy của Green Fox...).
        /// Nên gọi NGAY khi gây sát thương (trước khi kẻ địch bị tắt collider) để đòn hạ gục vẫn được tính.
        /// </summary>
        public static void ReportDamage(TankBrain attacker, EnemyBrain enemy)
        {
            ReportDamage(enemy, attacker);
        }

        /// <summary>Báo kẻ địch nhận sát thương, kèm người gây sát thương (info.Attacker) để kiểm tra có đứng trong vùng không.</summary>
        public static void ReportDamage(EnemyBrain enemy, UnityEngine.Object attacker)
        {
            for (int i = s_Active.Count - 1; i >= 0; i--)
            {
                GreenFox_FoxTerritory territory = s_Active[i];
                if (territory == null) { s_Active.RemoveAt(i); continue; }
                territory.NotifyDamageDealtInTerritory(enemy, attacker);
            }
        }

        /// <summary>Không biết người đánh (xem allowUnknownAttacker).</summary>
        public static void ReportDamage(EnemyBrain enemy)
        {
            ReportDamage(enemy, (UnityEngine.Object)null);
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(collision);
            if (targetRef == null) return;

            // Kẻ địch: giảm 30% tốc độ
            EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
            if (enemy != null)
            {
                m_EnemiesInside.TryGetValue(enemy, out int count);
                m_EnemiesInside[enemy] = count + 1;

                if (count == 0) GreenFox_EnemyMoveModifier.Apply(enemy, m_Key, 1f - m_SlowAmount);
            }

            // Xe tăng (đồng minh hoặc Green Fox): tăng 30% tốc độ
            TankBrain tank = targetRef.GetOwner<TankBrain>();
            if (tank != null)
            {
                m_TanksInside.TryGetValue(tank, out int count);
                m_TanksInside[tank] = count + 1;

                if (count == 0)
                {
                    TankStats percent = TankStats.Zeroed();
                    percent.MoveSpeed = m_SpeedBoostPercent; // Final = Base * (1 + 0.3)
                    tank.AddBonusStats(m_Key, TankStats.Zeroed(), percent);
                }
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(collision);
            if (targetRef == null) return;

            EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
            if (enemy != null && m_EnemiesInside.TryGetValue(enemy, out int ec))
            {
                ec--;
                if (ec <= 0)
                {
                    m_EnemiesInside.Remove(enemy);
                    GreenFox_EnemyMoveModifier.Remove(enemy, m_Key);
                }
                else m_EnemiesInside[enemy] = ec;
            }

            TankBrain tank = targetRef.GetOwner<TankBrain>();
            if (tank != null && m_TanksInside.TryGetValue(tank, out int tc))
            {
                tc--;
                if (tc <= 0)
                {
                    m_TanksInside.Remove(tank);
                    ReleaseTank(tank);
                }
                else m_TanksInside[tank] = tc;
            }
        }

        private void ReleaseTank(TankBrain tank)
        {
            if (tank == null) return;
            tank.RemoveBonusStats(m_Key);
        }

        /// <summary>
        /// Hồi năng lượng cho Green Fox khi có xe tăng gây sát thương. Chỉ hồi khi:
        /// - người gây sát thương đang đứng trong vùng (vị trí kẻ địch không liên quan).
        /// Dùng <see cref="ReportDamage"/> thay vì gọi trực tiếp hàm này.
        /// </summary>
        public void NotifyDamageDealtInTerritory(EnemyBrain enemy = null, UnityEngine.Object attacker = null)
        {
            if (m_Owner == null || m_TotalEnergyGained >= m_MaxEnergyGain) return;

            // Vị trí kẻ địch KHÔNG liên quan. Chỉ cần người gây sát thương đang đứng trong vùng
            // (kiểm tra theo vị trí, dùng được cho cả trụ/bẫy)
            if (requireAttackerInside)
            {
                if (attacker == null)
                {
                    // Không biết ai đánh: chỉ tính khi có ít nhất 1 xe đang đứng trong vòng.
                    if (!allowUnknownAttacker || m_TanksInside.Count == 0) return;
                }
                else
                {
                    // Xe tăng: chỉ tính khi đang có buff Fox Territory (nằm trong m_TanksInside).
                    // Hết buff (ra vòng / hết thời gian) thì không hồi nữa.
                    TankBrain attackerTank = ResolveTank(attacker);
                    if (attackerTank != null)
                    {
                        if (!m_TanksInside.ContainsKey(attackerTank)) return;
                    }
                    else
                    {
                        // Nguồn không phải xe tăng (trụ, bẫy...): kiểm tra theo vị trí.
                        if (!TryGetPosition(attacker, out Vector2 attackerPos)) return;
                        if (!IsInsideZone(attackerPos)) return;
                    }
                }
            }

            float energyToGain = Mathf.Min(m_EnergyPerHit, m_MaxEnergyGain - m_TotalEnergyGained);
            if (energyToGain <= 0f) return;

            m_Owner.GainEnergy(energyToGain);
            m_TotalEnergyGained += energyToGain;
        }

        private static TankBrain ResolveTank(UnityEngine.Object obj)
        {
            if (obj is TankBrain tb) return tb;
            if (obj is Component c && c != null) return c.GetComponentInParent<TankBrain>();
            if (obj is GameObject go && go != null) return go.GetComponentInParent<TankBrain>();
            return null;
        }

        private static bool TryGetPosition(UnityEngine.Object obj, out Vector2 position)
        {
            position = Vector2.zero;
            if (obj is Component c && c != null) { position = c.transform.position; return true; }
            if (obj is GameObject go && go != null) { position = go.transform.position; return true; }
            return false;
        }

        private bool IsInsideZone(Vector2 point)
        {
            if (m_Zone == null) m_Zone = GetComponent<CircleCollider2D>();
            return m_Zone != null && m_Zone.OverlapPoint(point);
        }

        private void OnDestroy()
        {
            s_Active.Remove(this);

            // Hết thời gian: gỡ toàn bộ buff/debuff còn lại
            foreach (var kv in m_TanksInside) ReleaseTank(kv.Key);
            foreach (var kv in m_EnemiesInside)
            {
                if (kv.Key != null) GreenFox_EnemyMoveModifier.Remove(kv.Key, m_Key);
            }

            m_TanksInside.Clear();
            m_EnemiesInside.Clear();
        }
    }
}