using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.WaterB
{
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public class WaterB_MobileAltar : MonoBehaviour
    {
        [Header("Altar Settings")]
        [SerializeField] private float altarDuration = 8f;
        [SerializeField] private float healInterval = 1f;       // Khoảng thời gian giữa mỗi lần hồi máu (giây)
        [SerializeField] private float healPerTick = 10f;       // Lượng HP hồi mỗi nhịp (10 HP)
        [SerializeField] private float defenseBoostMultiplier = 3.0f; // Tăng 300% giáp/khiên

        private Transform m_OwnerTransform;
        private TankBrain m_OwnerTank;
        private bool m_IsActive = false;

        private readonly HashSet<TankBrain> m_InsideTanks = new HashSet<TankBrain>();
        private readonly Dictionary<TankBrain, string> m_TankBonusKeys = new Dictionary<TankBrain, string>();
        private const string AltarDefenseKey = "MobileAltar_DefenseBoost";

        public void Initialize(Transform ownerTransform, float duration, float interval, float healAmount)
        {
            m_OwnerTransform = ownerTransform;
            m_OwnerTank = ownerTransform != null ? ownerTransform.GetComponent<TankBrain>() : null;

            if (duration > 0f) altarDuration = duration;
            if (interval > 0f) healInterval = interval;
            if (healAmount > 0f) healPerTick = healAmount;

            m_IsActive = true;

            // Đảm bảo Rigidbody2D ở dạng Kinematic để không bị tác động vật lý đẩy đi
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
            }

            // Bắt đầu chuỗi hoạt động của đài tế (hồi máu định kỳ và bám theo Water B)
            StartCoroutine(AltarRoutine());
        }

        private void Update()
        {
            // Đài tế di chuyển bám theo chân Water B
            if (m_OwnerTransform != null)
            {
                transform.position = m_OwnerTransform.position;
            }
        }

        private IEnumerator AltarRoutine()
        {
            float elapsed = 0f;
            WaitForSeconds waitInterval = new WaitForSeconds(healInterval);

            while (elapsed < altarDuration)
            {
                elapsed += healInterval;

                // Thực hiện hồi máu định kỳ cho tất cả mục tiêu bên trong vùng ảnh hưởng
                TriggerPeriodicHeal();

                yield return waitInterval;
            }

            // Hết thời gian, tự hủy đài tế
            CleanupAndDestroy();
        }

        private void TriggerPeriodicHeal()
        {
            if (!m_IsActive) return;

            foreach (var tank in m_InsideTanks)
            {
                if (tank != null)
                {
                    tank.Heal(healPerTick);
                    TextDamageManager.Instance?.Show(healPerTick, tank.transform.position);
                    Debug.Log($"[MobileAltar] Hồi phục {healPerTick} HP cho {tank.name}");
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!m_IsActive) return;

            TankBrain tank = collision.GetComponentInParent<TankBrain>();
            if (tank != null && !m_InsideTanks.Contains(tank))
            {
                m_InsideTanks.Add(tank);

                string bonusKey = AltarDefenseKey + "_" + tank.GetHashCode();
                m_TankBonusKeys[tank] = bonusKey;

                // Tăng 300% Defense/Khiên
                TankStats bonus = TankStats.Zeroed();
                bonus.Defense = defenseBoostMultiplier;
                tank.AddBonusStats(bonusKey, bonus);

                Debug.Log($"[MobileAltar] Xe tăng {tank.name} vào vùng ảnh hưởng, nhận +300% Defense.");
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            TankBrain tank = collision.GetComponentInParent<TankBrain>();
            if (tank != null && m_InsideTanks.Contains(tank))
            {
                RemoveTankBuff(tank);
                m_InsideTanks.Remove(tank);
                Debug.Log($"[MobileAltar] Xe tăng {tank.name} rời khỏi vùng ảnh hưởng.");
            }
        }

        private void RemoveTankBuff(TankBrain tank)
        {
            if (tank != null && m_TankBonusKeys.TryGetValue(tank, out string key))
            {
                tank.RemoveBonusStats(key);
                m_TankBonusKeys.Remove(tank);
            }
        }

        private void CleanupAndDestroy()
        {
            if (!m_IsActive) return;
            m_IsActive = false;

            // Xóa sạch buff khiên/giáp khi đài tế kết thúc
            foreach (var tank in m_InsideTanks)
            {
                RemoveTankBuff(tank);
            }

            m_InsideTanks.Clear();
            m_TankBonusKeys.Clear();

            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            CleanupAndDestroy();
        }
    }
}