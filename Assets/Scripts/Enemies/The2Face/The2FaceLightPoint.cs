using System.Collections.Generic;
using IronWasteland.Tanks;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Enemies.The2Face
{
    /// <summary>
    /// Điểm Sáng (LightPointTotem) của Boss The2Face.
    /// - Có máu riêng, bị quái đầm lầy phá hủy.
    /// - Di chuyển chậm bám theo Tank gần nhất.
    /// - Bật khi có Tank đứng trong bán kính quy định, tắt khi Tank rời đi.
    /// - Hỗ trợ đồng bộ IsLit và máu qua NetworkBehaviour khi chơi Co-op.
    /// </summary>
    public class The2FaceLightPoint : NetworkBehaviour
    {
        private static readonly List<The2FaceLightPoint> s_Active = new List<The2FaceLightPoint>();

        [Tooltip("SpriteRenderer thân Điểm Sáng.")]
        [SerializeField] private SpriteRenderer bodyRenderer;

        [Tooltip("Màu khi Điểm Sáng đang bật.")]
        [SerializeField] private Color litColor = new Color(1f, 0.83f, 0.25f, 1f);

        [Tooltip("Màu khi Điểm Sáng đang tắt.")]
        [SerializeField] private Color unlitColor = new Color(0.34f, 0.38f, 0.5f, 0.9f);

        [Tooltip("Tốc độ nhấp nháy khi đang bật.")]
        [SerializeField, Min(0f)] private float pulseSpeed = 2.2f;

        [Header("Self-Configured Size & Detection Range")]
        [Tooltip("Kích thước hiển thị gốc của Điểm Sáng (localScale).")]
        [SerializeField] private Vector3 baseScale = Vector3.one;

        [Tooltip("Phạm vi phát hiện Tank ở bên trong để bật Điểm Sáng (bán kính trigger, mét).")]
        [SerializeField, Min(0.1f)] private float litRadius = 2f;

        [Tooltip("Layer của Tank để Collider Trigger phát hiện.")]
        [SerializeField] private LayerMask tankLayer;

        [Tooltip("CircleCollider2D Trigger phát hiện Tank đứng trong phạm vi Điểm Sáng (tự tạo child LitTriggerZone nếu để trống).")]
        [SerializeField] private CircleCollider2D litTriggerCollider;

        private readonly NetworkVariable<bool> m_NetIsLit = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> m_NetHealth = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> m_NetMaxHealth = new(
            1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private The2FaceBrain m_Boss;
        private float m_MaxHealth;
        private float m_CurrentHealth;
        private float m_MoveSpeed;
        private float m_Elapsed;
        private Vector3 m_BaseScale;
        private ColliderOwnerRef m_LitTriggerOwnerRef;
        private bool m_Initialized;

        /// <summary>Danh sách toàn bộ Điểm Sáng đang tồn tại trong scene.</summary>
        public static IReadOnlyList<The2FaceLightPoint> ActivePoints => s_Active;

        /// <summary>Điểm Sáng còn sống hay không.</summary>
        public bool IsAlive => (IsSpawned && !IsServer) ? m_NetHealth.Value > 0f : (m_Initialized && m_CurrentHealth > 0f);

        /// <summary>Điểm Sáng đang được Tank kích hoạt hay không.</summary>
        public bool IsLit { get; private set; }

        /// <summary>Máu hiện tại.</summary>
        public float CurrentHealth => (IsSpawned && !IsServer) ? m_NetHealth.Value : m_CurrentHealth;

        /// <summary>Máu tối đa.</summary>
        public float MaxHealth => (IsSpawned && !IsServer) ? m_NetMaxHealth.Value : m_MaxHealth;

        private void Awake()
        {
            m_BaseScale = baseScale.sqrMagnitude > 0.0001f ? baseScale : transform.localScale;
            transform.localScale = m_BaseScale;
            if (bodyRenderer != null) bodyRenderer.color = unlitColor;

            ColliderOwnerRef ownerRef = GetComponent<ColliderOwnerRef>();
            if (ownerRef == null)
            {
                ownerRef = gameObject.AddComponent<ColliderOwnerRef>();
            }
            ownerRef.SetOwner(this);

            EnsureLitTriggerZone();
        }

        private void OnEnable()
        {
            if (!s_Active.Contains(this)) s_Active.Add(this);
        }

        private void OnDisable()
        {
            s_Active.Remove(this);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            s_Active.Remove(this);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                m_NetMaxHealth.Value = m_MaxHealth;
                m_NetHealth.Value = m_CurrentHealth;
                m_NetIsLit.Value = IsLit;
            }
            else
            {
                m_Initialized = true;
                IsLit = m_NetIsLit.Value;
                m_MaxHealth = m_NetMaxHealth.Value;
                m_CurrentHealth = m_NetHealth.Value;
                if (bodyRenderer != null) bodyRenderer.color = IsLit ? litColor : unlitColor;
            }
        }

        /// <summary>Khởi tạo Điểm Sáng do Boss sinh ra (phạm vi và kích thước tự cấu hình trên Điểm Sáng).</summary>
        public void Initialize(The2FaceBrain boss, float maxHealth, float moveSpeed)
        {
            m_Boss = boss;
            m_MaxHealth = Mathf.Max(1f, maxHealth);
            m_CurrentHealth = m_MaxHealth;
            m_MoveSpeed = Mathf.Max(0f, moveSpeed);
            m_Elapsed = 0f;
            m_Initialized = true;
            IsLit = false;

            EnsureLitTriggerZone();

            if (IsSpawned && IsServer)
            {
                m_NetMaxHealth.Value = m_MaxHealth;
                m_NetHealth.Value = m_CurrentHealth;
                m_NetIsLit.Value = false;
            }

            if (bodyRenderer != null) bodyRenderer.color = unlitColor;
        }

        private void Update()
        {
            m_Elapsed += Time.deltaTime;

            if (IsSpawned && !IsServer)
            {
                IsLit = m_NetIsLit.Value;
                m_CurrentHealth = m_NetHealth.Value;
                m_MaxHealth = m_NetMaxHealth.Value;
                UpdateVisual();
                return;
            }

            if (!m_Initialized || m_CurrentHealth <= 0f) return;

            IsLit = HasTankInsideTrigger();

            if (IsSpawned && IsServer && m_NetIsLit.Value != IsLit)
            {
                m_NetIsLit.Value = IsLit;
            }

            TankBrain nearest = FindNearestTank();
            UpdateMovement(nearest);
            UpdateVisual();
        }

        private bool HasTankInsideTrigger()
        {
            if (m_LitTriggerOwnerRef == null)
            {
                EnsureLitTriggerZone();
                if (m_LitTriggerOwnerRef == null) return false;
            }

            int mask = tankLayer.value != 0 ? tankLayer.value : LayerMask.GetMask("Tank");
            foreach (Collider2D col in m_LitTriggerOwnerRef.GetTriggerColliders())
            {
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;

                if (mask != 0 && (mask & (1 << col.gameObject.layer)) == 0)
                {
                    continue;
                }

                ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(col);
                TankBrain tank = targetRef?.GetOwner<TankBrain>() ?? col.GetComponentInParent<TankBrain>();
                if (tank != null && tank.IsAlive)
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureLitTriggerZone()
        {
            if (litTriggerCollider == null)
            {
                Transform existing = transform.Find("LitTriggerZone");
                GameObject zoneGo = existing != null ? existing.gameObject : new GameObject("LitTriggerZone");
                if (existing == null)
                {
                    zoneGo.transform.SetParent(transform, false);
                }

                // Giữ layer Default (0) để không bị tính nhầm là hitbox thân Điểm Sáng trên layer Tank.
                zoneGo.layer = 0;

                litTriggerCollider = zoneGo.GetComponent<CircleCollider2D>();
                if (litTriggerCollider == null)
                {
                    litTriggerCollider = zoneGo.AddComponent<CircleCollider2D>();
                }
            }

            litTriggerCollider.isTrigger = true;
            SyncLitTriggerRadius();

            GameObject triggerGo = litTriggerCollider.gameObject;
            Rigidbody2D rb = triggerGo.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = triggerGo.AddComponent<Rigidbody2D>();
            }
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.simulated = true;

            m_LitTriggerOwnerRef = triggerGo.GetComponent<ColliderOwnerRef>();
            if (m_LitTriggerOwnerRef == null)
            {
                m_LitTriggerOwnerRef = triggerGo.AddComponent<ColliderOwnerRef>();
            }
            m_LitTriggerOwnerRef.SetOwner(this);
        }

        private void SyncLitTriggerRadius()
        {
            if (litTriggerCollider == null) return;

            Vector3 lossy = litTriggerCollider.transform.lossyScale;
            float maxScale = Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y)));
            litTriggerCollider.radius = Mathf.Max(0.1f, litRadius) / maxScale;
        }

        private void OnValidate()
        {
            litRadius = Mathf.Max(0.1f, litRadius);
            if (litTriggerCollider != null)
            {
                litTriggerCollider.isTrigger = true;
                SyncLitTriggerRadius();
            }
        }

        private TankBrain FindNearestTank()
        {
            TankBrain[] tanks = FindObjectsByType<TankBrain>();
            TankBrain result = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < tanks.Length; i++)
            {
                TankBrain tank = tanks[i];
                if (tank == null || !tank.IsAlive) continue;

                float sqr = ((Vector2)tank.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    result = tank;
                }
            }

            return result;
        }

        private void UpdateMovement(TankBrain nearest)
        {
            if (nearest == null || m_MoveSpeed <= 0f) return;

            Vector2 toTank = (Vector2)nearest.transform.position - (Vector2)transform.position;
            float distance = toTank.magnitude;
            if (distance <= litRadius * 0.85f || distance <= 0.001f) return;

            Vector2 next = Vector2.MoveTowards(
                transform.position,
                nearest.transform.position,
                m_MoveSpeed * Time.deltaTime);
            transform.position = next;
        }

        private void UpdateVisual()
        {
            if (bodyRenderer != null)
            {
                Color targetColor = IsLit ? litColor : unlitColor;
                bodyRenderer.color = Color.Lerp(bodyRenderer.color, targetColor, Time.deltaTime * 8f);
            }

            float pulse = IsLit ? 1f + Mathf.Sin(m_Elapsed * pulseSpeed) * 0.06f : 1f;
            transform.localScale = m_BaseScale * pulse;
        }

        /// <summary>Nhận sát thương từ quái hoặc kỹ năng của Boss.</summary>
        public void ApplyDamage(float amount)
        {
            if (IsSpawned && !IsServer) return;
            if (!m_Initialized || m_CurrentHealth <= 0f || amount <= 0f) return;

            m_CurrentHealth = Mathf.Max(0f, m_CurrentHealth - amount);
            if (IsSpawned && IsServer) m_NetHealth.Value = m_CurrentHealth;

            if (bodyRenderer != null && m_CurrentHealth > 0f)
            {
                bodyRenderer.color = Color.white;
            }

            IronWasteland.CombatFeedback.Show(amount, transform.position, IronWasteland.TextValueType.EnemyDamage);

            if (m_CurrentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            IsLit = false;
            if (m_Boss != null)
            {
                m_Boss.OnLightPointDestroyed(this);
            }

            if (IsSpawned && IsServer && NetworkObject != null)
            {
                NetworkObject.Despawn(true);
                return;
            }

            Destroy(gameObject);
        }
    }
}
