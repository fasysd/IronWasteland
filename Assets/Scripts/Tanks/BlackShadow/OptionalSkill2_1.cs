using IronWasteland;
using IronWasteland.Tanks;
using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Kỹ năng Tùy Chọn 2 - Biến thể 1: Shadow Possession (Nhập Thể Bóng Tối).
/// Hòa làm một vào bóng của đồng minh, khiến Black Shadow không thể bị chỉ định hay nhận sát thương trực tiếp.
/// Trong trạng thái này, tốc độ di chuyển của cả hai cùng được gia tăng.
/// Khi hết thời gian duy trì, Black Shadow tự động xuất hiện trở lại bên cạnh đồng minh.
/// </summary>
[DisallowMultipleComponent]
public class OptionalSkill2_1 : MonoBehaviour
{
    public event Action<TankBrain> OnPossessionStarted;
    public event Action<TankBrain> OnPossessionEnded;

    [Header("Mục Tiêu & Thời Gian")]
    [Tooltip("Bán kính tìm kiếm đồng minh xung quanh vị trí con trỏ chuột.")]
    [SerializeField, Min(0.1f)] private float selectRadius = 1.5f;

    [Tooltip("Thời gian duy trì trạng thái nhập thể vào bóng đồng minh (giây). Hết thời gian mới tự thoát ra.")]
    [SerializeField, Min(0.5f)] private float duration = 6.0f;

    [Header("Chỉ Số Buff (Stats)")]
    [Tooltip("Lượng tốc độ di chuyển tăng thêm cho cả Black Shadow và đồng minh.")]
    [SerializeField, Min(0f)] private float moveSpeedBonus = 3.0f;

    [Header("Hiển Thị Hình Ảnh (Visuals)")]
    [Tooltip("Kéo thả GameObject hiển thị hào quang bóng tối dưới chân đồng minh vào đây (VisualPossessionAura).")]
    [SerializeField] private GameObject visualPossessionAura;

    [Tooltip("Kéo thả GameObject hiển thị khói đen bóng tối bùng tan vào đây (VisualBurst).")]
    [SerializeField] private GameObject visualBurst;

    [Tooltip("Thời gian hiển thị làn khói bóng tối khi chìm và xuất hiện (giây).")]
    [SerializeField, Min(0.05f)] private float burstDuration = 0.4f;

    [Tooltip("Tốc độ xoay nhẹ của vòng hào quang bóng tối (độ/giây).")]
    [SerializeField] private float auraRotationSpeed = 30f;

    private const string SelfBonusKey = "ShadowPossession_Self";
    private const string AllyBonusKey = "ShadowPossession_Ally";

    // Trạng thái nội bộ (Runtime State)
    private TankBrain ownerTank;
    private TankBrain allyTank;
    private bool isPossessing = false;
    private float remainingTime = 0f;
    private Collider2D[] cachedOwnerColliders;
    private Rigidbody2D ownerRb;

    // Thuộc tính công khai (Read-only Getters)
    public bool IsActive => isPossessing && remainingTime > 0f;
    public float RemainingTime => remainingTime;
    public float Duration => duration;
    public TankBrain TargetTank => allyTank;
    public TankBrain PossessedAlly => allyTank;
    public TankBrain OwnerTank => ownerTank;

    private void Awake()
    {
        if (visualPossessionAura != null)
        {
            visualPossessionAura.SetActive(false);
        }

        if (visualBurst != null)
        {
            visualBurst.SetActive(false);
        }
    }

    /// <summary>
    /// Kích hoạt kỹ năng Shadow Possession với vị trí con trỏ chuột chỉ định.
    /// Tìm xe tăng đồng minh quanh chuột, nếu có thì chìm vào bóng đồng minh và tăng tốc độ cho cả hai.
    /// </summary>
    public bool Initialize(TankBrain owner, Vector2 mouseWorldPos)
    {
        if (owner == null) return false;
        if (isPossessing) return false; // Đang nhập thể thì không cho kích hoạt đè

        this.ownerTank = owner;
        this.ownerRb = owner.GetComponent<Rigidbody2D>();

        if (mouseWorldPos == Vector2.zero && owner.HasLookTarget)
        {
            mouseWorldPos = owner.LookTarget;
        }

        if (!TryFindAllyAt(mouseWorldPos, owner, out TankBrain foundAlly))
        {
            return false;
        }

        this.allyTank = foundAlly;
        this.remainingTime = duration;
        this.isPossessing = true;

        Vector2 originPos = ownerTank.transform.position;

        // 1. Khói đen bùng tan tại vị trí xe Black Shadow chìm xuống
        PlayBurstAt(originPos);

        // 2. Ẩn phần nhìn của Black Shadow
        if (ownerTank.View != null)
        {
            ownerTank.View.gameObject.SetActive(false);
        }

        // 3. Tắt Collider để không nhận va chạm vật lý hay đạn từ kẻ địch
        cachedOwnerColliders = ownerTank.GetComponentsInChildren<Collider2D>(true);
        SetCollidersEnabled(false);

        // 4. Tăng tốc độ di chuyển cho cả Black Shadow và đồng minh
        TankStats selfBonus = TankStats.Zeroed();
        selfBonus.MoveSpeed = moveSpeedBonus;
        ownerTank.AddBonusStats(SelfBonusKey, selfBonus);

        TankStats allyBonus = TankStats.Zeroed();
        allyBonus.MoveSpeed = moveSpeedBonus;
        allyTank.AddBonusStats(AllyBonusKey, allyBonus);

        // 5. Hiển thị vòng hào quang bóng tối lớn dưới chân đồng minh
        if (visualPossessionAura != null)
        {
            visualPossessionAura.transform.position = allyTank.transform.position;
            visualPossessionAura.SetActive(true);
        }

        // 7. Đồng bộ vị trí Black Shadow tới đồng minh
        SyncPositionToAlly();

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        OnPossessionStarted?.Invoke(allyTank);
        return true;
    }

    /// <summary>
    /// Tìm kiếm xe tăng đồng minh (khác owner và còn sống) xung quanh vị trí chuột.
    /// </summary>
    public bool TryFindAllyAt(Vector2 targetPos, TankBrain owner, out TankBrain ally)
    {
        ally = null;
        Collider2D[] colliders = Physics2D.OverlapCircleAll(targetPos, selectRadius);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D col = colliders[i];
            if (col == null) continue;

            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(col);
            if (targetRef != null && targetRef.TryGetOwner<TankBrain>(out var target))
            {
                if (target != owner && target.IsAlive)
                {
                    ally = target;
                    return true;
                }
            }
        }

        return false;
    }

    private void Update()
    {
        if (!isPossessing) return;

        // Đếm ngược thời gian duy trì. Hết thời gian thì mới tự động thoát ra (không cho bấm lần 2)
        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0f)
        {
            ExitPossession();
        }
    }

    private void LateUpdate()
    {
        if (!isPossessing) return;

        // Nếu một trong hai xe bị chết hoặc bị hủy -> Lập tức thoát ra
        if (ownerTank == null || !ownerTank.IsAlive || allyTank == null || !allyTank.IsAlive)
        {
            ExitPossession();
            return;
        }

        // Neo vị trí Black Shadow đi theo đồng minh
        SyncPositionToAlly();

        // Cập nhật vị trí và xoay nhẹ hào quang bóng tối dưới chân đồng minh
        if (visualPossessionAura != null)
        {
            visualPossessionAura.transform.position = allyTank.transform.position;
            visualPossessionAura.transform.Rotate(0f, 0f, auraRotationSpeed * Time.deltaTime);
        }
    }

    private void SyncPositionToAlly()
    {
        if (ownerTank == null || allyTank == null) return;

        ownerTank.transform.position = allyTank.transform.position;
        if (ownerRb != null)
        {
            ownerRb.position = allyTank.transform.position;
            ownerRb.linearVelocity = Vector2.zero;
        }
    }

    /// <summary>
    /// Thoát khỏi trạng thái nhập thể bóng tối: phục hồi hiển thị, bật lại collider, hoàn trả chỉ số.
    /// </summary>
    public void ExitPossession()
    {
        if (!isPossessing) return;

        isPossessing = false;
        remainingTime = 0f;

        Vector2 exitPos = allyTank != null ? (Vector2)allyTank.transform.position : (Vector2)ownerTank.transform.position;

        // 1. Tắt hào quang bóng tối dưới chân đồng minh
        if (visualPossessionAura != null)
        {
            visualPossessionAura.SetActive(false);
        }

        // 2. Gỡ bỏ bonus stats tốc độ chạy của cả hai
        if (ownerTank != null)
        {
            ownerTank.RemoveBonusStats(SelfBonusKey);

            // Bật lại phần nhìn của Black Shadow
            if (ownerTank.View != null)
            {
                ownerTank.View.gameObject.SetActive(true);
            }

            // Bật lại các collider
            SetCollidersEnabled(true);

            // Đặt vị trí xuất hiện lệch nhẹ 0.8m so với đồng minh để không bị đè collider
            Vector2 offset = ownerTank.HasLookTarget
                ? ((Vector2)ownerTank.LookTarget - exitPos).normalized * 0.8f
                : Vector2.left * 0.8f;

            Vector2 spawnPos = exitPos + offset;
            ownerTank.transform.position = spawnPos;
            if (ownerRb != null)
            {
                ownerRb.position = spawnPos;
                ownerRb.linearVelocity = Vector2.zero;
            }

            // Phát khói đen bùng tan lúc tái xuất hiện
            PlayBurstAt(spawnPos);
        }

        if (allyTank != null)
        {
            allyTank.RemoveBonusStats(AllyBonusKey);
        }

        TankBrain prevAlly = allyTank;
        allyTank = null;

        OnPossessionEnded?.Invoke(prevAlly);
    }

    private void SetCollidersEnabled(bool isEnabled)
    {
        if (cachedOwnerColliders == null) return;

        for (int i = 0; i < cachedOwnerColliders.Length; i++)
        {
            if (cachedOwnerColliders[i] != null)
            {
                cachedOwnerColliders[i].enabled = isEnabled;
            }
        }
    }

    private void PlayBurstAt(Vector2 pos)
    {
        if (visualBurst != null)
        {
            visualBurst.transform.position = pos;
            visualBurst.SetActive(true);
            StopAllCoroutines();
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
            }
            StartCoroutine(HideBurstCoroutine());
        }
    }

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
        ExitPossession();
    }

    private void OnDestroy()
    {
        ExitPossession();
    }
}
