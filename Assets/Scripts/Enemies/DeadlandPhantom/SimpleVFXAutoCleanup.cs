using UnityEngine;

namespace IronWasteland.Enemies.DeadlandPhantom
{
    /// <summary>
    /// Component runtime tự động hủy GameObject hiệu ứng sau một khoảng thời gian nhất định.
    /// Dùng cho các sub-prefab (Impact, Explosion, Flash).
    /// Đặt trong runtime assembly để tương thích hoàn toàn với cả Editor lẫn Standalone build.
    /// </summary>
    [DisallowMultipleComponent]
    public class SimpleVFXAutoCleanup : MonoBehaviour
    {
        [Tooltip("Thời gian tồn tại của hiệu ứng trước khi tự hủy (giây).")]
        [SerializeField, Min(0.05f)] public float lifetime = 1f;

        private void Start()
        {
            Destroy(gameObject, Mathf.Max(0.05f, lifetime));
        }
    }
}
