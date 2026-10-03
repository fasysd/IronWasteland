using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Lop dieu khien Tank. Day la noi chua "logic game": chi so, di chuyen, dung 4 ky nang.
    /// No KHONG cham vao truc tiep sprite/animator, ma chi ra lenh cho TankModel.
    /// </summary>
    [DisallowMultipleComponent]
    public class TankBrain : MonoBehaviour
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 90;
        public const int SkillCount = 4;

        [Header("Model")]
        [Tooltip("Model tam dieu khien. Neu rong se tu tim trong hierarchy.")]
        [SerializeField] private TankModel model;

        [Header("Level")]
        [SerializeField, Range(MinLevel, MaxLevel)] private int level = MinLevel;

        [Header("Stats")]
        [SerializeField] private TankStats stats = new TankStats();

        [Header("Runtime")]
        [SerializeField] private float currentHealth;
        [SerializeField] private float currentEnergy;
        [SerializeField] private float[] skillCooldowns = new float[SkillCount];

        private Rigidbody2D m_Body;
        private Vector2 m_MoveInput;
        private Vector2 m_LookTarget;
        private bool m_HasLookTarget;

        public TankModel Model => model;
        public int Level => level;
        public TankStats Stats => stats;
        public float CurrentHealth => currentHealth;
        public float CurrentEnergy => currentEnergy;
        public float GetSkillCooldown(int index) => skillCooldowns[Mathf.Clamp(index, 0, SkillCount - 1)];

        #region Setup

        /// <summary>Do Factory gan Level + chi so va khoi tao mau/nang luong.</summary>
        public void Initialize(int level, TankStats stats)
        {
            this.level = Mathf.Clamp(level, MinLevel, MaxLevel);
            this.stats = stats != null ? stats : new TankStats();

            currentHealth = this.stats.MaxHealth;
            currentEnergy = this.stats.MaxEnergy;
            for (int i = 0; i < skillCooldowns.Length; i++) skillCooldowns[i] = 0f;
        }

        public void SetLevel(int level)
        {
            this.level = Mathf.Clamp(level, MinLevel, MaxLevel);
        }

        private void Awake()
        {
            if (model == null) model = GetComponentInChildren<TankModel>();

            // TankModel co the la prefab con => Awake cua no chay SAU Awake nay,
            // nen phai tu tim Rigidbody2D thay vi doc model.Body.
            m_Body = GetComponentInChildren<Rigidbody2D>();

            if (currentHealth <= 0f) currentHealth = stats.MaxHealth;
            if (currentEnergy <= 0f) currentEnergy = stats.MaxEnergy;
        }

        #endregion

        #region Input commands

        /// <summary>Nhan lenh di chuyen (Vector2) tu GameController.</summary>
        public void SetMoveInput(Vector2 input)
        {
            m_MoveInput = Vector2.ClampMagnitude(input, 1f);
        }

        /// <summary>Nhan lenh huong nhin. Point la vi tri muc tieu trong world (con tro chuot).</summary>
        public void SetLookTarget(Vector2 worldPoint)
        {
            m_LookTarget = worldPoint;
            m_HasLookTarget = true;
        }

        /// <summary>Tat action Look. Noi sung giu nguyen huong hien tai.</summary>
        public void ClearLookTarget()
        {
            m_HasLookTarget = false;
            model?.StopLook();
        }

        #endregion

        #region Gameplay

        private void Update()
        {
            TickCooldowns(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            // TODO: thay bang he thong di chuyen that su (dash, do loi, terrain...).
            if (m_Body != null)
            {
                m_Body.linearVelocity = m_MoveInput * stats.MoveSpeed;
            }

            if (model == null) return;

            // --- Action Move ---------------------------------------------------
            if (m_MoveInput.sqrMagnitude > 0.0001f)
            {
                model.Move(m_MoveInput.normalized);
            }
            else
            {
                model.Stop();
            }

            // --- Action Look: LUON chay, xoay noi sung ve con tro chuot -------
            if (m_HasLookTarget)
            {
                model.Look(m_LookTarget);
            }
        }

        /// <summary>Dung 1 trong 4 ky nang. Chi tai thoi diem nay moi co Debug.Log.</summary>
        public void UseSkill(int index)
        {
            if (index < 0 || index >= SkillCount) return;
            if (skillCooldowns[index] > 0f) return;

            // TODO: them logic tung ky nang (damage, buff, heal, dash...).

            Debug.Log($"[TankBrain] Level {level} su dung ky nang {index + 1}/{SkillCount}.");
            skillCooldowns[index] = GetCooldownWithReduction(GetBaseCooldown(index));
            model?.UseSkill(index);
        }

        /// <summary>Nhan sat thuong. TODO: ap dung Defense/ArmorPenetration.</summary>
        public void TakeDamage(float amount)
        {
            // TODO: tinh damage thuc te bang stats.Defense va stats.ArmorPenetration cua doi phuong.
            currentHealth -= amount;
        }

        public void Heal(float amount)
        {
            currentHealth = Mathf.Min(currentHealth + amount, stats.MaxHealth);
        }

        public void GainEnergy(float amount)
        {
            currentEnergy = Mathf.Min(currentEnergy + amount, stats.MaxEnergy);
        }

        public void SpendEnergy(float amount)
        {
            currentEnergy = Mathf.Max(currentEnergy - amount, 0f);
        }

        #endregion

        #region Helpers

        private void TickCooldowns(float deltaTime)
        {
            for (int i = 0; i < skillCooldowns.Length; i++)
            {
                if (skillCooldowns[i] > 0f)
                {
                    skillCooldowns[i] = Mathf.Max(0f, skillCooldowns[i] - deltaTime);
                }
            }
        }

        private float GetCooldownWithReduction(float baseCooldown)
        {
            return baseCooldown * (1f - Mathf.Clamp(stats.CooldownReduction, 0f, 1f));
        }

        private float GetBaseCooldown(int index)
        {
            // TODO: thay bang bang cooldown tung ky nang.
            return 5f;
        }

        #endregion
    }
}