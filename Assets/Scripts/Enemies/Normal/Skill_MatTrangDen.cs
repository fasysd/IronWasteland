using System;
using System.Collections;
using System.Collections.Generic;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Kỹ năng bức xạ của Mặt Trăng Đen:
    /// - Khi phát xung, mở trường bức xạ năng lượng xung quanh bản thân trong bán kính 2.5m.
    /// - Duy trì trong duration giây, mỗi tickInterval (0.5s) quét và kích hoạt sự kiện gây sát thương 100% Tấn công lên các Tank trong vùng.
    /// - Quản lý va chạm và quét mục tiêu thông qua ColliderOwnerRef.
    /// </summary>
    [DisallowMultipleComponent]
    public class Skill_MatTrangDen : MonoBehaviour
    {
        public event Action<Skill_MatTrangDen, List<TankBrain>> OnDamageDealt;

        [Header("Aura Timings")]
        [Tooltip("Thời gian tồn tại của trường bức xạ mỗi lần kích hoạt (giây).")]
        [SerializeField, Min(0.5f)] private float auraDuration = 2.0f;

        [Tooltip("Chu kỳ gây sát thương mỗi nhịp (0.5s theo GDD).")]
        [SerializeField, Min(0.1f)] private float tickInterval = 0.5f;

        [Tooltip("Bán kính trường bức xạ năng lượng (2.5m theo GDD).")]
        [SerializeField, Min(0.5f)] private float auraRadius = 2.5f;

        [Header("Layers")]
        [SerializeField] private LayerMask tankLayer;

        [Tooltip("Độ trễ chờ physics trigger cập nhật ban đầu (giây).")]
        [SerializeField, Min(0f)] private float hitCheckDelay = 0.02f;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        private ColliderOwnerRef m_OwnerRef;
        private CircleCollider2D m_Collider;
        private Coroutine m_AuraCoroutine;

        public float AuraDuration { get => auraDuration; set => auraDuration = value; }
        public float TickInterval { get => tickInterval; set => tickInterval = value; }
        public float AuraRadius { get => auraRadius; set => auraRadius = value; }
        public bool IsExecuting => m_AuraCoroutine != null;

        private void Awake()
        {
            m_OwnerRef = GetComponent<ColliderOwnerRef>();
            m_Collider = GetComponent<CircleCollider2D>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (m_Collider != null)
            {
                m_Collider.isTrigger = true;
                m_Collider.radius = auraRadius;
            }

            if (tankLayer.value == 0)
            {
                tankLayer = LayerMask.GetMask("Tank");
            }
        }

        public void CastSkill(Action onCompleted = null)
        {
            if (m_AuraCoroutine != null)
            {
                StopCoroutine(m_AuraCoroutine);
            }

            gameObject.SetActive(true);
            m_AuraCoroutine = StartCoroutine(AuraRoutine(onCompleted));
        }

        public void CancelSkill()
        {
            if (m_AuraCoroutine != null)
            {
                StopCoroutine(m_AuraCoroutine);
                m_AuraCoroutine = null;
            }

            gameObject.SetActive(false);
        }

        private IEnumerator AuraRoutine(Action onCompleted)
        {
            if (m_Collider != null)
            {
                m_Collider.radius = auraRadius;
            }

            if (hitCheckDelay > 0f)
            {
                yield return new WaitForSeconds(hitCheckDelay);
            }

            float elapsed = 0f;
            float tickTimer = 0f;

            // Tick đầu tiên ngay khi bật hào quang
            TriggerDamageTick();

            while (elapsed < auraDuration)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                tickTimer += dt;

                if (tickTimer >= tickInterval)
                {
                    tickTimer -= tickInterval;
                    TriggerDamageTick();
                }

                yield return null;
            }

            m_AuraCoroutine = null;
            gameObject.SetActive(false);
            onCompleted?.Invoke();
        }

        private void TriggerDamageTick()
        {
            var targets = new HashSet<TankBrain>();

            if (m_OwnerRef != null)
            {
                foreach (Collider2D col in m_OwnerRef.GetTriggerColliders())
                {
                    if (col == null) continue;
                    TankBrain tank = ResolveTankBrain(col);
                    if (tank != null && tank.IsAlive)
                    {
                        targets.Add(tank);
                    }
                }
            }

            // Fallback quét bằng Physics2D OverlapCircle xung quanh tâm kỹ năng
            if (targets.Count == 0)
            {
                Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, auraRadius, tankLayer);
                for (int i = 0; i < cols.Length; i++)
                {
                    TankBrain tank = ResolveTankBrain(cols[i]);
                    if (tank != null && tank.IsAlive)
                    {
                        targets.Add(tank);
                    }
                }
            }

            if (targets.Count > 0)
            {
                var result = new List<TankBrain>(targets);
                OnDamageDealt?.Invoke(this, result);
            }
        }

        private TankBrain ResolveTankBrain(Collider2D col)
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

        private void OnValidate()
        {
            auraDuration = Mathf.Max(0.5f, auraDuration);
            tickInterval = Mathf.Max(0.1f, tickInterval);
            auraRadius = Mathf.Max(0.5f, auraRadius);
            hitCheckDelay = Mathf.Max(0f, hitCheckDelay);
            if (m_Collider != null)
            {
                m_Collider.radius = auraRadius;
            }
        }
    }
}
