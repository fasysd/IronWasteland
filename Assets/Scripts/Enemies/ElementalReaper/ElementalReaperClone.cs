using System;
using System.Collections;
using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Enemies.ElementalReaper.Hitboxes;
using IronWasteland.Enemies.ElementalReaper.VFX;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.ElementalReaper
{
    /// <summary>
    /// Bản sao Tử Thần Nguyên Tố (ElementalReaperClone) trong Tế Đàn Sub-Arena:
    /// - Sở hữu 25% Max HP của Boss, DEF 25, SPD 90 (0.9 unit/s).
    /// - Mỗi bản sao gắn với một hệ nguyên tố (Thủy / Độc / Hỏa) và chỉ thi triển 2 chiêu thức của hệ đó.
    /// - Tự động xoay mặt:
    ///   + Khi di chuyển: xoay theo hướng di chuyển.
    ///   + Khi tung chiêu: xoay về phía Tank mục tiêu.
    ///   + Khi tự buff hoặc không có mục tiêu: xoay về góc 0.
    /// - Khi bị tiêu diệt: báo cho ElementalAltarsSubArena để giải phóng Tank bị nhốt.
    /// </summary>
    [DisallowMultipleComponent]
    public class ElementalReaperClone : EnemyBrain
    {
        [Header("Elemental Identity")]
        [Tooltip("Hệ nguyên tố của Bản sao (Thủy/Độc/Hỏa).")]
        [SerializeField] private ElementalType element = ElementalType.Fire;

        [Header("Movement & Speed")]
        [Tooltip("Khoảng cách dừng tiếp cận Tank mục tiêu (m).")]
        [SerializeField, Min(0f)] private float stopDistance = 1.5f;

        [Tooltip("Độ nhạy khi đổi hướng di chuyển (mượt chuyển động).")]
        [SerializeField, Min(0.01f)] private float turnSharpness = 14f;

        [Tooltip("Hệ số chia tốc độ di chuyển để quy đổi MoveSpeed sang unit/s (MoveSpeed / 100). Mặc định 100.")]
        [SerializeField, Min(1f)] private float moveSpeedDivisor = 100f;

        [Header("Rotation & Facing")]
        [Tooltip("Góc bù độ lệch ban đầu của Sprite/Model (độ).")]
        [SerializeField] private float rotationOffset = 0f;

        [Tooltip("Tốc độ xoay (độ/giây). Giá trị <= 0 xoay tức thời.")]
        [SerializeField] private float rotationSpeed = 720f;

        [Tooltip("Transform mục tiêu xoay. Mặc định tự lấy Model hoặc chính GameObject.")]
        [SerializeField] private Transform rotationPivot;

        [Header("Skill Cooldowns")]
        [Tooltip("Hồi chiêu kỹ năng 1 của hệ (giây).")]
        [SerializeField, Min(0.1f)] private float skill1Cooldown = 7.0f;

        [Tooltip("Hồi chiêu kỹ năng 2 của hệ (giây).")]
        [SerializeField, Min(0.1f)] private float skill2Cooldown = 13.0f;

        [Header("Skill Prefabs (Inspector Configuration)")]
        [Tooltip("Prefab Sóng Thủy Triều (Thủy 1).")]
        [SerializeField] private GameObject thuyTrieuPrefab;

        [Tooltip("Prefab Thủy Liệu VFX (Thủy 2).")]
        [SerializeField] private GameObject thuyLieuPrefab;

        [Tooltip("Prefab Độc Quật (Độc 1).")]
        [SerializeField] private GameObject docQuatPrefab;

        [Tooltip("Prefab Loạn Chuyển Nổ Độc (Độc 2).")]
        [SerializeField] private GameObject loanChuyenPrefab;

        [Tooltip("Prefab Phun Dung Nham (Hỏa 1).")]
        [SerializeField] private GameObject phunDungNhamPrefab;

        [Tooltip("Prefab Lốc Xoáy Lửa (Hỏa 2).")]
        [SerializeField] private GameObject fireTornadoPrefab;

        private ElementalAltarsSubArena m_Arena;
        private TankBrain m_TargetTank;
        private Rigidbody2D m_Rigidbody;
        private Animator m_Animator;
        private Vector2 m_DesiredVelocity;

        private float m_Skill1Timer;
        private float m_Skill2Timer;

        public event Action<ElementalReaperClone> OnCloneDefeated;

        public ElementalType Element { get => element; set => element = value; }
        public TankBrain TargetTank => m_TargetTank;
        public ElementalAltarsSubArena Arena => m_Arena;
        public float Skill1Timer => m_Skill1Timer;
        public float Skill2Timer => m_Skill2Timer;

        public GameObject ThuyTrieuPrefab { get => thuyTrieuPrefab; set => thuyTrieuPrefab = value; }
        public GameObject ThuyLieuPrefab { get => thuyLieuPrefab; set => thuyLieuPrefab = value; }
        public GameObject DocQuatPrefab { get => docQuatPrefab; set => docQuatPrefab = value; }
        public GameObject LoanChuyenPrefab { get => loanChuyenPrefab; set => loanChuyenPrefab = value; }
        public GameObject PhunDungNhamPrefab { get => phunDungNhamPrefab; set => phunDungNhamPrefab = value; }
        public GameObject FireTornadoPrefab { get => fireTornadoPrefab; set => fireTornadoPrefab = value; }

        protected override void Awake()
        {
            base.Awake();
            m_Rigidbody = GetComponent<Rigidbody2D>();
            if (rotationPivot == null)
            {
                rotationPivot = Model != null ? Model.transform : transform;
            }
            if (Model != null && Model.Animator != null)
            {
                m_Animator = Model.Animator;
            }
            else
            {
                m_Animator = GetComponentInChildren<Animator>();
            }

            if (m_Rigidbody != null)
            {
                m_Rigidbody.bodyType = RigidbodyType2D.Kinematic;
                m_Rigidbody.gravityScale = 0f;
            }

            ApplyAltarTint();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (rotationPivot == null)
            {
                rotationPivot = Model != null ? Model.transform : transform;
            }
            ApplyAltarTint();
        }
#endif

        public void ApplyAltarTint()
        {
            SpriteRenderer sr = Model != null ? Model.SpriteRenderer : GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                Color tintColor;
                switch (element)
                {
                    case ElementalType.Water:
                        tintColor = new Color(0.25f, 0.75f, 1.0f, 1f); // Lam Thủy
                        break;
                    case ElementalType.Poison:
                        tintColor = new Color(0.3f, 1.0f, 0.35f, 1f); // Lục Độc
                        break;
                    case ElementalType.Fire:
                    default:
                        tintColor = new Color(1.0f, 0.4f, 0.2f, 1f); // Đỏ Cam Hỏa
                        break;
                }
                sr.color = tintColor;
            }
        }

        public void InitializeClone(ElementalAltarsSubArena arena, TankBrain targetTank, ElementalType cloneElement, EnemyStats cloneStats)
        {
            m_Arena = arena;
            m_TargetTank = targetTank;
            element = cloneElement;

            Initialize(cloneStats);
            ApplyAltarTint();

            // Cấu hình hồi chiêu chuẩn theo hệ
            switch (element)
            {
                case ElementalType.Water:
                    skill1Cooldown = 8.0f;  // ThuyTrieu
                    skill2Cooldown = 14.0f; // ThuyLieu
                    break;
                case ElementalType.Poison:
                    skill1Cooldown = 6.0f;  // DocQuat
                    skill2Cooldown = 12.0f; // LoanChuyen
                    break;
                case ElementalType.Fire:
                default:
                    skill1Cooldown = 7.0f;  // PhunDungNham
                    skill2Cooldown = 13.0f; // FireTornado
                    break;
            }

            m_Skill1Timer = skill1Cooldown * 0.5f;
            m_Skill2Timer = skill2Cooldown * 0.3f;
        }

        protected override void OnBrainTick(float deltaTime)
        {
            if (!IsAlive) return;

            UpdateKinematicMovement(deltaTime);
            UpdateSkills(deltaTime);
        }

        private void FixedUpdate()
        {
            if (!IsAlive || m_Rigidbody == null) return;

            if (m_Rigidbody.bodyType == RigidbodyType2D.Kinematic)
            {
                m_Rigidbody.MovePosition(m_Rigidbody.position + m_DesiredVelocity * Time.fixedDeltaTime);
            }
            else
            {
                m_Rigidbody.linearVelocity = m_DesiredVelocity;
            }
        }

        private void UpdateKinematicMovement(float deltaTime)
        {
            if (m_TargetTank == null || !m_TargetTank.IsAlive)
            {
                m_DesiredVelocity = Vector2.zero;
                Model?.SetMoveSpeed(0f);
                return;
            }

            Vector2 toTarget = (Vector2)m_TargetTank.transform.position - (Vector2)transform.position;
            float distance = toTarget.magnitude;

            Vector2 targetVelocity = Vector2.zero;
            if (distance > stopDistance)
            {
                float divisor = moveSpeedDivisor > 0f ? moveSpeedDivisor : 100f;
                float actualMoveSpeed = (Stats != null ? Stats.MoveSpeed : 90f) / divisor;
                targetVelocity = toTarget.normalized * actualMoveSpeed;
            }

            float t = 1f - Mathf.Exp(-turnSharpness * deltaTime);
            m_DesiredVelocity = Vector2.Lerp(m_DesiredVelocity, targetVelocity, t);
            Model?.SetMoveSpeed(m_DesiredVelocity.magnitude);

            if (m_DesiredVelocity.sqrMagnitude > 0.001f)
            {
                RotateTowardsDirection(m_DesiredVelocity, instant: false, dt: deltaTime);
            }
        }

        private void UpdateSkills(float deltaTime)
        {
            m_Skill1Timer -= deltaTime;
            m_Skill2Timer -= deltaTime;

            if (m_TargetTank == null || !m_TargetTank.IsAlive) return;

            switch (element)
            {
                case ElementalType.Water:
                    if (m_Skill1Timer <= 0f) CastThuyTrieu();
                    else if (m_Skill2Timer <= 0f) CastThuyLieu();
                    break;

                case ElementalType.Poison:
                    if (m_Skill1Timer <= 0f) CastDocQuat();
                    else if (m_Skill2Timer <= 0f) CastLoanChuyen();
                    break;

                case ElementalType.Fire:
                default:
                    if (m_Skill1Timer <= 0f) CastPhunDungNham();
                    else if (m_Skill2Timer <= 0f) CastFireTornado();
                    break;
            }
        }

        #region Skill Execution

        private void CastThuyTrieu()
        {
            m_Skill1Timer = skill1Cooldown;
            if (m_TargetTank == null) return;

            RotateTowardsTarget(m_TargetTank.transform.position, instant: true);
            if (m_Animator != null) m_Animator.SetTrigger("ThuyTrieu");

            if (thuyTrieuPrefab != null)
            {
                Vector2 dir = ((Vector2)m_TargetTank.transform.position - (Vector2)transform.position).normalized;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                GameObject go = Instantiate(thuyTrieuPrefab, transform.position, Quaternion.Euler(0f, 0f, angle));
                var hitbox = go.GetComponent<ThuyTrieuHitbox>();
                if (hitbox != null)
                {
                    hitbox.Initialize(this, Stats != null ? Stats.Attack : 250f);
                }
            }
        }

        private void CastThuyLieu()
        {
            m_Skill2Timer = skill2Cooldown;
            RotateToZero(instant: true);
            if (m_Animator != null) m_Animator.SetTrigger("ThuyLieu");

            // Tự hồi 5% Max HP
            Heal(MaxHealth * 0.05f);

            // Tăng 25% DEF trong 5s
            StartCoroutine(BuffDefenseTemporary(5.0f, 0.25f));

            if (thuyLieuPrefab != null)
            {
                GameObject go = Instantiate(thuyLieuPrefab, transform);
                go.transform.localPosition = Vector3.zero;
                var vfx = go.GetComponent<ThuyLieuVFX>();
                if (vfx != null) vfx.Play();
            }
        }

        private void CastDocQuat()
        {
            m_Skill1Timer = skill1Cooldown;
            if (m_TargetTank == null) return;

            Vector2 targetPos = m_TargetTank.transform.position;
            Vector2 dir = (targetPos - (Vector2)transform.position).normalized;
            RotateTowardsTarget(targetPos, instant: true);
            if (m_Animator != null) m_Animator.SetTrigger("DocQuat");

            if (docQuatPrefab != null)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                Quaternion rot = Quaternion.Euler(0f, 0f, angle);
                GameObject go = Instantiate(docQuatPrefab, transform.position, rot);
                var hitbox = go.GetComponent<DocQuatHitbox>();
                if (hitbox != null)
                {
                    hitbox.Initialize(this, Stats != null ? Stats.Attack : 250f);
                }
            }
        }

        private void CastLoanChuyen()
        {
            m_Skill2Timer = skill2Cooldown;
            if (m_TargetTank == null) return;

            Vector2 targetPos = m_TargetTank.transform.position;
            transform.position = targetPos;
            RotateTowardsTarget(targetPos, instant: true);
            if (m_Animator != null) m_Animator.SetTrigger("LoanChuyen");

            if (loanChuyenPrefab != null)
            {
                GameObject go = Instantiate(loanChuyenPrefab, targetPos, Quaternion.identity);
                var hitbox = go.GetComponent<LoanChuyenBurstHitbox>();
                if (hitbox != null)
                {
                    hitbox.Initialize(this, Stats != null ? Stats.Attack : 250f);
                }
            }
        }

        private void CastPhunDungNham()
        {
            m_Skill1Timer = skill1Cooldown;
            if (m_TargetTank == null) return;

            Vector2 dir = ((Vector2)m_TargetTank.transform.position - (Vector2)transform.position).normalized;
            RotateTowardsTarget(m_TargetTank.transform.position, instant: true);
            if (m_Animator != null) m_Animator.SetTrigger("PhunDungNham");

            if (phunDungNhamPrefab != null)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                GameObject go = Instantiate(phunDungNhamPrefab, transform.position, Quaternion.Euler(0f, 0f, angle));
                var hitbox = go.GetComponent<PhunDungNhamHitbox>();
                if (hitbox != null)
                {
                    hitbox.Initialize(this, Stats != null ? Stats.Attack : 250f);
                }
            }
        }

        private void CastFireTornado()
        {
            m_Skill2Timer = skill2Cooldown;
            if (m_TargetTank == null) return;

            Vector2 targetPos = m_TargetTank.transform.position;
            RotateTowardsTarget(targetPos, instant: true);
            if (m_Animator != null) m_Animator.SetTrigger("HoaToc_Charge");

            if (fireTornadoPrefab != null)
            {
                GameObject go = Instantiate(fireTornadoPrefab, targetPos, Quaternion.identity);
                var zone = go.GetComponent<FireTornadoZone>();
                if (zone != null)
                {
                    zone.Initialize(this, Stats != null ? Stats.Attack : 250f);
                }
            }
        }

        private IEnumerator BuffDefenseTemporary(float duration, float ratio)
        {
            if (Stats == null) yield break;
            float originalDef = Stats.Defense;
            Stats.Defense += originalDef * ratio;
            yield return new WaitForSeconds(duration);
            if (Stats != null)
            {
                Stats.Defense = originalDef;
            }
        }

        #endregion

        #region Rotation

        public void RotateTowardsDirection(Vector2 direction, bool instant = false, float dt = -1f)
        {
            Transform pivot = GetRotationPivot();
            if (pivot == null) return;

            if (direction.sqrMagnitude < 0.0001f)
            {
                RotateToZero(instant);
                return;
            }

            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + rotationOffset;
            ApplyRotationAngle(pivot, targetAngle, instant, dt);
        }

        public void RotateTowardsTarget(Vector2 targetPosition, bool instant = false)
        {
            Vector2 dir = targetPosition - (Vector2)transform.position;
            RotateTowardsDirection(dir, instant);
        }

        public void RotateToZero(bool instant = false)
        {
            Transform pivot = GetRotationPivot();
            if (pivot == null) return;

            ApplyRotationAngle(pivot, 0f, instant);
        }

        private Transform GetRotationPivot()
        {
            if (rotationPivot != null) return rotationPivot;
            if (Model != null) return Model.transform;
            return transform;
        }

        private void ApplyRotationAngle(Transform pivot, float targetAngle, bool instant, float dt = -1f)
        {
            if (instant || rotationSpeed <= 0f)
            {
                pivot.rotation = Quaternion.Euler(0f, 0f, targetAngle);
                return;
            }

            float actualDt = dt > 0f ? dt : Time.deltaTime;
            float currentAngle = pivot.eulerAngles.z;
            float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, rotationSpeed * actualDt);
            pivot.rotation = Quaternion.Euler(0f, 0f, newAngle);
        }

        #endregion

        protected override void OnDeath()
        {
            OnCloneDefeated?.Invoke(this);
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }
}
