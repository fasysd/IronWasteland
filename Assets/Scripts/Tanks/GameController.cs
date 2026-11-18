using UnityEngine;
using UnityEngine.InputSystem;
using IronWasteland.Menu; // Thêm namespace để đọc dữ liệu từ Main Menu

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Đọc input của Player, tạo TankBrain từ ScriptableObject và gửi lệnh cho nó.
    /// Hỗ trợ cả khởi tạo trực tiếp từ Inspector lẫn lấy dữ liệu từ GameSession (Main Menu).
    /// Điều khiển kỹ năng 3 giai đoạn:
    /// 1. Chọn target / hướng ngắm qua vị trí con trỏ chuột (SetLookTarget).
    /// 2. Chọn kỹ năng (hỗ trợ GameSettings hoặc phím 1, 2, 3, 4). Phím 1 dùng để hủy và quay lại kỹ năng thường.
    /// 3. Xác nhận thi triển bằng Click chuột trái (gửi lại LookTarget, gọi UseSkill, sau đó reset về kỹ năng thường 0).
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [Header("Tank Setup")]
        [SerializeField] private TankDefinition tankDefinition;

        [Tooltip("Level của Tank, thay đổi được theo ý thích Player. Chỉ đọc trong lúc khởi tạo.")]
        [SerializeField, Range(TankDefinition.MinLevel, TankDefinition.MaxLevel)] private int playerLevel = 1;

        [Tooltip("Index trong danh sách TankView của TankDefinition.")]
        [SerializeField] private int viewId;

        [SerializeField] private Vector2 spawnPosition = Vector2.zero;

        [Tooltip("ID phụ kiện trong EquipmentDatabase (-1 = không trang bị).")]
        [SerializeField] private int equipmentId = -1;

        [Header("Optional Skills Selection")]
        [Tooltip("Lựa chọn biến thể kỹ năng OptionSkill1 (0 = Cường hóa, 1 = Shadow Link, 2 = Shadow Guard).")]
        [SerializeField] private int optionSkill1 = 0;

        [Tooltip("Lựa chọn biến thể kỹ năng OptionSkill2.")]
        [SerializeField] private int optionSkill2 = 0;

        [Header("Look & Targeting")]
        [Tooltip("Camera để tính hướng nhìn. Nếu rỗng sẽ dùng Camera.main.")]
        [SerializeField] private Camera targetCamera;

        [Header("Skill Selection State")]
        [Tooltip("Kỹ năng hiện đang được chọn (0 = Kỹ năng thường, 1 = Chuyên dụng, 2 = Phụ 1, 3 = Phụ 2).")]
        [SerializeField] private int selectedSkillIndex = 0;

        private TankBrain m_PlayerTank;

        public TankBrain PlayerTank => m_PlayerTank;
        public int SelectedSkillIndex => selectedSkillIndex;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;

            // Mặc định lấy từ Inspector để test trực tiếp Scene
            TankDefinition definition = tankDefinition;
            var context = new TankBrainContext
            {
                Level = playerLevel,
                EquipmentId = equipmentId,
                OptionSkill1 = optionSkill1,
                OptionSkill2 = optionSkill2,
            };

            // Tích hợp từ Main Menu: Nếu có lựa chọn từ GameSession thì ghi đè lại
            if (GameSession.HasSelection && GameSession.Tank != null)
            {
                definition = GameSession.Tank;
                context = GameSession.Context;
            }

            // Khởi tạo 1 lần duy nhất (guard).
            if (m_PlayerTank == null && definition != null)
            {
                m_PlayerTank = definition.CreateTank(context);
            }

            if (m_PlayerTank == null)
            {
                Debug.LogError($"[{nameof(GameController)}] Không tạo được Tank. Kiểm tra TankDefinition.");
                return;
            }

            m_PlayerTank.transform.position = new Vector3(spawnPosition.x, spawnPosition.y, m_PlayerTank.transform.position.z);
        }

        private void Update()
        {
            if (m_PlayerTank == null) return;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            // --- Di chuyển: Hỗ trợ GameSettings kết hợp phím WASD / phím mũi tên truyền thống ---
            float x = 0f;
            float y = 0f;

            if (keyboard != null)
            {
                if (GameSettings.IsHeld(GameAction.MoveRight, keyboard) || keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
                if (GameSettings.IsHeld(GameAction.MoveLeft, keyboard) || keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
                if (GameSettings.IsHeld(GameAction.MoveUp, keyboard) || keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
                if (GameSettings.IsHeld(GameAction.MoveDown, keyboard) || keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
            }

            m_PlayerTank.SetMoveInput(Vector2.ClampMagnitude(new Vector2(x, y), 1f));

            // --- Giai đoạn 1: Target / Hướng ngắm (liên tục cập nhật theo con trỏ chuột) ---
            if (mouse != null && targetCamera != null)
            {
                Vector3 world = targetCamera.ScreenToWorldPoint(mouse.position.ReadValue());
                m_PlayerTank.SetLookTarget(new Vector2(world.x, world.y));
            }

            // --- Giai đoạn 2: Chọn kỹ năng (Hỗ trợ GameSettings HOẶC phím số 1, 2, 3, 4 / Numpad) ---
            // Phím 1 (hoặc action BasicAttack): Kỹ năng thường (hoặc dùng để hủy kỹ năng đang chọn)
            // Phím 2, 3, 4 (hoặc action tương ứng): Chọn kỹ năng (1, 2, 3)
            if (keyboard != null)
            {
                bool selectBasic = GameSettings.WasPressedThisFrame(GameAction.BasicAttack, keyboard) || keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame;
                bool selectSpecial = GameSettings.WasPressedThisFrame(GameAction.SpecialSkill, keyboard) || keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame;
                bool selectOpt1 = GameSettings.WasPressedThisFrame(GameAction.Option1, keyboard) || keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame;
                bool selectOpt2 = GameSettings.WasPressedThisFrame(GameAction.Option2, keyboard) || keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame;

                if (selectBasic)
                {
                    selectedSkillIndex = 0;
                }
                else if (selectSpecial)
                {
                    selectedSkillIndex = 1;
                }
                else if (selectOpt1)
                {
                    selectedSkillIndex = 2;
                }
                else if (selectOpt2)
                {
                    selectedSkillIndex = 3;
                }
            }

            // Chuột phải cũng hỗ trợ hủy lựa chọn kỹ năng để quay lại kỹ năng thường
            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                selectedSkillIndex = 0;
            }

            // --- Giai đoạn 3: Xác nhận thi triển bằng Click chuột trái ---
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                // Gửi lại vị trí trỏ chuột tại thời điểm click để đảm bảo chính xác
                if (targetCamera != null)
                {
                    Vector3 world = targetCamera.ScreenToWorldPoint(mouse.position.ReadValue());
                    m_PlayerTank.SetLookTarget(new Vector2(world.x, world.y));
                }

                // Thi triển kỹ năng đã chọn
                m_PlayerTank.UseSkill(selectedSkillIndex);

                // Sau khi click (dù thành công hay thất bại), chuyển kỹ năng được chọn thành kỹ năng thường
                selectedSkillIndex = 0;
            }
        }
    }
}