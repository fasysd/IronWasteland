using IronWasteland.Tanks;
using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Kỹ năng Tùy Chọn 1 - Biến thể 3: Shadow Guard (Bóng Ma Bảo Vệ).
/// Bao phủ đồng minh (hoặc bản thân) bằng một bóng ma bảo vệ xoay quanh.
/// Khi chịu đòn tấn công từ kẻ địch, bóng này sẽ lập tức triệt tiêu hoàn toàn
/// đòn đánh đó, bùng tan thành làn khói đen rồi biến mất.
/// </summary>
[DisallowMultipleComponent]
public class OptionalSkill1_3 : MonoBehaviour, ITankShield
{
    public event Action<bool> OnShieldChange;
    public event Action OnShieldBreak;

    [Header("Cài Đặt Thời Gian & Mục Tiêu")]
    [Tooltip("Thời gian duy trì khiên bảo vệ (giây).")]
    [SerializeField, Min(0.1f)] private float duration = 10f;

    [Tooltip("Bán kính tìm kiếm đồng minh xung quanh vị trí con trỏ chuột.")]
    [SerializeField, Min(0.1f)] private float selectRadius = 1.0f;

    [Tooltip("Cho phép tự tạo khiên cho bản thân nếu không trỏ vào đồng minh nào.")]
    [SerializeField] private bool canTargetSelf = true;

    [Header("Trạng Thái Khiên")]
    [Tooltip("Trạng thái khiên có đang hoạt động hay không.")]
    public bool isShield = false;

    private float remainingTime = 0f;
    private bool isInvoke = false;
    private TankBrain targetTank;

    [Header("Hiển Thị Hình Ảnh (Visuals)")]
    [Tooltip("Kéo thả GameObject hiển thị bóng ma bảo vệ vào đây (VisualGuard).")]
    [SerializeField] private GameObject visualGuard;

    [Tooltip("Kéo thả GameObject hiển thị khói đen bùng tan vào đây (VisualBurst).")]
    [SerializeField] private GameObject visualBurst;

    [Tooltip("Bán kính bóng ma xoay quanh thân xe.")]
    [SerializeField, Min(0.1f)] private float orbitRadius = 1.6f;

    [Tooltip("Tốc độ xoay quanh thân xe (độ/giây).")]
    [SerializeField] private float orbitSpeed = 150f;

    [Tooltip("Thời gian hiển thị làn khói bùng tan khi đỡ đòn (giây).")]
    [SerializeField, Min(0.05f)] private float burstDuration = 0.4f;

    public float RemainingTime => remainingTime;
    public float Duration => duration;
    public bool IsShield => isShield;
    public bool IsActive => isShield && remainingTime > 0f;
    public TankBrain TargetTank => targetTank;
    public GameObject VisualGuard => visualGuard;
    public GameObject VisualBurst => visualBurst;

    private float currentAngle;

    private void Awake()
    {
        if (visualGuard != null)
        {
            visualGuard.SetActive(false);
        }

        if (visualBurst != null)
        {
            visualBurst.SetActive(false);
        }
    }

    /// <summary>
    /// Kích hoạt khiên bảo vệ cho xe mục tiêu chỉ định.
    /// </summary>
    public void Initialize(TankBrain target)
    {
        if (target == null) return;

        // Gỡ khiên cũ nếu mục tiêu trước đó còn khiên của kỹ năng này
        if (targetTank != null && ReferenceEquals(targetTank.Shield, this))
        {
            targetTank.Shield = null;
        }

        targetTank = target;
        remainingTime = duration;
        isShield = true;
        isInvoke = false;

        targetTank.Shield = this;

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (visualGuard != null)
        {
            currentAngle = 0f;
            Vector3 offset = new Vector3(orbitRadius, 0f, 0f);
            visualGuard.transform.position = targetTank.transform.position + offset;
            visualGuard.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
            visualGuard.SetActive(true);
        }

        OnShieldChange?.Invoke(true);
    }

    /// <summary>
    /// Kích hoạt khiên bằng vị trí con trỏ chuột.
    /// Nếu trỏ vào đồng minh -> tạo khiên cho đồng minh.
    /// Nếu không trỏ vào đồng minh và canTargetSelf = true -> tự tạo khiên cho bản thân.
    /// </summary>
    public bool Initialize(TankBrain owner, Vector2 targetWorldPos)
    {
        if (owner == null) return false;

        if (targetWorldPos == Vector2.zero && owner.HasLookTarget)
        {
            targetWorldPos = owner.LookTarget;
        }

        // 1. Ưu tiên tìm xe đồng minh dưới con trỏ chuột
        if (TryFindAllyAt(targetWorldPos, owner, out TankBrain foundAlly))
        {
            Initialize(foundAlly);
            return true;
        }

        // 2. Nếu không trỏ vào đồng minh nào: tự tạo khiên cho bản thân
        if (canTargetSelf && owner.IsAlive)
        {
            Initialize(owner);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Tìm xe tăng đồng minh (TankBrain khác chủ sở hữu và còn sống) xung quanh vị trí chuột.
    /// </summary>
    public bool TryFindAllyAt(Vector2 targetPos, TankBrain owner, out TankBrain ally)
    {
        ally = null;
        Collider2D[] colliders = Physics2D.OverlapCircleAll(targetPos, selectRadius);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D col = colliders[i];
            if (col == null) continue;

            TankBrain target = col.GetComponentInParent<TankBrain>();
            if (target != null && target != owner && target.IsAlive)
            {
                ally = target;
                return true;
            }
        }

        return false;
    }

    private void Update()
    {
        // Đếm ngược thời gian khiên tồn tại
        if (remainingTime > 0)
        {
            isInvoke = false;
            remainingTime -= Time.deltaTime;
        }

        // Hết thời gian khiên -> tự biến mất
        if (remainingTime <= 0 && isInvoke == false)
        {
            RemoveShield();
            isInvoke = true;
        }
    }

    private void LateUpdate()
    {
        // Cập nhật vị trí bóng ma xoay quanh mục tiêu
        if (isShield && targetTank != null && targetTank.IsAlive && visualGuard != null)
        {
            currentAngle = (currentAngle + orbitSpeed * Time.deltaTime) % 360f;
            float rad = currentAngle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * orbitRadius;
            visualGuard.transform.position = targetTank.transform.position + offset;
            visualGuard.transform.rotation = Quaternion.Euler(0f, 0f, currentAngle - 90f);
        }
        else if (isShield && (targetTank == null || !targetTank.IsAlive))
        {
            RemoveShield();
        }
    }

    /// <summary>
    /// Xử lý triệt tiêu hoàn toàn đòn đánh khi có khiên (triển khai từ ITankShield).
    /// </summary>
    public bool TryAbsorbDamage(ref float incomingDamage)
    {
        if (CheckAndConsumeShield())
        {
            incomingDamage = 0f;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Phá vỡ hoặc giải phóng khiên chủ động (triển khai từ ITankShield).
    /// </summary>
    public void BreakShield()
    {
        RemoveShield();
    }

    /// <summary>
    /// Kiểm tra nếu có khiên: hủy khiên, tạo hiệu ứng khói bùng tan và triệt tiêu hoàn toàn đòn đánh.
    /// Trả về true nếu đã triệt tiêu đòn đánh thành công.
    /// </summary>
    public bool CheckAndConsumeShield()
    {
        if (isShield && remainingTime > 0)
        {
            PlayBurstEffect();
            RemoveShield();
            OnShieldBreak?.Invoke();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gỡ bỏ khiên và ẩn bóng ma bảo vệ.
    /// </summary>
    public void RemoveShield()
    {
        isShield = false;
        remainingTime = 0f;

        if (targetTank != null && ReferenceEquals(targetTank.Shield, this))
        {
            targetTank.Shield = null;
        }

        if (visualGuard != null)
        {
            visualGuard.SetActive(false);
        }

        OnShieldChange?.Invoke(false);
    }

    private void PlayBurstEffect()
    {
        if (visualBurst != null && targetTank != null)
        {
            visualBurst.transform.position = targetTank.transform.position;
            visualBurst.SetActive(true);
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
            }
            StopAllCoroutines();
            StartCoroutine(HideBurstCoroutine());
        }
    }

    /// <summary>
    /// Coroutine chờ thời gian burstDuration rồi ẩn visual khói bùng nổ.
    /// </summary>
    private IEnumerator HideBurstCoroutine()
    {
        yield return new WaitForSeconds(burstDuration);
        if (visualBurst != null)
        {
            visualBurst.SetActive(false);
        }
    }

    private void OnDisable()
    {
        RemoveShield();
        if (visualBurst != null)
        {
            visualBurst.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        RemoveShield();
        if (visualBurst != null)
        {
            visualBurst.SetActive(false);
        }
    }
}
