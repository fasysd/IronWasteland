using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Camera bám theo xe của người chơi cục bộ khi vào phòng mạng.
    /// Để trống <see cref="target"/> lúc offline — camera giữ nguyên như cũ.
    /// </summary>
    public class NetCameraFollow : MonoBehaviour
    {
        [Tooltip("Xe mà camera bám theo; gán lúc runtime bởi NetPlayerController.")]
        [SerializeField] private Transform target;

        [Tooltip("Độ mượt bám theo (càng lớn bám càng nhanh).")]
        [SerializeField, Min(0f)] private float smoothSpeed = 8f;

        /// <summary>Đặt mục tiêu bám theo (thường là xe của người chơi cục bộ).</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = new Vector3(target.position.x, target.position.y, transform.position.z);
            float blend = smoothSpeed <= 0f ? 1f : 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, blend);
        }
    }
}
