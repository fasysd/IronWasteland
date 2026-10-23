using IronWasteland;
using IronWasteland.Enemies;
using IronWasteland.Tanks;
using System;
using System.Collections;
using UnityEngine;

namespace IronWasteland.Tanks.BlackShadow
{
    /// <summary>
    /// Kỹ năng Tùy Chọn 2: Shadow Ambush (Mai Phục Bóng Tối).
    /// Dịch chuyển tức thì ra ngay phía sau lưng mục tiêu kẻ địch và gây sát thương.
    /// Đòn tấn công kế tiếp nếu đánh trúng từ phía sau lưng sẽ gây thêm lượng lớn sát thương chí mạng
    /// thông qua chỉ số phụ DamageMultiplier.
    /// </summary>
    [DisallowMultipleComponent]
    public class OptionalSkill2 : MonoBehaviour
    {
        public event Action<EnemyBrain> OnAmbushExecuted;
        public event Action<bool> OnBackstabBuffChange;

        [Header("Mục Tiêu & Dịch Chuyển")]
        [Tooltip("Bán kính tìm kiếm kẻ địch xung quanh vị trí con trỏ chuột.")]
        [SerializeField, Min(0.1f)] private float selectRadius = 1.5f;

        [Tooltip("Khoảng cách đặt xe tank ở phía sau lưng mục tiêu.")]
        [SerializeField, Min(0.1f)] private float teleportOffsetDistance = 1.8f;

        [Header("Sát Thương & Buff Chí Mạng")]
        [Tooltip("Sát thương gây ra tức thì khi dịch chuyển mai phục.")]
        [SerializeField, Min(0f)] private float ambushDamage = 60f;

        [Tooltip("Hệ số sát thương chí mạng cộng thêm cho đòn tấn công kế tiếp từ sau lưng (1.5 = +150% damage).")]
        [SerializeField, Min(0f)] private float bonusDamageMultiplier = 1.5f;

        [Tooltip("Thời gian duy trì buff chờ đòn tấn công kế tiếp (giây).")]
        [SerializeField, Min(0.5f)] private float buffDuration = 6.0f;

        [Header("Hiển Thị Hình Ảnh (Visuals)")]
        [Tooltip("Kéo thả GameObject hiển thị móng nanh ma quỷ cào xé (VisualClaw).")]
        [SerializeField] private GameObject visualClaw;

        [Tooltip("Kéo thả GameObject hiển thị khói đen bóng tối bùng tan (VisualBurst).")]
        [SerializeField] private GameObject visualBurst;

        [Tooltip("Thời gian hiển thị hiệu ứng móng nanh ma quỷ (giây).")]
        [SerializeField, Min(0.05f)] private float clawDuration = 0.35f;

        [Tooltip("Thời gian hiển thị làn khói bóng tối (giây).")]
        [SerializeField, Min(0.05f)] private float burstDuration = 0.4f;

        private const string AmbushCritBonusKey = "ShadowAmbush_Crit";

        // Trạng thái nội bộ (Runtime State)
        private TankBrain ownerTank;
        private EnemyBrain currentTarget;
        private bool hasBackstabBuff = false;
        private float buffRemainingTime = 0f;
        private Coroutine hideVisualsCoroutine;

        // Thuộc tính công khai (Read-only Getters & UI)
        public bool IsActive => hasBackstabBuff && buffRemainingTime > 0f;
        public float RemainingTime => buffRemainingTime;
        public float Duration => buffDuration;
        public EnemyBrain TargetEnemy => currentTarget;
        public bool HasBackstabBuff => hasBackstabBuff;
        public float BonusDamageMultiplier => bonusDamageMultiplier;
        public TankBrain OwnerTank => ownerTank;

        private void Awake()
        {
            if (visualClaw != null)
            {
                visualClaw.SetActive(false);
            }

            if (visualBurst != null)
            {
                visualBurst.SetActive(false);
            }
        }

        /// <summary>
        /// Kích hoạt kỹ năng Shadow Ambush với vị trí con trỏ chuột chỉ định.
        /// Tìm kẻ địch quanh con trỏ chuột, nếu có thì teleport ra sau lưng và gây sát thương.
        /// </summary>
        public bool Initialize(TankBrain owner, Vector2 mouseWorldPos)
        {
            if (owner == null) return false;

            // Dọn dẹp trạng thái của lần kích hoạt trước trước khi bắt đầu lượt mới
            Clear();

            this.ownerTank = owner;

            if (mouseWorldPos == Vector2.zero && owner.HasLookTarget)
            {
                mouseWorldPos = owner.LookTarget;
            }

            if (!TryFindEnemyAt(mouseWorldPos, out EnemyBrain enemy))
            {
                return false;
            }

            currentTarget = enemy;
            Vector2 originPos = ownerTank.transform.position;
            Vector2 behindPos = CalculateBehindPosition(enemy, originPos);

            // 1. Hiển thị khói đen biến mất tại vị trí cũ
            PlayBurstAt(originPos);

            // 2. Dịch chuyển xe tank ra sau lưng mục tiêu
            ownerTank.transform.position = behindPos;
            Rigidbody2D ownerRb = ownerTank.GetComponent<Rigidbody2D>();
            if (ownerRb != null)
            {
                ownerRb.position = behindPos;
                ownerRb.linearVelocity = Vector2.zero;
            }

            // 3. Hướng nòng súng ngắm thẳng vào tâm kẻ địch
            ownerTank.SetLookTarget(enemy.transform.position);

            // 4. Gây sát thương Shadow Ambush ngay lập tức lên kẻ địch
            enemy.TakeDamage(ambushDamage);
            TextDamageManager.Instance?.Show(ambushDamage, enemy.transform.position);

            // 5. Hiển thị móng nanh ma quỷ cào xé tại vị trí kẻ địch
            Vector2 slashDir = (Vector2)enemy.transform.position - behindPos;
            PlayClawAt(enemy.transform.position, slashDir);

            // 6. Gán buff chí mạng cho đòn đánh kế tiếp
            ApplyBackstabBuff();

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            OnAmbushExecuted?.Invoke(enemy);
            return true;
        }

        /// <summary>
        /// Quét tìm kẻ địch còn sống xung quanh vị trí con trỏ chuột.
        /// </summary>
        public bool TryFindEnemyAt(Vector2 targetPos, out EnemyBrain foundEnemy)
        {
            foundEnemy = null;
            Collider2D[] colliders = Physics2D.OverlapCircleAll(targetPos, selectRadius);

            float closestDistance = float.MaxValue;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D col = colliders[i];
                if (col == null) continue;

                ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(col);
                if (targetRef != null && targetRef.TryGetOwner<EnemyBrain>(out var enemy) && enemy.IsAlive)
                {
                    float dist = Vector2.Distance(targetPos, enemy.transform.position);
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        foundEnemy = enemy;
                    }
                }
            }

            return foundEnemy != null;
        }

        /// <summary>
        /// Xác định hướng đằng sau lưng của kẻ địch dựa trên trạng thái di chuyển và hướng nhìn.
        /// </summary>
        public Vector2 GetBehindDirection(EnemyBrain enemy, Vector2 attackerPos)
        {
            if (enemy == null) return Vector2.left;

            // 1. Nếu kẻ địch đang di chuyển -> đằng sau là hướng ngược chiều vận tốc
            Rigidbody2D rb = enemy.GetComponent<Rigidbody2D>();
            if (rb != null && rb.linearVelocity.sqrMagnitude > 0.05f)
            {
                return -rb.linearVelocity.normalized;
            }

            // 2. Nếu kẻ địch có góc xoay z khác 0 (transform.right là hướng nhìn chuẩn trong 2D)
            float zAngle = enemy.transform.eulerAngles.z;
            if (Mathf.Abs(Mathf.DeltaAngle(zAngle, 0f)) > 1f)
            {
                return -(Vector2)enemy.transform.right;
            }

            // 3. Nếu Model con có SpriteRenderer lật flipX
            SpriteRenderer sr = enemy.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                if (sr.flipX)
                {
                    return Vector2.right;
                }

                float modelZ = sr.transform.eulerAngles.z;
                if (Mathf.Abs(Mathf.DeltaAngle(modelZ, 0f)) > 1f)
                {
                    return -(Vector2)sr.transform.right;
                }
            }

            // 4. Nếu đứng yên và không có thông tin xoay:
            // Kẻ địch thường hướng mặt về phía người chơi, do đó sau lưng địch nằm ở hướng từ người chơi xuyên qua địch
            Vector2 fromAttacker = ((Vector2)enemy.transform.position - attackerPos).normalized;
            if (fromAttacker.sqrMagnitude > 0.01f)
            {
                return fromAttacker;
            }

            return Vector2.left;
        }

        /// <summary>
        /// Tính toán vị trí xuất hiện sau lưng kẻ địch.
        /// </summary>
        public Vector2 CalculateBehindPosition(EnemyBrain enemy, Vector2 attackerPos)
        {
            Vector2 behindDir = GetBehindDirection(enemy, attackerPos);
            return (Vector2)enemy.transform.position + behindDir * teleportOffsetDistance;
        }

        /// <summary>
        /// Kiểm tra xe tank người chơi có đang đứng ở phía sau lưng kẻ địch hay không.
        /// </summary>
        public bool IsHittingFromBehind(Vector2 tankPos, EnemyBrain enemy)
        {
            if (enemy == null) return false;

            // Hướng mặt phía trước của kẻ địch
            Vector2 facingDir = -GetBehindDirection(enemy, tankPos);

            // Vector từ kẻ địch tới vị trí của xe tank
            Vector2 fromEnemyToTank = tankPos - (Vector2)enemy.transform.position;

            // Nếu Dot < 0: xe tank đang nằm ở nửa không gian phía sau lưng kẻ địch
            return Vector2.Dot(fromEnemyToTank, facingDir) < 0f;
        }

        /// <summary>
        /// Gán khối chỉ số DamageMultiplier chí mạng vào TankBrain.
        /// </summary>
        private void ApplyBackstabBuff()
        {
            if (hasBackstabBuff && ownerTank != null)
            {
                ownerTank.RemoveBonusStats(AmbushCritBonusKey);
            }

            TankStats critBonus = TankStats.Zeroed();
            critBonus.DamageMultiplier = bonusDamageMultiplier;
            ownerTank.AddBonusStats(AmbushCritBonusKey, critBonus);

            hasBackstabBuff = true;
            buffRemainingTime = buffDuration;
            OnBackstabBuffChange?.Invoke(true);
        }

        /// <summary>
        /// Kiểm tra đòn đánh kế tiếp: nếu xe tank đang ở sau lưng kẻ địch thì trả về true để tính sát thương chí mạng.
        /// Tiêu hao buff ngay lập tức sau đòn đánh này.
        /// </summary>
        public bool CheckAndConsumeBackstab(Vector2 tankPos, EnemyBrain enemy)
        {
            if (!hasBackstabBuff) return false;

            bool isBackstab = IsHittingFromBehind(tankPos, enemy);
            ConsumeBuff();
            return isBackstab;
        }

        /// <summary>
        /// Tiêu hao và gỡ bỏ buff chí mạng khỏi xe tank.
        /// </summary>
        public void ConsumeBuff()
        {
            if (!hasBackstabBuff) return;

            hasBackstabBuff = false;
            buffRemainingTime = 0f;

            if (ownerTank != null)
            {
                ownerTank.RemoveBonusStats(AmbushCritBonusKey);
            }

            OnBackstabBuffChange?.Invoke(false);
        }

        private void PlayBurstAt(Vector2 pos)
        {
            if (visualBurst != null)
            {
                visualBurst.transform.position = pos;
                visualBurst.SetActive(true);
            }
        }

        private void PlayClawAt(Vector2 pos, Vector2 direction)
        {
            if (visualClaw != null)
            {
                visualClaw.transform.position = pos;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                visualClaw.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                visualClaw.SetActive(true);
            }

            StopAllCoroutines();
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
            }
            hideVisualsCoroutine = StartCoroutine(HideVisualsCoroutine());
        }

        private IEnumerator HideVisualsCoroutine()
        {
            yield return new WaitForSeconds(Mathf.Max(clawDuration, burstDuration));
            if (visualClaw != null) visualClaw.SetActive(false);
            if (visualBurst != null) visualBurst.SetActive(false);
        }

        private void Update()
        {
            if (hasBackstabBuff)
            {
                buffRemainingTime -= Time.deltaTime;
                if (buffRemainingTime <= 0f)
                {
                    ConsumeBuff();
                }
            }
        }

        /// <summary>
        /// Đặt lại toàn bộ trạng thái runtime của Shadow Ambush về mặc định.
        /// CHỈ reset state: không phát event, không tắt GameObject, không ghi đè trường cấu hình.
        /// Dùng chung cho CancelSkill (kết thúc trong trận) và Clear (dọn dẹp khi tái sử dụng).
        /// </summary>
        private void ResetRuntimeState()
        {
            // Dừng coroutine ẩn hiệu ứng đang chạy (nếu có) rồi xoá tham chiếu
            if (hideVisualsCoroutine != null)
            {
                StopCoroutine(hideVisualsCoroutine);
            }

            hideVisualsCoroutine = null;

            // Gỡ buff chí mạng còn treo trên xe tăng chủ thể
            if (ownerTank != null)
            {
                ownerTank.RemoveBonusStats(AmbushCritBonusKey);
            }

            hasBackstabBuff = false;
            buffRemainingTime = 0f;
            currentTarget = null;

            // Ẩn toàn bộ hiệu ứng hình ảnh
            if (visualClaw != null) visualClaw.SetActive(false);
            if (visualBurst != null) visualBurst.SetActive(false);
        }

        /// <summary>
        /// Dọn dẹp để tái sử dụng: reset state và gỡ owner.
        /// Không phát event, không tắt GameObject, KHÔNG xoá subscriber —
        /// người nghe tự gỡ đăng ký của mình trong OnDisable/OnDestroy.
        /// Initialize gọi hàm này trước tiên.
        /// </summary>
        public void Clear()
        {
            ResetRuntimeState();
            ownerTank = null;
        }

        /// <summary>
        /// Kết thúc kỹ năng Shadow Ambush: dọn state, báo kết thúc buff chí mạng rồi tắt GameObject.
        /// </summary>
        public void CancelSkill()
        {
            // Ghi nhớ trạng thái buff trước khi reset để chỉ phát event khi thực sự có buff đang treo
            bool hadBackstabBuff = hasBackstabBuff;

            ResetRuntimeState();

            if (hadBackstabBuff)
            {
                OnBackstabBuffChange?.Invoke(false);
            }

            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            CancelSkill();
        }

        private void OnDestroy()
        {
            CancelSkill();
        }
    }
}
