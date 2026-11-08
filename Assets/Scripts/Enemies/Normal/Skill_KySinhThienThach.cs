using System;
using System.Collections;
using System.Collections.Generic;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Kỹ năng tự nổ hình tròn của Ký Sinh Thiên Thạch.
    /// Sau X giây kể từ khi được kích hoạt sẽ quét các mục tiêu trong vùng nổ hình tròn và kích hoạt sự kiện OnFound.
    /// Luôn kích hoạt OnFound kể cả khi không tìm thấy mục tiêu nào.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(ColliderOwnerRef))]
    public class Skill_KySinhThienThach : MonoBehaviour
    {
        public event Action<Skill_KySinhThienThach, IReadOnlyList<TankBrain>> OnFound;

        [Header("Explosion Settings")]
        [Tooltip("Thời gian trễ (X giây) trước khi kích nổ.")]
        [SerializeField, Min(0f)] private float detonationDelay = 0.5f;

        [Tooltip("Thời gian chờ ngắn để Physics2D cập nhật Trigger colliders.")]
        [SerializeField, Min(0f)] private float hitCheckDelay = 0.05f;

        private CircleCollider2D m_Collider;
        private ColliderOwnerRef m_OwnerRef;
        private Coroutine m_CastCoroutine;

        public float DetonationDelay => detonationDelay;
        public bool IsCasting => m_CastCoroutine != null;

        private void Awake()
        {
            m_OwnerRef = GetComponent<ColliderOwnerRef>();
            m_Collider = GetComponent<CircleCollider2D>();

            if (m_Collider != null)
            {
                m_Collider.isTrigger = true;
            }
        }

        private void OnDisable()
        {
            m_CastCoroutine = null;
        }

        private void OnValidate()
        {
            if (m_Collider == null) m_Collider = GetComponent<CircleCollider2D>();
            if (m_Collider != null)
            {
                m_Collider.isTrigger = true;
            }
        }

        public void Cast(Action onCompleted = null)
        {
            if (m_CastCoroutine != null)
            {
                StopCoroutine(m_CastCoroutine);
            }

            gameObject.SetActive(true);
            m_CastCoroutine = StartCoroutine(CastRoutine(onCompleted));
        }

        public void CancelCast()
        {
            if (m_CastCoroutine != null)
            {
                StopCoroutine(m_CastCoroutine);
                m_CastCoroutine = null;
            }

            gameObject.SetActive(false);
        }

        private IEnumerator CastRoutine(Action onCompleted)
        {
            yield return new WaitForSeconds(detonationDelay);

            if (hitCheckDelay > 0f)
            {
                yield return new WaitForSeconds(hitCheckDelay);
            }

            var targets = new HashSet<TankBrain>();

            // 1. Quét từ ColliderOwnerRef trigger tracking
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

            var result = new List<TankBrain>(targets);
            OnFound?.Invoke(this, result);

            m_CastCoroutine = null;
            gameObject.SetActive(false);
            onCompleted?.Invoke();
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
    }
}
