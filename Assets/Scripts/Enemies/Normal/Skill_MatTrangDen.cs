using System;
using System.Collections;
using System.Collections.Generic;
using IronWasteland.Enemies.Navigation;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Kỹ năng của Mặt Trăng Đen gồm 2 giai đoạn:
    /// - Giai đoạn 1: Chờ teleportDelay rồi dịch chuyển tức thời đến vị trí mục tiêu.
    /// - Giai đoạn 2: Chờ damageDelay rồi quét sát thương diện rộng xung quanh vị trí mới.
    /// </summary>
    [DisallowMultipleComponent]
    public class Skill_MatTrangDen : MonoBehaviour
    {
        public event Action<Skill_MatTrangDen, List<TankBrain>> OnDamageDealt;
        public event Action OnTeleported;

        [Header("Skill Timings")]
        [Tooltip("Thời gian chuẩn bị trước khi dịch chuyển (giây).")]
        [SerializeField, Min(0f)] private float teleportDelay = 0.2f;

        [Tooltip("Thời gian tụ năng lượng sau khi dịch chuyển trước khi gây sát thương (giây).")]
        [SerializeField, Min(0f)] private float damageDelay = 0.3f;

        [Header("Layers")]
        [SerializeField] private LayerMask tankLayer;

        [Tooltip("Độ trễ chờ physics trigger cập nhật nếu dùng collider nội tại (giây).")]
        [SerializeField, Min(0f)] private float hitCheckDelay = 0.02f;

        private ColliderOwnerRef m_OwnerRef;
        private CircleCollider2D m_Collider;
        private Coroutine m_SkillCoroutine;

        public float TeleportDelay { get => teleportDelay; set => teleportDelay = value; }
        public float DamageDelay { get => damageDelay; set => damageDelay = value; }
        public bool IsExecuting => m_SkillCoroutine != null;

        private void Awake()
        {
            m_OwnerRef = GetComponent<ColliderOwnerRef>();
            m_Collider = GetComponent<CircleCollider2D>();

            if (m_Collider != null)
            {
                m_Collider.isTrigger = true;
            }

            if (tankLayer.value == 0)
            {
                tankLayer = LayerMask.GetMask("Tank");
            }
        }

        public void CastSkill(EnemyMovementAgent agent, Transform enemyTransform, Vector2 targetPosition, Action onCompleted = null)
        {
            if (m_SkillCoroutine != null)
            {
                StopCoroutine(m_SkillCoroutine);
            }

            gameObject.SetActive(true);
            m_SkillCoroutine = StartCoroutine(SkillRoutine(agent, enemyTransform, targetPosition, onCompleted));
        }

        public void CancelSkill()
        {
            if (m_SkillCoroutine != null)
            {
                StopCoroutine(m_SkillCoroutine);
                m_SkillCoroutine = null;
            }

            gameObject.SetActive(false);
        }

        private IEnumerator SkillRoutine(EnemyMovementAgent agent, Transform enemyTransform, Vector2 targetPosition, Action onCompleted)
        {
            // Giai đoạn 1: Chờ teleportDelay rồi dịch chuyển đến vị trí mục tiêu
            if (teleportDelay > 0f)
            {
                yield return new WaitForSeconds(teleportDelay);
            }

            if (agent != null)
            {
                agent.Teleport(targetPosition);
            }
            else if (enemyTransform != null)
            {
                enemyTransform.position = new Vector3(targetPosition.x, targetPosition.y, enemyTransform.position.z);
            }

            OnTeleported?.Invoke();

            // Giai đoạn 2: Chờ damageDelay rồi phát nổ gây sát thương xung quanh
            if (damageDelay > 0f)
            {
                yield return new WaitForSeconds(damageDelay);
            }

            if (hitCheckDelay > 0f)
            {
                yield return new WaitForSeconds(hitCheckDelay);
            }

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

            var result = new List<TankBrain>(targets);
            OnDamageDealt?.Invoke(this, result);

            m_SkillCoroutine = null;
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

        private void OnValidate()
        {
            teleportDelay = Mathf.Max(0f, teleportDelay);
            damageDelay = Mathf.Max(0f, damageDelay);
            hitCheckDelay = Mathf.Max(0f, hitCheckDelay);
        }
    }
}
