using System;
using UnityEngine;

namespace IronWasteland.Tanks.BlackShadow
{
    /// <summary>
    /// Hiệu ứng tàn hỏa đen (OptionalSkill1Orb) sinh ra tại vị trí kẻ địch ngã xuống,
    /// tự động tăng tốc bay và hút về thân xe tank khi kỹ năng cường hóa (OptionalSkill1) được kích hoạt.
    /// Khi chạm vào thân xe thì tự thu hồi về Object Pool, không cần xử lý thêm gì.
    /// </summary>
    [DisallowMultipleComponent]
    public class OptionalSkill1Orb : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Vận tốc ban đầu khi bắt đầu bay về thân xe.")]
        [SerializeField, Min(0f)] private float initialSpeed = 2f;

        [Tooltip("Vận tốc tối đa khi bị hút về thân xe.")]
        [SerializeField, Min(0f)] private float maxSpeed = 16f;

        [Tooltip("Gia tốc hút về (đơn vị/s²).")]
        [SerializeField, Min(0f)] private float acceleration = 25f;

        [Tooltip("Khoảng cách tối thiểu tới thân xe để tính là đã chạm đích.")]
        [SerializeField, Min(0.05f)] private float reachDistance = 0.4f;

        [Tooltip("Thời gian tồn tại tối đa phòng trường hợp mục tiêu bị hủy hoặc mất dấu.")]
        [SerializeField, Min(0.5f)] private float maxLifeTime = 5f;

        [Header("Visual Juice")]
        [Tooltip("Văng nhẹ một khoảng ngẫu nhiên ban đầu trước khi bị lực hút kéo về.")]
        [SerializeField] private bool popOutAtStart = true;

        [Tooltip("Thời gian bung nhẹ ban đầu (giây).")]
        [SerializeField, Min(0.01f)] private float popDuration = 0.15f;

        [Tooltip("Lực bung nhẹ ban đầu.")]
        [SerializeField, Min(0f)] private float popForce = 3f;

        private Transform targetTransform;
        private Action<OptionalSkill1Orb> returnToPool;

        private float currentSpeed;
        private float elapsedTime;
        private bool isInitialized;
        private bool isPopping;
        private float popTimer;
        private Vector2 popVelocity;

        private SpriteRenderer spriteRenderer;
        private TrailRenderer trailRenderer;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            trailRenderer = GetComponent<TrailRenderer>();
            if (trailRenderer == null)
                trailRenderer = GetComponentInChildren<TrailRenderer>();
        }

        private void Start()
        {
            // Hỗ trợ trường hợp kéo thả trực tiếp vào scene để test mà không qua TankBrain
            if (!isInitialized && targetTransform == null)
            {
                var tank = FindAnyObjectByType<IronWasteland.Tanks.TankBrain>();
                if (tank != null)
                {
                    Initialize(transform.position, tank.transform);
                }
            }
        }

        private void Update()
        {
            if (!isInitialized)
                return;

            elapsedTime += Time.deltaTime;
            if (elapsedTime >= maxLifeTime || targetTransform == null)
            {
                FinishOrb();
                return;
            }

            // Giai đoạn 1: Bung nhẹ ra ngoài từ vị trí kẻ địch chết để tạo hiệu ứng tự nhiên
            if (isPopping)
            {
                popTimer -= Time.deltaTime;
                transform.position += (Vector3)(popVelocity * Time.deltaTime);
                popVelocity = Vector2.MoveTowards(popVelocity, Vector2.zero, popForce * 2.5f * Time.deltaTime);

                if (popTimer <= 0f)
                {
                    isPopping = false;
                }
                return;
            }

            // Giai đoạn 2: Tăng tốc và bị hút về phía thân xe
            Vector3 targetPosition = targetTransform.position;
            Vector2 toTarget = targetPosition - transform.position;
            float distance = toTarget.magnitude;

            // Đã chạm đến thân xe -> kết thúc
            if (distance <= reachDistance)
            {
                FinishOrb();
                return;
            }

            Vector2 direction = toTarget / distance;
            currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * Time.deltaTime);
            transform.position += (Vector3)(direction * (currentSpeed * Time.deltaTime));

            // Xoay đầu tàn hỏa theo hướng bay
            if (direction.sqrMagnitude > 0.001f)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        /// <summary>
        /// Khởi tạo tàn hỏa đen tại vị trí spawn và bắt đầu hút về phía thân xe (target).
        /// </summary>
        public void Initialize(Vector2 spawnPosition, Transform target, Action<OptionalSkill1Orb> returnToPool = null)
        {
            Clear();

            // Đảm bảo GameObject đang active để Update chạy ngay
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            transform.position = new Vector3(spawnPosition.x, spawnPosition.y, 0f);
            this.targetTransform = target;
            this.returnToPool = returnToPool;
            this.currentSpeed = initialSpeed;
            this.elapsedTime = 0f;
            this.isInitialized = true;

            if (trailRenderer != null)
            {
                trailRenderer.Clear();
            }

            if (popOutAtStart)
            {
                Vector2 randomDirection = UnityEngine.Random.insideUnitCircle.normalized;
                if (randomDirection.sqrMagnitude < 0.01f)
                    randomDirection = Vector2.up;

                popVelocity = randomDirection * popForce;
                popTimer = popDuration;
                isPopping = true;
            }
            else
            {
                isPopping = false;
            }
        }

        /// <summary>
        /// Xóa sạch trạng thái để tái sử dụng trong Object Pool.
        /// </summary>
        public void Clear()
        {
            isInitialized = false;
            isPopping = false;
            targetTransform = null;
            currentSpeed = 0f;
            elapsedTime = 0f;
            popTimer = 0f;
            popVelocity = Vector2.zero;

            if (trailRenderer != null)
            {
                trailRenderer.Clear();
            }
        }

        private void FinishOrb()
        {
            Action<OptionalSkill1Orb> callback = returnToPool;
            Clear();

            if (callback != null)
            {
                callback(this);
            }
            else if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            Clear();
        }
    }
}
