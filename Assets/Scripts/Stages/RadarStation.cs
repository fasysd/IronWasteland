using UnityEngine;
using IronWasteland.Enemies;
using IronWasteland.Tanks;

namespace IronWasteland.Stages
{
    /// <summary>
    /// Tram Radar (man 1-3): tank dung trong vung 3 m va giu 5 giay, sau khi da don sach quai
    /// quanh do, thi tram duoc kich hoat.
    ///
    /// Cach dung trong scene:
    ///   1. Tao GameObject + component nay tai vi tri tram (them SpriteRenderer neu can nhin).
    ///   2. Gan Director (hoac de trong - tu tim).
    ///   3. Trong StageDefinition: objectives = Radar, Count = so tram can kich hoat,
    ///      TargetId = radarId cua tram.
    /// </summary>
    [DisallowMultipleComponent]
    public class RadarStation : MonoBehaviour
    {
        [Header("Dinh danh")]
        [Tooltip("Trung voi ObjectiveSpec.TargetId cua objective Radar, vi du: radar_1.")]
        [SerializeField] private string radarId = "radar_1";
        [SerializeField] private string displayName;
        [Tooltip("Vị trí của trạm để người chơi biết đường tới.")]
        [SerializeField] private string locationHint;

        [Header("Dieu kien kich hoat")]
        [Tooltip("Ban kinh tank phai dung trong (met).")]
        [SerializeField] private float radius = 3f;
        [Tooltip("Thoi gian phai dung lien tuc (giay).")]
        [SerializeField] private float holdTime = 5f;
        [Tooltip("Bat: phai don sach quai trong ban kinh kiem tra truoc khi tram chay.")]
        [SerializeField] private bool requireNoEnemies = true;
        [SerializeField] private float enemyCheckRadius = 8f;

        [Header("Ket noi")]
        [SerializeField] private StageDirector director;
        [SerializeField] private bool logEvents = true;

        private float m_Hold;
        private bool m_Activated;
        private Transform m_Tank;

        public string RadarId => radarId;
        public string DisplayName => displayName;
        public bool HasStartedActivation { get; private set; }
        public bool IsTankInRange { get; private set; }
        public bool IsBlockedByEnemies { get; private set; }
        public bool IsActivated => m_Activated;

        public string Instruction
        {
            get
            {
                if (IsActivated) return "Đã bật";
                if (IsBlockedByEnemies) return "Dọn quái quanh radar";
                if (IsTankInRange) return $"Giữ vị trí · {Mathf.CeilToInt(Mathf.Max(0f, holdTime - m_Hold))} giây";
                return $"Đến {locationHint}, đứng yên {Mathf.CeilToInt(holdTime)} giây";
            }
        }

        /// <summary>0..1 - dung cho thanh tien do tren HUD/gizmo.</summary>
        public float Hold01 => holdTime > 0f ? Mathf.Clamp01(m_Hold / holdTime) : 1f;

        private void Awake()
        {
            if (director == null) director = FindAnyObjectByType<StageDirector>();
        }

        private void Update()
        {
            if (m_Activated) return;

            if (m_Tank == null)
            {
                GameController gc = FindAnyObjectByType<GameController>();
                if (gc != null && gc.PlayerTank != null) m_Tank = gc.PlayerTank.transform;
            }

            IsTankInRange = m_Tank != null && Vector2.Distance(m_Tank.position, transform.position) <= radius;
            IsBlockedByEnemies = IsTankInRange && requireNoEnemies && EnemiesNearby();
            if (!IsTankInRange || IsBlockedByEnemies)
            {
                m_Hold = 0f;
                return;
            }

            HasStartedActivation = true;
            m_Hold += Time.deltaTime;
            if (logEvents && m_Hold > 0f && Mathf.FloorToInt(m_Hold * 2f) != Mathf.FloorToInt((m_Hold - Time.deltaTime) * 2f))
                Debug.Log($"[RadarStation] {radarId}: giu tram {m_Hold:0.0}/{holdTime:0.0}s");

            if (m_Hold >= holdTime) Activate();
        }

        private bool EnemiesNearby()
        {
            EnemyBrain[] enemies = FindObjectsByType<EnemyBrain>(FindObjectsInactive.Exclude);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyBrain enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive) continue;
                if (Vector2.Distance(enemy.transform.position, transform.position) <= enemyCheckRadius) return true;
            }
            return false;
        }

        private void Activate()
        {
            m_Activated = true;
            if (logEvents) Debug.Log($"[RadarStation] {radarId} DA KICH HOAT.");
            if (director != null) director.ReportRadarActivated(radarId);
            else Debug.LogWarning($"[RadarStation] '{radarId}' kich hoat nhung khong tim thay StageDirector.");
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = m_Activated ? new Color(0.3f, 1f, 0.5f, 0.8f) : new Color(0.4f, 0.75f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, radius);

            if (requireNoEnemies)
            {
                Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.25f);
                Gizmos.DrawWireSphere(transform.position, enemyCheckRadius);
            }

            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.4f, $"Radar: {radarId}");
        }
#endif
    }
}
