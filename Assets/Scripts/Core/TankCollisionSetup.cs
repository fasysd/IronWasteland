using UnityEngine;

namespace IronWasteland.Core
{
    /// <summary>
    /// Thiết lập vật lý dùng chung cho co-op.
    ///
    /// Xe tăng nằm ở layer <c>Tank</c>; đồng đội được phép xuyên qua nhau nên tắt va chạm
    /// Tank ↔ Tank ngay khi game khởi động. Cách này không sửa <c>ProjectSettings</c> và chỉ
    /// tác động đúng cặp layer Tank–Tank; xe vẫn va chạm với quái (layer <c>Enemy</c>) bình thường.
    /// </summary>
    public static class TankCollisionSetup
    {
        /// <summary>Tên layer của xe tăng (khai báo trong ProjectSettings/TagManager).</summary>
        public const string TankLayerName = "Tank";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            int tankLayer = LayerMask.NameToLayer(TankLayerName);
            if (tankLayer < 0) return;

            Physics2D.IgnoreLayerCollision(tankLayer, tankLayer, true);
        }
    }
}
