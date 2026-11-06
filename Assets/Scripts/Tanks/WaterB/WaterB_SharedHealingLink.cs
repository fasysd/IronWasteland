using System.Collections;
using UnityEngine;

namespace IronWasteland.Tanks.WaterB
{
    /// <summary>
    /// Lien ket hoi phuc giua Water B va 1 dong minh.
    /// Bat ky hieu qua hoi mau nao ma 1 ben nhan duoc cung se tu dong chia se 1 phan sang ben con lai.
    /// Phan chia se la LOGIC thuan (qua event TankBrain.OnHealed) - vien dan chi la hinh anh.
    /// </summary>
    [DisallowMultipleComponent]
    public class WaterB_SharedHealingLink : MonoBehaviour
    {
        [Header("Visual Only")]
        [SerializeField] private WaterB_HealingProjectile linkProjectilePrefab; // Chi de tao tia lien ket cho nguoi choi nhin
        [SerializeField, Min(0.05f)] private float rapidFireInterval = 0.3f;     // Interval giua cac dot ban chuoi dan

        [Header("Multi-Projectile Burst Settings")]
        [SerializeField] private int burstCount = 3;             // So luong vien dan ban lan luot trong 1 dot
        [SerializeField] private float burstDelay = 0.04f;     // Khoang cach thoi gian giua cac vien dan

        private TankBrain m_OwnerTank;      // Water B
        private TankBrain m_TargetAllyTank; // RedNova hoac dong minh khac
        private float m_SharedPercent;
        private bool m_IsActive;
        private Coroutine m_ContinuousLinkCoroutine;

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

            transform.position = m_TargetAllyTank.transform.position;
            transform.SetParent(m_TargetAllyTank.transform);

            // LOGIC: lang nghe hoi mau cua CA HAI tank
            m_OwnerTank.OnHealed += HandleTankHealed;
            m_TargetAllyTank.OnHealed += HandleTankHealed;

            // HINH ANH: ban dan lien tuc de tao tia lien ket
            m_ContinuousLinkCoroutine = StartCoroutine(ContinuousLinkBeamRoutine());

            Destroy(gameObject, duration > 0f ? duration : 10f);
        }

        private void OnDestroy()
        {
            m_IsActive = false;

            if (m_OwnerTank != null) m_OwnerTank.OnHealed -= HandleTankHealed;
            if (m_TargetAllyTank != null) m_TargetAllyTank.OnHealed -= HandleTankHealed;
        }

        private void Update()
        {
            if (!m_IsActive || m_OwnerTank == null || m_TargetAllyTank == null)
            {
                if (m_ContinuousLinkCoroutine != null) StopCoroutine(m_ContinuousLinkCoroutine);
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Duoc goi khi BAT KI tank nao trong lien ket nhan mau.
        /// Chia se hoi NGAY LAP TUC, khong phu thuoc vao dan.
        /// </summary>
        private void HandleTankHealed(TankBrain receiver, float amount, bool isByShare)
        {
            // Mau do chia se thi KHONG chia se lai -> khong bi vong lap vo tan
            if (!m_IsActive || isByShare || amount <= 0f) return;

            TankBrain other = (receiver == m_OwnerTank) ? m_TargetAllyTank : m_OwnerTank;
            if (other == null) return;

            float sharedAmount = amount * m_SharedPercent;
            if (sharedAmount <= 0f) return;

            other.Heal(sharedAmount, true);
            TextDamageManager.Instance?.Show(sharedAmount, other.transform.position);

            Debug.Log($"[SharedHealing] {receiver.name} nhan {amount} HP -> chia se {sharedAmount} HP cho {other.name}.");

            // Dan chi de nhin
            StartCoroutine(ShootVisualProjectile(receiver, other));
        }

        private IEnumerator ContinuousLinkBeamRoutine()
        {
            var wait = new WaitForSeconds(rapidFireInterval);

            while (m_IsActive && m_OwnerTank != null && m_TargetAllyTank != null)
            {
                StartCoroutine(ShootVisualProjectile(m_OwnerTank, m_TargetAllyTank));
                yield return wait;
            }
        }

        /// <summary>Ban 1 vien dan HINH ANH (power = 0): khong hoi mau, khong hien text, khong goi callback.</summary>
        private IEnumerator ShootVisualProjectile(TankBrain from, TankBrain to)
        {
            if (linkProjectilePrefab == null || from == null || to == null) yield break;

            var burstWait = new WaitForSeconds(burstDelay);

            for (int i = 0; i < burstCount; i++)
            {
                if (from == null || to == null || !m_IsActive) break;

                Vector3 startPos = from.transform.position;
                Vector3 direction = (to.transform.position - startPos).normalized;

                WaterB_HealingProjectile proj = Instantiate(linkProjectilePrefab, startPos, Quaternion.identity);
                proj.Initialize(m_OwnerTank, direction, 0f, false, null);

                if (i < burstCount - 1)
                {
                    yield return burstWait;
                }
            }
        }
    }
}