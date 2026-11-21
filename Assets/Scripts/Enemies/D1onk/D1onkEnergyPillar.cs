using System;
using IronWasteland.Core;
using IronWasteland.Utilities;
using UnityEngine;

namespace IronWasteland.Enemies.D1onk
{
    public enum D1onkPillarColor
    {
        White,
        Red,
        Yellow,
        Blue
    }

    /// <summary>
    /// Cột năng lượng trong buồng lái chiến hạm D1onk.
    /// Có 4 cột ở 4 góc: Trắng (White), Đỏ (Red), Vàng (Yellow), Xanh (Blue).
    /// Mỗi cột có 500 HP và có thể bị người chơi bên trong buồng lái tấn công tiêu diệt.
    /// 
    /// RÀNG BUỘC COLLIDER:
    /// Tuyệt đối không gắn ColliderOwnerRef trỏ về Boss Brain trên bất kỳ cột nào!
    /// </summary>
    [DisallowMultipleComponent]
    public class D1onkEnergyPillar : EnemyBrain
    {
        [Header("Pillar Identity")]
        [Tooltip("Màu sắc của cột năng lượng (Trắng / Đỏ / Vàng / Xanh).")]
        [SerializeField] private D1onkPillarColor pillarColor = D1onkPillarColor.White;

        private D1onkMothershipSubArena m_Arena;

        public D1onkPillarColor PillarColor
        {
            get => pillarColor;
            set => pillarColor = value;
        }

        public D1onkMothershipSubArena Arena => m_Arena;

        public void InitializePillar(D1onkPillarColor color, D1onkMothershipSubArena arena, float hp = 500f)
        {
            pillarColor = color;
            m_Arena = arena;

            EnemyStats pillarStats = new EnemyStats
            {
                MaxHealth = hp,
                Attack = 0f,
                Defense = 0f,
                MoveSpeed = 0f
            };

            Initialize(pillarStats);
            IsCrowdControlImmune = true;

            // Ràng buộc Collider: Đảm bảo có Collider2D và ColliderOwnerRef riêng, TUYỆT ĐỐI không trỏ về Boss Brain!
            gameObject.layer = 7; // Layer Enemy

            var col = GetComponent<CircleCollider2D>();
            if (col == null)
            {
                col = gameObject.AddComponent<CircleCollider2D>();
                col.radius = 0.8f;
            }

            var ownerRef = GetComponent<ColliderOwnerRef>();
            if (ownerRef == null)
            {
                ownerRef = gameObject.AddComponent<ColliderOwnerRef>();
            }
            ownerRef.SetOwner(this);

            var sr = GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Circle;
                sr.color = GetColorForPillar(color);
                sr.transform.localScale = Vector3.one * 1.6f;
                sr.sortingOrder = 5;
            }
        }

        private static Color GetColorForPillar(D1onkPillarColor color)
        {
            switch (color)
            {
                case D1onkPillarColor.White: return Color.white;
                case D1onkPillarColor.Red: return new Color(1f, 0.2f, 0.2f, 1f);
                case D1onkPillarColor.Yellow: return new Color(1f, 0.9f, 0.1f, 1f);
                case D1onkPillarColor.Blue: return new Color(0.2f, 0.6f, 1f, 1f);
                default: return Color.white;
            }
        }

        protected override void OnBrainTick(float deltaTime)
        {
        }

        protected override void OnDeath()
        {
            if (m_Arena != null)
            {
                m_Arena.OnPillarDestroyed(this);
            }
        }
    }
}
