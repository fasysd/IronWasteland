using System;
using System.Collections;
using System.Collections.Generic;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Kỹ năng hình tam giác của Kiến Lửa Khổng Lồ.
    /// Kích hoạt tạm thời để quét và tìm các mục tiêu TankBrain trong phạm vi hình tam giác.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ColliderOwnerRef))]
    public class Skill_KIenLuaKhongLo : MonoBehaviour
    {
        public event Action<Skill_KIenLuaKhongLo, TankBrain> OnFound;

        [Header("Hit Detection")]
        [SerializeField, Min(0f)] private float hitCheckDelay = 0.1f;
        [SerializeField, Min(0.1f)] private float range = 5f;
        [SerializeField, Min(0.1f)] private float baseWidth = 4f;
        [SerializeField, Min(0f)] private float apexOffset = 0.5f;

        private ColliderOwnerRef m_OwnerRef;
        private PolygonCollider2D m_PolygonCollider;
        private Coroutine m_CastCoroutine;

        public float HitCheckDelay => hitCheckDelay;
        public float Range => range;
        public float BaseWidth => baseWidth;
        public float ApexOffset => apexOffset;
        public bool IsCasting => m_CastCoroutine != null;

        private void Awake()
        {
            m_OwnerRef = GetComponent<ColliderOwnerRef>();
            m_PolygonCollider = GetComponent<PolygonCollider2D>();

            if (m_PolygonCollider != null)
            {
                m_PolygonCollider.isTrigger = true;
            }
        }

        private void OnDisable()
        {
            m_CastCoroutine = null;
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
            yield return new WaitForSeconds(hitCheckDelay);

            if (m_OwnerRef != null)
            {
                IEnumerable<Collider2D> colliders = m_OwnerRef.GetTriggerColliders();
                var targets = new HashSet<TankBrain>();

                foreach (Collider2D col in colliders)
                {
                    if (col == null) continue;

                    TankBrain tank = ResolveTankBrain(col);
                    if (tank != null && tank.IsAlive)
                    {
                        targets.Add(tank);
                    }
                }

                foreach (TankBrain tank in targets)
                {
                    OnFound?.Invoke(this, tank);
                }
            }

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

