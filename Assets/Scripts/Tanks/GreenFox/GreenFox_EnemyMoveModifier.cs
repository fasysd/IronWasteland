using System.Collections.Generic;
using IronWasteland.Enemies;
using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    /// <summary>
    /// Quản lý TẬP TRUNG mọi hiệu ứng đổi tốc độ di chuyển lên 1 kẻ địch (trói chân, làm chậm lãnh địa...).
    /// Mỗi nguồn (key) có 1 hệ số nhân; tốc độ cuối = tốc độ gốc * tích các hệ số. Gỡ hết nguồn thì trả về tốc độ gốc.
    /// Sửa lỗi cũ: trói chân chồng nhau làm kẻ địch đứng yên vĩnh viễn, và công thức hồi tốc lãnh địa sai.
    /// </summary>
    [DisallowMultipleComponent]
    public class GreenFox_EnemyMoveModifier : MonoBehaviour
    {
        private EnemyBrain m_Enemy;
        private float m_BaseSpeed;
        private readonly Dictionary<string, float> m_Multipliers = new Dictionary<string, float>();
        private readonly Dictionary<string, float> m_Expiry = new Dictionary<string, float>();
        private readonly List<string> m_Expired = new List<string>();

        /// <summary>multiplier: 0 = trói chân, 0.7 = giảm 30% tốc độ. duration &lt;= 0: giữ đến khi gọi Remove.</summary>
        public static void Apply(EnemyBrain enemy, string key, float multiplier, float duration = 0f)
        {
            if (enemy == null || enemy.Stats == null) return;

            var mod = enemy.GetComponent<GreenFox_EnemyMoveModifier>();
            if (mod == null) mod = enemy.gameObject.AddComponent<GreenFox_EnemyMoveModifier>();

            mod.m_Enemy = enemy;
            mod.Set(key, multiplier, duration);
        }

        public static void Remove(EnemyBrain enemy, string key)
        {
            if (enemy == null) return;

            var mod = enemy.GetComponent<GreenFox_EnemyMoveModifier>();
            if (mod != null) mod.Unset(key);
        }

        private void Set(string key, float multiplier, float duration)
        {
            if (m_Multipliers.Count == 0) m_BaseSpeed = m_Enemy.Stats.MoveSpeed; // chụp tốc độ gốc khi bắt đầu bị ảnh hưởng

            m_Multipliers[key] = Mathf.Max(0f, multiplier);
            if (duration > 0f) m_Expiry[key] = Time.time + duration;
            else m_Expiry.Remove(key);

            Recompute();
        }

        private void Unset(string key)
        {
            if (!m_Multipliers.Remove(key)) return;
            m_Expiry.Remove(key);
            Recompute();
        }

        private void Recompute()
        {
            if (m_Enemy == null || m_Enemy.Stats == null) return;

            float product = 1f;
            foreach (var kv in m_Multipliers) product *= kv.Value;

            m_Enemy.Stats.MoveSpeed = m_BaseSpeed * product; // hết nguồn => product = 1 => trả về tốc độ gốc
        }

        private void Update()
        {
            if (m_Expiry.Count == 0) return;

            m_Expired.Clear();
            foreach (var kv in m_Expiry)
            {
                if (Time.time >= kv.Value) m_Expired.Add(kv.Key);
            }
            for (int i = 0; i < m_Expired.Count; i++) Unset(m_Expired[i]);
        }

        private void OnDestroy()
        {
            // Component bị gỡ khi kẻ địch còn sống: trả tốc độ gốc
            if (m_Multipliers.Count > 0 && m_Enemy != null && m_Enemy.Stats != null)
            {
                m_Enemy.Stats.MoveSpeed = m_BaseSpeed;
            }
        }
    }
}
