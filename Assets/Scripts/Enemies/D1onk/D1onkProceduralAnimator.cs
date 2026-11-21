using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Enemies.D1onk
{
    /// <summary>
    /// Hệ thống diễn hoạt Procedural xoay bộ phận quanh trục Z (localRotation.eulerAngles.z) cho Trưởng Quản Ngục D1onk.
    /// 
    /// RÀNG BUỘC KIẾN TRÚC:
    /// - Không sử dụng khung xương / SpriteSkin.
    /// - Vị trí (localPosition), pivot, tỉ lệ (localScale) và phân cấp cha-con của tất cả bộ phận đã chuẩn xác từ Prefab
    ///   nên TUYỆT ĐỐI KHÔNG ĐƯỢC THAY ĐỔI.
    /// - Toàn bộ hoạt ảnh (Idle, Move, TrungPhat, LazeNguyenTo, ToaNhietDienRong, VienBinhTinhLinh, Charging, Stun, Die)
    ///   được sinh ra hoàn toàn bằng cách xoay góc Z quanh pivot có sẵn của từng GameObject bộ phận.
    /// - Không hardcode: Mọi Transform đều được cấu hình tường minh trên Inspector và có cảnh báo nếu thiếu.
    /// </summary>
    [DisallowMultipleComponent]
    public class D1onkProceduralAnimator : MonoBehaviour
    {
        #region Inspector References
        [Header("Body Hierarchy References")]
        [Tooltip("Transform phần Thân của D1onk.")]
        [SerializeField] private Transform than;

        [Tooltip("Transform phần Đầu của D1onk.")]
        [SerializeField] private Transform dau;

        [Tooltip("Transform phần Vai trái.")]
        [SerializeField] private Transform vaiTrai;

        [Tooltip("Transform phần Vai phải.")]
        [SerializeField] private Transform vaiPhai;

        [Tooltip("Transform phần Vai mặt trong bên trái.")]
        [SerializeField] private Transform vaiMatTrongTrai;

        [Tooltip("Transform phần Vai mặt trong bên phải.")]
        [SerializeField] private Transform vaiMatTrongPhai;

        [Tooltip("Transform phần Tay trái.")]
        [SerializeField] private Transform tayTrai;

        [Tooltip("Transform phần Tay phải.")]
        [SerializeField] private Transform tayPhai;

        [Tooltip("Transform phần Chiến hạm (thân tàu).")]
        [SerializeField] private Transform chienHam;

        [Tooltip("Transform GameObject cha chứa cụm Súng 2 nòng.")]
        [SerializeField] private Transform sung2NongParent;

        [Tooltip("Transform GameObject cha chứa cụm Súng 3 nòng.")]
        [SerializeField] private Transform sung3NongParent;

        [Tooltip("Danh sách toàn bộ 14 nòng súng (10 súng 2 nòng + 4 súng 3 nòng).")]
        [SerializeField] private Transform[] allGuns;
        #endregion

        #region Idle Breathing Settings
        [Header("=== IDLE BREATHING ===")]
        [Tooltip("Tốc độ dao động Idle (rad/s).")]
        [SerializeField, Range(0.5f, 5f)] private float idleSpeed = 1.8f;

        [Tooltip("Biên độ Thân (±độ). Spec: ±1.5°.")]
        [SerializeField, Range(0.5f, 10f)] private float idleThanAmplitude = 1.5f;

        [Tooltip("Biên độ Đầu (±độ). Spec: ±1.0°. Ngược pha với Thân.")]
        [SerializeField, Range(0.5f, 10f)] private float idleDauAmplitude = 1.0f;

        [Tooltip("Biên độ Vai (±độ).")]
        [SerializeField, Range(0.5f, 10f)] private float idleVaiAmplitude = 1.5f;

        [Tooltip("Biên độ Tay (±độ). Spec: ±3.5°.")]
        [SerializeField, Range(0.5f, 15f)] private float idleTayAmplitude = 3.5f;

        [Tooltip("Biên độ Chiến hạm (±độ).")]
        [SerializeField, Range(0.1f, 5f)] private float idleChienHamAmplitude = 0.6f;
        #endregion

        #region Move Settings
        [Header("=== MOVE ===")]
        [Tooltip("Hệ số nhân tốc độ so với Idle.")]
        [SerializeField, Range(1f, 4f)] private float moveSpeedMultiplier = 2f;

        [Tooltip("Biên độ Thân khi di chuyển (±độ).")]
        [SerializeField, Range(1f, 15f)] private float moveThanAmplitude = 3.0f;

        [Tooltip("Biên độ Tay khi di chuyển (±độ).")]
        [SerializeField, Range(2f, 20f)] private float moveTayAmplitude = 6.0f;
        #endregion

        #region PhaoHamDaNong Settings
        [Header("=== PHÁO HẠM ĐA NÒNG (Bị động) ===")]
        [Tooltip("Thân giật ngửa (độ).")]
        [SerializeField, Range(2f, 20f)] private float phaoThanRecoil = 8.0f;

        [Tooltip("Đầu chúi xuống (độ).")]
        [SerializeField, Range(2f, 15f)] private float phaoDauChui = 6.0f;

        [Tooltip("Tay gồng giật phản lực (độ).")]
        [SerializeField, Range(10f, 50f)] private float phaoTayRecoil = 26.0f;

        [Tooltip("Tần số rung chấn pháo (Hz).")]
        [SerializeField, Range(20f, 80f)] private float phaoTremorFreq = 50f;

        [Tooltip("Biên độ rung chấn pháo (độ).")]
        [SerializeField, Range(1f, 10f)] private float phaoTremorAmp = 3.5f;
        #endregion

        #region TrungPhat Settings
        [Header("=== TRỪNG PHẠT ===")]
        [Tooltip("Tay vung lên cao (độ).")]
        [SerializeField, Range(15f, 60f)] private float trungPhatTayVung = 38.0f;

        [Tooltip("Tay dộng xuống (độ).")]
        [SerializeField, Range(10f, 50f)] private float trungPhatTayDong = 25.0f;

        [Tooltip("Thân gập xuống khi dộng (độ).")]
        [SerializeField, Range(3f, 20f)] private float trungPhatThanGap = 10.0f;
        #endregion

        #region LazeNguyenTo Settings
        [Header("=== LAZE NGUYÊN TỐ ===")]
        [Tooltip("Thân nâng lên khóa hướng (độ).")]
        [SerializeField, Range(2f, 15f)] private float lazeThanNang = 5.0f;

        [Tooltip("Tay trái hướng tia (độ).")]
        [SerializeField, Range(10f, 45f)] private float lazeTayTraiAngle = 24.0f;

        [Tooltip("Tay phải hướng tia (độ).")]
        [SerializeField, Range(10f, 45f)] private float lazeTayPhaiAngle = 20.0f;

        [Tooltip("Tần số rung giật cao tần (Hz).")]
        [SerializeField, Range(20f, 70f)] private float lazeJitterFreq = 40f;

        [Tooltip("Biên độ rung giật (độ).")]
        [SerializeField, Range(0.5f, 5f)] private float lazeJitterAmp = 1.5f;
        #endregion

        #region ToaNhietDienRong Settings
        [Header("=== TỎA NHIỆT DIỆN RỘNG ===")]
        [Tooltip("Thân ưỡn ngửa (độ).")]
        [SerializeField, Range(3f, 20f)] private float toaNhietThanNgua = 8.0f;

        [Tooltip("Tay dang rộng (độ).")]
        [SerializeField, Range(20f, 70f)] private float toaNhietTayDang = 50.0f;

        [Tooltip("Tần số rung chuyển (Hz).")]
        [SerializeField, Range(20f, 70f)] private float toaNhietShudderFreq = 45f;

        [Tooltip("Biên độ rung chuyển (độ).")]
        [SerializeField, Range(0.5f, 8f)] private float toaNhietShudderAmp = 2.2f;
        #endregion

        #region VienBinhTinhLinh Settings
        [Header("=== VIỆN BINH TINH LINH ===")]
        [Tooltip("Tay phải giơ cao hiệu triệu (độ). Spec: Tay phải giơ cao.")]
        [SerializeField, Range(20f, 70f)] private float vienBinhTayPhaiGio = 55.0f;

        [Tooltip("Tay trái hỗ trợ triệu hồi (độ).")]
        [SerializeField, Range(10f, 50f)] private float vienBinhTayTraiAngle = 30.0f;

        [Tooltip("Đầu hướng lên kết giới (độ).")]
        [SerializeField, Range(5f, 25f)] private float vienBinhDauAngle = 12.0f;

        [Tooltip("Tốc độ lượn sóng ma pháp (rad/s).")]
        [SerializeField, Range(1f, 8f)] private float vienBinhWaveSpeed = 3f;
        #endregion

        #region Charging Settings
        [Header("=== CHARGING (Tụ lực) ===")]
        [Tooltip("Thân gập thấp (độ).")]
        [SerializeField, Range(2f, 15f)] private float chargingThanGap = 6.0f;

        [Tooltip("Đầu cúi tập trung (độ).")]
        [SerializeField, Range(3f, 20f)] private float chargingDauCui = 8.0f;

        [Tooltip("Tay thu sát (độ).")]
        [SerializeField, Range(10f, 40f)] private float chargingTayThu = 22.0f;

        [Tooltip("Tần số rung khởi đầu (Hz).")]
        [SerializeField, Range(5f, 20f)] private float chargingFreqStart = 12f;

        [Tooltip("Tần số rung cực đại (Hz).")]
        [SerializeField, Range(20f, 60f)] private float chargingFreqEnd = 35f;

        [Tooltip("Thời gian đạt tần số cực đại (giây).")]
        [SerializeField, Range(0.5f, 5f)] private float chargingRampTime = 2.5f;
        #endregion

        #region Stun Settings
        [Header("=== STUNNED (Choáng) ===")]
        [Tooltip("Thân nghiêng lệch (độ).")]
        [SerializeField, Range(3f, 20f)] private float stunThanLech = 10.0f;

        [Tooltip("Đầu rũ gục (độ).")]
        [SerializeField, Range(10f, 35f)] private float stunDauRu = 20.0f;

        [Tooltip("Tay buông thõng (độ).")]
        [SerializeField, Range(5f, 30f)] private float stunTayBuong = 16.0f;

        [Tooltip("Tốc độ đung đưa bất lực (rad/s).")]
        [SerializeField, Range(0.5f, 4f)] private float stunSwaySpeed = 1.5f;
        #endregion

        #region Die Settings
        [Header("=== DIE (Rũ xuống) ===")]
        [Tooltip("Thân đổ gập (độ).")]
        [SerializeField, Range(5f, 30f)] private float dieThanDo = 15.0f;

        [Tooltip("Đầu gục (độ).")]
        [SerializeField, Range(10f, 40f)] private float dieDauGuc = 22.0f;

        [Tooltip("Tay rũ (độ).")]
        [SerializeField, Range(10f, 50f)] private float dieTayRu = 28.0f;

        [Tooltip("Thời gian rũ xuống (giây).")]
        [SerializeField, Range(0.3f, 3f)] private float dieFallDuration = 0.8f;
        #endregion

        #region Transition Settings
        [Header("=== TRANSITION ===")]
        [Tooltip("Thời gian chuyển tiếp mượt mà giữa 2 trạng thái (giây).")]
        [SerializeField, Range(0.05f, 0.5f)] private float transitionDuration = 0.15f;
        #endregion

        // Current runtime state
        private D1onkAnimState m_CurrentState = D1onkAnimState.Idle;
        private float m_StateTimer = 0f;
        private float m_CustomDuration = 0f;
        private Action m_OnCompleteCallback = null;
        private bool m_IsCustomPlaying = false;

        // State transition blending
        private float m_TransitionTimer = 0f;
        private bool m_IsTransitioning = false;
        private PartAngles m_TransitionFromAngles;

        // Cached initial transforms to GUARANTEE position and pivot preservation
        private readonly Dictionary<Transform, Vector3> m_InitialPositions = new Dictionary<Transform, Vector3>();
        private readonly Dictionary<Transform, Quaternion> m_InitialRotations = new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<Transform, float> m_InitialRotationsZ = new Dictionary<Transform, float>();
        private bool m_IsInitialized = false;

        // Struct for blending
        private struct PartAngles
        {
            public float than, dau, vaiTrai, vaiPhai, tayTrai, tayPhai, chienHam;
        }

        #region Public Properties
        public Transform Than => than;
        public Transform Dau => dau;
        public Transform VaiTrai => vaiTrai;
        public Transform VaiPhai => vaiPhai;
        public Transform VaiMatTrongTrai => vaiMatTrongTrai;
        public Transform VaiMatTrongPhai => vaiMatTrongPhai;
        public Transform TayTrai => tayTrai;
        public Transform TayPhai => tayPhai;
        public Transform ChienHam => chienHam;
        public Transform Sung2NongParent => sung2NongParent;
        public Transform Sung3NongParent => sung3NongParent;
        public Transform[] AllGuns => allGuns;
        public D1onkAnimState CurrentState => m_CurrentState;
        public bool IsInitialized => m_IsInitialized;
        #endregion

        private void Awake()
        {
            InitializeHierarchy();
        }

        private void Start()
        {
            ValidateReferences();
        }

        /// <summary>
        /// Khởi tạo và ghi nhớ chính xác localPosition và localRotation ban đầu của toàn bộ bộ phận.
        /// </summary>
        public void InitializeHierarchy(bool force = false)
        {
            if (m_IsInitialized && !force) return;

            m_InitialPositions.Clear();
            m_InitialRotations.Clear();
            m_InitialRotationsZ.Clear();

            CacheTransform(than);
            CacheTransform(dau);
            CacheTransform(vaiTrai);
            CacheTransform(vaiPhai);
            CacheTransform(vaiMatTrongTrai);
            CacheTransform(vaiMatTrongPhai);
            CacheTransform(tayTrai);
            CacheTransform(tayPhai);
            CacheTransform(chienHam);
            CacheTransform(sung2NongParent);
            CacheTransform(sung3NongParent);

            if (allGuns != null)
            {
                foreach (var gun in allGuns)
                {
                    CacheTransform(gun);
                }
            }

            m_IsInitialized = true;
        }

        private void CacheTransform(Transform t)
        {
            if (t == null) return;
            m_InitialPositions[t] = t.localPosition;
            m_InitialRotations[t] = t.localRotation;
            m_InitialRotationsZ[t] = t.localEulerAngles.z;
        }

        /// <summary>
        /// Kiểm tra tường minh các tham chiếu Inspector, ghi log cảnh báo rõ ràng nếu thiếu.
        /// TUYỆT ĐỐI không tự ý AddComponent ngầm.
        /// </summary>
        public void ValidateReferences()
        {
            if (than == null) Debug.LogWarning("[D1onkProceduralAnimator] Missing reference: 'than' (Thân) is not assigned on Inspector!", this);
            if (dau == null) Debug.LogWarning("[D1onkProceduralAnimator] Missing reference: 'dau' (Đầu) is not assigned on Inspector!", this);
            if (vaiTrai == null) Debug.LogWarning("[D1onkProceduralAnimator] Missing reference: 'vaiTrai' (Vai trái) is not assigned on Inspector!", this);
            if (vaiPhai == null) Debug.LogWarning("[D1onkProceduralAnimator] Missing reference: 'vaiPhai' (Vai phải) is not assigned on Inspector!", this);
            if (vaiMatTrongTrai == null) Debug.LogWarning("[D1onkProceduralAnimator] Missing reference: 'vaiMatTrongTrai' (Vai mặt trong trái) is not assigned on Inspector!", this);
            if (vaiMatTrongPhai == null) Debug.LogWarning("[D1onkProceduralAnimator] Missing reference: 'vaiMatTrongPhai' (Vai mặt trong phải) is not assigned on Inspector!", this);
            if (tayTrai == null) Debug.LogWarning("[D1onkProceduralAnimator] Missing reference: 'tayTrai' (Tay trái) is not assigned on Inspector!", this);
            if (tayPhai == null) Debug.LogWarning("[D1onkProceduralAnimator] Missing reference: 'tayPhai' (Tay phải) is not assigned on Inspector!", this);
            if (chienHam == null) Debug.LogWarning("[D1onkProceduralAnimator] Missing reference: 'chienHam' (Chiến hạm) is not assigned on Inspector!", this);
            if (sung2NongParent == null) Debug.LogWarning("[D1onkProceduralAnimator] Missing reference: 'sung2NongParent' (Súng 2 nòng) is not assigned on Inspector!", this);
            if (sung3NongParent == null) Debug.LogWarning("[D1onkProceduralAnimator] Missing reference: 'sung3NongParent' (Súng 3 nòng) is not assigned on Inspector!", this);
            if (allGuns == null || allGuns.Length < 14) Debug.LogWarning($"[D1onkProceduralAnimator] Missing gun references: expected 14 guns, got {allGuns?.Length ?? 0}!", this);
        }

        private void Update()
        {
            ManualTick(Time.deltaTime);
        }

        /// <summary>
        /// Tiến trình cập nhật diễn hoạt theo thời gian. Cung cấp ManualTick để unit test có thể kiểm thử trực tiếp.
        /// </summary>
        public void ManualTick(float dt)
        {
            if (!m_IsInitialized) InitializeHierarchy();

            m_StateTimer += dt;

            // Xử lý transition blending
            if (m_IsTransitioning)
            {
                m_TransitionTimer += dt;
                if (m_TransitionTimer >= transitionDuration)
                {
                    m_IsTransitioning = false;
                }
            }

            // Xử lý hoàn tất kỹ năng có thời hạn
            if (m_IsCustomPlaying && m_CustomDuration > 0f && m_StateTimer >= m_CustomDuration)
            {
                var cb = m_OnCompleteCallback;
                m_IsCustomPlaying = false;
                BeginTransitionTo(D1onkAnimState.Idle);
                m_CustomDuration = 0f;
                m_OnCompleteCallback = null;
                cb?.Invoke();
            }

            // Tính toán góc xoay Z cho từng bộ phận dựa trên trạng thái hiện tại
            EvaluateAndApplyRotations(m_CurrentState, m_StateTimer);

            // BẢO VỆ TUYỆT ĐỐI RÀNG BUỘC KIẾN TRÚC: Bảo toàn vị trí localPosition
            EnforcePositionPreservation();
        }

        /// <summary>
        /// Bắt đầu chuyển tiếp mượt mà sang trạng thái mới.
        /// </summary>
        private void BeginTransitionTo(D1onkAnimState newState)
        {
            if (m_CurrentState == newState && !m_IsCustomPlaying) return;

            // Lưu lại góc hiện tại để blend
            m_TransitionFromAngles = CaptureCurrentAngles();
            m_IsTransitioning = true;
            m_TransitionTimer = 0f;
            m_CurrentState = newState;
            m_StateTimer = 0f;
        }

        private PartAngles CaptureCurrentAngles()
        {
            return new PartAngles
            {
                than = GetCurrentDeltaZ(than),
                dau = GetCurrentDeltaZ(dau),
                vaiTrai = GetCurrentDeltaZ(vaiTrai),
                vaiPhai = GetCurrentDeltaZ(vaiPhai),
                tayTrai = GetCurrentDeltaZ(tayTrai),
                tayPhai = GetCurrentDeltaZ(tayPhai),
                chienHam = GetCurrentDeltaZ(chienHam)
            };
        }

        /// <summary>
        /// Kích hoạt một diễn hoạt kỹ năng procedural trong khoảng thời gian xác định, sau đó tự hồi về Idle.
        /// </summary>
        public void PlayAnimation(D1onkAnimState state, float duration = 1.5f, Action onComplete = null)
        {
            if (!m_IsInitialized) InitializeHierarchy();

            if (duration <= 0f)
            {
                BeginTransitionTo(state);
                m_CustomDuration = 0f;
                m_IsCustomPlaying = false;
                m_OnCompleteCallback = null;
                onComplete?.Invoke();
                return;
            }

            BeginTransitionTo(state);
            m_CustomDuration = duration;
            m_OnCompleteCallback = onComplete;
            m_IsCustomPlaying = true;
        }

        /// <summary>
        /// Đặt trực tiếp trạng thái diễn hoạt lặp vô hạn (như Idle, Move, Charging, Stun, Die).
        /// </summary>
        public void SetState(D1onkAnimState state)
        {
            if (!m_IsInitialized) InitializeHierarchy();

            BeginTransitionTo(state);
            m_CustomDuration = 0f;
            m_IsCustomPlaying = false;
            m_OnCompleteCallback = null;
        }

        private void EvaluateAndApplyRotations(D1onkAnimState state, float t)
        {
            PartAngles target = EvaluateState(state, t);

            // Blend từ góc cũ sang góc mới nếu đang chuyển tiếp
            if (m_IsTransitioning && transitionDuration > 0f)
            {
                float blendT = Mathf.Clamp01(m_TransitionTimer / transitionDuration);
                // Dùng SmoothStep cho chuyển tiếp mượt
                blendT = Mathf.SmoothStep(0f, 1f, blendT);

                target.than = Mathf.Lerp(m_TransitionFromAngles.than, target.than, blendT);
                target.dau = Mathf.Lerp(m_TransitionFromAngles.dau, target.dau, blendT);
                target.vaiTrai = Mathf.Lerp(m_TransitionFromAngles.vaiTrai, target.vaiTrai, blendT);
                target.vaiPhai = Mathf.Lerp(m_TransitionFromAngles.vaiPhai, target.vaiPhai, blendT);
                target.tayTrai = Mathf.Lerp(m_TransitionFromAngles.tayTrai, target.tayTrai, blendT);
                target.tayPhai = Mathf.Lerp(m_TransitionFromAngles.tayPhai, target.tayPhai, blendT);
                target.chienHam = Mathf.Lerp(m_TransitionFromAngles.chienHam, target.chienHam, blendT);
            }

            // Áp dụng góc xoay quanh trục Z, cộng dồn với góc ban đầu (initialRotZ + deltaZ)
            ApplyRotationZ(than, target.than);
            ApplyRotationZ(dau, target.dau);
            ApplyRotationZ(vaiTrai, target.vaiTrai);
            ApplyRotationZ(vaiPhai, target.vaiPhai);
            ApplyRotationZ(vaiMatTrongTrai, target.vaiTrai * 0.5f);
            ApplyRotationZ(vaiMatTrongPhai, target.vaiPhai * 0.5f);
            ApplyRotationZ(tayTrai, target.tayTrai);
            ApplyRotationZ(tayPhai, target.tayPhai);
            ApplyRotationZ(chienHam, target.chienHam);
        }

        /// <summary>
        /// Tính toán góc xoay delta Z cho từng bộ phận theo trạng thái và thời gian.
        /// Tách ra method riêng để hỗ trợ blending.
        /// </summary>
        private PartAngles EvaluateState(D1onkAnimState state, float t)
        {
            PartAngles a = default;

            switch (state)
            {
                case D1onkAnimState.Idle:
                    EvaluateIdle(ref a, t);
                    break;
                case D1onkAnimState.Move:
                    EvaluateMove(ref a, t);
                    break;
                case D1onkAnimState.PhaoHamDaNong:
                    EvaluatePhaoHamDaNong(ref a, t);
                    break;
                case D1onkAnimState.TrungPhat:
                    EvaluateTrungPhat(ref a, t);
                    break;
                case D1onkAnimState.LazeNguyenTo:
                    EvaluateLazeNguyenTo(ref a, t);
                    break;
                case D1onkAnimState.ToaNhietDienRong:
                    EvaluateToaNhietDienRong(ref a, t);
                    break;
                case D1onkAnimState.VienBinhTinhLinh:
                    EvaluateVienBinhTinhLinh(ref a, t);
                    break;
                case D1onkAnimState.Charging:
                    EvaluateCharging(ref a, t);
                    break;
                case D1onkAnimState.Stun:
                    EvaluateStun(ref a, t);
                    break;
                case D1onkAnimState.Die:
                    EvaluateDie(ref a, t);
                    break;
            }

            return a;
        }

        #region Animation Evaluators

        /// <summary>
        /// Idle Breathing: Thân ±1.5°, Đầu ±1.0° ngược pha, Tay ±3.5°.
        /// Hơi thở nhẹ nhàng, tự nhiên — tất cả cấu hình trên Inspector.
        /// </summary>
        private void EvaluateIdle(ref PartAngles a, float t)
        {
            float w = idleSpeed;
            float sinT = Mathf.Sin(w * t);
            float sinTShifted = Mathf.Sin(w * t + Mathf.PI); // Ngược pha hoàn toàn
            float sinTArm = Mathf.Sin(w * t + 0.8f);

            a.than = sinT * idleThanAmplitude;
            a.dau = sinTShifted * idleDauAmplitude; // Ngược pha với Thân
            a.vaiTrai = sinT * idleVaiAmplitude;
            a.vaiPhai = -sinT * idleVaiAmplitude;
            a.tayTrai = sinTArm * idleTayAmplitude;
            a.tayPhai = -sinTArm * idleTayAmplitude;
            a.chienHam = Mathf.Sin(w * 0.5f * t) * idleChienHamAmplitude;
        }

        /// <summary>
        /// Move: Phiên bản mạnh hơn của Idle, lắc lư rõ ràng khi di chuyển.
        /// </summary>
        private void EvaluateMove(ref PartAngles a, float t)
        {
            float w = idleSpeed * moveSpeedMultiplier;

            a.than = Mathf.Sin(w * t) * moveThanAmplitude;
            a.dau = Mathf.Cos(w * t) * (moveThanAmplitude + 0.5f);
            a.vaiTrai = Mathf.Sin(w * t) * moveThanAmplitude;
            a.vaiPhai = -Mathf.Sin(w * t) * moveThanAmplitude;
            a.tayTrai = Mathf.Sin(w * t + 0.5f) * moveTayAmplitude;
            a.tayPhai = -Mathf.Sin(w * t + 0.5f) * moveTayAmplitude;
            a.chienHam = Mathf.Sin(w * t) * (moveThanAmplitude * 0.5f);
        }

        /// <summary>
        /// Pháo Hạm Đa Nòng (Bị động): Thân giật ngửa, Đầu chúi xuống, Tay gồng giật phản lực.
        /// Rung chấn mạnh theo nhịp pháo bắn.
        /// </summary>
        private void EvaluatePhaoHamDaNong(ref PartAngles a, float t)
        {
            float totalDur = m_CustomDuration > 0f ? m_CustomDuration : 0.8f;
            float u = Mathf.Clamp01(t / totalDur);
            float envelope = ComputeEnvelope(u, 0.08f, 0.85f);

            float blastTremor = Mathf.Sin(t * phaoTremorFreq) * phaoTremorAmp;

            a.than = (-phaoThanRecoil + blastTremor * 0.7f) * envelope;
            a.dau = (phaoDauChui - blastTremor * 0.4f) * envelope;
            a.vaiTrai = (phaoThanRecoil + blastTremor * 0.5f) * envelope;
            a.vaiPhai = (-phaoThanRecoil - blastTremor * 0.5f) * envelope;
            a.tayTrai = (-phaoTayRecoil + blastTremor) * envelope;
            a.tayPhai = (phaoTayRecoil - blastTremor) * envelope;
            a.chienHam = (blastTremor * 0.6f) * envelope;
        }

        /// <summary>
        /// Trừng Phạt: 3 pha — (1) Giơ tay lên cao, (2) Dộng mạnh xuống + thân gập, (3) Hồi phục.
        /// </summary>
        private void EvaluateTrungPhat(ref PartAngles a, float t)
        {
            float totalDur = m_CustomDuration > 0f ? m_CustomDuration : 1.5f;
            float u = Mathf.Clamp01(t / totalDur);

            if (u < 0.4f)
            {
                // Phase 1: Giơ 2 tay lên cao, ngửa người tụ điện sấm sét
                float p = u / 0.4f;
                float curve = Mathf.SmoothStep(0f, 1f, p);
                a.than = -6f * curve;
                a.dau = 10f * curve;
                a.vaiTrai = -8f * curve;
                a.vaiPhai = 8f * curve;
                a.tayTrai = -trungPhatTayVung * curve;
                a.tayPhai = trungPhatTayVung * curve;
            }
            else if (u < 0.7f)
            {
                // Phase 2: Đập mạnh 2 tay chỉ điểm sấm sét giáng xuống
                float p = (u - 0.4f) / 0.3f;
                float curve = Mathf.SmoothStep(0f, 1f, p);
                a.than = Mathf.Lerp(-6f, trungPhatThanGap, curve);
                a.dau = Mathf.Lerp(10f, -12f, curve);
                a.vaiTrai = Mathf.Lerp(-8f, 10f, curve);
                a.vaiPhai = Mathf.Lerp(8f, -10f, curve);
                a.tayTrai = Mathf.Lerp(-trungPhatTayVung, trungPhatTayDong, curve);
                a.tayPhai = Mathf.Lerp(trungPhatTayVung, -trungPhatTayDong, curve);
                // Rung va đập
                a.than += Mathf.Sin(t * 50f) * 1.5f * (1f - p);
            }
            else
            {
                // Phase 3: Hồi phục mượt mà
                float p = (u - 0.7f) / 0.3f;
                float curve = Mathf.SmoothStep(0f, 1f, p);
                a.than = Mathf.Lerp(trungPhatThanGap, 0f, curve);
                a.dau = Mathf.Lerp(-12f, 0f, curve);
                a.vaiTrai = Mathf.Lerp(10f, 0f, curve);
                a.vaiPhai = Mathf.Lerp(-10f, 0f, curve);
                a.tayTrai = Mathf.Lerp(trungPhatTayDong, 0f, curve);
                a.tayPhai = Mathf.Lerp(-trungPhatTayDong, 0f, curve);
            }
        }

        /// <summary>
        /// Laze Nguyên Tố: Thân/tay nâng lên khóa hướng, rung giật cao tần liên tục.
        /// </summary>
        private void EvaluateLazeNguyenTo(ref PartAngles a, float t)
        {
            float totalDur = m_CustomDuration > 0f ? m_CustomDuration : 2.5f;
            float u = Mathf.Clamp01(t / totalDur);
            float envelope = ComputeEnvelope(u, 0.08f, 0.85f);

            float jitter = Mathf.Sin(t * lazeJitterFreq) * lazeJitterAmp;

            a.than = (lazeThanNang + jitter * 0.5f) * envelope;
            a.dau = (-4.0f + jitter * 0.3f) * envelope;
            a.vaiTrai = 4.0f * envelope;
            a.vaiPhai = -4.0f * envelope;
            a.tayTrai = (-lazeTayTraiAngle + jitter) * envelope;
            a.tayPhai = (-lazeTayPhaiAngle - jitter) * envelope;
            a.chienHam = (jitter * 0.8f) * envelope;
        }

        /// <summary>
        /// Tỏa Nhiệt Diện Rộng: Bung rộng 2 tay sang 2 bên, thân ưỡn ngửa.
        /// </summary>
        private void EvaluateToaNhietDienRong(ref PartAngles a, float t)
        {
            float totalDur = m_CustomDuration > 0f ? m_CustomDuration : 1.5f;
            float u = Mathf.Clamp01(t / totalDur);
            float envelope = ComputeEnvelope(u, 0.08f, 0.85f);

            float shudder = Mathf.Sin(t * toaNhietShudderFreq) * toaNhietShudderAmp;

            a.than = (-toaNhietThanNgua + shudder * 0.5f) * envelope;
            a.dau = (12.0f + shudder * 0.4f) * envelope;
            a.vaiTrai = 12.0f * envelope;
            a.vaiPhai = -12.0f * envelope;
            a.tayTrai = (-toaNhietTayDang + shudder) * envelope;
            a.tayPhai = (toaNhietTayDang - shudder) * envelope;
            a.chienHam = shudder * envelope;
        }

        /// <summary>
        /// Viện Binh Tinh Linh: Tay phải giơ cao hiệu triệu, tay trái hỗ trợ, thân lượn sóng ma pháp.
        /// Spec: "Tay phải giơ cao hiệu triệu".
        /// </summary>
        private void EvaluateVienBinhTinhLinh(ref PartAngles a, float t)
        {
            float totalDur = m_CustomDuration > 0f ? m_CustomDuration : 1.8f;
            float u = Mathf.Clamp01(t / totalDur);
            float envelope = ComputeEnvelope(u, 0.08f, 0.85f);

            float wave = Mathf.Sin(t * vienBinhWaveSpeed);
            float summonPulse = Mathf.Sin(t * 5f) * 6.0f;

            a.than = (wave * 4.0f) * envelope;
            a.dau = (vienBinhDauAngle + wave * 2.0f) * envelope;
            a.vaiTrai = (-6.0f + wave * 2.0f) * envelope;
            a.vaiPhai = (12.0f - wave * 2.0f) * envelope;
            // Tay phải giơ CAO hiệu triệu (góc lớn hơn)
            a.tayPhai = (vienBinhTayPhaiGio + summonPulse) * envelope;
            // Tay trái hỗ trợ ở mức thấp hơn
            a.tayTrai = (-vienBinhTayTraiAngle + Mathf.Sin(t * 3f) * 4.0f) * envelope;
            a.chienHam = (wave * 1.5f) * envelope;
        }

        /// <summary>
        /// Charging: Tụ lực trước bùng nổ. Thân gập, tay thu, rung tăng dần.
        /// </summary>
        private void EvaluateCharging(ref PartAngles a, float t)
        {
            float freq = Mathf.Lerp(chargingFreqStart, chargingFreqEnd, Mathf.Clamp01(t / chargingRampTime));
            float tremor = Mathf.Sin(t * freq) * 1.8f;

            a.than = chargingThanGap + tremor * 0.6f;
            a.dau = -chargingDauCui + tremor * 0.4f;
            a.vaiTrai = -3.0f + tremor * 0.3f;
            a.vaiPhai = 3.0f - tremor * 0.3f;
            a.tayTrai = chargingTayThu + tremor;
            a.tayPhai = -chargingTayThu - tremor;
            a.chienHam = tremor * 0.8f;
        }

        /// <summary>
        /// Stunned: Đầu rũ gục, thân nghiêng, tay buông thõng đung đưa bất lực.
        /// </summary>
        private void EvaluateStun(ref PartAngles a, float t)
        {
            float limp = Mathf.Sin(t * stunSwaySpeed);

            a.than = -stunThanLech + limp * 1.5f;
            a.dau = -stunDauRu + limp * 2.0f;
            a.vaiTrai = -6.0f;
            a.vaiPhai = -4.0f;
            a.tayTrai = stunTayBuong + limp * 4.0f;
            a.tayPhai = -stunTayBuong + limp * 4.0f;
        }

        /// <summary>
        /// Die: Rũ xuống toàn bộ — có animation rũ dần thay vì nhảy instant.
        /// </summary>
        private void EvaluateDie(ref PartAngles a, float t)
        {
            // Rũ dần trong dieFallDuration giây
            float fallProgress = Mathf.Clamp01(t / dieFallDuration);
            float curve = Mathf.SmoothStep(0f, 1f, fallProgress);

            a.than = dieThanDo * curve;
            a.dau = -dieDauGuc * curve;
            a.vaiTrai = 10.0f * curve;
            a.vaiPhai = -10.0f * curve;
            a.tayTrai = dieTayRu * curve;
            a.tayPhai = -dieTayRu * curve;
            a.chienHam = -2.0f * curve;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Tính envelope fade-in / fade-out cho các kỹ năng có thời hạn.
        /// </summary>
        private static float ComputeEnvelope(float normalizedTime, float fadeInEnd, float fadeOutStart)
        {
            if (normalizedTime < fadeInEnd)
                return Mathf.SmoothStep(0f, 1f, normalizedTime / fadeInEnd);
            if (normalizedTime > fadeOutStart)
                return Mathf.SmoothStep(1f, 0f, (normalizedTime - fadeOutStart) / (1f - fadeOutStart));
            return 1f;
        }

        private void ApplyRotationZ(Transform t, float deltaZ)
        {
            if (t == null) return;
            if (m_InitialRotationsZ.TryGetValue(t, out float baseZ))
            {
                t.localRotation = Quaternion.Euler(0f, 0f, baseZ + deltaZ);
            }
        }

        /// <summary>
        /// BẢO VỆ TUYỆT ĐỐI RÀNG BUỘC KIẾN TRÚC:
        /// Ép buộc localPosition của mọi bộ phận luôn luôn giữ nguyên giá trị ban đầu, không bao giờ bị lệch.
        /// </summary>
        private void EnforcePositionPreservation()
        {
            foreach (var kvp in m_InitialPositions)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.localPosition = kvp.Value;
                }
            }
        }

        /// <summary>
        /// Hàm kiểm tra độ toàn vẹn của vị trí và pivot. Phục vụ tự động kiểm thử.
        /// Trả về true nếu toàn bộ bộ phận vẫn bảo toàn 100% localPosition ban đầu.
        /// </summary>
        public bool VerifyPositionsPreserved(float tolerance = 0.0001f)
        {
            if (!m_IsInitialized) InitializeHierarchy();

            foreach (var kvp in m_InitialPositions)
            {
                if (kvp.Key == null) continue;
                float dist = Vector3.Distance(kvp.Key.localPosition, kvp.Value);
                if (dist > tolerance)
                {
                    Debug.LogError($"[D1onkProceduralAnimator] Position violation on '{kvp.Key.name}'! Expected {kvp.Value}, got {kvp.Key.localPosition} (diff={dist:F6})");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Lấy giá trị góc xoay delta Z hiện tại của một bộ phận so với góc khởi tạo ban đầu.
        /// </summary>
        public float GetCurrentDeltaZ(Transform t)
        {
            if (t == null) return 0f;
            if (m_InitialRotationsZ.TryGetValue(t, out float baseZ))
            {
                return Mathf.DeltaAngle(baseZ, t.localEulerAngles.z);
            }
            return 0f;
        }

        /// <summary>
        /// Reset toàn bộ bộ phận về rotation ban đầu (useful khi tắt hoặc reset animator).
        /// </summary>
        public void ResetToInitialPose()
        {
            if (!m_IsInitialized) return;
            foreach (var kvp in m_InitialRotations)
            {
                if (kvp.Key != null)
                    kvp.Key.localRotation = kvp.Value;
            }
            foreach (var kvp in m_InitialPositions)
            {
                if (kvp.Key != null)
                    kvp.Key.localPosition = kvp.Value;
            }
        }

        #endregion

#if UNITY_EDITOR
        [Header("=== EDITOR PREVIEW ===")]
        [Tooltip("Bật xem trước animation trong Editor (không cần Play Mode).")]
        [SerializeField] private bool editorPreview = false;

        [Tooltip("Trạng thái preview trong Editor.")]
        [SerializeField] private D1onkAnimState editorPreviewState = D1onkAnimState.Idle;

        private float m_EditorTime = 0f;
        private double m_LastEditorTime = 0;
        private bool m_EditorInitialized = false;

        private void OnValidate()
        {
            // Trong Editor, tự động tìm và gán nếu còn trống để thuận tiện cấu hình Inspector
            AutoWireIfEmpty();
        }

        /// <summary>
        /// Editor Update để hiển thị preview animation khi không ở Play Mode.
        /// </summary>
        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.update += EditorUpdate;
            }
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.update -= EditorUpdate;
                // Reset về pose ban đầu khi tắt preview
                if (m_EditorInitialized)
                {
                    ResetToInitialPose();
                    m_EditorInitialized = false;
                }
            }
        }

        private void EditorUpdate()
        {
            if (Application.isPlaying) return;
            if (!editorPreview) 
            {
                if (m_EditorInitialized)
                {
                    ResetToInitialPose();
                    m_EditorInitialized = false;
                    m_EditorTime = 0f;
                }
                return;
            }
            if (this == null) return;

            // Khởi tạo nếu chưa
            if (!m_EditorInitialized)
            {
                InitializeHierarchy(true);
                m_EditorInitialized = true;
                m_LastEditorTime = UnityEditor.EditorApplication.timeSinceStartup;
                m_EditorTime = 0f;
            }

            double now = UnityEditor.EditorApplication.timeSinceStartup;
            float dt = (float)(now - m_LastEditorTime);
            m_LastEditorTime = now;

            // Clamp delta để tránh giật khi editor lag
            dt = Mathf.Min(dt, 0.1f);
            m_EditorTime += dt;

            // Tính và áp dụng rotations cho trạng thái preview
            PartAngles angles = EvaluateState(editorPreviewState, m_EditorTime);
            ApplyRotationZ(than, angles.than);
            ApplyRotationZ(dau, angles.dau);
            ApplyRotationZ(vaiTrai, angles.vaiTrai);
            ApplyRotationZ(vaiPhai, angles.vaiPhai);
            ApplyRotationZ(vaiMatTrongTrai, angles.vaiTrai * 0.5f);
            ApplyRotationZ(vaiMatTrongPhai, angles.vaiPhai * 0.5f);
            ApplyRotationZ(tayTrai, angles.tayTrai);
            ApplyRotationZ(tayPhai, angles.tayPhai);
            ApplyRotationZ(chienHam, angles.chienHam);

            // Bảo toàn position
            EnforcePositionPreservation();

            // Yêu cầu Scene View repaint
            UnityEditor.SceneView.RepaintAll();
        }

        public void AutoWireIfEmpty()
        {
            Transform root = transform;
            Transform model = root.Find("EnemyModel") ?? root;
            Transform ship = model.Find("Chiến hạm");

            if (chienHam == null && ship != null) chienHam = ship;

            if (ship != null)
            {
                if (than == null) than = ship.Find("Thân");
                if (sung2NongParent == null) sung2NongParent = ship.Find("Súng 2 nòng");
                if (sung3NongParent == null) sung3NongParent = ship.Find("Súng 3 nòng");
            }

            if (than != null)
            {
                if (dau == null) dau = than.Find("Đầu");
                if (vaiTrai == null) vaiTrai = than.Find("Vai trái");
                if (vaiPhai == null) vaiPhai = than.Find("Vai phải");
                if (tayTrai == null) tayTrai = than.Find("Tay trái");
                if (tayPhai == null) tayPhai = than.Find("Tay phải");

                if (vaiTrai != null && vaiMatTrongTrai == null) vaiMatTrongTrai = vaiTrai.Find("Vai mặt trong");
                if (vaiPhai != null && vaiMatTrongPhai == null) vaiMatTrongPhai = vaiPhai.Find("Vai mặt trong");
            }

            if ((allGuns == null || allGuns.Length < 14) && sung2NongParent != null && sung3NongParent != null)
            {
                var gunList = new List<Transform>();
                foreach (Transform c in sung2NongParent) gunList.Add(c);
                foreach (Transform c in sung3NongParent) gunList.Add(c);
                allGuns = gunList.ToArray();
            }
        }
#endif
    }
}
