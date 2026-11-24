using IronWasteland.Net;
using IronWasteland.Save;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Đọc input của Player, tạo TankBrain từ ScriptableObject và gửi lệnh cho nó.
    /// Điều khiển kỹ năng 3 giai đoạn:
    /// 1. Chọn target / hướng ngắm qua vị trí con trỏ chuột (SetLookTarget).
    /// 2. Chọn kỹ năng (phím 1, 2, 3, 4). Phím 1 dùng để hủy và quay lại kỹ năng thường.
    /// 3. Xác nhận thi triển bằng Click chuột trái (gửi lại LookTarget, gọi UseSkill, sau đó reset về kỹ năng thường 0).
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [Header("Tank Setup")]
        [SerializeField] private TankDefinition tankDefinition;

        [Tooltip("Tự tạo tank khi Awake. Tắt khi chơi online: host spawn xe qua mạng rồi gọi BindTank.")]
        [SerializeField] private bool createLocalTankOnAwake = true;

        [Tooltip("Level của Tank, thay đổi được theo ý thích Player. Chỉ đọc trong lúc khởi tạo.")]
        [SerializeField, Range(TankDefinition.MinLevel, TankDefinition.MaxLevel)] private int playerLevel = 1;

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
        private NetPlayerController m_NetController;

        public TankBrain PlayerTank => m_PlayerTank;
        public int SelectedSkillIndex => selectedSkillIndex;
        public TankDefinition TankDefinition => m_PlayerTank != null && m_PlayerTank.Definition != null ? m_PlayerTank.Definition : tankDefinition;

        /// <summary>Cấp của xe đang chơi (1..90), HUD hiện cạnh tên xe.</summary>
        public int PlayerLevel => m_PlayerTank != null ? m_PlayerTank.Level : playerLevel;

        /// <summary>Vị trí khởi tạo xe trong màn.</summary>
        public Vector2 SpawnPosition => spawnPosition;

        /// <summary>Gán xe do mạng spawn cho controller này (dùng khi online).</summary>
        public void BindTank(TankBrain tank)
        {
            m_PlayerTank = tank;
            m_NetController = tank != null ? tank.GetComponent<NetPlayerController>() : null;
            if (tank != null)
            {
                if (tank.Definition != null) tankDefinition = tank.Definition;
                playerLevel = tank.Level;
                equipmentId = tank.Context.EquipmentId;
                optionSkill1 = tank.Context.OptionSkill1;
                optionSkill2 = tank.Context.OptionSkill2;
            }
        }

        /// <summary>Bỏ liên kết với xe hiện tại mà không huỷ đối tượng.</summary>
        public void UnbindTank()
        {
            m_PlayerTank = null;
            m_NetController = null;
        }

        /// <summary>
        /// Huỷ xe offline đã tạo trong Awake trước khi vào phòng mạng.
        /// Ưu tiên Despawn nếu đối tượng đã được spawn bởi NGO.
        /// </summary>
        public void DestroyLocalTank()
        {
            if (m_PlayerTank == null) return;

            NetworkObject networkObject = m_PlayerTank.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned) networkObject.Despawn(true);
            else Destroy(m_PlayerTank.gameObject);

            m_PlayerTank = null;
            m_NetController = null;
        }

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;

            // Chế độ online: host spawn xe qua mạng, GameController chỉ nhận bind sau đó.
            if (!createLocalTankOnAwake || TankBrain.NetworkSessionActive) return;

            // Đồng bộ dữ liệu xe từ SaveSystem nếu có
            if (SaveSystem.Instance != null)
            {
                int selectedIndex = SaveSystem.Instance.LastSelectedTankIndex;
                TankDefinition def = TankRegistry.GetDefinition(selectedIndex);
                if (def != null)
                {
                    tankDefinition = def;
                }

                int level = SaveSystem.Instance.GetTankLevel(selectedIndex);
                if (level > 0) playerLevel = level;
                equipmentId = SaveSystem.Instance.GetEquippedAccessory(selectedIndex);
                optionSkill1 = SaveSystem.Instance.GetOptionSkill(selectedIndex, 0);
                optionSkill2 = SaveSystem.Instance.GetOptionSkill(selectedIndex, 1);
            }

            // Xây TankBrainContext từ fields hiện có (level, equipmentId, optionSkills).
            var context = new TankBrainContext
            {
                Level = playerLevel,
                EquipmentId = equipmentId,
                OptionSkill1 = optionSkill1,
                OptionSkill2 = optionSkill2
            };

            // Khởi tạo 1 lần duy nhất (guard).
            if (m_PlayerTank == null && tankDefinition != null)
            {
                m_PlayerTank = tankDefinition.CreateTank(context);
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

            // --- Di chuyển: WASD / phím mũi tên ---
            float x = 0f;
            float y = 0f;

            if (keyboard != null)
            {
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
            }

            m_PlayerTank.SetMoveInput(Vector2.ClampMagnitude(new Vector2(x, y), 1f));

            // --- Giai đoạn 1: Target / Hướng ngắm (liên tục cập nhật theo con trỏ chuột) ---
            if (mouse != null && targetCamera != null)
            {
                Vector3 world = targetCamera.ScreenToWorldPoint(mouse.position.ReadValue());
                m_PlayerTank.SetLookTarget(new Vector2(world.x, world.y));
            }

            // --- Giai đoạn 2: Thi triển kỹ năng bằng phím (theo tài liệu thiết kế) ---
            // J = kỹ năng thường (slot 0 - đánh trúng hồi năng lượng)
            // K = kỹ năng chuyên biệt (slot 1)
            // L = kỹ năng tuỳ chọn 1 (slot 2)
            // I = kỹ năng tuỳ chọn 2 (slot 3)
            // Chuột chỉ để ngắm, không bắn - đúng danh sách phím trong màn Cài đặt của tài liệu.
            if (keyboard != null)
            {
                if (keyboard.jKey.wasPressedThisFrame) CastSkill(0);
                else if (keyboard.kKey.wasPressedThisFrame) CastSkill(1);
                else if (keyboard.lKey.wasPressedThisFrame) CastSkill(2);
                else if (keyboard.iKey.wasPressedThisFrame) CastSkill(3);
            }
        }

        /// <summary>
        /// Ngắm lại theo con trỏ chuột rồi thi triển ngay kỹ năng ở slot tương ứng.
        /// Dùng cho phím J/K/L/I.
        /// </summary>
        private void CastSkill(int index)
        {
            if (m_PlayerTank == null) return;

            Vector2 aim = m_PlayerTank.transform.position;
            Mouse mouse = Mouse.current;
            if (mouse != null && targetCamera != null)
            {
                Vector3 world = targetCamera.ScreenToWorldPoint(mouse.position.ReadValue());
                aim = new Vector2(world.x, world.y);
                m_PlayerTank.SetLookTarget(aim);
            }

            selectedSkillIndex = index;

            // Thi triển kỹ năng đã chọn: trong phòng mạng, client phải xin host;
            // host và chế độ offline gọi thẳng.
            if (m_NetController != null && m_NetController.IsSpawned && !m_NetController.IsServer)
            {
                m_NetController.RequestCast(index, aim);
            }
            else
            {
                m_PlayerTank.UseSkill(index);
            }
        }
    }
}
