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
    /// Hồi năng lượng: KHÔNG cần ai gọi hàm báo. Territory tự đăng ký vào EnemyBrain.OnTakeDamageEvent
    /// của mọi kẻ địch đang có trên màn (quét định kỳ để bắt cả kẻ địch mới sinh ra), nên mọi nguồn
    /// sát thương (đạn, chiêu thức, DoT, DamageEnemy...) đều được tính mà không phải sửa EnemyBrain.
    /// Hợp lệ khi người gây sát thương (Green Fox hoặc đồng minh) đang đứng trong vùng, bất kể kẻ địch bị đánh ở đâu.
    /// Lưu ý: OnTakeDamageEvent chỉ phát khi đòn thật sự gây sát thương (đòn bị né/chặn sạch thì không tính),
    /// và phát TRƯỚC khi kẻ địch chết nên đòn hạ gục vẫn được tính.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class GreenFox_FoxTerritory : MonoBehaviour
    {
        // Quy tắc hồi năng lượng (hằng số trong code để không bị giá trị cũ lưu trong prefab che mất).
        private const bool RequireAttackerInside = true;  // Người gây sát thương phải đang được buff / đứng trong vòng.
        private const bool AllowUnknownAttacker = true;   // Đòn không khai báo Attacker: tính nếu có ít nhất 1 xe trong vòng.
        private const bool DebugLog = true;               // Bật log lý do hồi / không hồi. Đặt false khi xong.
        private const float EnemyScanInterval = 0.25f;    // Chu kỳ quét kẻ địch mới để đăng ký lắng nghe sát thương (giây).

        private TankBrain m_Owner;
        private float m_SlowAmount;
        private float m_SpeedBoostPercent;
        private float m_EnergyPerHit;
        private float m_MaxEnergyGain;
        private float m_TotalEnergyGained;
        private string m_Key;
        private CircleCollider2D m_Zone;
        private float m_NextScanTime;

        private readonly Dictionary<EnemyBrain, int> m_EnemiesInside = new Dictionary<EnemyBrain, int>();
        private readonly Dictionary<TankBrain, int> m_TanksInside = new Dictionary<TankBrain, int>();

        // Kẻ địch đã đăng ký lắng nghe OnTakeDamageEvent (để không đăng ký trùng và gỡ hết khi kết thúc).
        private readonly HashSet<EnemyBrain> m_HookedEnemies = new HashSet<EnemyBrain>();

        public void Initialize(TankBrain owner, float duration, float slowAmount, float speedBoostPercent, float energyPerHit, float maxEnergyGain)
        {
            m_Owner = owner;
            m_SlowAmount = Mathf.Clamp01(slowAmount);
            m_SpeedBoostPercent = speedBoostPercent;
            m_EnergyPerHit = energyPerHit;
            m_MaxEnergyGain = maxEnergyGain;
            m_TotalEnergyGained = 0f;
            m_Key = $"GreenFox_Territory_{System.Guid.NewGuid():N}";

            // Đăng ký ngay cho kẻ địch đang có trên màn, kẻ địch sinh sau sẽ được Update bắt.
            HookNewEnemies();
            m_NextScanTime = Time.time + EnemyScanInterval;

            Destroy(gameObject, duration);
        }

        private void Update()
        {
            if (Time.time < m_NextScanTime) return;
            m_NextScanTime = Time.time + EnemyScanInterval;
            HookNewEnemies();
        }

        /// <summary>Đăng ký lắng nghe sát thương cho mọi kẻ địch chưa được đăng ký.</summary>
        private void HookNewEnemies()
        {
            EnemyBrain[] enemies = Object.FindObjectsByType<EnemyBrain>();
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyBrain enemy = enemies[i];
                if (enemy == null) continue;
                if (!m_HookedEnemies.Add(enemy)) continue; // đã đăng ký rồi

                enemy.OnTakeDamageEvent += HandleEnemyTookDamage;
            }
        }

        /// <summary>Có kẻ địch vừa nhận sát thương thật sự: kiểm tra người gây sát thương rồi hồi năng lượng.</summary>
        private void HandleEnemyTookDamage(DamageInfo info)
        {
            OnDamageDealtInTerritory(info.Attacker);
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(collision);
            if (targetRef == null) return;

            // Kẻ địch: giảm 30% tốc độ
            // Chỉ tính collider thuộc THÂN kẻ địch/xe (nằm trong hierarchy của chúng), không tính đạn/vật thể
            // do chúng bắn ra (cũng mang ColliderOwnerRef có Owner là chúng nhưng bay độc lập).
            EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
            if (enemy != null && IsPartOf(collision, enemy))
            {
                m_EnemiesInside.TryGetValue(enemy, out int count);
                m_EnemiesInside[enemy] = count + 1;

                if (count == 0) GreenFox_EnemyMoveModifier.Apply(enemy, m_Key, 1f - m_SlowAmount);
            }

            // Xe tăng (đồng minh hoặc Green Fox): tăng 30% tốc độ
            TankBrain tank = targetRef.GetOwner<TankBrain>();
            if (tank != null && IsPartOf(collision, tank))
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
            if (enemy != null && IsPartOf(collision, enemy) && m_EnemiesInside.TryGetValue(enemy, out int ec))
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
            if (tank != null && IsPartOf(collision, tank) && m_TanksInside.TryGetValue(tank, out int tc))
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

        /// <summary>True nếu collider nằm trong hierarchy của chủ (thân xe/kẻ địch), false nếu là đạn/vật thể độc lập.</summary>
        private static bool IsPartOf(Collider2D collider, Component owner)
        {
            if (collider == null || owner == null) return false;
            return collider.transform == owner.transform || collider.transform.IsChildOf(owner.transform);
        }

        private void ReleaseTank(TankBrain tank)
        {
            if (tank == null) return;
            tank.RemoveBonusStats(m_Key);
        }

        /// <summary>
        /// Hồi năng lượng cho Green Fox khi có xe tăng gây sát thương. Chỉ hồi khi:
        /// - người gây sát thương đang đứng trong vùng (vị trí kẻ địch không liên quan).
        /// </summary>
        private void OnDamageDealtInTerritory(UnityEngine.Object attacker)
        {
            if (m_Owner == null)
            {
                if (DebugLog) Debug.Log("[Territory] Bỏ qua: m_Owner = null (Initialize chưa được gọi?)", this);
                return;
            }

            if (m_TotalEnergyGained >= m_MaxEnergyGain)
            {
                if (DebugLog) Debug.Log($"[Territory] Bỏ qua: đã đạt tối đa {m_MaxEnergyGain} năng lượng.", this);
                return;
            }

            // Vị trí kẻ địch KHÔNG liên quan. Chỉ cần người gây sát thương đang được buff / đứng trong vùng.
            if (RequireAttackerInside)
            {
                if (attacker == null)
                {
                    // Không biết ai đánh: chỉ tính khi TẤT CẢ đồng minh (trừ Green Fox) đều đang trong vòng,
                    // để chắc chắn người đánh không đứng ngoài. Có đồng minh ở ngoài vòng thì không tính.
                    if (!AllowUnknownAttacker || !IsUnknownAttackerSafelyInside())
                    {
                        if (DebugLog) Debug.Log($"[Territory] Bỏ qua: Attacker=NULL và có đồng minh đứng ngoài vòng (hoặc không có xe nào trong vòng). Số xe trong vòng = {m_TanksInside.Count}", this);
                        return;
                    }
                }
                else
                {
                    TankBrain attackerTank = ResolveTank(attacker);
                    if (attackerTank != null)
                    {
                        // Xe tăng: chỉ tính khi đang có buff Fox Territory (nằm trong m_TanksInside).
                        if (!m_TanksInside.ContainsKey(attackerTank))
                        {
                            if (DebugLog) Debug.Log($"[Territory] Bỏ qua: xe '{attackerTank.name}' KHÔNG có buff (không nằm trong vòng). Số xe trong vòng = {m_TanksInside.Count}", this);
                            return;
                        }
                    }
                    else
                    {
                        // Nguồn không phải xe tăng (trụ, bẫy...): kiểm tra theo vị trí.
                        if (!TryGetPosition(attacker, out Vector2 attackerPos) || !IsInsideZone(attackerPos))
                        {
                            if (DebugLog) Debug.Log($"[Territory] Bỏ qua: nguồn '{attacker.name}' ({attacker.GetType().Name}) không phải xe tăng và nằm ngoài vòng.", this);
                            return;
                        }
                    }
                }
            }

            float energyToGain = Mathf.Min(m_EnergyPerHit, m_MaxEnergyGain - m_TotalEnergyGained);
            if (energyToGain <= 0f)
            {
                if (DebugLog) Debug.Log($"[Territory] Bỏ qua: energyToGain = {energyToGain} (EnergyPerHit = {m_EnergyPerHit}, MaxEnergyGain = {m_MaxEnergyGain}) — kiểm tra thông số skill2SubDesign.", this);
                return;
            }

            m_Owner.GainEnergy(energyToGain);
            m_TotalEnergyGained += energyToGain;

            if (DebugLog) Debug.Log($"[Territory] HỒI +{energyToGain} năng lượng (tổng {m_TotalEnergyGained}/{m_MaxEnergyGain}). Attacker={(attacker != null ? attacker.name : "NULL")}", this);
        }

        // Cache danh sách xe tăng theo frame (tránh tìm lại mỗi lần có sát thương).
        private static TankBrain[] s_TankCache;
        private static int s_TankCacheFrame = -1;

        private static TankBrain[] GetAllTanks()
        {
            if (s_TankCache == null || s_TankCacheFrame != Time.frameCount)
            {
                s_TankCache = Object.FindObjectsByType<TankBrain>();
                s_TankCacheFrame = Time.frameCount;
            }
            return s_TankCache;
        }

        /// <summary>
        /// Với đòn không rõ người đánh: true chỉ khi mọi đồng minh (không tính Green Fox) đều đang trong vòng.
        /// Nếu không có đồng minh nào khác thì xét chính Green Fox có đang trong vòng không.
        /// </summary>
        private bool IsUnknownAttackerSafelyInside()
        {
            bool anyOther = false;
            TankBrain[] tanks = GetAllTanks();
            for (int i = 0; i < tanks.Length; i++)
            {
                TankBrain t = tanks[i];
                if (t == null || !t.isActiveAndEnabled || t == m_Owner) continue;

                anyOther = true;
                if (!m_TanksInside.ContainsKey(t)) return false; // có đồng minh đứng ngoài vòng
            }

            if (anyOther) return true;
            return m_Owner != null && m_TanksInside.ContainsKey(m_Owner);
        }

        /// <summary>
        /// Từ "người gây sát thương" (xe tăng, đạn, vật thể phụ...) lần ra xe tăng chủ.
        /// Ưu tiên OwnerRef (hệ thống chủ sở hữu dùng chung của project), sau đó mới tìm TankBrain ở object cha.
        /// </summary>
        private static TankBrain ResolveTank(UnityEngine.Object obj)
        {
            if (obj is TankBrain tb) return tb;

            GameObject go = obj as GameObject;
            if (go == null && obj is Component c && c != null) go = c.gameObject;
            if (go == null) return null;

            OwnerRef ownerRef = go.GetComponentInParent<OwnerRef>();
            if (ownerRef != null)
            {
                TankBrain ownerTank = ownerRef.GetOwner<TankBrain>();
                if (ownerTank != null) return ownerTank;
            }

            return go.GetComponentInParent<TankBrain>();
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
            // Hết thời gian: gỡ đăng ký lắng nghe sát thương khỏi mọi kẻ địch
            foreach (EnemyBrain enemy in m_HookedEnemies)
            {
                if (enemy != null) enemy.OnTakeDamageEvent -= HandleEnemyTookDamage;
            }
            m_HookedEnemies.Clear();

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