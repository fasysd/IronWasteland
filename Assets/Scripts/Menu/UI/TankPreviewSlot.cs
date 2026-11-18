using UnityEngine;
using UnityEngine.UI;

namespace IronWasteland.Menu
{
    /// <summary>
    /// Một ô hiển thị xe trong UI: RawImage lấy hình từ <see cref="TankPreviewStage"/>,
    /// và tự rơi về ảnh tĩnh (portrait / chữ cái đầu) nếu xe không có TankView để dựng.
    /// </summary>
    public sealed class TankPreviewSlot
    {
        private readonly TankPreviewStage m_Stage;
        private readonly RawImage m_Raw;
        private readonly Image m_Fallback;
        private readonly bool m_Live;

        private TankPreviewSlot(TankPreviewStage stage, RawImage raw, Image fallback, bool live)
        {
            m_Stage = stage;
            m_Raw = raw;
            m_Fallback = fallback;
            m_Live = live;
        }

        /// <param name="live">True: xe có animation, chiếm camera (chỉ một ô live hiện cùng lúc).
        /// False: ảnh tĩnh nhỏ, dùng cho danh sách.</param>
        public static TankPreviewSlot Create(
            Transform parent, string name, float x, float y, float w, float h,
            TankPreviewStage stage, bool live)
        {
            RectTransform root = UIKit.NewRect(name, parent);
            UIKit.Place(root, x, y, w, h);

            Image fallback = UIKit.IconAt(root, "Fallback", 0, 0, w, h);

            // RawImage vuông (RenderTexture vuông) đặt giữa ô để không bị méo.
            float s = Mathf.Min(w, h);
            RectTransform rawRt = UIKit.NewRect("TankView", root);
            UIKit.Place(rawRt, (w - s) * 0.5f, (h - s) * 0.5f, s, s);

            var raw = rawRt.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            raw.color = Color.white;
            raw.gameObject.SetActive(false);

            return new TankPreviewSlot(stage, raw, fallback, live);
        }

        public void Show(TankEntry entry)
        {
            Texture texture = null;

            if (entry != null && m_Stage != null)
            {
                if (m_Live)
                {
                    if (m_Stage.Show(entry)) texture = m_Stage.LiveTexture;
                }
                else
                {
                    texture = m_Stage.GetSnapshot(entry);
                }
            }

            bool has = texture != null;
            m_Raw.gameObject.SetActive(has);
            m_Fallback.gameObject.SetActive(!has);

            if (has)
            {
                m_Raw.texture = texture;
            }
            else
            {
                Sprite sprite = entry == null ? null : (entry.portrait != null ? entry.portrait : entry.listIcon);
                UIKit.SetIcon(m_Fallback, sprite, entry != null ? entry.displayName : "?");
            }
        }

        /// <summary>Gọi khi màn hình chứa ô này bị ẩn (chỉ có tác dụng với ô live).</summary>
        public void Hide()
        {
            if (m_Live && m_Stage != null) m_Stage.Hide();
        }
    }
}
