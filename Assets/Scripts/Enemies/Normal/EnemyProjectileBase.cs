using System;
using System.Collections;
using System.Collections.Generic;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Lớp cơ sở cho các loại đạn/projectile của quái vật.
    /// Quản lý vận tốc, thời gian tồn tại, phát hiện va chạm và dọn dẹp.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(ColliderOwnerRef))]
    public abstract class EnemyProjectileBase : MonoBehaviour
    {
        public event Action<EnemyProjectileBase, TankBrain> TargetHit;

        [Header("Projectile Settings")]
        [Tooltip("Tốc độ bay của viên đạn.")]
        [SerializeField, Min(0f)] protected float speed = 8f;

        [Tooltip("Thời gian tồn tại tối đa trước khi tự biến mất (giây).")]
        [SerializeField, Min(0.1f)] protected float lifetime = 4f;

        [Tooltip("Sát thương cơ bản.")]
        [SerializeField, Min(0f)] protected float baseDamage = 10f;

        [Header("Collision Delay")]
        [Tooltip("Độ trễ kiểm tra va chạm để Physics2D nạp đầy đủ collider.")]
        [SerializeField, Min(0f)] protected float hitCheckDelay = 0.02f;

        protected Rigidbody2D m_Body;
        protected Collider2D m_Collider;
        protected ColliderOwnerRef m_OwnerRef;

        protected Vector2 m_Direction;
        protected float m_ElapsedTime;
        protected bool m_Initialized;
        protected bool m_HasHit;
        protected Coroutine m_HitCheckCoroutine;
        protected Action<EnemyProjectileBase> m_OnFinished;

        public float Speed => speed;
        public float Lifetime => lifetime;
        public float BaseDamage { get => baseDamage; set => baseDamage = value; }
        public bool IsInitialized => m_Initialized;

        protected virtual void Awake()
        {
            m_Body = GetComponent<Rigidbody2D>();
            m_Collider = GetComponent<Collider2D>();
            m_OwnerRef = GetComponent<ColliderOwnerRef>();

            if (m_Collider != null)
            {
                m_Collider.isTrigger = true;
            }
        }

        protected virtual void OnEnable()
        {
            if (m_OwnerRef != null)
            {
                m_OwnerRef.TriggerEnter += HandleTriggerEntered;
            }
        }

        protected virtual void OnDisable()
        {
            Clear();
        }

        public virtual void Clear()
        {
            if (m_HitCheckCoroutine != null)
            {
                StopCoroutine(m_HitCheckCoroutine);
                m_HitCheckCoroutine = null;
            }

            if (m_OwnerRef != null)
            {
                m_OwnerRef.TriggerEnter -= HandleTriggerEntered;
            }

            if (m_Body != null)
            {
                m_Body.linearVelocity = Vector2.zero;
            }

            m_Direction = Vector2.zero;
            m_ElapsedTime = 0f;
            m_Initialized = false;
            m_HasHit = false;
            m_OnFinished = null;
            TargetHit = null;
        }

        public virtual void Initialize(
            Vector2 direction,
            float damageAmount,
            Action<EnemyProjectileBase, TankBrain> onTargetHit = null,
            Action<EnemyProjectileBase> onFinished = null)
        {
            Clear();

            m_Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            baseDamage = damageAmount;
            m_OnFinished = onFinished;
            if (onTargetHit != null)
            {
                TargetHit += onTargetHit;
            }
            m_Initialized = true;
            m_ElapsedTime = 0f;

            transform.right = m_Direction;

            if (m_Body != null)
            {
                m_Body.linearVelocity = m_Direction * speed;
            }

            if (m_OwnerRef != null && isActiveAndEnabled)
            {
                m_OwnerRef.TriggerEnter -= HandleTriggerEntered;
                m_OwnerRef.TriggerEnter += HandleTriggerEntered;
            }
        }

        public virtual void Initialize(Vector2 direction, float damageAmount, Action<EnemyProjectileBase> onFinished)
        {
            Initialize(direction, damageAmount, null, onFinished);
        }

        protected virtual void Update()
        {
            if (!m_Initialized) return;

            m_ElapsedTime += Time.deltaTime;
            if (m_ElapsedTime >= lifetime)
            {
                Despawn();
            }
        }

        protected virtual void FixedUpdate()
        {
            if (!m_Initialized || m_Body == null) return;

            m_Body.linearVelocity = m_Direction * speed;
        }

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (m_OwnerRef == null)
            {
                HandleTriggerEntered(other);
            }
        }

        protected virtual void HandleTriggerEntered(Collider2D other)
        {
            if (!m_Initialized) return;

            // Xử lý tức thì nếu collider va chạm trực tiếp trúng xe tăng
            if (other != null)
            {
                TankBrain tank = ResolveTankBrain(other);
                if (tank != null && tank.IsAlive)
                {
                    OnTanksHit(new HashSet<TankBrain> { tank });
                    return;
                }
            }

            if (m_HitCheckCoroutine == null && gameObject.activeInHierarchy)
            {
                m_HitCheckCoroutine = StartCoroutine(CheckCollisionsRoutine(other));
            }
        }

        protected virtual IEnumerator CheckCollisionsRoutine(Collider2D directCollider)
        {
            if (hitCheckDelay > 0f)
            {
                yield return new WaitForSeconds(hitCheckDelay);
            }
            m_HitCheckCoroutine = null;

            if (!this || !gameObject.activeInHierarchy || !m_Initialized)
            {
                yield break;
            }

            var tanks = new HashSet<TankBrain>();

            // 1. Quét từ ColliderOwnerRef
            if (m_OwnerRef != null)
            {
                foreach (Collider2D col in m_OwnerRef.GetTriggerColliders())
                {
                    if (col == null) continue;
                    TankBrain tank = ResolveTankBrain(col);
                    if (tank != null && tank.IsAlive)
                    {
                        tanks.Add(tank);
                    }
                }
            }

            // 2. Kiểm tra trực tiếp collider va chạm nếu có
            if (directCollider != null)
            {
                TankBrain tank = ResolveTankBrain(directCollider);
                if (tank != null && tank.IsAlive)
                {
                    tanks.Add(tank);
                }
            }

            if (tanks.Count > 0)
            {
                OnTanksHit(tanks);
            }
        }

        protected virtual TankBrain ResolveTankBrain(Collider2D col)
        {
            if (col == null) return null;

            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(col);
            if (targetRef != null && targetRef.Owner != null)
            {
                if (targetRef.Owner is TankBrain directBrain)
                {
                    return directBrain;
                }

                if (targetRef.Owner is Component comp)
                {
                    TankBrain brain = comp.GetComponent<TankBrain>() ?? comp.GetComponentInParent<TankBrain>();
                    if (brain != null) return brain;
                }
                else if (targetRef.Owner is GameObject go)
                {
                    TankBrain brain = go.GetComponent<TankBrain>() ?? go.GetComponentInParent<TankBrain>();
                    if (brain != null) return brain;
                }
            }

            return col.GetComponentInParent<TankBrain>();
        }

        protected abstract void OnTanksHit(HashSet<TankBrain> tanks);

        protected void NotifyTargetHit(TankBrain tank)
        {
            if (tank != null)
            {
                TargetHit?.Invoke(this, tank);
            }
        }

        public virtual void Despawn()
        {
            Action<EnemyProjectileBase> onFinished = m_OnFinished;
            Clear();

            if (onFinished != null)
            {
                onFinished(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        protected virtual void OnValidate()
        {
            speed = Mathf.Max(0f, speed);
            lifetime = Mathf.Max(0.1f, lifetime);
            hitCheckDelay = Mathf.Max(0f, hitCheckDelay);
        }
    }
}
