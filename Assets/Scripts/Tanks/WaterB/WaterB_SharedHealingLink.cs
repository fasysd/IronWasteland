using UnityEngine;

namespace IronWasteland.Tanks.WaterB
{
    [DisallowMultipleComponent]
    public class WaterB_SharedHealingLink : MonoBehaviour
    {
        [Header("Visual References")]
        [SerializeField] private LineRenderer lineRenderer;

        private TankBrain m_OwnerTank;
        private TankBrain m_TargetAllyTank;
        private float m_SharedPercent;
        private float m_Duration;
        private bool m_IsActive;

        // Cờ chống gọi lặp vô tận khi 2 bên qua lại chia sẻ máu
        private bool m_IsSharing = false;

        public void Initialize(TankBrain owner, TankBrain targetAlly, float sharedPercent, float duration)
        {
            m_OwnerTank = owner;
            m_TargetAllyTank = targetAlly;
            m_SharedPercent = sharedPercent;
            m_Duration = duration > 0f ? duration : 10f;
            m_IsActive = true;

            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
            }

            if (m_TargetAllyTank != null)
            {
                transform.position = m_TargetAllyTank.transform.position;
                transform.SetParent(m_TargetAllyTank.transform);
            }

            Destroy(gameObject, m_Duration);
        }

        private void Update()
        {
            if (!m_IsActive || m_OwnerTank == null || m_TargetAllyTank == null)
            {
                Destroy(gameObject);
                return;
            }

            // Cập nhật đường nối LineRenderer giữa 2 xe tăng
            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(0, m_OwnerTank.transform.position);
                lineRenderer.SetPosition(1, m_TargetAllyTank.transform.position);
            }
        }

        /// <summary>
        /// Xử lý san sẻ hiệu quả hồi máu qua lại giữa Chủ nhân (Water B) và Đồng minh.
        /// </summary>
        public void TriggerSharedHeal(TankBrain receiver, float healAmount)
        {
            if (!m_IsActive || m_IsSharing) return;

            // Bật cờ khóa để ngăn sự kiện gọi ngược lại lặp vô tận
            m_IsSharing = true;

            float sharedAmount = healAmount * m_SharedPercent;

            if (receiver == m_OwnerTank && m_TargetAllyTank != null)
            {
                // Chủ nhân nhận máu -> San sẻ sang cho Đồng minh
                m_TargetAllyTank.Heal(sharedAmount);
                TextDamageManager.Instance?.Show(sharedAmount, m_TargetAllyTank.transform.position);
                Debug.Log($"[SharedHealing] Chủ nhân nhận {healAmount} HP -> San sẻ {sharedAmount} HP cho đồng minh!");
            }
            else if (receiver == m_TargetAllyTank && m_OwnerTank != null)
            {
                // Đồng minh nhận máu -> San sẻ sang cho Chủ nhân (Water B)
                m_OwnerTank.Heal(sharedAmount);
                TextDamageManager.Instance?.Show(sharedAmount, m_OwnerTank.transform.position);
                Debug.Log($"[SharedHealing] Đồng minh nhận {healAmount} HP -> San sẻ {sharedAmount} HP cho chủ nhân!");
            }

            // Mở khóa cờ
            m_IsSharing = false;
        }
    }
}