using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    [DisallowMultipleComponent]
    public class GreenFox_EnergyAbsorbEffect : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField, Min(1f)] private float moveSpeed = 18f;            // Tốc độ lao xuống
        [SerializeField, Min(0.1f)] private float maxLifetime = 3f;

        [Header("Spawn Position Adjustments")]
        [SerializeField, Min(0.5f)] private float heightOffset = 4f;        // Độ cao xuất hiện phía trên xe tăng (mét)
        [SerializeField, Range(0f, 3f)] private float randomXOffset = 1f;    // Độ lệch ngang ngẫu nhiên cho tự nhiên

        private Transform m_TargetTankTransform;
        private bool m_IsInitialized = false;

        public void Initialize(Transform targetTank)
        {
            if (targetTank == null)
            {
                Destroy(gameObject);
                return;
            }

            m_TargetTankTransform = targetTank;

            // Tính toán điểm sinh hiệu ứng: Ở trên cao phía trên xe tăng (Y + heightOffset)
            float offsetX = Random.Range(-randomXOffset, randomXOffset);
            Vector3 spawnPosition = targetTank.position + new Vector3(offsetX, heightOffset, 0f);
            transform.position = spawnPosition;

            // Xoay đầu hiệu ứng hướng xuống phía xe tăng
            Vector3 dir = (targetTank.position - transform.position).normalized;
            if (dir != Vector3.zero)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }

            m_IsInitialized = true;
            Destroy(gameObject, maxLifetime);
        }

        private void Update()
        {
            if (!m_IsInitialized || m_TargetTankTransform == null) return;

            // Cập nhật điểm đến liên tục theo vị trí xe tăng (kể cả khi xe tăng đang di chuyển)
            Vector3 targetPosition = m_TargetTankTransform.position;

            // Lao mượt mà từ trên cao xuống xe tăng
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );

            // Khi chạm vào tâm xe tăng thì tiêu biến
            if (Vector3.Distance(transform.position, targetPosition) < 0.3f)
            {
                Destroy(gameObject);
            }
        }
    }
}