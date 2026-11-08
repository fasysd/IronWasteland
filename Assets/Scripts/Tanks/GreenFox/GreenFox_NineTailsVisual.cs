using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    /// <summary>
    /// HÌNH ẢNH 9 đuôi của Nine-Tailed Assault (2C). Không có chức năng gì khác ngoài hiển thị.
    /// Cách dùng: tạo prefab chứa 9 chiếc đuôi (tự đặt vị trí từng đuôi trong prefab), gắn script này lên object gốc,
    /// rồi kéo prefab vào ô "Nine Tails Visual Prefab" của TankBrain_GreenFox.
    /// Khi 2C kích hoạt, prefab được gắn vào xe (mặc định ngay giữa xe, đi theo vị trí xe) và tự biến mất khi hết thời gian.
    /// Không gắn sẵn script này cũng được: TankBrain sẽ tự thêm với giá trị mặc định (giữa xe, không xoay).
    /// </summary>
    [DisallowMultipleComponent]
    public class GreenFox_NineTailsVisual : MonoBehaviour
    {
        [Tooltip("Dịch cả cụm đuôi so với tâm xe (X, Y). Mặc định (0,0) = ngay giữa xe.")]
        [SerializeField] private Vector2 offset = Vector2.zero;

        [Tooltip("Bật: cụm đuôi xoay theo thân xe (luôn ở phía sau khi xe đổi hướng). Tắt: chỉ đi theo vị trí xe.")]
        [SerializeField] private bool followBodyRotation = false;

        private TankBrain m_Owner;

        public void Initialize(TankBrain owner, float duration)
        {
            if (owner == null)
            {
                Destroy(gameObject);
                if (gameObject != null) Destroy(gameObject);
                return;
            }

            m_Owner = owner;

            // Gắn parent vào xe nhưng KHÔNG dùng localPosition cố định nữa
            transform.SetParent(owner.transform);

            // Nếu bạn muốn nó đi theo hướng xoay của thân xe (BodyPivot), 
            // hãy cập nhật vị trí dựa trên hướng nhìn của xe ngay từ đầu (hoặc trong Update/LateUpdate)
            UpdatePositionAndRotation();

            Destroy(gameObject, duration);
        }

        private void LateUpdate()
        {
            if (m_Owner == null) return;

            UpdatePositionAndRotation();
        }

        private void UpdatePositionAndRotation()
        {
            Transform referenceTransform = (m_Owner.View != null && m_Owner.View.BodyPivot != null)
                ? m_Owner.View.BodyPivot
                : m_Owner.transform;

            if (followBodyRotation)
            {
                // 1. Nếu bật xoay theo xe: Vừa xoay góc, vừa dịch chuyển offset theo hướng xe đang quay
                // Transform.TransformPoint sẽ dịch điểm offset(-1, 0) từ hệ tọa độ của xe sang hệ tọa độ Thế giới (World Space) dựa theo góc quay hiện tại của xe
                transform.position = referenceTransform.TransformPoint(new Vector3(offset.x, offset.y, 0f));
                transform.rotation = referenceTransform.rotation;
            }
            else
            {
                // 2. Nếu tắt xoay theo xe (chỉ đi theo vị trí): Đặt vị trí cục bộ thuần túy
                transform.localPosition = new Vector3(offset.x, offset.y, 0f);
            }
        }
    }
}
