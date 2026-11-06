using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Hieu ung lam cham (slow) tam thoi gan len TankBrain.
    /// Dung bonus stats co san cua TankBrain nen khong dung vao logic di chuyen:
    /// - Ap dung: giam MoveSpeed theo phan tram cua BaseStats.
    /// - Het thoi gian (hoac tank bi huy): tra lai chi so goc.
    ///
    /// Nhieu nguon lam cham cung luc: component duoc dung lai, lay muc giam manh nhat
    /// va thoi gian dai nhat (khong cong don).
    /// </summary>
    [DisallowMultipleComponent]
    public class TankSlowDebuff : MonoBehaviour
    {
        private TankBrain m_Tank;
        private string m_Key;
        private float m_SecondsLeft;
        private float m_AppliedPercent = -1f;
        private bool m_Applied;

        /// <summary>Gan (hoac lam moi) hieu ung lam cham cho tank.</summary>
        public static void Apply(TankBrain tank, float slowPercent, float duration)
        {
            if (tank == null || !tank.IsAlive) return;
            if (slowPercent <= 0f || duration <= 0f) return;

            TankSlowDebuff debuff = tank.GetComponent<TankSlowDebuff>();
            if (debuff == null) debuff = tank.gameObject.AddComponent<TankSlowDebuff>();

            debuff.Begin(Mathf.Clamp01(slowPercent), duration);
        }

        private void Awake()
        {
            m_Tank = GetComponent<TankBrain>();
            m_Key = "slow_" + GetEntityId().ToString();
        }

        private void Begin(float slowPercent, float duration)
        {
            if (m_Tank == null) m_Tank = GetComponent<TankBrain>();
            if (m_Tank == null) return;

            // Muc giam doi -> go ban cu roi ap ban moi (AddBonusStats cong don).
            if (m_Applied && !Mathf.Approximately(m_AppliedPercent, slowPercent)) Release();

            if (!m_Applied) ApplyPercent(slowPercent);

            m_SecondsLeft = Mathf.Max(m_SecondsLeft, duration);
        }

        private void ApplyPercent(float slowPercent)
        {
            float baseSpeed = m_Tank.BaseStats != null ? m_Tank.BaseStats.MoveSpeed : m_Tank.Stats.MoveSpeed;
            if (baseSpeed <= 0f) return;

            TankStats delta = TankStats.Zeroed();
            delta.MoveSpeed = -baseSpeed * slowPercent;

            m_Tank.AddBonusStats(m_Key, delta);
            m_AppliedPercent = slowPercent;
            m_Applied = true;
        }

        private void Update()
        {
            if (!m_Applied) return;

            m_SecondsLeft -= Time.deltaTime;
            if (m_SecondsLeft <= 0f) Release();
        }

        private void Release()
        {
            if (!m_Applied) return;

            m_Applied = false;
            m_AppliedPercent = -1f;
            m_SecondsLeft = 0f;

            if (m_Tank != null) m_Tank.RemoveBonusStats(m_Key);
        }

        private void OnDestroy()
        {
            Release();
        }
    }
}
