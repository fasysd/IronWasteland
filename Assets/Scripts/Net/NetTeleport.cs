using IronWasteland.Tanks;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Dịch chuyển tức thời HỢP LỆ (kỹ năng teleport / lướt / hoán đổi vị trí).
    /// Offline: đổi transform trực tiếp như trước.
    /// Trong phiên mạng: chỉ server được quyết định, vì NetworkTransform của xe do chủ xe
    /// giữ quyền ghi — server đổi vị trí trên bản sao của mình sẽ bị chủ xe ghi đè ở tick sau.
    /// Vì vậy server cập nhật vị trí, mở "cửa sổ miễn kiểm tra tốc độ" rồi gửi lệnh cho chủ xe
    /// áp đúng vị trí đó; chủ xe ghi lên NetworkTransform và mọi máy nhận được.
    /// </summary>
    public static class NetTeleport
    {
        /// <summary>Dịch chuyển xe tới vị trí world (giữ nguyên trục Z hiện tại).</summary>
        public static void Move(TankBrain tank, Vector3 position)
        {
            if (tank == null)
                return;

            if (!TankBrain.NetworkSessionActive)
            {
                ApplyLocal(tank, position);
                return;
            }

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
                return;

            NetPlayerController controller = tank.GetComponent<NetPlayerController>();
            if (controller != null && controller.IsSpawned)
            {
                controller.ServerTeleport(position);
                return;
            }

            // Xe chưa được quản lý mạng (ví dụ tank offline còn trong scene) — áp trực tiếp.
            ApplyLocal(tank, position);
        }

        /// <summary>Tạo hình chữ nhật bao quanh một vùng tròn để truyền cho StageBounds/CameraFollow2D.</summary>
        public static Rect CreateCircleAreaRect(Vector2 center, float radius)
        {
            float r = Mathf.Max(0.5f, radius);
            return new Rect(center.x - r, center.y - r, r * 2f, r * 2f);
        }

        /// <summary>
        /// Cập nhật vùng giới hạn StageBounds và CameraFollow2D cho đúng Tank bị dịch chuyển
        /// (Offline hoặc Co-op Host/Client sở hữu Tank đó).
        /// </summary>
        public static void SetTankAreaBounds(TankBrain tank, bool useOverride, Rect area)
        {
            if (tank == null)
                return;

            if (TankBrain.NetworkSessionActive)
            {
                NetPlayerController controller = tank.GetComponent<NetPlayerController>();
                if (controller != null && controller.IsSpawned)
                {
                    controller.ServerSetAreaBounds(useOverride, area);
                    return;
                }
            }

            if (useOverride)
            {
                IronWasteland.Core.StageBounds.ApplyOverrideBounds(area, snapCamera: true);
            }
            else
            {
                IronWasteland.Core.StageBounds.ResetToDefaultBounds(snapCamera: true);
            }
        }

        private static void ApplyLocal(TankBrain tank, Vector3 position)
        {
            Vector3 fixedPosition = new Vector3(position.x, position.y, tank.transform.position.z);
            tank.transform.position = fixedPosition;

            Rigidbody2D body = tank.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = fixedPosition;
                body.linearVelocity = Vector2.zero;
            }
        }
    }
}
