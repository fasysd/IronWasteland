using System;
using System.Collections;
using System.Collections.Generic;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Vùng đầm lầy do Quái Vật Đầm Lầy tạo ra.
    /// - Quản lý mục tiêu và va chạm 100% thông qua ColliderOwnerRef.
    /// - KHÔNG chứa logic gây sát thương trực tiếp: Chỉ kích hoạt event trả về danh sách mục tiêu
    ///   tìm được để EnemyBrain (hoặc component điều khiển) tự kích hoạt sát thương.
    /// - Kích hoạt event trả mục tiêu 2 lần độc lập (không dùng chung danh sách target):
    ///   + Lần 1: Khi vừa xuất hiện, sau khoảng delay ngắn để physics nạp trigger.
    ///   + Lần 2: Khi hết thời gian tồn tại và phát nổ (trước khi Despawn).
    /// - Trong thời gian tồn tại: làm giảm 50% tốc độ di chuyển của xe tăng trong vùng.
    /// - Hiệu ứng giảm tốc lập tức biến mất ngay khi xe tăng đi ra khỏi vùng (ColliderOwnerRef.TriggerExit).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(ColliderOwnerRef))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class SwampZone_QuaiVatDamLay : MonoBehaviour
    {
        [Header("Swamp Zone Settings")]
        [Tooltip("Thời gian tồn tại của vùng trước khi phát nổ và biến mất (giây).")]
        [SerializeField, Min(0.5f)] private float swampDuration = 3.5f;

        [Tooltip("Tỷ lệ giảm tốc độ di chuyển khi xe tăng ở trong vùng (0.5 = giảm 50%).")]
        [SerializeField, Range(0.1f, 0.9f)] private float slowPercent = 0.5f;

        [Tooltip("Thời gian chờ ngắn để physics nạp trigger collider khi vừa kích hoạt (giây).")]
        [SerializeField, Min(0f)] private float hitCheckDelay = 0.05f;

        [Header("Layers")]
        [SerializeField] private LayerMask tankLayer;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color activeColor = new Color(0.35f, 0.45f, 0.2f, 0.45f);
        [SerializeField] private Color warningColor = new Color(0.55f, 0.25f, 0.1f, 0.65f);

        /// <summary>
        /// Kích hoạt khi tìm được mục tiêu (cả khi xuất hiện lẫn khi phát nổ).
        /// EnemyBrain đăng ký sự kiện này để kích hoạt sát thương.
        /// </summary>
        public event Action<SwampZone_QuaiVatDamLay, HashSet<TankBrain>> OnTargetsDetected;

        /// <summary>
        /// Kích hoạt riêng khi vừa xuất hiện (sau hitCheckDelay).
        /// </summary>
        public event Action<SwampZone_QuaiVatDamLay, HashSet<TankBrain>> OnAppearTargets;

        /// <summary>
        /// Kích hoạt riêng khi hết thời gian và phát nổ (trước khi Despawn).
        /// </summary>
        public event Action<SwampZone_QuaiVatDamLay, HashSet<TankBrain>> OnExplodeTargets;

        private Collider2D m_Collider;
        private ColliderOwnerRef m_OwnerRef;
        private Rigidbody2D m_Rigidbody;
        private float m_ElapsedTime;
        private bool m_IsActive;
        private string m_SlowKey;
        private Coroutine m_AppearCoroutine;
        private Action<SwampZone_QuaiVatDamLay> m_OnDespawn;

        // Quản lý riêng biệt cho hiệu ứng làm chậm
        private readonly HashSet<TankBrain> m_SlowedTanks = new HashSet<TankBrain>();

        public float SwampDuration { get => swampDuration; set => swampDuration = value; }
        public float SlowPercent { get => slowPercent; set => slowPercent = value; }
        public float RemainingTime => Mathf.Max(0f, swampDuration - m_ElapsedTime);
        public bool IsActive => m_IsActive;

        private void Awake()
        {
            m_Collider = GetComponent<Collider2D>();
            m_OwnerRef = GetComponent<ColliderOwnerRef>();
            m_Rigidbody = GetComponent<Rigidbody2D>();

            // Đảm bảo có Kinematic Rigidbody2D để trigger events (Enter/Exit) luôn tin cậy trong Unity 2D
            if (m_Rigidbody == null)
            {
                m_Rigidbody = gameObject.AddComponent<Rigidbody2D>();
            }
            m_Rigidbody.bodyType = RigidbodyType2D.Kinematic;
            m_Rigidbody.simulated = true;
            m_Rigidbody.useFullKinematicContacts = true;

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (m_Collider != null)
            {
                m_Collider.isTrigger = true;
            }

            if (tankLayer.value == 0)
            {
                tankLayer = LayerMask.GetMask("Tank");
            }

            m_SlowKey = "Swamp_Slow_" + System.Guid.NewGuid().ToString("N");
        }

        private void OnEnable()
        {
            if (m_OwnerRef != null)
            {
                m_OwnerRef.TriggerEnter += HandleTriggerEntered;
                m_OwnerRef.TriggerStay += HandleTriggerStay;
                m_OwnerRef.TriggerExit += HandleTriggerExited;
            }
        }

        private void OnDisable()
        {
            if (m_OwnerRef != null)
            {
                m_OwnerRef.TriggerEnter -= HandleTriggerEntered;
                m_OwnerRef.TriggerStay -= HandleTriggerStay;
                m_OwnerRef.TriggerExit -= HandleTriggerExited;
            }
            ClearAllSlowDebuffs();
        }

        public void SpawnAt(Vector2 position, Action<SwampZone_QuaiVatDamLay> onDespawn = null)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            m_OnDespawn = onDespawn;
            m_ElapsedTime = 0f;
            m_IsActive = true;
            m_SlowedTanks.Clear();

            if (spriteRenderer != null)
            {
                spriteRenderer.color = activeColor;
            }

            gameObject.SetActive(true);

            // Quét mục tiêu lần 1 tại thời điểm xuất hiện
            if (m_AppearCoroutine != null)
            {
                StopCoroutine(m_AppearCoroutine);
            }
            m_AppearCoroutine = StartCoroutine(AppearDamageRoutine());
        }

        private IEnumerator AppearDamageRoutine()
        {
            if (hitCheckDelay > 0f)
            {
                yield return new WaitForSeconds(hitCheckDelay);
            }
            else
            {
                yield return new WaitForFixedUpdate();
            }

            if (!m_IsActive) yield break;

            // Quét target tại thời điểm xuất hiện và kích hoạt event trả target về EnemyBrain
            HashSet<TankBrain> targets = GetCurrentTargets();
            OnAppearTargets?.Invoke(this, targets);
            OnTargetsDetected?.Invoke(this, targets);

            m_AppearCoroutine = null;
        }

        private void Update()
        {
            if (!m_IsActive) return;

            m_ElapsedTime += Time.deltaTime;

            // Cảnh báo phát nổ trong 0.8 giây cuối
            float timeLeft = swampDuration - m_ElapsedTime;
            if (spriteRenderer != null && timeLeft <= 0.8f)
            {
                float t = Mathf.PingPong(m_ElapsedTime * 6f, 1f);
                spriteRenderer.color = Color.Lerp(activeColor, warningColor, t);
            }

            // Kiểm tra an toàn debuff: nếu xe tăng đã chết hoặc không còn collider nào trong ColliderOwnerRef
            ValidateSlowedTanks();

            if (m_ElapsedTime >= swampDuration)
            {
                ExplodeAndDespawn();
            }
        }

        /// <summary>
        /// Quét và trả về danh sách các xe tăng đang có mặt trong trigger của ColliderOwnerRef tại thời điểm hiện tại.
        /// Sử dụng 100% ColliderOwnerRef. Không chứa logic gây sát thương.
        /// </summary>
        public HashSet<TankBrain> GetCurrentTargets()
        {
            var targets = new HashSet<TankBrain>();
            CollectTanksInZone(targets);
            return targets;
        }

        /// <summary>
        /// Thu thập các xe tăng đang có mặt trong trigger của ColliderOwnerRef nạp vào tập kết quả đã cấp.
        /// </summary>
        public void CollectTanksInZone(HashSet<TankBrain> results)
        {
            if (results == null || m_OwnerRef == null) return;

            foreach (Collider2D col in m_OwnerRef.GetTriggerColliders())
            {
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;

                if (tankLayer.value != 0 && (tankLayer.value & (1 << col.gameObject.layer)) == 0)
                {
                    continue;
                }

                TankBrain tank = ResolveTankBrain(col);
                if (tank != null && tank.IsAlive)
                {
                    results.Add(tank);
                }
            }
        }

        private void ExplodeAndDespawn()
        {
            // Quét lại target độc lập từ ColliderOwnerRef tại thời điểm nổ và kích hoạt event trả về EnemyBrain
            HashSet<TankBrain> targets = GetCurrentTargets();
            OnExplodeTargets?.Invoke(this, targets);
            OnTargetsDetected?.Invoke(this, targets);

            // Gỡ bỏ debuff và thu hồi về Pool
            Despawn();
        }

        private void HandleTriggerEntered(Collider2D other)
        {
            if (!m_IsActive || other == null) return;

            if (tankLayer.value != 0 && (tankLayer.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            TankBrain tank = ResolveTankBrain(other);
            if (tank != null && tank.IsAlive)
            {
                if (m_SlowedTanks.Add(tank))
                {
                    ApplySlowDebuff(tank);
                }
            }
        }

        private void HandleTriggerStay(Collider2D other)
        {
            if (!m_IsActive || other == null) return;

            if (tankLayer.value != 0 && (tankLayer.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            TankBrain tank = ResolveTankBrain(other);
            if (tank != null && tank.IsAlive)
            {
                if (m_SlowedTanks.Add(tank))
                {
                    ApplySlowDebuff(tank);
                }
            }
        }

        private void HandleTriggerExited(Collider2D other)
        {
            if (other == null) return;

            TankBrain tank = ResolveTankBrain(other);
            if (tank != null && m_SlowedTanks.Contains(tank))
            {
                // Kiểm tra xem xe tăng còn collider nào khác vẫn nằm trong ColliderOwnerRef không
                if (!IsTankStillInZone(tank))
                {
                    m_SlowedTanks.Remove(tank);
                    RemoveSlowDebuff(tank);
                }
            }
        }

        private bool IsTankStillInZone(TankBrain tank)
        {
            if (tank == null || !tank.IsAlive || m_OwnerRef == null) return false;

            foreach (Collider2D col in m_OwnerRef.GetTriggerColliders())
            {
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;

                if (ResolveTankBrain(col) == tank)
                {
                    return true;
                }
            }

            return false;
        }

        private void ValidateSlowedTanks()
        {
            if (m_SlowedTanks.Count == 0) return;

            List<TankBrain> toRemove = null;
            foreach (TankBrain tank in m_SlowedTanks)
            {
                if (tank == null || !tank.IsAlive || !IsTankStillInZone(tank))
                {
                    toRemove ??= new List<TankBrain>();
                    toRemove.Add(tank);
                }
            }

            if (toRemove != null)
            {
                for (int i = 0; i < toRemove.Count; i++)
                {
                    TankBrain t = toRemove[i];
                    m_SlowedTanks.Remove(t);
                    RemoveSlowDebuff(t);
                }
            }
        }

        private void ApplySlowDebuff(TankBrain tank)
        {
            if (tank == null || !tank.IsAlive) return;

            float slowAmount = tank.BaseStats.MoveSpeed * slowPercent;
            tank.AddBonusStats(m_SlowKey, new TankStats { MoveSpeed = -slowAmount });
        }

        private void RemoveSlowDebuff(TankBrain tank)
        {
            if (tank != null)
            {
                tank.RemoveBonusStats(m_SlowKey);
            }
        }

        private void ClearAllSlowDebuffs()
        {
            foreach (TankBrain tank in m_SlowedTanks)
            {
                RemoveSlowDebuff(tank);
            }
            m_SlowedTanks.Clear();
        }

        public void Despawn()
        {
            if (!m_IsActive) return;

            m_IsActive = false;

            if (m_AppearCoroutine != null)
            {
                StopCoroutine(m_AppearCoroutine);
                m_AppearCoroutine = null;
            }

            ClearAllSlowDebuffs();
            gameObject.SetActive(false);

            Action<SwampZone_QuaiVatDamLay> callback = m_OnDespawn;
            m_OnDespawn = null;
            callback?.Invoke(this);
        }

        private TankBrain ResolveTankBrain(Collider2D col)
        {
            if (col == null) return null;

            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(col);
            if (targetRef != null)
            {
                if (targetRef.TryGetOwner<TankBrain>(out TankBrain directBrain) && directBrain != null)
                {
                    return directBrain;
                }

                if (targetRef.Owner is TankBrain brain)
                {
                    return brain;
                }

                if (targetRef.Owner is Component comp)
                {
                    TankBrain compBrain = comp.GetComponent<TankBrain>() ?? comp.GetComponentInParent<TankBrain>();
                    if (compBrain != null) return compBrain;
                }
                else if (targetRef.Owner is GameObject go)
                {
                    TankBrain goBrain = go.GetComponent<TankBrain>() ?? go.GetComponentInParent<TankBrain>();
                    if (goBrain != null) return goBrain;
                }
            }

            return col.GetComponentInParent<TankBrain>();
        }

        private void OnValidate()
        {
            swampDuration = Mathf.Max(0.5f, swampDuration);
            slowPercent = Mathf.Clamp(slowPercent, 0.1f, 0.9f);
        }
    }
}
