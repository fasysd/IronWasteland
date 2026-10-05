using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Hanh dong cua Tank.
    /// - Move va UseSkill la action "dong" (chi mot cai chay tai mot thoi diem).
    /// - Look la action lien tuc, chay song song voi Move (xem <see cref="TankView.IsLooking"/>).
    /// </summary>
    public enum TankAction
    {
        Idle,
        Move,
        Look,
        UseSkill1,
        UseSkill2,
        UseSkill3,
        UseSkill4,
    }

    /// <summary>
    /// Phan Visual + Physical cua Tank: sprite, animator, collider, rigidbody.
    /// KHONG chua logic game (speed, damage, skill...). No chi day dieu khien hinh anh.
    /// Cac ham public day la "hanh dong" cua Tank, moi ham se chay animation tuong ung.
    /// </summary>
    [DisallowMultipleComponent]
    public class TankView : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Animator cua Tank ( tren root ).")]
        [SerializeField] private Animator animator;

        [Tooltip("Pivot xoay doc lap phia tren (Weapon/Nong sung). Chi Model dung transform nay.")]
        [SerializeField] private Transform weaponPivot;

        [Tooltip("Pivot xoay theo huong di chuyen (Hull + Track).")]
        [SerializeField] private Transform bodyPivot;

        [Header("Look")]
        [Tooltip("Toc do xoay noi sung (do/giay).")]
        [SerializeField] private float lookSmoothing = 900f;

        [Header("References (hitbox)")]
        [Tooltip("Rigidbody2D dung cho hitbox. TankModel KHONG sua truc tiep, chi de reference.")]
        [SerializeField] private Rigidbody2D body;

        private TankAction m_Action = TankAction.Idle;
        private bool m_IsLooking;
        private Vector2 m_LookTarget;
        private bool m_HasLookTarget;
        private float m_LookAngle;

        // Animator parameter hash
        private static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");

        public Rigidbody2D Body => body;
        public Transform WeaponPivot => weaponPivot;
        public Transform BodyPivot => bodyPivot;
        public TankAction CurrentAction => m_Action;

        /// <summary>
        /// True khi action Look dang chay. Look chay lien tuc, song song
        /// voi Move/UseSkill nen khong dung chung bien CurrentAction.
        /// </summary>
        public bool IsLooking => m_IsLooking;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (bodyPivot == null) bodyPivot = transform;
            m_LookAngle = bodyPivot.eulerAngles.z;

            // TankModel co the la prefab con cua Tank. Rigidbody2D nam tren Tank
            // nen tim o cha khi chua co tren chinh no.
            if (body == null) body = GetComponentInParent<Rigidbody2D>();
        }

        private void Update()
        {
            // Look luôn chay lien tuc: noi sung luon bam theo con tro chuot.
            // Neu chua co target thi noi sung giu nguyen huong hien tai.
            UpdateLookRotation(Time.deltaTime);
        }

        #region Action - duoc TankBrain goi

        /// <summary>Xoay toan than Tank theo huong di chuyen.</summary>
        public void Move(Vector2 direction)
        {
            m_Action = TankAction.Move;

            if (direction.sqrMagnitude > 0.0001f)
            {
                bodyPivot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            }

            SetFloat(HashMoveSpeed, direction.magnitude);
        }

        /// <summary>
        /// Nhin ve mot diem trong world (vi du vi tri con tro chuot).
        /// Day la action lien tuc, chay SONG SONG voi Move va UseSkill.
        /// Moi thang Tank goi lai de noi sung bam theo muc tieu.
        /// </summary>
        public void Look(Vector2 worldPoint)
        {
            m_IsLooking = true;
            m_LookTarget = worldPoint;
            m_HasLookTarget = true;
        }

        /// <summary>Tat action Look. Noi sung giu nguyen huong hien tai.</summary>
        public void StopLook()
        {
            m_IsLooking = false;
        }

        /// <summary>Dung 1 trong 4 ky nang (index 0..3).</summary>
        public void UseSkill(int index)
        {
            if (index < 0 || index > 3) return;
            m_Action = (TankAction)((int)TankAction.UseSkill1 + index);
        }

        /// <summary>Dung hanh dong / ve idle.</summary>
        public void Stop()
        {
            m_Action = TankAction.Idle;
            SetFloat(HashMoveSpeed, 0f);
        }

        #endregion

        /// <summary>
        /// Noi sung luon huong ve giua thiet ke Tank va muc tieu look tren man hinh.
        /// </summary>
        private void UpdateLookRotation(float deltaTime)
        {
            if (weaponPivot == null || !m_HasLookTarget) return;

            Vector3 origin = transform.position;

            Vector2 dir = new Vector2(m_LookTarget.x - origin.x, m_LookTarget.y - origin.y);
            if (dir.sqrMagnitude <= 0.0001f) return;

            // Goc mong muon tren truc Z, chuan hoa ve (-180, 180].
            float targetAngle = NormalizeAngle(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

            // Xoay nganh nhat theo goc ngan tu A sang B (tranh quay vong kinh).
            float delta = Mathf.DeltaAngle(m_LookAngle, targetAngle);
            float t = 1f - Mathf.Exp(-lookSmoothing * deltaTime);
            m_LookAngle = NormalizeAngle(m_LookAngle + delta * t);

            // Gan rotation WORLD: dung du co parent xoay hay khong,
            // noi sung luon chi dung mot huong muc tieu.
            weaponPivot.rotation = Quaternion.Euler(0f, 0f, m_LookAngle);
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            if (angle <= -180f) angle += 360f;
            return angle;
        }

        private void SetFloat(int hash, float value)
        {
            if (animator != null) animator.SetFloat(hash, value);
        }

        private void OnValidate()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (bodyPivot == null) bodyPivot = transform;
            if (body == null) body = GetComponentInParent<Rigidbody2D>();
            lookSmoothing = Mathf.Max(0f, lookSmoothing);
        }
    }
}