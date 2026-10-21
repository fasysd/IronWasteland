using IronWasteland.Tanks;
using System;
using UnityEngine;

/// <summary>
/// Kỹ năng Tùy Chọn 1 - Biến thể 2: Shadow Link (Dải Liên Kết Bóng Tối).
/// Tạo liên kết bóng tối với một đồng minh (TankBrain khác bản thân), giúp tăng tốc độ di chuyển
/// cho cả hai đồng thời giảm thời gian hồi kỹ năng thường cho đồng minh đó.
/// Hiển thị dải xích nối 2 xe và khói đen dưới thân mỗi xe.
/// </summary>
[DisallowMultipleComponent]
public class OptionalSkill1_2 : MonoBehaviour
{
    [Header("Mục Tiêu & Thời Gian")]
    [Tooltip("Bán kính tìm kiếm đồng minh xung quanh vị trí con trỏ chuột.")]
    [SerializeField, Min(0.1f)] private float selectRadius = 1.0f;

    [Tooltip("Thời gian duy trì dải liên kết bóng tối (giây).")]
    [SerializeField, Min(0.1f)] private float duration = 8f;

    [Header("Chỉ Số Buff (Stats)")]
    [Tooltip("Lượng tốc độ di chuyển tăng thêm cho cả Black Shadow và đồng minh.")]
    [SerializeField, Min(0f)] private float moveSpeedBonus = 2.0f;

    [Tooltip("Phần trăm giảm thời gian hồi chiêu kỹ năng thường cho đồng minh (0.3 = giảm 30%).")]
    [SerializeField, Range(0f, 1f)] private float cooldownReductionBonus = 0.3f;

    [Header("Hình Ảnh (Visuals)")]
    [Tooltip("Sprite hình ảnh sợi xích bóng tối.")]
    [SerializeField] private Sprite chainSprite;

    [Tooltip("Thứ tự hiển thị (Sorting Order) của dây xích.")]
    [SerializeField] private int chainSortingOrder = 1;

    [Tooltip("Sprite khói đen tỏa ra dưới thân mỗi xe (asset_shadow_guard_satellite).")]
    [SerializeField] private Sprite smokeSprite;

    [Tooltip("Kích thước khói đen dưới thân xe.")]
    [SerializeField] private Vector3 smokeScale = new Vector3(0.8f, 0.8f, 1f);

    [Tooltip("Thứ tự hiển thị (Sorting Order) của khói đen.")]
    [SerializeField] private int smokeSortingOrder = -2;

    public event Action<TankBrain> OnLinkStarted;
    public event Action<TankBrain> OnLinkEnded;

    public bool IsLinked => isLinked;
    public TankBrain LinkedAlly => allyTank;
    public float RemainingDuration => remainingDuration;

    private const string SelfBonusKey = "ShadowLink_Self";
    private const string AllyBonusKey = "ShadowLink_Ally";

    private TankBrain ownerTank;
    private TankBrain allyTank;
    private bool isLinked;
    private float remainingDuration;

    private GameObject vfxRoot;
    private GameObject chainGo;
    private GameObject ownerSmokeGo;
    private GameObject allySmokeGo;

    private void Awake()
    {
        // Tắt SpriteRenderer gắn sẵn trên GameObject này (nếu có) để tránh hiện xích thừa trên xe
        SpriteRenderer selfSr = GetComponent<SpriteRenderer>();
        if (selfSr != null)
        {
            if (chainSprite == null && selfSr.sprite != null)
            {
                chainSprite = selfSr.sprite;
            }
            selfSr.enabled = false;
        }

        vfxRoot = new GameObject($"{gameObject.name}_Visuals");
        vfxRoot.transform.SetParent(null);

        chainGo = CreateVfxObject("ShadowChain", chainSprite, chainSortingOrder, Vector3.one);
        ownerSmokeGo = CreateVfxObject("OwnerSmoke", smokeSprite, smokeSortingOrder, smokeScale);
        allySmokeGo = CreateVfxObject("AllySmoke", smokeSprite, smokeSortingOrder, smokeScale);

        SetVisualsActive(false);
    }

    private GameObject CreateVfxObject(string objName, Sprite sprite, int sortingOrder, Vector3 scale)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(vfxRoot.transform);
        go.transform.localScale = scale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;

        return go;
    }

    /// <summary>
    /// Kích hoạt liên kết bóng tối với đồng minh tại vị trí ngắm của xe.
    /// </summary>
    public bool Initialize(TankBrain owner)
    {
        return owner != null && Initialize(owner, owner.LookTarget);
    }

    /// <summary>
    /// Kiểm tra tại vị trí chuột xem có phải TankBrain và không phải bản thân hay không.
    /// Nếu thỏa mãn: tăng tốc độ cho cả hai, giảm hồi chiêu cho đồng minh và bật visual.
    /// </summary>
    public bool Initialize(TankBrain owner, Vector2 targetWorldPos)
    {
        if (owner == null) return false;
        this.ownerTank = owner;

        if (targetWorldPos == Vector2.zero && owner.HasLookTarget)
        {
            targetWorldPos = owner.LookTarget;
        }

        if (!TryFindAllyAt(targetWorldPos, out TankBrain foundAlly))
        {
            return false;
        }

        if (isLinked) BreakLink();

        this.allyTank = foundAlly;
        this.remainingDuration = duration;
        this.isLinked = true;

        // Tăng tốc độ di chuyển cho Black Shadow
        TankStats selfBonus = TankStats.Zeroed();
        selfBonus.MoveSpeed = moveSpeedBonus;
        ownerTank.AddBonusStats(SelfBonusKey, selfBonus);

        // Tăng tốc độ di chuyển + giảm hồi chiêu kỹ năng thường cho đồng minh
        TankStats allyBonus = TankStats.Zeroed();
        allyBonus.MoveSpeed = moveSpeedBonus;
        allyBonus.CooldownReduction = cooldownReductionBonus;
        allyTank.AddBonusStats(AllyBonusKey, allyBonus);

        if (!gameObject.activeSelf) gameObject.SetActive(true);

        SetVisualsActive(true);
        UpdateVisuals();

        OnLinkStarted?.Invoke(allyTank);
        return true;
    }

    /// <summary>
    /// Tìm kiếm tất cả các collider xung quanh vị trí chuột.
    /// Nếu có collider nào thuộc về xe tăng đồng minh (khác bản thân và còn sống) thì chọn người đó,
    /// nếu không có collider nào là đồng minh thì mới trả về false.
    /// </summary>
    public bool TryFindAllyAt(Vector2 targetPos, out TankBrain ally)
    {
        ally = null;
        Collider2D[] colliders = Physics2D.OverlapCircleAll(targetPos, selectRadius);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D col = colliders[i];
            if (col == null) continue;

            TankBrain target = col.GetComponentInParent<TankBrain>();
            if (target != null && target != ownerTank && target.IsAlive)
            {
                ally = target;
                return true;
            }
        }

        return false;
    }

    private void Update()
    {
        if (!isLinked) return;

        // Nếu một trong hai xe tăng bị hủy hoặc chết -> Ngắt liên kết
        if (ownerTank == null || !ownerTank.IsAlive || allyTank == null || !allyTank.IsAlive)
        {
            BreakLink();
            return;
        }

        // Đếm ngược thời gian duy trì liên kết
        remainingDuration -= Time.deltaTime;
        if (remainingDuration <= 0f)
        {
            BreakLink();
        }
    }

    private void LateUpdate()
    {
        if (isLinked) UpdateVisuals();
    }

    /// <summary>
    /// Cập nhật vị trí sợi xích nối từ xe Black Shadow sang xe đồng minh và khói đen dưới thân mỗi xe.
    /// </summary>
    private void UpdateVisuals()
    {
        if (ownerTank == null || allyTank == null) return;

        Vector3 start = ownerTank.transform.position;
        Vector3 end = allyTank.transform.position;

        // 1. Cập nhật sợi xích nối 2 xe
        if (chainGo != null)
        {
            Vector3 diff = end - start;
            float dist = diff.magnitude;

            if (dist > 0.01f)
            {
                // Vì pivot của sprite asset_shadow_chain nằm ở đầu bên trái (x = 33.38px),
                // nên đặt vị trí của chainGo ngay tại start để sợi xích bắt đầu từ xe Black Shadow vươn tới đồng minh
                chainGo.transform.position = start;
                chainGo.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg);

                // Chiều dài thực tế từ pivot tới đuôi xích
                float baseLength = 9.9f;
                if (chainSprite != null && chainSprite.rect.width > 0f)
                {
                    baseLength = (chainSprite.rect.width - chainSprite.pivot.x) / chainSprite.pixelsPerUnit;
                }

                chainGo.transform.localScale = new Vector3(dist / Mathf.Max(0.01f, baseLength), 1f, 1f);
            }
        }

        // 2. Cập nhật vị trí khói đen dưới thân mỗi xe
        if (ownerSmokeGo != null) ownerSmokeGo.transform.position = start;
        if (allySmokeGo != null) allySmokeGo.transform.position = end;
    }

    private void SetVisualsActive(bool isActive)
    {
        if (chainGo != null) chainGo.SetActive(isActive);
        if (ownerSmokeGo != null) ownerSmokeGo.SetActive(isActive);
        if (allySmokeGo != null) allySmokeGo.SetActive(isActive);
    }

    /// <summary>
    /// Ngắt liên kết bóng tối, hoàn trả toàn bộ chỉ số buff và ẩn hình ảnh.
    /// </summary>
    public void BreakLink()
    {
        if (!isLinked) return;

        isLinked = false;
        remainingDuration = 0f;

        if (ownerTank != null) ownerTank.RemoveBonusStats(SelfBonusKey);
        if (allyTank != null) allyTank.RemoveBonusStats(AllyBonusKey);

        SetVisualsActive(false);

        TankBrain prevAlly = allyTank;
        allyTank = null;

        OnLinkEnded?.Invoke(prevAlly);
    }

    private void OnDisable()
    {
        BreakLink();
    }

    private void OnDestroy()
    {
        BreakLink();
        if (vfxRoot != null)
        {
            Destroy(vfxRoot);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        if (chainSprite == null)
        {
            chainSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Tank/Sprites/skill/blackShadow/asset_shadow_chain.png");
        }
        if (smokeSprite == null)
        {
            smokeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Tank/Sprites/skill/blackShadow/asset_shadow_guard_satellite.png");
        }
    }
#endif
}
