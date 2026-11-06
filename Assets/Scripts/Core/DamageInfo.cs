using UnityEngine;

namespace IronWasteland.Core
{
    /// <summary>
    /// Cau truc du lieu sat thuong truyen qua toan bo he thong combat va su kien.
    /// </summary>
    public struct DamageInfo
    {
        /// <summary>Doi tuong gay sat thuong (TankBrain, EnemyBrain hoac null).</summary>
        public Object Attacker;

        /// <summary>Doi tuong nhan sat thuong (TankBrain, EnemyBrain).</summary>
        public Object Target;

        /// <summary>Sat thuong goc truoc khi tinh giam tru boi phong thu.</summary>
        public float RawDamage;

        /// <summary>Sat thuong thuc te gay ra sau khi ap dung phong thu va xuyen giap.</summary>
        public float FinalDamage;

        /// <summary>
        /// Lượng sát thương đã bị khiên hấp thụ trong đòn đánh này (0 nếu không có khiên nào chắn đòn).
        /// Sát thương thực tế trừ vào máu = <see cref="HealthDamage"/>.
        /// Giữ nguyên ý nghĩa cũ của <see cref="FinalDamage"/> (sát thương sau giáp, trước khiên).
        /// </summary>
        public float AbsorbedByShield;

        /// <summary>Ty le xuyen giap cua ke tan cong (0..1, toi da 100%).</summary>
        public float ArmorPenetration;

        /// <summary>Vi tri gay sat thuong trong world space (de hien text damage, hieu ung).</summary>
        public Vector2 HitPosition;

        /// <summary>
        /// True: Sát thương trừ trực tiếp vào máu, bỏ qua khâu hấp thụ khiên.
        /// Dùng cho sát thương theo thời gian (Thiêu Đốt) và cho phần sát thương đã được gánh hộ
        /// (Guardian Current, Guardian Wall, Earth Guardian).
        /// <para>
        /// Cờ này CHỈ nói "bỏ qua khiên". Việc "đòn đã bị chuyển hướng hay chưa" và "đây có phải
        /// sát thương định kỳ hay không" được ghi riêng ở <see cref="IsRedirected"/> và
        /// <see cref="IsDamageOverTime"/> — xem chú thích của hai cờ đó.
        /// </para>
        /// </summary>
        public bool IsDirectToHealth;

        /// <summary>
        /// True: đòn này là phần sát thương đã được chuyển hướng (gánh hộ) từ một đối tượng khác
        /// sang người nhận hiện tại. Dùng để chặn chuyển hướng lặp vô hạn: đòn đã mang cờ này sẽ
        /// không bị chuyển hướng thêm lần nữa (tối đa 1 tầng).
        /// </summary>
        public bool IsRedirected;

        /// <summary>
        /// True: đây là sát thương theo thời gian (DoT) chứ không phải một đòn đánh thật.
        /// Cho phép cơ chế chặn đòn phân biệt "đỡ một đòn" với "một tick cháy/độc",
        /// tránh việc một tick sát thương nhỏ ăn mất lượt chặn dành cho đòn chí mạng.
        /// </summary>
        public bool IsDamageOverTime;

        /// <summary>Sát thương thực tế trừ vào máu sau khi trừ đi phần khiên đã hấp thụ.</summary>
        public float HealthDamage => Mathf.Max(0f, FinalDamage - Mathf.Max(0f, AbsorbedByShield));

        public DamageInfo(
            Object attacker,
            Object target,
            float rawDamage,
            float armorPenetration = 0f,
            Vector2 hitPosition = default,
            bool isDirectToHealth = false,
            bool isRedirected = false,
            bool isDamageOverTime = false)
        {
            Attacker = attacker;
            Target = target;
            RawDamage = rawDamage;
            FinalDamage = rawDamage;
            AbsorbedByShield = 0f;
            ArmorPenetration = Mathf.Clamp01(armorPenetration);
            HitPosition = hitPosition;
            IsDirectToHealth = isDirectToHealth;
            IsRedirected = isRedirected;
            IsDamageOverTime = isDamageOverTime;
        }
    }
}
