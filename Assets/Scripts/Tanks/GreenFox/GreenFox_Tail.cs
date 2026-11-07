using System;
using System.Collections;
using IronWasteland.Enemies;
using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    /// <summary>
    /// 1 chiếc đuôi của Nine-Tailed Assault (2C). Xòe thành hình quạt phía SAU Green Fox, đung đưa nhẹ,
    /// cứ mỗi "giãn cách tấn công" thì quất ra kẻ địch được chọn rồi thu về.
    /// Prefab chỉ cần 1 sprite/hiệu ứng đuôi (hướng sang phải = trục +X; nếu lệch thì chỉnh Rotation Offset).
    /// Không cần gắn script này sẵn: NineTailedAssault tự thêm vào.
    /// </summary>
    [DisallowMultipleComponent]
    public class GreenFox_Tail : MonoBehaviour
    {
        [Header("Pose")]
        [SerializeField, Min(0f)] private float restRadius = 0.7f;      // Khoảng cách gốc đuôi so với tâm xe
        [SerializeField] private float rotationOffset = 0f;             // Chỉnh nếu sprite đuôi không hướng +X
        [SerializeField, Min(0f)] private float swayAngle = 6f;         // Biên độ đung đưa (độ)
        [SerializeField, Min(0f)] private float swaySpeed = 2.5f;

        [Header("Lash")]
        [SerializeField, Min(0.05f)] private float lashDuration = 0.35f;
        [SerializeField, Range(0.1f, 0.9f)] private float lashOutPortion = 0.4f; // Phần thời gian quất ra (đòn trúng ở cuối phần này)

        [Header("Grow / Shrink")]
        [SerializeField, Min(0.01f)] private float growTime = 0.25f;

        private TankBrain m_Owner;
        private int m_Index;
        private int m_Count;
        private float m_Spread;
        private bool m_Lashing;
        private Vector3 m_BaseScale;
        private Func<EnemyBrain> m_PickTarget;
        private Action<EnemyBrain> m_OnHit;

        /// <param name="spreadDegrees">Độ rộng của quạt đuôi (độ).</param>
        /// <param name="pickTarget">Chọn mục tiêu cho mỗi lần quất (null = không có kẻ địch).</param>
        /// <param name="onHit">Gọi khi đòn quất trúng (gây sát thương ở đây).</param>
        public void Initialize(TankBrain owner, int index, int count, float spreadDegrees,
                               float interval, float duration, float startDelay,
                               Func<EnemyBrain> pickTarget, Action<EnemyBrain> onHit)
        {
            m_Owner = owner;
            m_Index = index;
            m_Count = Mathf.Max(1, count);
            m_Spread = spreadDegrees;
            m_PickTarget = pickTarget;
            m_OnHit = onHit;

            m_BaseScale = transform.localScale;
            transform.localScale = Vector3.zero;

            ApplyRestPose();
            StartCoroutine(TailRoutine(Mathf.Max(0.05f, interval), duration, startDelay));
        }

        private void LateUpdate()
        {
            if (m_Owner == null)
            {
                Destroy(gameObject);
                return;
            }

            if (!m_Lashing) ApplyRestPose();
        }

        private IEnumerator TailRoutine(float interval, float duration, float startDelay)
        {
            // Mốc thời gian tính từ lúc kích hoạt (xòe đuôi diễn ra song song, không làm lố thời gian duy trì)
            float endTime = Time.time + duration;
            float nextStrike = Time.time + startDelay;

            yield return ScaleTo(1f);

            while (Time.time < endTime && m_Owner != null)
            {
                float wait = nextStrike - Time.time;
                if (wait > 0f) yield return new WaitForSeconds(wait);
                if (Time.time >= endTime) break;

                EnemyBrain target = m_PickTarget != null ? m_PickTarget() : null;
                if (target != null) yield return Lash(target);

                nextStrike += interval;
                if (nextStrike < Time.time) nextStrike = Time.time; // lỡ nhịp thì quất ngay nhịp kế
            }

            yield return ScaleTo(0f);
            Destroy(gameObject);
        }

        private IEnumerator Lash(EnemyBrain enemy)
        {
            m_Lashing = true;
            float elapsed = 0f;
            bool hit = false;

            while (elapsed < lashDuration)
            {
                if (enemy == null || m_Owner == null) break;

                elapsed += Time.deltaTime;
                float p = Mathf.Clamp01(elapsed / lashDuration);

                GetRestPose(out Vector3 rest, out float restAngle);

                Vector3 targetPos = enemy.transform.position;
                targetPos.z = rest.z;

                // Ra nhanh (ease-out) rồi thu về mượt (ease-in-out)
                float k = p < lashOutPortion
                    ? EaseOut(p / lashOutPortion)
                    : 1f - Mathf.SmoothStep(0f, 1f, (p - lashOutPortion) / (1f - lashOutPortion));

                Vector2 toTarget = targetPos - rest;
                float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;

                transform.position = Vector3.Lerp(rest, targetPos, k);
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(restAngle, targetAngle, Mathf.Clamp01(k * 2f)) + rotationOffset);

                if (!hit && p >= lashOutPortion)
                {
                    hit = true;
                    m_OnHit?.Invoke(enemy);
                }

                yield return null;
            }

            m_Lashing = false;
        }

        private IEnumerator ScaleTo(float factor)
        {
            Vector3 from = transform.localScale;
            Vector3 to = m_BaseScale * factor;
            float t = 0f;

            while (t < growTime)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / growTime));
                yield return null;
            }
            transform.localScale = to;
        }

        private static float EaseOut(float x) => 1f - (1f - x) * (1f - x);

        private void ApplyRestPose()
        {
            if (m_Owner == null) return;

            GetRestPose(out Vector3 pos, out float angle);
            transform.position = pos;
            transform.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);
        }

        /// <summary>Vị trí/góc nghỉ: quạt đuôi nằm phía sau xe (ngược hướng thân xe).</summary>
        private void GetRestPose(out Vector3 position, out float angle)
        {
            Vector2 facing = (Vector2)m_Owner.transform.right;
            if (m_Owner.View != null && m_Owner.View.BodyPivot != null)
            {
                facing = m_Owner.View.BodyPivot.right;
            }

            float backAngle = Mathf.Atan2(-facing.y, -facing.x) * Mathf.Rad2Deg;
            float t = m_Count > 1 ? m_Index / (float)(m_Count - 1) : 0.5f;
            float sway = Mathf.Sin(Time.time * swaySpeed + m_Index * 0.7f) * swayAngle;

            angle = backAngle + Mathf.Lerp(-m_Spread * 0.5f, m_Spread * 0.5f, t) + sway;

            float rad = angle * Mathf.Deg2Rad;
            Vector3 center = m_Owner.transform.position;
            position = new Vector3(center.x + Mathf.Cos(rad) * restRadius, center.y + Mathf.Sin(rad) * restRadius, 0f);
        }
    }
}
