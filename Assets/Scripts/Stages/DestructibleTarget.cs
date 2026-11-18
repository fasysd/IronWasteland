using UnityEngine;
using IronWasteland.Enemies;

namespace IronWasteland.Stages
{
    /// <summary>
    /// Gan len mot vat the pha duoc trong man (Cum Nam Me, Tru Dien Truong, ...).
    ///
    /// Vat the nay VAN la EnemyBrain (theo quyet dinh Q2: vat the moi truong ke thua EnemyBrain)
    /// nen tank danh no nhu danh quai. Component nay chi lam mot viec: khi no chet thi bao
    /// cho StageDirector biet "muc tieu <targetId> da bi pha" de tinh objective DestroyTargets.
    ///
    /// Vi sao khong dem bang kill? Vi mot so man chi can pha 3 tru trong khi van co quai,
    /// neu dem kill thi khong phan biet duoc dau la tru.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyBrain))]
    public class DestructibleTarget : MonoBehaviour
    {
        [Header("Dinh danh")]
        [Tooltip("Trung voi ObjectiveSpec.TargetId trong StageDefinition, vi du: nest / tower.")]
        [SerializeField] private string targetId = "target";

        [Header("Ket noi")]
        [SerializeField] private StageDirector director;

        [Header("Giao dien (tuy chon)")]
        [SerializeField] private bool showHealthBar = true;
        [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 1.2f, 0f);

        private EnemyBrain m_Brain;
        private bool m_Reported;

        public string TargetId => targetId;
        public bool IsDestroyed => m_Brain == null || !m_Brain.IsAlive;

        /// <summary>0..1 - dung cho thanh mau noi bo.</summary>
        public float Health01
        {
            get
            {
                if (m_Brain == null || m_Brain.Stats == null || m_Brain.Stats.MaxHealth <= 0f) return 0f;
                return Mathf.Clamp01(m_Brain.CurrentHealth / m_Brain.Stats.MaxHealth);
            }
        }

        private void Awake()
        {
            m_Brain = GetComponent<EnemyBrain>();
            if (m_Brain != null) m_Brain.OnDeathEvent += OnTargetDied;
        }

        private void Start()
        {
            if (director == null) director = FindAnyObjectByType<StageDirector>();
        }

        private void OnDestroy()
        {
            if (m_Brain != null) m_Brain.OnDeathEvent -= OnTargetDied;
        }

        private void OnTargetDied()
        {
            if (m_Reported) return;
            m_Reported = true;

            if (director != null)
            {
                director.ReportTargetDestroyed(targetId);
            }
            else
            {
                Debug.LogWarning($"[DestructibleTarget] '{targetId}' bi pha nhung khong tim thay StageDirector.");
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.55f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.8f);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.4f, $"Pha duoc: {targetId}");
        }
#endif
    }
}
