using System;
using TMPro;
using UnityEngine.UI;

namespace IronWasteland.Menu
{
    /// <summary>Hộp thoại xác nhận hai nút: HỦY BỎ / XÁC NHẬN.</summary>
    public sealed class ConfirmPopup : MenuPopup
    {
        private TextMeshProUGUI m_Title;
        private TextMeshProUGUI m_Message;
        private Button m_ConfirmButton;
        private Action m_OnConfirm;

        protected override void Build()
        {
            Image panel = UIKit.PanelAt(Content, "Panel", 680, 400, 560, 280,
                UITheme.PanelDark, UITheme.BorderBright, 1.5f, true);
            UIKit.Brackets(panel.transform, UITheme.Alpha(UITheme.Orange, 0.8f), 22f, 2f, 10f);

            m_Title = UIKit.Label(panel.transform, "", 30, 24, 500, 36, 26, UITheme.Text,
                TextAlignmentOptions.Left, FontStyles.Bold, false, true);
            m_Message = UIKit.Label(panel.transform, "", 30, 76, 500, 100, 18, UITheme.TextDim,
                TextAlignmentOptions.TopLeft, FontStyles.Normal, true);

            UIKit.TextBtn(panel.transform, "HỦY BỎ", 30, 200, 240, 52, Looks.Secondary, 18, Close);
            m_ConfirmButton = UIKit.TextBtn(panel.transform, "XÁC NHẬN", 290, 200, 240, 52,
                Looks.Primary, 18, OnConfirmClicked);
        }

        public void Open(string title, string message, string confirmLabel, bool danger, Action onConfirm)
        {
            m_Title.text = title;
            m_Message.text = message;
            m_OnConfirm = onConfirm;

            m_ConfirmButton.GetComponentInChildren<TextMeshProUGUI>().text = confirmLabel;
            UIKit.SetLook(m_ConfirmButton, danger ? Looks.Danger : Looks.Primary, true);

            Ctx.Screens.OpenPopup(this);
        }

        private void OnConfirmClicked()
        {
            Action callback = m_OnConfirm;
            m_OnConfirm = null;
            Close();
            callback?.Invoke();
        }
    }
}
