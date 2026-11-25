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

            float finalHp = (Stats != null && Stats.MaxHealth > 0f) ? Stats.MaxHealth : hp;

            EnemyStats pillarStats = new EnemyStats
            {
                MaxHealth = finalHp,
                Attack = 0f,
                Defense = 0f,
                MoveSpeed = 0f
            };

            Initialize(pillarStats);
            IsCrowdControlImmune = true;

            // Ràng buộc Collider: Đảm bảo có Collider2D và ColliderOwnerRef riêng trên Prefab, TUYỆT ĐỐI không trỏ về Boss Brain!
            if (gameObject.layer == 0) gameObject.layer = 7; // Layer Enemy nếu chưa đặt

            var col = GetComponent<Collider2D>();
            if (col == null)
            {
                Debug.LogError($"[D1onkEnergyPillar] {gameObject.name} thiếu Collider2D trên Prefab! Vui lòng cấu hình Collider trên Prefab thay vì code.", this);
            }

            var ownerRef = GetComponent<ColliderOwnerRef>();
            if (ownerRef == null)
            {
                Debug.LogError($"[D1onkEnergyPillar] {gameObject.name} thiếu ColliderOwnerRef trên Prefab! Vui lòng cấu hình trên Prefab thay vì code.", this);
            }
            else
            {
                ownerRef.SetOwner(this);
            }

            var sr = GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                Debug.LogError($"[D1onkEnergyPillar] {gameObject.name} thiếu SpriteRenderer trên Prefab! Vui lòng cấu hình SpriteRenderer trên Prefab thay vì code.", this);
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
                Destroy(this.gameObject);
            }
        }
    }
}
