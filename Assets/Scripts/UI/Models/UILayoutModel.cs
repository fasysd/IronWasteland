namespace IronWasteland.UiModels
{
    /// <summary>
    /// Quy đổi toạ độ giữa hệ thiết kế (gốc trên-trái, chiều cao 1080) và
    /// RectTransform neo góc dưới-trái (pivot 0,0).
    /// </summary>
    public static class UILayoutModel
    {
        /// <summary>Chiều cao hệ thiết kế của toàn bộ màn hình UI.</summary>
        public const float DesignHeight = 1080f;

        /// <summary>Đổi toạ độ y thiết kế (mép trên) sang anchoredPosition y (mép dưới).</summary>
        public static float AnchoredY(float designY, float height, float parentHeight = DesignHeight)
        {
            return parentHeight - (designY + height);
        }

        /// <summary>Đổi anchoredPosition y (mép dưới) về toạ độ y thiết kế (mép trên).</summary>
        public static float DesignY(float anchoredY, float height, float parentHeight = DesignHeight)
        {
            return parentHeight - anchoredY - height;
        }
    }
}
