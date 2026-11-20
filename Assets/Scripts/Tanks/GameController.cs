using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Cấu hình khởi tạo cho từng xe Tank.
    /// </summary>
    [System.Serializable]
    public class TankSetupConfig
    {
        [Tooltip("Tên gợi nhớ cho Tank trong danh sách.")]
        [SerializeField] private string tankName = "Player Tank";

        [Tooltip("ScriptableObject định nghĩa xe tăng.")]
        [SerializeField] private TankDefinition tankDefinition;

        [Tooltip("Level của Tank (1 - 100).")]
        [SerializeField, Range(TankDefinition.MinLevel, TankDefinition.MaxLevel)] private int playerLevel = 1;

        [Tooltip("Vị trí spawn lúc bắt đầu.")]
        [SerializeField] private Vector2 spawnPosition = Vector2.zero;

        [Tooltip("ID phụ kiện trong EquipmentDatabase (-1 = không trang bị).")]
        [SerializeField] private int equipmentId = -1;

        [Tooltip("Lựa chọn biến thể kỹ năng OptionSkill1 (0 = Cường hóa, 1 = Shadow Link, 2 = Shadow Guard).")]
        [SerializeField] private int optionSkill1 = 0;

        [Tooltip("Lựa chọn biến thể kỹ năng OptionSkill2.")]
        [SerializeField] private int optionSkill2 = 0;

        public string TankName { get => tankName; set => tankName = value; }
        public TankDefinition TankDefinition { get => tankDefinition; set => tankDefinition = value; }
        public int PlayerLevel { get => playerLevel; set => playerLevel = value; }
        public Vector2 SpawnPosition { get => spawnPosition; set => spawnPosition = value; }
        public int EquipmentId { get => equipmentId; set => equipmentId = value; }
        public int OptionSkill1 { get => optionSkill1; set => optionSkill1 = value; }
        public int OptionSkill2 { get => optionSkill2; set => optionSkill2 = value; }

        public TankBrainContext ToContext()
        {
            return new TankBrainContext
            {
                Level = playerLevel,
                EquipmentId = equipmentId,
                OptionSkill1 = optionSkill1,
                OptionSkill2 = optionSkill2
            };
        }
    }

    /// <summary>
    /// Đọc input của Player, quản lý danh sách từ 1 đến 4 Tank.
    /// Chỉ điều khiển 1 tank tại một thời điểm theo chỉ số controlledTankIndex (0 - 3).
    /// Camera theo dõi xe tăng đang được điều khiển.
    /// 
    /// Điều khiển kỹ năng 3 giai đoạn:
    /// 1. Chọn target / hướng ngắm qua vị trí con trỏ chuột (SetLookTarget).
    /// 2. Chọn kỹ năng (phím 1, 2, 3, 4). Phím 1 dùng để hủy và quay lại kỹ năng thường.
    /// 3. Xác nhận thi triển bằng Click chuột trái.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [Header("Tanks Configuration (Min 1, Max 4)")]
        [Tooltip("Danh sách cấu hình tối thiểu 1, tối đa 4 tank.")]
        [SerializeField] private TankSetupConfig[] tankConfigs = new TankSetupConfig[1]
        {
            new TankSetupConfig()
        };

        [Header("Active Control Index")]
        [Tooltip("Chỉ số xe Tank đang được điều khiển trực tiếp (0 đến 3).")]
        [SerializeField, Range(0, 3)] private int controlledTankIndex = 0;

        [Header("Camera & Following")]
        [Tooltip("Camera để tính hướng nhìn và theo dõi Tank. Nếu rỗng sẽ dùng Camera.main.")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("Bật chế độ Camera bám theo Tank đang được điều khiển.")]
        [SerializeField] private bool followControlledTank = true;

        [Tooltip("Tốc độ bám mượt mà của Camera (0 = bám tức thì / snap).")]
        [SerializeField, Min(0f)] private float cameraFollowSmoothSpeed = 10f;

        [Header("Skill Selection State")]
        [Tooltip("Kỹ năng hiện đang được chọn của tank đang điều khiển (0 = Thường, 1 = Chuyên dụng, 2 = Phụ 1, 3 = Phụ 2).")]
        [SerializeField] private int selectedSkillIndex = 0;

        // Legacy fields for backward compatibility with existing scenes/prefabs
        [HideInInspector]
        [SerializeField] private TankDefinition tankDefinition;
        [HideInInspector]
        [SerializeField] private int playerLevel = 1;
        [HideInInspector]
        [SerializeField] private Vector2 spawnPosition = Vector2.zero;
        [HideInInspector]
        [SerializeField] private int equipmentId = -1;
        [HideInInspector]
        [SerializeField] private int optionSkill1 = 0;
        [HideInInspector]
        [SerializeField] private int optionSkill2 = 0;

        private readonly List<TankBrain> m_SpawnedTanks = new List<TankBrain>();
        private int m_PreviousControlledIndex = -1;

        public IReadOnlyList<TankBrain> SpawnedTanks => m_SpawnedTanks;
        public int ControlledTankIndex => controlledTankIndex;
        public int SelectedSkillIndex => selectedSkillIndex;

        /// <summary>
        /// Tank hiện đang được người chơi điều khiển.
        /// </summary>
        public TankBrain ControlledTank
        {
            get
            {
                if (m_SpawnedTanks.Count == 0) return null;
                int validIndex = Mathf.Clamp(controlledTankIndex, 0, m_SpawnedTanks.Count - 1);
                return m_SpawnedTanks[validIndex];
            }
        }

        /// <summary>
        /// Tương thích ngược: Trả về Tank đang được điều khiển.
        /// </summary>
        public TankBrain PlayerTank => ControlledTank;

        private void OnValidate()
        {
            // Đảm bảo tankConfigs tối thiểu 1, tối đa 4
            if (tankConfigs == null || tankConfigs.Length == 0)
            {
                tankConfigs = new TankSetupConfig[1] { new TankSetupConfig() };
            }
            else if (tankConfigs.Length > 4)
            {
                Array.Resize(ref tankConfigs, 4);
            }

            controlledTankIndex = Mathf.Clamp(controlledTankIndex, 0, Mathf.Max(0, tankConfigs.Length - 1));
        }

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;

            MigrateLegacyFieldsIfNecessary();
            SpawnAllTanks();
        }

        private void Start()
        {
            ApplyControlledTankSwitch(controlledTankIndex, snapCamera: true);
        }

        /// <summary>
        /// Chuyển đổi dữ liệu cũ nếu scene hoặc test chưa thiết lập mảng tankConfigs.
        /// </summary>
        private void MigrateLegacyFieldsIfNecessary()
        {
            if ((tankConfigs == null || tankConfigs.Length == 0 || (tankConfigs.Length == 1 && tankConfigs[0].TankDefinition == null))
                && tankDefinition != null)
            {
                tankConfigs = new TankSetupConfig[1]
                {
                    new TankSetupConfig
                    {
                        TankName = tankDefinition.name,
                        TankDefinition = tankDefinition,
                        PlayerLevel = playerLevel,
                        SpawnPosition = spawnPosition,
                        EquipmentId = equipmentId,
                        OptionSkill1 = optionSkill1,
                        OptionSkill2 = optionSkill2
                    }
                };
            }
        }

        /// <summary>
        /// Khởi tạo các Tank theo danh sách cấu hình.
        /// </summary>
        public void SpawnAllTanks()
        {
            m_SpawnedTanks.Clear();

            if (tankConfigs == null || tankConfigs.Length == 0)
            {
                Debug.LogWarning($"[{nameof(GameController)}] Không có cấu hình Tank nào được định nghĩa!");
                return;
            }

            int spawnCount = Mathf.Clamp(tankConfigs.Length, 1, 4);
            for (int i = 0; i < spawnCount; i++)
            {
                TankSetupConfig cfg = tankConfigs[i];
                if (cfg == null || cfg.TankDefinition == null)
                {
                    Debug.LogWarning($"[{nameof(GameController)}] Cấu hình Tank tại vị trí index {i} không có TankDefinition hợp lệ.");
                    continue;
                }

                TankBrainContext ctx = cfg.ToContext();
                TankBrain tank = cfg.TankDefinition.CreateTank(ctx);
                if (tank != null)
                {
                    tank.transform.position = new Vector3(cfg.SpawnPosition.x, cfg.SpawnPosition.y, tank.transform.position.z);
                    tank.name = $"{cfg.TankName} (Index {i})";
                    m_SpawnedTanks.Add(tank);
                }
                else
                {
                    Debug.LogError($"[{nameof(GameController)}] Tạo Tank index {i} thất bại!");
                }
            }

            if (m_SpawnedTanks.Count == 0)
            {
                Debug.LogError($"[{nameof(GameController)}] Không có xe Tank nào được khởi tạo thành công.");
            }
        }

        /// <summary>
        /// Thay đổi xe Tank được điều khiển theo index (0 đến 3).
        /// </summary>
        public void SwitchControlledTank(int newIndex, bool snapCamera = false)
        {
            if (m_SpawnedTanks.Count == 0) return;

            int clampedIndex = Mathf.Clamp(newIndex, 0, m_SpawnedTanks.Count - 1);
            if (m_PreviousControlledIndex >= 0 && m_PreviousControlledIndex < m_SpawnedTanks.Count)
            {
                // Dừng di chuyển tank cũ
                TankBrain oldTank = m_SpawnedTanks[m_PreviousControlledIndex];
                if (oldTank != null)
                {
                    oldTank.SetMoveInput(Vector2.zero);
                }
            }

            controlledTankIndex = clampedIndex;
            m_PreviousControlledIndex = clampedIndex;
            selectedSkillIndex = 0;

            if (snapCamera && targetCamera != null && ControlledTank != null)
            {
                Vector3 tPos = ControlledTank.transform.position;
                targetCamera.transform.position = new Vector3(tPos.x, tPos.y, targetCamera.transform.position.z);
            }
        }

        private void ApplyControlledTankSwitch(int newIndex, bool snapCamera = false)
        {
            SwitchControlledTank(newIndex, snapCamera);
        }

        private void Update()
        {
            if (m_SpawnedTanks.Count == 0) return;

            // Kiểm tra nếu controlledTankIndex bị thay đổi trên Inspector
            int validIndex = Mathf.Clamp(controlledTankIndex, 0, m_SpawnedTanks.Count - 1);
            if (validIndex != m_PreviousControlledIndex)
            {
                ApplyControlledTankSwitch(validIndex, snapCamera: false);
            }

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            // Hỗ trợ chuyển đổi Tank bằng phím số (F5-F8 hoặc NumPad 5-8 để không đè lên phím kỹ năng 1-4)
            HandleTankSwitchingInput(keyboard);

            TankBrain currentTank = ControlledTank;
            if (currentTank == null) return;

            // --- Di chuyển: WASD / phím mũi tên chỉ truyền cho Tank đang active ---
            float x = 0f;
            float y = 0f;

            if (keyboard != null)
            {
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
            }

            currentTank.SetMoveInput(Vector2.ClampMagnitude(new Vector2(x, y), 1f));

            // Đảm bảo tất cả các xe không được điều khiển dừng di chuyển
            for (int i = 0; i < m_SpawnedTanks.Count; i++)
            {
                if (i != validIndex && m_SpawnedTanks[i] != null)
                {
                    m_SpawnedTanks[i].SetMoveInput(Vector2.zero);
                }
            }

            // --- Giai đoạn 1: Target / Hướng ngắm (liên tục cập nhật theo con trỏ chuột) ---
            if (mouse != null && targetCamera != null)
            {
                Vector3 world = targetCamera.ScreenToWorldPoint(mouse.position.ReadValue());
                currentTank.SetLookTarget(new Vector2(world.x, world.y));
            }

            // --- Giai đoạn 2: Chọn kỹ năng (Phím 1, 2, 3, 4) ---
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                {
                    selectedSkillIndex = 0;
                }
                else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                {
                    selectedSkillIndex = 1;
                }
                else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                {
                    selectedSkillIndex = 2;
                }
                else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame)
                {
                    selectedSkillIndex = 3;
                }
            }

            // Chuột phải hủy lựa chọn kỹ năng để quay lại kỹ năng thường
            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                selectedSkillIndex = 0;
            }

            // --- Giai đoạn 3: Xác nhận thi triển bằng Click chuột trái ---
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                if (targetCamera != null)
                {
                    Vector3 world = targetCamera.ScreenToWorldPoint(mouse.position.ReadValue());
                    currentTank.SetLookTarget(new Vector2(world.x, world.y));
                }

                currentTank.UseSkill(selectedSkillIndex);
                selectedSkillIndex = 0;
            }
        }

        private void LateUpdate()
        {
            UpdateCameraFollow();
        }

        /// <summary>
        /// Camera bám theo vị trí của xe Tank đang được điều khiển.
        /// </summary>
        private void UpdateCameraFollow()
        {
            if (!followControlledTank || targetCamera == null) return;

            TankBrain currentTank = ControlledTank;
            if (currentTank == null) return;

            Vector3 targetPos = currentTank.transform.position;
            Vector3 currentCamPos = targetCamera.transform.position;
            Vector3 desiredPos = new Vector3(targetPos.x, targetPos.y, currentCamPos.z);

            if (cameraFollowSmoothSpeed <= 0f)
            {
                targetCamera.transform.position = desiredPos;
            }
            else
            {
                targetCamera.transform.position = Vector3.Lerp(currentCamPos, desiredPos, Time.deltaTime * cameraFollowSmoothSpeed);
            }
        }

        /// <summary>
        /// Hỗ trợ phím nóng tiện ích để chuyển đổi tank nhanh:
        /// - Tab: Chuyển sang tank tiếp theo (vòng tròn).
        /// - Phím F1 - F4 hoặc Numpad 5 - 8.
        /// </summary>
        private void HandleTankSwitchingInput(Keyboard keyboard)
        {
            if (keyboard == null || m_SpawnedTanks.Count <= 1) return;

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                int nextIndex = (controlledTankIndex + 1) % m_SpawnedTanks.Count;
                SwitchControlledTank(nextIndex);
                return;
            }

            if (keyboard.f1Key.wasPressedThisFrame && m_SpawnedTanks.Count > 0) SwitchControlledTank(0);
            else if (keyboard.f2Key.wasPressedThisFrame && m_SpawnedTanks.Count > 1) SwitchControlledTank(1);
            else if (keyboard.f3Key.wasPressedThisFrame && m_SpawnedTanks.Count > 2) SwitchControlledTank(2);
            else if (keyboard.f4Key.wasPressedThisFrame && m_SpawnedTanks.Count > 3) SwitchControlledTank(3);
        }
    }
}
