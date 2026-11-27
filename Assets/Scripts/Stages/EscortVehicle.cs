using System.Collections.Generic;
using UnityEngine;
using IronWasteland.Enemies;
using IronWasteland.Tanks;
using Unity.Netcode;

namespace IronWasteland.Stages
{
    /// <summary>
    /// Xe ho tong (Xe Tu Binh Mones o man 1-3, Xe Pha Thanh o man 2-3).
    ///
    /// Nhiem vu: tu chay theo lo trinh, bao tien do ve StageDirector, va co the bi quai pha.
    /// Vi AI cua quai trong du an chi nham vao TankBrain, xe nay tu quet quai xung quanh
    /// va tu tru mau de mo phong "bi tan cong" - neu khong xe se khong bao gio chet
    /// va objective ho tong khong con y nghia.
    ///
    /// Cach dung trong scene:
    ///   1. Tao GameObject + component nay (+ SpriteRenderer de nhin thay).
    ///   2. Keo cac waypoint vao list (toa do world, di tu phan tu 0).
    ///   3. Gan Director (hoac de trong - tu tim StageDirector trong scene).
    ///   4. Trong StageDefinition: objectives = Escort, Count = so met can di, TargetId = escortId.
    /// </summary>
    [DisallowMultipleComponent]
    public class EscortVehicle : MonoBehaviour
    {
        [Header("Dinh danh")]
        [Tooltip("Trung voi ObjectiveSpec.TargetId cua objective Escort, vi du: mones / ram.")]
        [SerializeField] private string escortId = "mones";
        [SerializeField] private string displayName;
        [Tooltip("Nếu có, xe chỉ xuất phát sau khi trạm radar này được kích hoạt.")]
        [SerializeField] private RadarStation startRadar;

        [Header("Lo trinh")]
        [Tooltip("Cac diem can di qua (toa do world). Di tu phan tu 0 den het.")]
        [SerializeField] private List<Vector2> waypoints = new List<Vector2>();
        [SerializeField] private float speed = 2f;
        [SerializeField] private float arriveDistance = 0.15f;
        [Tooltip("Xe chờ khi người chơi ở quá xa. 0 = xe luôn tiếp tục đi.")]
        [SerializeField, Min(0f)] private float maxPlayerDistance;

        [Header("Sinh menh")]
        [SerializeField] private float maxHealth = 2000f;
        [SerializeField] private float currentHealth = 2000f;

        [Header("Bi quai tan cong")]
        [Tooltip("Ban kinh quet quai de tru mau (mo phong quai danh vao xe).")]
        [SerializeField] private float enemyDamageRadius = 1.6f;
        [SerializeField] private float enemyDamageInterval = 1f;
        [Tooltip("He so nhan vao Attack cua quai moi lan tru mau. 0.1 = 8 quai quanh xe tru ~120 HP/giay.")]
        [SerializeField] private float enemyDamageScale = 0.1f;

        [Header("Buff dong doi")]
        [Tooltip("Tank o trong ban kinh nay thi xe chay nhanh hon (GDD: +20%).")]
        [SerializeField] private float tankBonusRadius = 5f;
        [SerializeField] private float tankBonusMultiplier = 1.2f;

        [Header("Ket noi")]
        [SerializeField] private StageDirector director;
        [SerializeField] private bool logEvents = true;

        private float m_Travelled;
        private float m_PathLength;
        private int m_NextWaypoint;
        private float m_DamageTimer;
        private float m_ReportTimer;
        private bool m_Destroyed;
        private bool m_Arrived;
        private Transform m_Tank;

        public string EscortId => escortId;
        public string DisplayName => displayName;
        public IReadOnlyList<Vector2> Waypoints => waypoints;
        public float Health01 => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        public float Travelled => m_Travelled;
        public bool IsDestroyed => m_Destroyed;

        /// <summary>Xe da di het lo trinh: tu day khong con bi quai pha nua.</summary>
        public bool HasArrived => m_Arrived;
        public bool IsWaitingForRadar => startRadar != null && !startRadar.IsActivated;
        public bool IsWaitingForPlayer { get; private set; }

        public string StatusLabel => m_Destroyed ? "BỊ PHÁ HỦY" :
            m_Arrived ? "ĐÃ TỚI ĐÍCH" :
            IsWaitingForRadar ? "CHỜ RADAR 1" :
            IsWaitingForPlayer ? "CHỜ TANK ÁP SÁT" : "ĐANG HỘ TỐNG";

        private void Awake()
        {
            if (currentHealth <= 0f) currentHealth = maxHealth;
            if (director == null) director = FindAnyObjectByType<StageDirector>();
            m_PathLength = ComputePathLength();
        }

        /// <summary>Tong do dai lo trinh - dung de chot tien do khi xe toi dich.</summary>
        private float ComputePathLength()
        {
            float total = 0f;
            for (int i = 1; i < waypoints.Count; i++) total += Vector2.Distance(waypoints[i - 1], waypoints[i]);
            return total;
        }

        private void Start()
        {
            // Neu co waypoint thi dat xe dung tai diem dau tien.
            if (waypoints.Count > 0) transform.position = waypoints[0];
            ReportProgress(force: true);
        }

        private void Update()
        {
            if (m_Destroyed || IsWaitingForRadar) return;

            float playerDistance = DistanceToNearestPlayer();
            IsWaitingForPlayer = !m_Arrived && maxPlayerDistance > 0f && playerDistance > maxPlayerDistance;
            if (!IsWaitingForPlayer) TickMovement(playerDistance);
            TickEnemyDamage();
            TickProgressReport();
        }

        private void TickMovement(float playerDistance)
        {
            if (m_NextWaypoint >= waypoints.Count)
            {
                if (!m_Arrived)
                {
                    m_Arrived = true;
                    // Xe dung cach waypoint cuoi mot khoang nho (arriveDistance) nen tien do bi hut;
                    // chot len dung bang do dai lo trinh de objective ho tong duoc tinh la xong.
                    m_Travelled = Mathf.Max(m_Travelled, m_PathLength);
                    ReportProgress(force: true);
                    if (logEvents) Debug.Log($"[EscortVehicle] {escortId} da toi dich sau {m_Travelled:0.0} m.");
                }
                return;
            }

            Vector2 target = waypoints[m_NextWaypoint];
            Vector2 pos = transform.position;
            Vector2 delta = target - pos;

            if (delta.magnitude <= arriveDistance)
            {
                m_NextWaypoint++;
                return;
            }

            float speedMultiplier = playerDistance <= tankBonusRadius ? tankBonusMultiplier : 1f;
            float step = speed * speedMultiplier * Time.deltaTime;
            Vector2 move = Vector2.ClampMagnitude(delta, step);
            transform.position = pos + move;
            m_Travelled += move.magnitude;

            // Xoay ve huong di (neu co sprite).
            if (move.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(move.y, move.x) * Mathf.Rad2Deg - 90f;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private float DistanceToNearestPlayer()
        {
            if (maxPlayerDistance > 0f && TankBrain.NetworkSessionActive && NetworkManager.Singleton.IsServer)
            {
                float nearest = float.PositiveInfinity;
                foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    if (client.PlayerObject == null ||
                        !client.PlayerObject.TryGetComponent(out TankBrain tank) || !tank.IsAlive) continue;
                    nearest = Mathf.Min(nearest, Vector2.Distance(transform.position, tank.transform.position));
                }
                return nearest;
            }

            if (m_Tank == null)
            {
                GameController gc = FindAnyObjectByType<GameController>();
                if (gc != null && gc.PlayerTank != null) m_Tank = gc.PlayerTank.transform;
            }

            return m_Tank != null ? Vector2.Distance(transform.position, m_Tank.position) : float.PositiveInfinity;
        }

        private void TickEnemyDamage()
        {
            // Da toi dich thi khong con bi tan cong nua (nhiem vu ho tong da xong).
            if (m_Arrived) return;

            m_DamageTimer += Time.deltaTime;
            if (m_DamageTimer < enemyDamageInterval) return;
            m_DamageTimer = 0f;

            EnemyBrain[] enemies = FindObjectsByType<EnemyBrain>(FindObjectsInactive.Exclude);
            float damage = 0f;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyBrain enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive) continue;
                if (Vector2.Distance(enemy.transform.position, transform.position) > enemyDamageRadius) continue;
                damage += (enemy.Stats != null ? enemy.Stats.Attack : 10f) * enemyDamageScale;
            }

            if (damage > 0f) TakeDamage(damage);
        }

        private void TickProgressReport()
        {
            m_ReportTimer += Time.deltaTime;
            if (m_ReportTimer < 0.25f) return;
            m_ReportTimer = 0f;
            ReportProgress(force: false);
        }

        private void ReportProgress(bool force)
        {
            if (director == null) return;
            director.ReportEscortProgress(escortId, m_Travelled);
        }

        /// <summary>Quai hoac vat the khac gay sat thuong len xe.</summary>
        public void TakeDamage(float amount)
        {
            if (m_Destroyed || amount <= 0f) return;

            currentHealth -= amount;
            if (logEvents) Debug.Log($"[EscortVehicle] {escortId} -{amount:0} HP con {Mathf.Max(0f, currentHealth):0}/{maxHealth:0}");

            if (currentHealth <= 0f)
            {
                currentHealth = 0f;
                m_Destroyed = true;
                if (director != null) director.ReportEscortDestroyed(escortId);
                if (logEvents) Debug.Log($"[EscortVehicle] {escortId} bi pha huy.");
                gameObject.SetActive(false);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.85f, 1f, 0.9f);
            for (int i = 0; i < waypoints.Count - 1; i++) Gizmos.DrawLine(waypoints[i], waypoints[i + 1]);
            for (int i = 0; i < waypoints.Count; i++) Gizmos.DrawWireSphere(waypoints[i], 0.25f);

            Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, enemyDamageRadius);

            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, tankBonusRadius);

            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f, $"Ho tong: {escortId}");
        }
#endif
    }
}
