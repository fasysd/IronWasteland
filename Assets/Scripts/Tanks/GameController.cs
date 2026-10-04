using UnityEngine;
using UnityEngine.InputSystem;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Doc input cua Player, tao TankBrain tu ScriptableObject va gui lenh cho no.
    /// INPUT TAM THOI: check truc tiep Keyboard/Mouse trong Update bang if/else.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [Header("Tank Setup")]
        [SerializeField] private TankBrainFactory tankFactory;

        [Tooltip("Level cua Tank, thay doi duoc theo y thich Player.")]
        [SerializeField, Range(TankBrain.MinLevel, TankBrain.MaxLevel)] private int playerLevel = 1;

        [Tooltip("Index trong danh sach Model cua TankBrainFactory.")]
        [SerializeField] private int modelId;

        [SerializeField] private Vector2 spawnPosition = Vector2.zero;

        [Header("Look")]
        [Tooltip("Camera de tinh huong nhin. Neu rong se dung Camera.main.")]
        [SerializeField] private Camera targetCamera;

        private TankBrain m_PlayerTank;

        public TankBrain PlayerTank => m_PlayerTank;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;

            m_PlayerTank = tankFactory != null
                ? tankFactory.CreateTank(playerLevel, modelId, spawnPosition)
                : null;

            if (m_PlayerTank == null)
            {
                Debug.LogError($"[{nameof(GameController)}] Khong tao duoc Tank. Kiem tra TankBrainFactory.");
            }
        }

        // === INPUT TAM THOI: chi if/else trong Update ===
        private void Update()
        {
            if (m_PlayerTank == null) return;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            // --- Di chuyen: WASD / phim mui ten ---
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

            // --- Look: noi sung luon nhin vao con tro chuot ---
            if (mouse != null && targetCamera != null)
            {
                Vector3 world = targetCamera.ScreenToWorldPoint(mouse.position.ReadValue());
                m_PlayerTank.SetLookTarget(new Vector2(world.x, world.y));
            }

            // --- 4 ky nang: J / K / L / I, chi kich hoat khi nha tay ---
            if (keyboard != null)
            {
                if (keyboard.jKey.wasReleasedThisFrame)
                    m_PlayerTank.UseSkill(0);
                else if (keyboard.kKey.wasReleasedThisFrame)
                    m_PlayerTank.UseSkill(1);
                else if (keyboard.lKey.wasReleasedThisFrame)
                    m_PlayerTank.UseSkill(2);
                else if (keyboard.iKey.wasReleasedThisFrame)
                    m_PlayerTank.UseSkill(3);
            }
        }
    }
}