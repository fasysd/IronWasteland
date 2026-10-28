using UnityEngine;
using System.Collections;

namespace IronWasteland.Tanks.WaterB
{
    [DisallowMultipleComponent]
    public class WaterB_SharedHealingLink : MonoBehaviour
    {
        [Header("Prefab Link Settings")]
        [SerializeField] private WaterB_HealingProjectile linkProjectilePrefab; // Prefab đạn bắn liên tục tạo tia liên kết
        [SerializeField, Min(0.05f)] private float rapidFireInterval = 0.15f;    // Tốc độ bắn cực nhanh giữa 2 tank

        private TankBrain m_OwnerTank;      // Water B
        private TankBrain m_TargetAllyTank; // RedNova hoặc đồng minh khác
        private float m_SharedPercent;
        private float m_Duration;
        private bool m_IsActive;
        private Coroutine m_ContinuousLinkCoroutine;

        // Cờ chống vòng lặp vô tận khi chia sẻ máu qua lại
        private bool m_IsSharing = false;

        public void Initialize(TankBrain owner, TankBrain targetAlly, float sharedPercent, float duration)
        {
            m_OwnerTank = owner;
            m_TargetAllyTank = targetAlly;
            m_SharedPercent = sharedPercent;
            m_Duration = duration > 0f ? duration : 10f;
            m_IsActive = true;

            if (m_TargetAllyTank != null)
            {
                transform.position = m_TargetAllyTank.transform.position;
                transform.SetParent(m_TargetAllyTank.transform);
            }

            // Bắt đầu coroutine bắn prefab liên tục để tạo tia liên kết trực quan
            m_ContinuousLinkCoroutine = StartCoroutine(ContinuousLinkBeamRoutine());

            Destroy(gameObject, m_Duration);
        }

        private void Update()
        {
            if (!m_IsActive || m_OwnerTank == null || m_TargetAllyTank == null)
            {
                if (m_ContinuousLinkCoroutine != null) StopCoroutine(m_ContinuousLinkCoroutine);
                Destroy(gameObject);
            }
        }

        private IEnumerator ContinuousLinkBeamRoutine()
        {
            while (m_IsActive && m_OwnerTank != null && m_TargetAllyTank != null)
            {
                ShootVisualLinkProjectile(m_OwnerTank.transform.position, m_TargetAllyTank);
                yield return new WaitForSeconds(rapidFireInterval);
            }
        }

        private void ShootVisualLinkProjectile(Vector3 startPos, TankBrain targetTank)
        {
            if (linkProjectilePrefab == null) return;

            Vector3 direction = (targetTank.transform.position - startPos).normalized;
            WaterB_HealingProjectile proj = Instantiate(linkProjectilePrefab, startPos, Quaternion.identity);

            // Đạn trực quan truyền giá trị 0f, callback được lọc bỏ nên không hiện text hay hồi máu thừa
            proj.Initialize(m_OwnerTank, direction, 0f, false, (val) => { });
        }

        /// <summary>
        /// Được gọi khi BẤT KÌ tank nào trong liên kết nhận được máu.
        /// </summary>
        public void NotifyTankHealed(TankBrain receiver, float healAmount, bool isHealingByShare)
        {
            if (!m_IsActive || m_IsSharing || isHealingByShare || healAmount <= 0f) return;

            m_IsSharing = true;

            float sharedAmount = healAmount * m_SharedPercent;
            TankBrain otherTank = (receiver == m_OwnerTank) ? m_TargetAllyTank : m_OwnerTank;

            if (otherTank != null && sharedAmount > 0f)
            {
                ShootHealingProjectileTo(receiver.transform.position, otherTank, sharedAmount);
                Debug.Log($"[SharedHealing] {receiver.name} nhận {healAmount} HP -> Bắn đạn chia sẻ {sharedAmount} HP cho {otherTank.name}!");
            }

            m_IsSharing = false;
        }

        private void ShootHealingProjectileTo(Vector3 startPos, TankBrain targetTank, float amount)
        {
            if (amount <= 0f) return;

            if (linkProjectilePrefab == null)
            {
                ApplyHealingByShareDirectly(targetTank, amount);
                return;
            }

            Vector3 direction = (targetTank.transform.position - startPos).normalized;
            WaterB_HealingProjectile proj = Instantiate(linkProjectilePrefab, startPos, Quaternion.identity);

            proj.Initialize(m_OwnerTank, direction, amount, false, (val) =>
            {
                if (val > 0f)
                {
                    ApplyHealingByShareDirectly(targetTank, val);
                }
            });
        }

        private void ApplyHealingByShareDirectly(TankBrain targetTank, float amount)
        {
            if (amount <= 0f) return;

            if (targetTank is TankBrain_WaterB waterBTarget)
            {
                waterBTarget.HealByShare(amount);
            }
            else
            {
                targetTank.Heal(amount, true);
                TextDamageManager.Instance?.Show(amount, targetTank.transform.position);
            }
        }
    }
}