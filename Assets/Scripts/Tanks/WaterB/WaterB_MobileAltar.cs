using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.WaterB
{
    /// <summary>
    /// Mobile Altar (GDD): Water B không thể tấn công trong thời gian này, nhưng cứ mỗi 2s hồi máu
    /// (50% ATK + 2% Giới hạn máu) và tạo khiên (80% ATK + 5% Giới hạn máu, kéo dài 5s) cho mọi đồng minh xung quanh.
    /// Đồng minh đang có khiên của đài tế thì khiên được đặt lại giá trị và thời gian duy trì.
    ///
    /// Vùng ảnh hưởng = CircleCollider2D (Is Trigger) đã cài trên prefab.
    /// Khiên đi qua TankBrain.AddShield (ShieldEffect). Hồi máu đi qua TankBrain_WaterB.HealAlly
    /// (Heal(amount,false) nên Shared Healing vẫn kích hoạt).
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public class WaterB_MobileAltar : MonoBehaviour
    {
        private TankBrain_WaterB m_Owner;
        private float m_Duration;
        private float m_Interval;
        private float m_HealPerTick;
        private float m_ShieldAmount;
        private float m_ShieldDuration;
        private Action m_OnFinished;
        private string m_ShieldId;
        private bool m_Finished;

        private readonly Dictionary<TankBrain, int> m_Inside = new Dictionary<TankBrain, int>();
        private readonly List<TankBrain> m_Snapshot = new List<TankBrain>();

        public void Initialize(TankBrain_WaterB owner, float duration, float interval,
                               float healPerTick, float shieldAmount, float shieldDuration,
                               Action onFinished)
        {
            m_Owner = owner;
            m_Duration = duration > 0f ? duration : 10f;
            m_Interval = interval > 0f ? interval : 2f;
            m_HealPerTick = healPerTick;
            m_ShieldAmount = shieldAmount;
            m_ShieldDuration = shieldDuration;
            m_OnFinished = onFinished;

            // Cùng 1 Id cho mọi khiên của đài tế (theo Water B) -> luôn là 1 lớp khiên duy nhất trên mỗi đồng minh
            m_ShieldId = $"WaterB_MobileAltar_Shield_{owner.InstanceKey}";

            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;

            StartCoroutine(AltarRoutine());
        }

        private void LateUpdate()
        {
            // Đài tế bám theo Water B
            if (m_Owner != null) transform.position = m_Owner.transform.position;
        }

        private IEnumerator AltarRoutine()
        {
            float endTime = Time.time + m_Duration;
            var wait = new WaitForSeconds(m_Interval);

            while (Time.time < endTime)
            {
                if (m_Owner == null || !m_Owner.IsAlive) break;

                PulseHealAndShield();
                yield return wait;
            }

            Finish();
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (m_Finished) return;

            TankBrain tank = collision.GetComponentInParent<TankBrain>();
            if (tank == null) return;

            m_Inside.TryGetValue(tank, out int count);
            m_Inside[tank] = count + 1;
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            TankBrain tank = collision.GetComponentInParent<TankBrain>();
            if (tank == null || !m_Inside.TryGetValue(tank, out int count)) return;

            count--;
            if (count <= 0) m_Inside.Remove(tank);
            else m_Inside[tank] = count;
        }

        private void PulseHealAndShield()
        {
            // Duyệt trên bản sao: hồi máu/khiên có thể kích hoạt sự kiện làm thay đổi tập hợp
            m_Snapshot.Clear();
            foreach (var kv in m_Inside)
            {
                if (kv.Key != null && kv.Key.IsAlive) m_Snapshot.Add(kv.Key);
            }

            for (int i = 0; i < m_Snapshot.Count; i++)
            {
                TankBrain tank = m_Snapshot[i];

                // Hồi máu
                m_Owner.HealAlly(tank, m_HealPerTick);

                // Khiên: gỡ khiên cũ của đài tế (nếu có) rồi tạo lại => đặt lại cả giá trị lẫn thời gian
                tank.RemoveEffect(m_ShieldId);
                tank.AddShield(m_ShieldAmount, m_ShieldDuration, m_ShieldId, m_Owner, "Mobile Altar");
            }
        }

        private void Finish()
        {
            if (m_Finished) return;
            m_Finished = true;

            m_OnFinished?.Invoke(); // báo Water B được phép tấn công lại
            m_OnFinished = null;
            m_Inside.Clear();

            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            // Dự phòng: bị xóa đột ngột vẫn trả lại khả năng tấn công cho Water B
            if (!m_Finished)
            {
                m_Finished = true;
                m_OnFinished?.Invoke();
                m_OnFinished = null;
            }
        }
    }
}