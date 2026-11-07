using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland
{
    /// <summary>
    /// Vật thể test sát thương: gắn lên một GameObject có Collider2D rồi đặt vào Scene.
    /// </summary>
    /// <remarks>
    /// Khi chạm vào xe người chơi (<see cref="TankBrain"/>), vật thể này gây một lượng sát thương
    /// và in ra Console: sát thương thô, sát thương sau khi trừ giáp (giá trị dự kiến), lượng khiên
    /// hấp thụ, lượng máu thật sự bị trừ. Dùng để kiểm tra công thức giảm sát thương và hệ thống khiên.
    /// <para>
    /// Lưu ý: sự kiện va chạm 2D cần ít nhất một trong hai phía có <see cref="Rigidbody2D"/>.
    /// Xe người chơi đã có Rigidbody2D nên thường không cần thêm cho vật thể này; nếu không thấy log,
    /// hãy thêm Rigidbody2D với Body Type = Kinematic cho GameObject này.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class DamageTestTrigger : MonoBehaviour
    {
        [Header("Cấu Hình Sát Thương")]
        [Tooltip("Sát thương thô (trước khi trừ giáp và khiên) gây ra mỗi lần chạm.")]
        [SerializeField, Min(0f)] private float rawDamage = 100f;

        [Tooltip("Tỉ lệ xuyên giáp: 0 = không xuyên giáp, 1 = bỏ qua toàn bộ giáp của xe.")]
        [SerializeField, Range(0f, 1f)] private float armorPenetration = 0f;

        [Tooltip("Thời gian chờ tối thiểu giữa hai lần gây sát thương lên cùng một xe (giây).")]
        [SerializeField, Min(0f)] private float cooldown = 0.5f;

        [Header("Hiển Thị")]
        [Tooltip("In log chi tiết ra Console mỗi lần gây sát thương.")]
        [SerializeField] private bool logToConsole = true;

        [Tooltip("Hiện con số sát thương bay lên tại vị trí xe bằng TextDamageManager.")]
        [SerializeField] private bool showDamageText = true;

        /// <summary>Thời điểm sớm nhất được gây sát thương tiếp theo, tính riêng cho từng xe.</summary>
        private readonly Dictionary<TankBrain, float> m_NextHitTimes = new Dictionary<TankBrain, float>();

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryHit(other);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision == null)
            {
                return;
            }

            TryHit(collision.collider);
        }

        private void OnDestroy()
        {
            m_NextHitTimes.Clear();
        }

        /// <summary>
        /// Kiểm tra collider vừa chạm: nếu collider đó thuộc một xe người chơi còn sống thì gây sát thương.
        /// </summary>
        /// <param name="other">Collider vừa va chạm.</param>
        private void TryHit(Collider2D other)
        {
            if (other == null)
            {
                return;
            }

            // Collider không gắn ColliderOwnerRef (tường, địa hình, đạn...) thì bỏ qua.
            ColliderOwnerRef ownerRef = ColliderOwnerRef.FromCollider(other);
            if (ownerRef == null)
            {
                return;
            }

            if (!ownerRef.TryGetOwner<TankBrain>(out TankBrain tank) || tank == null || !tank.IsAlive)
            {
                return;
            }

            if (m_NextHitTimes.TryGetValue(tank, out float nextHitTime) && Time.time < nextHitTime)
            {
                return;
            }

            m_NextHitTimes[tank] = Time.time + cooldown;
            ApplyHit(tank);
        }

        /// <summary>
        /// Gây sát thương lên một xe, sau đó đo chênh lệch máu/khiên để biết sát thương thực tế đã vào.
        /// </summary>
        /// <param name="tank">Xe nhận sát thương.</param>
        private void ApplyHit(TankBrain tank)
        {
            float healthBefore = tank.CurrentHealth;
            float shieldBefore = tank.CurrentShield;
            float expectedFinalDamage = CombatUtility.ResolveDamage(rawDamage, tank.Stats.Defense, armorPenetration);

            DamageInfo info = new DamageInfo(gameObject, tank, rawDamage, armorPenetration, tank.transform.position);
            tank.TakeDamage(info);

            float healthLost = Mathf.Max(0f, healthBefore - tank.CurrentHealth);
            float shieldAbsorbed = Mathf.Max(0f, shieldBefore - tank.CurrentShield);
            float totalDealt = healthLost + shieldAbsorbed;

            if (logToConsole)
            {
                Debug.Log(
                    $"[DamageTest] {tank.name} | Thô: {rawDamage:0.##} | Xuyên giáp: {armorPenetration:P0}" +
                    $" | Giáp xe: {tank.Stats.Defense:0.##} → dự kiến sau giáp: {expectedFinalDamage:0.##}" +
                    $" | Khiên hấp thụ: {shieldAbsorbed:0.##} | Máu mất: {healthLost:0.##}" +
                    $" | HP: {healthBefore:0.##} → {tank.CurrentHealth:0.##}",
                    this);
            }

            if (showDamageText && totalDealt > 0f)
            {
                TextDamageManager.Instance?.Show(totalDealt, tank.transform.position);
            }
        }
    }
}
