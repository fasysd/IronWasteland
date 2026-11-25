using System.Collections;
using UnityEngine;

namespace IronWasteland.Enemies.DeadlandPhantom
{
    public enum CardinalDirection
    {
        East = 0,   // 0 deg (Right)
        North = 1,  // 90 deg (Up)
        West = 2,   // 180 deg (Left)
        South = 3   // 270 deg (Down)
    }

    /// <summary>
    /// VFX 5: Vòm Khiên Tứ Hướng (CardinalShield).
    /// Vòm năng lượng bao quanh Boss chia 4 góc với 1 góc mở phát sáng xanh ngọc (#00E5FF)
    /// và mũi tên chỉ hướng hở, kèm hiệu ứng phát sáng phản đòn khi bị bắn trúng góc đóng.
    /// </summary>
    [DisallowMultipleComponent]
    public class CardinalShieldVFX : MonoBehaviour
    {
        [Header("Quadrants (4 Cung Khiên)")]
        [Tooltip("Cung Đông (0 độ - Phải)")]
        [SerializeField] private SpriteRenderer quadrantEast;
        [Tooltip("Cung Bắc (90 độ - Trên)")]
        [SerializeField] private SpriteRenderer quadrantNorth;
        [Tooltip("Cung Tây (180 độ - Trái)")]
        [SerializeField] private SpriteRenderer quadrantWest;
        [Tooltip("Cung Nam (270 độ - Dưới)")]
        [SerializeField] private SpriteRenderer quadrantSouth;

        [Header("Open Direction Indicator")]
        [Tooltip("Mũi tên chỉ hướng hở.")]
        [SerializeField] private Transform indicatorArrow;
        [SerializeField] private SpriteRenderer arrowSprite;
        [SerializeField, Min(0.5f)] private float arrowDistance = 2.4f;

        [Header("Deflection Flash Effect")]
        [SerializeField] private GameObject reflectFlashPrefab;

        [Header("Shield Styling & Colors")]
        [SerializeField] private Color closedColor = new Color(0.29f, 0.078f, 0.549f, 0.45f); // #4A148C tím hư không
        [SerializeField] private Color openColor = new Color(0f, 0.898f, 1f, 0.95f);          // #00E5FF xanh ngọc sáng
        [SerializeField] private Color reflectFlashColor = new Color(1f, 0.2f, 0.4f, 1f);     // Đỏ phản đòn
        [SerializeField, Min(0.1f)] private float pulseSpeed = 4f;

        private CardinalDirection m_CurrentOpenDir = CardinalDirection.North;
        private float m_Age;
        private Coroutine m_FlashRoutine;

        public CardinalDirection CurrentOpenDirection => m_CurrentOpenDir;

        private void Start()
        {
            UpdateShieldVisuals();
        }

        private void Update()
        {
            m_Age += Time.deltaTime;

            // Hover and pulse indicator arrow
            if (indicatorArrow != null)
            {
                float targetAngle = GetAngleFromDirection(m_CurrentOpenDir);
                indicatorArrow.localRotation = Quaternion.Euler(0f, 0f, targetAngle - 90f); // texture points up

                float rad = targetAngle * Mathf.Deg2Rad;
                float hover = arrowDistance + 0.15f * Mathf.Sin(m_Age * pulseSpeed * 1.5f);
                indicatorArrow.localPosition = new Vector3(Mathf.Cos(rad) * hover, Mathf.Sin(rad) * hover, 0f);

                if (arrowSprite != null)
                {
                    float pulseAlpha = 0.8f + 0.2f * Mathf.Sin(m_Age * pulseSpeed * 2f);
                    Color c = openColor;
                    c.a = pulseAlpha;
                    arrowSprite.color = c;
                }
            }

            // Pulse the open quadrant
            SpriteRenderer openSr = GetQuadrantRenderer(m_CurrentOpenDir);
            if (openSr != null)
            {
                float alpha = 0.75f + 0.25f * Mathf.Sin(m_Age * pulseSpeed);
                Color c = openColor;
                c.a = alpha;
                openSr.color = c;
            }
        }

        /// <summary>Chuyển hướng mở của khiên.</summary>
        public void SetOpenDirection(CardinalDirection dir)
        {
            m_CurrentOpenDir = dir;
            UpdateShieldVisuals();
        }

        public void SetOpenDirection(int dirIndex)
        {
            SetOpenDirection((CardinalDirection)(Mathf.Clamp(dirIndex, 0, 3)));
        }

        /// <summary>
        /// Xác định cung hướng (East, North, West, South) từ vector hướng tương đối.
        /// </summary>
        public static CardinalDirection GetDirectionFromVector(Vector2 relativeDir)
        {
            float angle = Mathf.Atan2(relativeDir.y, relativeDir.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            if (angle >= 45f && angle < 135f) return CardinalDirection.North;
            if (angle >= 135f && angle < 225f) return CardinalDirection.West;
            if (angle >= 225f && angle < 315f) return CardinalDirection.South;
            return CardinalDirection.East;
        }

        /// <summary>
        /// Kiểm tra vị trí của đòn tấn công có trúng vào hướng đang mở của khiên hay không.
        /// </summary>
        public bool IsDirectionOpen(Vector2 worldAttackerPos)
        {
            Vector2 relativeDir = (worldAttackerPos - (Vector2)transform.position).normalized;
            return GetDirectionFromVector(relativeDir) == m_CurrentOpenDir;
        }

        /// <summary>
        /// Xử lý khi có đòn tấn công va vào khiên.
        /// Trả về TRUE nếu bị cản (góc đóng) và phát sáng phản đòn.
        /// Trả về FALSE nếu đòn đánh xuyên qua góc mở an toàn.
        /// </summary>
        public bool HandleIncomingAttack(Vector2 hitPoint, Vector2 attackDirection)
        {
            // Hướng tấn công từ ngoài vào tâm Boss (ngược attackDirection hoặc từ hitPoint)
            Vector2 incomingDir = attackDirection.sqrMagnitude > 0.001f
                ? -attackDirection.normalized
                : (hitPoint - (Vector2)transform.position).normalized;

            CardinalDirection hitQuadrant = GetDirectionFromVector(incomingDir);
            bool isBlocked = hitQuadrant != m_CurrentOpenDir;

            if (isBlocked)
            {
                PlayReflectFlash(hitPoint);
            }
            return isBlocked;
        }

        /// <summary>Phát hiệu ứng phản đòn tại điểm va chạm khi bị bắn vào góc đóng.</summary>
        public void PlayReflectFlash(Vector2 hitPoint)
        {
            if (reflectFlashPrefab != null)
            {
                Instantiate(reflectFlashPrefab, hitPoint, Quaternion.identity);
            }

            if (m_FlashRoutine != null) StopCoroutine(m_FlashRoutine);
            m_FlashRoutine = StartCoroutine(FlashClosedQuadrantsRoutine());
        }

        private IEnumerator FlashClosedQuadrantsRoutine()
        {
            float flashDuration = 0.18f;
            float elapsed = 0f;

            while (elapsed < flashDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / flashDuration;
                Color flash = Color.Lerp(reflectFlashColor, closedColor, t);

                for (int i = 0; i < 4; i++)
                {
                    if ((CardinalDirection)i != m_CurrentOpenDir)
                    {
                        SpriteRenderer sr = GetQuadrantRenderer((CardinalDirection)i);
                        if (sr != null) sr.color = flash;
                    }
                }
                yield return null;
            }

            UpdateShieldVisuals();
        }

        private void UpdateShieldVisuals()
        {
            for (int i = 0; i < 4; i++)
            {
                CardinalDirection dir = (CardinalDirection)i;
                SpriteRenderer sr = GetQuadrantRenderer(dir);
                if (sr == null) continue;

                if (dir == m_CurrentOpenDir)
                {
                    sr.color = openColor;
                    sr.sortingOrder = 7;
                }
                else
                {
                    sr.color = closedColor;
                    sr.sortingOrder = 5;
                }
            }
        }

        private SpriteRenderer GetQuadrantRenderer(CardinalDirection dir)
        {
            switch (dir)
            {
                case CardinalDirection.East: return quadrantEast;
                case CardinalDirection.North: return quadrantNorth;
                case CardinalDirection.West: return quadrantWest;
                case CardinalDirection.South: return quadrantSouth;
                default: return quadrantNorth;
            }
        }

        private static float GetAngleFromDirection(CardinalDirection dir)
        {
            switch (dir)
            {
                case CardinalDirection.East: return 0f;
                case CardinalDirection.North: return 90f;
                case CardinalDirection.West: return 180f;
                case CardinalDirection.South: return 270f;
                default: return 90f;
            }
        }
    }
}
