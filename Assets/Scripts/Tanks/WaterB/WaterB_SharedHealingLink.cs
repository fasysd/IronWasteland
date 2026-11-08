using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.WaterB
{
    /// <summary>
    /// Shared Healing: sợi dây nước trong suốt, lấp lánh, nối liên tục Water B với 1 đồng minh.
    /// Bất kỳ hiệu quả hồi máu nào mà 1 bên nhận được cũng tự động chia sẻ 1 phần sang bên còn lại.
    ///
    /// - LOGIC: nghe sự kiện TankBrain.OnHealed của cả 2 xe (không phụ thuộc hình ảnh).
    /// - HÌNH ẢNH: 1 LineRenderer nối 2 xe (uốn lượn + lấp lánh), mỗi lần có hồi máu thì 1 xung sáng chạy dọc
    ///   sợi dây từ bên nhận sang bên còn lại và tỏa hiệu ứng hồi phục ở bên đó. KHÔNG còn bắn đạn.
    ///
    /// Có thể dùng không cần prefab (script tự tạo dây mặc định). Nếu prefab có sẵn LineRenderer thì dùng cái đó
    /// (script vẫn điều khiển vị trí, gradient màu/lấp lánh và độ rộng nhấp nháy).
    /// </summary>
    [DisallowMultipleComponent]
    public class WaterB_SharedHealingLink : MonoBehaviour
    {
        private const int KeyCount = 8; // Gradient của Unity tối đa 8 key màu / 8 key alpha

        [Header("Rope")]
        [SerializeField, Range(4, 64)] private int segments = 24;
        [SerializeField, Min(0.01f)] private float ropeWidth = 0.18f;            // Chỉ dùng khi tự tạo dây mặc định
        [SerializeField, Min(0f)] private float waveAmplitude = 0.25f;           // Độ uốn lượn của dây
        [SerializeField, Min(0f)] private float waveFrequency = 1.5f;            // Số chu kỳ sóng dọc dây
        [SerializeField] private float waveSpeed = 4f;
        [SerializeField, Min(0f)] private float widthBreathing = 0.15f;          // Dây phồng/xẹp nhẹ
        [SerializeField, Min(0.05f)] private float fadeOutTime = 0.5f;           // Mờ dần khi sắp hết thời gian

        [Header("Sparkle")]
        [SerializeField] private Color ropeColor = new Color(0.55f, 0.95f, 1f, 0.5f);   // Nước trong suốt
        [SerializeField] private Color sparkleColor = new Color(1f, 1f, 1f, 0.95f);     // Điểm sáng lấp lánh
        [SerializeField, Min(0f)] private float sparkleSpeed = 6f;

        [Header("Heal Pulse (xung sáng chạy dọc dây khi có hồi máu)")]
        [Tooltip("Prefab xung sáng (nên có TrailRenderer). Để trống = tự tạo xung mặc định.")]
        [SerializeField] private GameObject pulsePrefab;
        [Tooltip("Hiệu ứng tỏa hồi phục (particle...) sinh ra tại xe nhận máu. Để trống = không có.")]
        [SerializeField] private GameObject healBurstPrefab;
        [SerializeField, Min(0.1f)] private float pulseTravelTime = 0.35f;
        [SerializeField, Min(1)] private int maxPulses = 8;
        [SerializeField, Min(0.1f)] private float burstLifetime = 2f;
        [SerializeField] private Color pulseColor = new Color(0.7f, 1f, 1f, 1f);
        [SerializeField, Min(0.01f)] private float pulseWidth = 0.35f;
        [SerializeField, Min(0.05f)] private float pulseTrailTime = 0.3f;

        private class Pulse
        {
            public GameObject Go;
            public TrailRenderer Trail;
            public TankBrain Target;
            public bool TowardAlly; // true: chạy từ Water B sang đồng minh; false: ngược lại
            public float Progress;
        }

        private TankBrain m_OwnerTank;      // Water B
        private TankBrain m_TargetAllyTank; // Đồng minh được liên kết
        private float m_SharedPercent;
        private bool m_IsActive;
        private float m_EndTime;

        private LineRenderer m_Line;
        private float m_BaseWidthMultiplier = 1f;
        private Gradient m_Gradient;
        private readonly GradientColorKey[] m_ColorKeys = new GradientColorKey[KeyCount];
        private readonly GradientAlphaKey[] m_AlphaKeys = new GradientAlphaKey[KeyCount];
        private Vector3[] m_Points;

        // Hai đầu dây & hướng vuông góc (tính lại mỗi frame, xung sáng dùng chung để bám đúng sợi dây)
        private Vector3 m_A;
        private Vector3 m_B;
        private Vector3 m_Perp = Vector3.up;

        private readonly List<Pulse> m_Pulses = new List<Pulse>();

        public void Initialize(TankBrain owner, TankBrain targetAlly, float sharedPercent, float duration)
        {
            if (owner == null || targetAlly == null)
            {
                Destroy(gameObject);
                return;
            }

            m_OwnerTank = owner;
            m_TargetAllyTank = targetAlly;
            m_SharedPercent = sharedPercent;
            m_IsActive = true;

            float life = duration > 0f ? duration : 10f;
            m_EndTime = Time.time + life;

            // LOGIC: lắng nghe hồi máu của CẢ HAI tank
            m_OwnerTank.OnHealed += HandleTankHealed;
            m_TargetAllyTank.OnHealed += HandleTankHealed;

            // HÌNH ẢNH: sợi dây nối 2 xe
            SetupLine();
            UpdateRope();
            ApplyGradient(1f);

            Destroy(gameObject, life);
        }

        private void OnDestroy()
        {
            m_IsActive = false;

            if (m_OwnerTank != null) m_OwnerTank.OnHealed -= HandleTankHealed;
            if (m_TargetAllyTank != null) m_TargetAllyTank.OnHealed -= HandleTankHealed;

            for (int i = 0; i < m_Pulses.Count; i++)
            {
                if (m_Pulses[i].Go != null) Destroy(m_Pulses[i].Go);
            }
            m_Pulses.Clear();
        }

        private void Update()
        {
            if (!m_IsActive || m_OwnerTank == null || m_TargetAllyTank == null
                || !m_OwnerTank.IsAlive || !m_TargetAllyTank.IsAlive)
            {
                Destroy(gameObject);
                return;
            }

            float remaining = m_EndTime - Time.time;
            float fade = fadeOutTime > 0f ? Mathf.Clamp01(remaining / fadeOutTime) : 1f;

            UpdateRope();
            ApplyGradient(fade);
            UpdatePulses();
        }

        // ------------------------------------------------------------------
        // LOGIC: chia sẻ hồi máu
        // ------------------------------------------------------------------

        /// <summary>
        /// Được gọi khi BẤT KÌ tank nào trong liên kết nhận máu.
        /// Chia sẻ hồi NGAY LẬP TỨC; phần hình ảnh chỉ là xung sáng chạy dọc dây.
        /// </summary>
        private void HandleTankHealed(TankBrain receiver, float amount, bool isByShare)
        {
            // Máu do chia sẻ thì KHÔNG chia sẻ lại -> không bị vòng lặp vô tận
            if (!m_IsActive || isByShare || amount <= 0f) return;

            TankBrain other = (receiver == m_OwnerTank) ? m_TargetAllyTank : m_OwnerTank;
            if (other == null) return;

            float sharedAmount = amount * m_SharedPercent;
            if (sharedAmount <= 0f) return;

            other.Heal(sharedAmount, true);
            TextDamageManager.Instance?.Show(sharedAmount, other.transform.position);

            Debug.Log($"[SharedHealing] {receiver.name} nhận {amount} HP -> chia sẻ {sharedAmount} HP cho {other.name}.");

            // HÌNH ẢNH: tỏa hồi phục ở bên nhận, xung sáng chạy sang bên còn lại
            SpawnBurst(receiver);
            SpawnPulse(other, receiver == m_OwnerTank);
        }

        // ------------------------------------------------------------------
        // HÌNH ẢNH: sợi dây
        // ------------------------------------------------------------------
        private void SetupLine()
        {
            m_Line = GetComponent<LineRenderer>();
            if (m_Line == null)
            {
                m_Line = gameObject.AddComponent<LineRenderer>();
                m_Line.startWidth = ropeWidth;
                m_Line.endWidth = ropeWidth;
                m_Line.numCapVertices = 4;
                m_Line.numCornerVertices = 2;
                m_Line.material = new Material(Shader.Find("Sprites/Default"));
                m_Line.sortingOrder = 5;
            }

            m_Line.useWorldSpace = true;
            m_Line.loop = false;
            m_Line.positionCount = segments;
            m_BaseWidthMultiplier = m_Line.widthMultiplier;

            m_Points = new Vector3[segments];
            m_Gradient = new Gradient();
        }

        private void UpdateRope()
        {
            m_A = m_OwnerTank.transform.position;
            m_B = m_TargetAllyTank.transform.position;
            m_A.z = 0f;
            m_B.z = 0f;

            Vector3 dir = m_B - m_A;
            float len = dir.magnitude;
            m_Perp = len > 0.0001f ? new Vector3(-dir.y, dir.x, 0f) / len : Vector3.up;

            transform.position = (m_A + m_B) * 0.5f;

            for (int i = 0; i < segments; i++)
            {
                m_Points[i] = RopePoint(i / (segments - 1f));
            }
            m_Line.SetPositions(m_Points);

            m_Line.widthMultiplier = m_BaseWidthMultiplier * (1f + Mathf.Sin(Time.time * 3f) * widthBreathing);
        }

        /// <summary>Điểm trên sợi dây tại t (0 = Water B, 1 = đồng minh). 2 đầu dây đứng yên, giữa dây uốn lượn.</summary>
        private Vector3 RopePoint(float t)
        {
            float envelope = Mathf.Sin(t * Mathf.PI);
            float wave = Mathf.Sin(t * waveFrequency * Mathf.PI * 2f - Time.time * waveSpeed);
            return m_A + (m_B - m_A) * t + m_Perp * (wave * waveAmplitude * envelope);
        }

        /// <summary>Lấp lánh: màu/độ trong suốt từng đoạn dây dao động giữa màu nước và màu điểm sáng.</summary>
        private void ApplyGradient(float fade)
        {
            for (int i = 0; i < KeyCount; i++)
            {
                float t = i / (KeyCount - 1f);
                float s = 0.5f + 0.5f * Mathf.Sin(Time.time * sparkleSpeed + i * 2.1f);

                Color c = Color.Lerp(ropeColor, sparkleColor, s);
                float a = Mathf.Lerp(ropeColor.a, sparkleColor.a, s) * fade;

                m_ColorKeys[i] = new GradientColorKey(c, t);
                m_AlphaKeys[i] = new GradientAlphaKey(a, t);
            }

            m_Gradient.SetKeys(m_ColorKeys, m_AlphaKeys);
            m_Line.colorGradient = m_Gradient;
        }

        // ------------------------------------------------------------------
        // HÌNH ẢNH: xung sáng & hiệu ứng tỏa hồi phục
        // ------------------------------------------------------------------
        private void SpawnPulse(TankBrain target, bool towardAlly)
        {
            if (target == null || m_Pulses.Count >= maxPulses) return;

            Vector3 startPos = RopePoint(towardAlly ? 0f : 1f);

            GameObject go = pulsePrefab != null
                ? Instantiate(pulsePrefab, startPos, Quaternion.identity)
                : new GameObject("WaterB_SharedHealPulse");
            go.transform.position = startPos;

            TrailRenderer trail = go.GetComponent<TrailRenderer>();
            if (trail == null)
            {
                trail = go.AddComponent<TrailRenderer>();
                trail.time = pulseTrailTime;
                trail.startWidth = pulseWidth;
                trail.endWidth = 0f;
                trail.minVertexDistance = 0.05f;
                trail.numCapVertices = 4;
                trail.material = new Material(Shader.Find("Sprites/Default"));
                trail.startColor = pulseColor;
                trail.endColor = new Color(pulseColor.r, pulseColor.g, pulseColor.b, 0f);
            }
            trail.Clear();
            trail.emitting = true;

            m_Pulses.Add(new Pulse
            {
                Go = go,
                Trail = trail,
                Target = target,
                TowardAlly = towardAlly,
                Progress = 0f
            });
        }

        private void UpdatePulses()
        {
            for (int i = m_Pulses.Count - 1; i >= 0; i--)
            {
                Pulse p = m_Pulses[i];

                if (p.Go == null)
                {
                    m_Pulses.RemoveAt(i);
                    continue;
                }

                p.Progress += Time.deltaTime / pulseTravelTime;
                float t = Mathf.Clamp01(p.Progress);
                p.Go.transform.position = RopePoint(p.TowardAlly ? t : 1f - t);

                if (p.Progress >= 1f)
                {
                    SpawnBurst(p.Target); // Tỏa hồi phục ở bên nhận máu chia sẻ

                    if (p.Trail != null)
                    {
                        p.Trail.emitting = false;
                        Destroy(p.Go, p.Trail.time); // chờ vệt sáng tan hết rồi mới xóa
                    }
                    else
                    {
                        Destroy(p.Go);
                    }
                    m_Pulses.RemoveAt(i);
                }
            }
        }

        private void SpawnBurst(TankBrain target)
        {
            if (healBurstPrefab == null || target == null) return;

            GameObject burst = Instantiate(healBurstPrefab, target.transform.position, Quaternion.identity, target.transform);
            Destroy(burst, burstLifetime);
        }
    }
}
