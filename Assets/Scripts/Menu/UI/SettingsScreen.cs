using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace IronWasteland.Menu
{
    /// <summary>
    /// Cài đặt: Âm lượng tổng (vòng tròn) + 3 thanh trượt (Hiệu ứng / Nhạc nền / Giọng thông báo)
    /// và bảng gán phím. Mọi thay đổi áp dụng ngay và tự lưu khi rời màn hình.
    /// Bấm vào ô phím -> "..." -> nhấn phím mới (ESC để hủy). Trùng phím thì hai hành động đổi chỗ.
    /// </summary>
    public sealed class SettingsScreen : MenuScreen
    {
        private RadialVolumeKnob m_Knob;

        private readonly Slider[] m_Sliders = new Slider[3];
        private readonly TextMeshProUGUI[] m_SliderValues = new TextMeshProUGUI[3];
        private static readonly AudioChannel[] SliderChannels = { AudioChannel.Sfx, AudioChannel.Music, AudioChannel.Voice };

        private readonly Dictionary<GameAction, Button> m_KeyButtons = new Dictionary<GameAction, Button>();
        private readonly Dictionary<GameAction, TextMeshProUGUI> m_KeyLabels = new Dictionary<GameAction, TextMeshProUGUI>();
        private GameAction? m_Listening;

        // ============================================================
        // DỰNG
        // ============================================================

        protected override void Build()
        {
            BuildHeader("SYSTEM / CONFIGURATION", "CÀI ĐẶT HỆ THỐNG", true, false);
            BuildAudioPanel();
            BuildKeyPanel();

            UIKit.TextBtn(Content, "MẶC ĐỊNH", 1255, 915, 200, 70, Looks.Secondary, 18, OnReset);
            UIKit.TextBtn(Content, "ÁP DỤNG & QUAY LẠI", 1475, 915, 350, 70, Looks.Primary, 20, OnApplyAndBack);

            BuildFooter("TÙY CHỈNH ÂM THANH & ĐIỀU KHIỂN", "THAY ĐỔI ĐƯỢC LƯU TỰ ĐỘNG");
        }

        private void BuildAudioPanel()
        {
            Image panel = UIKit.PanelAt(Content, "AudioPanel", 95, 195, 1130, 700, UITheme.PanelDark, UITheme.Border);
            Transform p = panel.transform;

            UIKit.PanelAt(p, "Accent", 20, 24, 3, 40, UITheme.Orange);
            UIKit.Label(p, "ÂM THANH", 34, 22, 300, 26, 20, UITheme.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            UIKit.Label(p, "AUDIO CHANNELS", 34, 50, 300, 14, 11, UITheme.TextFaint);

            // --- Vòng tròn âm lượng tổng ---
            RectTransform ring = UIKit.NewRect("MasterRing", p);
            UIKit.Place(ring, 50, 130, 300, 300);

            var track = ring.gameObject.AddComponent<Image>();
            track.sprite = UIKit.RingSprite;
            track.color = UITheme.PanelLight;
            track.raycastTarget = true; // bắt chuột cho núm xoay

            RectTransform fillRt = UIKit.NewRect("Fill", ring);
            UIKit.Stretch(fillRt);
            var fill = fillRt.gameObject.AddComponent<Image>();
            fill.sprite = UIKit.RingSprite;
            fill.color = UITheme.Orange;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            fill.raycastTarget = false;

            TextMeshProUGUI number = UIKit.Label(ring, "78", 0, 100, 300, 90, 80, UITheme.Text,
                TextAlignmentOptions.Center, FontStyles.Normal, false, true, true);
            UIKit.Place(number.rectTransform, 60, 100, 180, 90);
            UIKit.Label(ring, "%", 232, 128, 40, 30, 22, UITheme.Orange, TextAlignmentOptions.Left, FontStyles.Bold);

            m_Knob = ring.gameObject.AddComponent<RadialVolumeKnob>();
            m_Knob.Bind(fill, number);
            m_Knob.ValueChanged += v => GameSettings.SetVolume(AudioChannel.Master, v);

            UIKit.Label(p, "ÂM LƯỢNG TỔNG", 50, 450, 300, 22, 16, UITheme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Label(p, "MASTER VOLUME", 50, 474, 300, 16, 11, UITheme.TextFaint, TextAlignmentOptions.Center);
            UIKit.Label(p, "KÉO HOẶC CUỘN CHUỘT TRÊN VÒNG TRÒN", 30, 506, 340, 16, 10, UITheme.TextFaint, TextAlignmentOptions.Center);

            UIKit.PanelAt(p, "Divider", 400, 100, 1, 440, UITheme.Border);

            // --- 3 thanh trượt ---
            string[] names = { "HIỆU ỨNG ÂM THANH", "NHẠC NỀN", "GIỌNG THÔNG BÁO" };
            string[] subs = { "SFX", "MUSIC", "ANNOUNCER" };
            Color[] colors = { UITheme.Orange, UITheme.Teal, UITheme.Purple };

            for (int i = 0; i < 3; i++)
            {
                int index = i;
                float y = 110 + i * 160f;

                UIKit.Label(p, names[i], 440, y, 400, 24, 18, UITheme.Text, TextAlignmentOptions.Left, FontStyles.Bold);
                UIKit.Label(p, subs[i], 440, y + 28, 400, 14, 11, UITheme.TextFaint);
                m_SliderValues[i] = UIKit.Label(p, "0", 990, y - 4, 100, 42, 36, UITheme.Text,
                    TextAlignmentOptions.Right, FontStyles.Bold);
                m_Sliders[i] = UIKit.SliderAt(p, "Slider_" + subs[i], 440, y + 78, 650, 28, colors[i],
                    v => OnSliderChanged(index, v));
            }

            UIKit.Label(p, "THAY ĐỔI ĐƯỢC LƯU TỰ ĐỘNG", 30, 650, 600, 16, 12, UITheme.TextFaint);
        }

        private void BuildKeyPanel()
        {
            Image panel = UIKit.PanelAt(Content, "KeyPanel", 1255, 195, 570, 700, UITheme.PanelDark, UITheme.Border);
            Transform p = panel.transform;

            UIKit.PanelAt(p, "Accent", 20, 24, 3, 40, UITheme.Teal);
            UIKit.Label(p, "BẢNG ĐIỀU KHIỂN NHANH", 34, 22, 460, 26, 20, UITheme.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            UIKit.Label(p, "KEY BINDINGS", 34, 50, 300, 14, 11, UITheme.TextFaint);

            const float y0 = 96f;
            const float pitch = 78f;

            BuildKeyRow(p, 0, y0, pitch, "DI CHUYỂN",
                GameAction.MoveUp, GameAction.MoveLeft, GameAction.MoveDown, GameAction.MoveRight);

            // Ngắm / xoay nòng: luôn là chuột, không gán lại.
            BuildRowLabel(p, 1, y0, pitch, "NGẮM / XOAY NÒNG");
            Image mouse = UIKit.PanelAt(p, "MouseCap", 442, y0 + pitch + 16, 100, 46, UITheme.PanelDark, UITheme.Border);
            UIKit.Label(mouse.transform, "CHUỘT", 0, 0, 100, 46, 16, UITheme.TextDim, TextAlignmentOptions.Center, FontStyles.Bold);

            BuildKeyRow(p, 2, y0, pitch, "BẮN THƯỜNG", GameAction.BasicAttack);
            BuildKeyRow(p, 3, y0, pitch, "KỸ NĂNG CHUYÊN BIỆT", GameAction.SpecialSkill);
            BuildKeyRow(p, 4, y0, pitch, "TÙY CHỌN 1", GameAction.Option1);
            BuildKeyRow(p, 5, y0, pitch, "TÙY CHỌN 2", GameAction.Option2);
            BuildKeyRow(p, 6, y0, pitch, "TẠM DỪNG", GameAction.Pause);

            UIKit.Label(p, "BẤM VÀO Ô PHÍM ĐỂ GÁN LẠI · ESC ĐỂ HỦY", 34, 664, 520, 16, 11, UITheme.TextFaint);
        }

        private static void BuildRowLabel(Transform p, int row, float y0, float pitch, string label)
        {
            float y = y0 + row * pitch;
            UIKit.PanelAt(p, "RowLine", 28, y, 514, 1, UITheme.Border);
            UIKit.Label(p, label, 34, y + 26, 260, 26, 16, UITheme.TextDim, TextAlignmentOptions.Left, FontStyles.Bold, false, true);
        }

        private void BuildKeyRow(Transform p, int row, float y0, float pitch, string label, params GameAction[] actions)
        {
            BuildRowLabel(p, row, y0, pitch, label);
            float y = y0 + row * pitch + 16;

            const float capW = 58f;
            const float gap = 8f;
            bool multi = actions.Length > 1;
            float width = multi ? capW : 100f;
            float total = actions.Length * width + (actions.Length - 1) * gap;
            float x = 542f - total;

            for (int i = 0; i < actions.Length; i++)
            {
                GameAction action = actions[i];
                Button cap = UIKit.Btn(p, "Key_" + action, x + i * (width + gap), y, width, 46, Looks.Secondary,
                    () => BeginListen(action));
                TextMeshProUGUI text = UIKit.Label(cap.transform, "", 0, 0, width, 46, 17, UITheme.Text,
                    TextAlignmentOptions.Center, FontStyles.Bold, false, true);
                UIKit.Stretch(text.rectTransform, 4f, 0f, 4f, 0f);

                m_KeyButtons[action] = cap;
                m_KeyLabels[action] = text;
            }
        }

        // ============================================================
        // HIỂN THỊ
        // ============================================================

        public override void OnShown()
        {
            base.OnShown();
            m_Listening = null;
            RefreshAll();
        }

        public override void OnHidden()
        {
            base.OnHidden();
            m_Listening = null;
            GameSettings.Save(); // tự lưu khi rời màn hình
        }

        public override bool OnBack()
        {
            // ESC khi đang chờ phím: chỉ hủy việc gán, chưa thoát màn hình.
            if (m_Listening.HasValue)
            {
                m_Listening = null;
                RefreshKeys();
                return true;
            }
            return false;
        }

        private void RefreshAll()
        {
            m_Knob.SetValueWithoutNotify(GameSettings.GetVolume(AudioChannel.Master));

            for (int i = 0; i < 3; i++)
            {
                float v = GameSettings.GetVolume(SliderChannels[i]);
                m_Sliders[i].SetValueWithoutNotify(v);
                m_SliderValues[i].text = Mathf.RoundToInt(v * 100f).ToString();
            }

            RefreshKeys();
        }

        private void RefreshKeys()
        {
            foreach (KeyValuePair<GameAction, Button> pair in m_KeyButtons)
            {
                bool listening = m_Listening.HasValue && m_Listening.Value == pair.Key;
                m_KeyLabels[pair.Key].text = listening ? "..." : GameSettings.KeyLabel(GameSettings.GetKey(pair.Key));
                m_KeyLabels[pair.Key].color = listening ? UITheme.Orange : UITheme.Text;
                UIKit.SetLook(pair.Value, listening ? Looks.CardActive : Looks.Secondary);
            }
        }

        // ============================================================
        // HÀNH ĐỘNG
        // ============================================================

        private void OnSliderChanged(int index, float value)
        {
            GameSettings.SetVolume(SliderChannels[index], value);
            m_SliderValues[index].text = Mathf.RoundToInt(value * 100f).ToString();
        }

        private void BeginListen(GameAction action)
        {
            m_Listening = action;
            RefreshKeys();
        }

        private void Update()
        {
            if (!m_Listening.HasValue) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            foreach (KeyControl control in keyboard.allKeys)
            {
                if (!control.wasPressedThisFrame) continue;

                Key key = control.keyCode;
                if (key == Key.None || key == Key.Escape || key == Key.IMESelected) continue;

                GameSettings.SetKey(m_Listening.Value, key);
                m_Listening = null;
                RefreshKeys();
                return;
            }
        }

        private void OnReset()
        {
            GameSettings.ResetAll();
            GameSettings.Save();
            m_Listening = null;
            RefreshAll();
            Ctx.Screens.Toast("ĐÃ KHÔI PHỤC CÀI ĐẶT MẶC ĐỊNH.", true);
        }

        private void OnApplyAndBack()
        {
            GameSettings.Save();
            Ctx.Screens.Back();
        }
    }
}
