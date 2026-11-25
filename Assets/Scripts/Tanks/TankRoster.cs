using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Danh sách xe hiển thị trong Garage, giữ đúng thứ tự người chơi thấy từ trên xuống.
    /// Thêm xe chỉ cần kéo thả TankDefinition vào asset này, không phải sửa code.
    /// </summary>
    [CreateAssetMenu(menuName = "IronWasteland/Tank/Tank Roster", fileName = "TankRoster")]
    public class TankRoster : ScriptableObject
    {
        [Tooltip("Các xe trong Garage, theo đúng thứ tự hiển thị.")]
        [SerializeField] private TankDefinition[] tanks = new TankDefinition[0];

        /// <summary>Các xe theo thứ tự hiển thị (bỏ qua phần tử null nếu có ô trống).</summary>
        public IReadOnlyList<TankDefinition> Tanks => tanks;

        /// <summary>Số xe hợp lệ trong roster.</summary>
        public int Count
        {
            get
            {
                if (tanks == null) return 0;

                int count = 0;
                for (int i = 0; i < tanks.Length; i++)
                {
                    if (tanks[i] != null) count++;
                }

                return count;
            }
        }
    }
}
