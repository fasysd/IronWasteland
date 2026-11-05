namespace IronWasteland.Core
{
    /// <summary>
    /// Cac loai hieu ung khong che (Crowd Control - CC).
    /// </summary>
    public enum CrowdControlType
    {
        None = 0,

        /// <summary>Troi chan / giam giu: khong the di chuyen, van co the ngam va dung ky nang.</summary>
        Root = 1,

        /// <summary>Khoa skill / cam lang: khong the dung ky nang, van co the di chuyen va ngam.</summary>
        Silence = 2,

        /// <summary>Lam choang: vo hieu hoa toan dien (khong di chuyen, khong ngam, khong dung ky nang).</summary>
        Stun = 3,

        /// <summary>Lam cham: giam % toc do di chuyen.</summary>
        Slow = 4,

        /// <summary>Day lui: bi day theo huong va luc.</summary>
        Knockback = 5,
    }

    /// <summary>
    /// Du lieu cua 1 trang thai khong che dang hoat dong tren Entity.
    /// </summary>
    [System.Serializable]
    public class ActiveCrowdControl
    {
        public CrowdControlType Type;
        public float Duration;
        public float RemainingTime;
        public float Strength; // Dùng cho % làm chậm (0.2 = 20%) hoặc lực đẩy lùi

        public bool IsExpired => RemainingTime <= 0f;

        public ActiveCrowdControl(CrowdControlType type, float duration, float strength = 0f)
        {
            Type = type;
            Duration = duration;
            RemainingTime = duration;
            Strength = strength;
        }

        public void Tick(float deltaTime)
        {
            RemainingTime -= deltaTime;
        }
    }
}
