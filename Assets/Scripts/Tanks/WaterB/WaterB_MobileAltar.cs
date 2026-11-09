using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IronWasteland.Core.Effects;

namespace IronWasteland.Tanks.WaterB
{
    /// <summary>
    /// Mobile Altar (GDD): Water B không thể tấn công trong thời gian này, nhưng cứ mỗi 2s hồi máu
    /// (50% ATK + 2% Giới hạn máu) và tạo khiên (80% ATK + 5% Giới hạn máu, kéo dài 5s) cho mọi đồng minh xung quanh.
    /// Đồng minh đang có khiên của đài tế thì khiên được đặt lại giá trị và thời gian duy trì.
    ///
    /// Vùng ảnh hưởng = CircleCollider2D (Is Trigger) đã cài trên prefab.
    /// Mục tiêu được xác định DUY NHẤT qua ColliderOwnerRef và GetTriggerColliders().
    /// Khiên đi qua TankBrain.AddShield (ShieldEffect). Hồi máu đi qua TankBrain_WaterB.HealAlly
    /// (Heal(amount,false) nên Shared Healing vẫn kích hoạt).
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(ColliderOwnerRef))]
    [DisallowMultipleComponent]
    public class WaterB_MobileAltar : MonoBehaviour
    {
        [Header("Shield Visual")]
        [Tooltip("Prefab hào quang xanh nhạt quanh xe đang có khiên đài tế (gắn WaterB_ShieldAura). Để trống = tự tạo hào quang mặc định.")]
        [SerializeField] private GameObject shieldAuraPrefab;

        private ColliderOwnerRef m_OwnerRef;
        private TankBrain_WaterB m_Owner;
        private float m_Duration;
        private float m_Interval;
        private float m_HealPerTick;
        private float m_ShieldAmount;
        private float m_ShieldDuration;
        private Action m_OnFinished;
        private string m_ShieldId;
        private bool m_Finished;

        private readonly List<TankBrain> m_Snapshot = new List<TankBrain>();
        private readonly Dictionary<TankBrain, WaterB_ShieldAura> m_Auras = new Dictionary<TankBrain, WaterB_ShieldAura>();

        private void Awake()
        {
            m_OwnerRef = GetComponent<ColliderOwnerRef>();
        }

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

            if (m_OwnerRef == null) m_OwnerRef = GetComponent<ColliderOwnerRef>();
            if (m_OwnerRef != null) m_OwnerRef.SetOwner(owner);

            // Cùng 1 Id cho mọi khiên của đài tế (theo Water B) -> luôn là 1 lớp khiên duy nhất trên mỗi đồng minh
            m_ShieldId = $"WaterB_MobileAltar_Shield_{owner.InstanceKey}";

            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;

            // Đặt đúng vị trí Water B ngay để lần hồi đầu tiên quét đúng vùng
            transform.position = owner.transform.position;

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

        private void PulseHealAndShield()
        {
            m_Snapshot.Clear();

            if (m_OwnerRef != null)
            {
                foreach (Collider2D col in m_OwnerRef.GetTriggerColliders())
                {
                    if (col == null) continue;

                    ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(col);
                    if (targetRef == null) continue;

                    TankBrain tank = targetRef.GetOwner<TankBrain>();
                    if (tank != null && tank.IsAlive && !m_Snapshot.Contains(tank))
                    {
                        m_Snapshot.Add(tank);
                    }
                }
            }

            // Đảm bảo caster (Water B) còn sống luôn nằm trong vùng tác dụng
            if (m_Owner != null && m_Owner.IsAlive && !m_Snapshot.Contains(m_Owner))
            {
                m_Snapshot.Add(m_Owner);
            }

            for (int i = 0; i < m_Snapshot.Count; i++)
            {
                TankBrain tank = m_Snapshot[i];

                // Hồi máu
                m_Owner.HealAlly(tank, m_HealPerTick);

                // Khiên: gỡ khiên cũ của đài tế (nếu có) rồi tạo lại => đặt lại cả giá trị lẫn thời gian
                tank.RemoveEffect(m_ShieldId);
                ShieldEffect shield = tank.AddShield(m_ShieldAmount, m_ShieldDuration, m_ShieldId, m_Owner, "Mobile Altar");

                if (shield != null) EnsureShieldAura(tank);
            }
        }

        /// <summary>Hào quang xanh nhạt quanh xe có khiên đài tế. Mỗi xe 1 hào quang, tự biến mất khi khiên hết.</summary>
        private void EnsureShieldAura(TankBrain tank)
        {
            if (m_Auras.TryGetValue(tank, out WaterB_ShieldAura existing) && existing != null)
            {
                existing.Refresh();
                return;
            }

            GameObject go = shieldAuraPrefab != null
                ? Instantiate(shieldAuraPrefab, tank.transform.position, Quaternion.identity)
                : new GameObject("WaterB_ShieldAura");

            WaterB_ShieldAura aura = go.GetComponent<WaterB_ShieldAura>();
            if (aura == null) aura = go.AddComponent<WaterB_ShieldAura>();

            aura.Initialize(tank, m_ShieldId);
            m_Auras[tank] = aura;
        }

        private void Finish()
        {
            if (m_Finished) return;
            m_Finished = true;

            m_OnFinished?.Invoke(); // báo Water B được phép tấn công lại
            m_OnFinished = null;
            m_Snapshot.Clear();

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
