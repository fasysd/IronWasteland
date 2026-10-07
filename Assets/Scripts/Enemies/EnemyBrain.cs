using UnityEngine;

namespace IronWasteland.Enemies
{
    /// <summary>
    /// Lop cha (abstract) cua moi ke dich. Day la noi chua LOGIC + RUNTIME STATE:
    /// - bang chi so (EnemyStats)
    /// - mau hien tai
    ///
    /// KHONG xu ly visual / va cham truc tiep - do EnemyModel (con) phu trach;
    /// su kien va cham duoc Model forward len day qua C# event.
    ///
    /// KY NANG: hoan toan do LOP CON quan ly. NormalEnemyBrain / EliteEnemyBrain
    /// tu tao field CD / timer va method thi trien rieng cho ky nang cua minh.
    /// EnemyBrain khong chua gi ve skill.
    ///
    /// Lop con bat buoc cai dat:
    /// - <see cref="OnBrainTick(float)"/>: logic moi frame (AI, di chuyen...).
    /// - <see cref="OnDeath()"/>: xu ly khi chet.
    /// Hook tuy chon: <see cref="OnDamaged(float)"/>, <see cref="OnContact"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class EnemyBrain : MonoBehaviour
    {
        [Header("Model")]
        [Tooltip("Model con (visual + collider). Tu tim trong hierarchy neu de trong.")]
        [SerializeField] private EnemyModel model;

        [Header("Info")]
        [Tooltip("Ten hien thi cho UI / Debug.")]
        [SerializeField] private string displayName;

        [Header("Stats")]
        [Tooltip("Chi so cua ke dich (Attack / MaxHealth / Defense / MoveSpeed).")]
        [SerializeField] private EnemyStats stats = new EnemyStats();

        [Header("Runtime")]
        [SerializeField] private float currentHealth;

        private bool m_Initialized;
        private bool m_Dead;

        public EnemyModel Model => model;
        public string DisplayName => displayName;

        /// <summary>Chi so hien tai (chinh sua truc tiep tren prefab).</summary>
        public EnemyStats Stats => stats;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => stats != null ? stats.MaxHealth : 0f;
        public bool IsAlive => !m_Dead && currentHealth > 0f;

        #region Setup

        /// <summary>
        /// Khoi tao ke dich: gan chi so, day mau, ket noi Model.
        /// Goi 1 lan tu spawner sau khi spawn (neu khong, Awake tu khoi tao voi chi so tren prefab).
        /// </summary>
        public virtual void Initialize(EnemyStats overrideStats = null)
        {
            if (overrideStats != null) stats = overrideStats;
            if (stats == null) stats = new EnemyStats();

            currentHealth = stats.MaxHealth;
            m_Dead = false;
            m_Initialized = true;

            if (model == null) model = GetComponentInChildren<EnemyModel>();
            model?.RegisterOwner(this);
            model?.PlaySpawn();
        }

        #endregion

        #region Combat

        /// <summary>Nhan sat thuong. TODO: ap dung Defense (giam sat thuong nhan tu Tank).</summary>
        public virtual void TakeDamage(float amount)
        {
            if (!IsAlive) return;

            // TODO: tinh sat thuong thuc te bang stats.Defense khi lam logic that.
            currentHealth -= amount;
            model?.PlayHit();

            OnDamaged(amount);

            if (currentHealth <= 0f)
            {
                currentHealth = 0f;
                Die();
            }
        }

        public virtual void Heal(float amount)
        {
            if (!IsAlive) return;
            currentHealth = Mathf.Min(currentHealth + amount, MaxHealth);
        }

        protected virtual void Die()
        {
            if (m_Dead) return;
            m_Dead = true;

            model?.PlayDeath();
            OnDeath();
        }

        #endregion

        #region Lifecycle

        private void Awake()
        {
            if (model == null) model = GetComponentInChildren<EnemyModel>();
            model?.RegisterOwner(this);

            // Chua ai goi Initialize (prefab dat truc tiep vao scene de debug) -> khoi tao mac dinh.
            if (!m_Initialized) Initialize();
        }

        private void OnEnable()
        {
            if (model != null) model.Contact += HandleModelContact;
        }

        private void OnDisable()
        {
            if (model != null) model.Contact -= HandleModelContact;
        }

        private void Update()
        {
            if (!IsAlive) return;

            OnBrainTick(Time.deltaTime);
        }

        // Va cham do EnemyModel chuyen tiep - Brain chi xu ly nghia logic.
        private void HandleModelContact(ColliderOwnerRef other) => OnContact(other);

        #endregion

        #region Hooks - lop con cai dat

        /// <summary>Goi moi frame khi ke dich con song (AI, di chuyen...). Bat buoc.</summary>
        protected abstract void OnBrainTick(float deltaTime);

        /// <summary>Goi 1 lan khi ke dich chet. Bat buoc.</summary>
        protected abstract void OnDeath();

        /// <summary>Goi moi lan nhan sat thuong (truoc khi kiem tra chet).</summary>
        protected virtual void OnDamaged(float amount) { }

        /// <summary>Su kien va cham do EnemyModel chuyen tiep. TODO: xu ly va cham.</summary>
        protected virtual void OnContact(ColliderOwnerRef other) { }

        #endregion

        protected virtual void OnValidate()
        {
            if (model == null) model = GetComponentInChildren<EnemyModel>();
            if (stats == null) stats = new EnemyStats();
            if (string.IsNullOrEmpty(displayName)) displayName = name;
        }
    }
}