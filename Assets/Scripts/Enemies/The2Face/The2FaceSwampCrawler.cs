using IronWasteland.Enemies.Normal;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.The2Face
{
    /// <summary>
    /// Quái vật đầm lầy được The2Face triệu hồi trong giai đoạn Bùng nổ.
    /// Khác bản gốc: chỉ tìm và phá Điểm Sáng thay vì tấn công Tank.
    /// </summary>
    public class The2FaceSwampCrawler : NormalEnemy_QuaiVatDamLay
    {
        [Tooltip("Chu kỳ gây sát thương lên Điểm Sáng (giây).")]
        [SerializeField, Min(0.2f)] private float totemAttackInterval = 0.8f;

        [Tooltip("Khoảng cách đủ gần để phá Điểm Sáng (mét).")]
        [SerializeField, Min(0.5f)] private float totemAttackRange = 2.5f;

        private float m_NextTotemHitTime;

        protected override Transform ScanAndValidateTarget()
        {
            The2FaceLightPoint best = null;
            float bestSqr = float.MaxValue;

            var points = The2FaceLightPoint.ActivePoints;
            for (int i = 0; i < points.Count; i++)
            {
                The2FaceLightPoint point = points[i];
                if (point == null || !point.IsAlive) continue;

                float sqr = ((Vector2)point.transform.position - anchorPointD).sqrMagnitude;
                if (sqr > radiusC * radiusC) continue;

                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = point;
                }
            }

            return best != null ? best.transform : null;
        }

        protected override void OnBrainTick(float deltaTime)
        {
            base.OnBrainTick(deltaTime);

            if (currentTarget == null || Time.time < m_NextTotemHitTime) return;

            The2FaceLightPoint point = currentTarget.GetComponent<The2FaceLightPoint>();
            if (point == null || !point.IsAlive) return;

            if (Vector2.Distance(transform.position, point.transform.position) > totemAttackRange) return;

            point.ApplyDamage(Stats.Attack);
            m_NextTotemHitTime = Time.time + totemAttackInterval;
        }
    }
}
