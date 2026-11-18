using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IronWasteland.Menu
{
    public enum AudioChannel
    {
        Master = 0,
        Sfx = 1,
        Music = 2,
        Voice = 3,
    }

    /// <summary>Các hành động có thể gán phím. (Ngắm / xoay nòng luôn là chuột, không gán lại.)</summary>
    public enum GameAction
    {
        MoveUp = 0,
        MoveLeft = 1,
        MoveDown = 2,
        MoveRight = 3,
        BasicAttack = 4,   // Kỹ năng 0 (thường)
        SpecialSkill = 5,  // Kỹ năng 1
        Option1 = 6,       // Kỹ năng 2
        Option2 = 7,       // Kỹ năng 3
        Pause = 8,
    }

    /// <summary>
    /// Cài đặt người chơi: âm lượng 4 kênh + bảng phím. Lưu bằng PlayerPrefs (JSON).
    ///
    /// - Âm lượng Master áp thẳng vào AudioListener.volume.
    /// - Kênh Sfx / Music / Voice: hệ thống âm thanh của game đọc qua <see cref="GetVolume"/>.
    /// - Phím: lớp NGOÀI (GameController) đọc qua <see cref="IsHeld"/> / <see cref="WasReleasedThisFrame"/>.
    ///   TankBrain vẫn không đọc input (Rule 1).
    /// </summary>
    public static class GameSettings
    {
        [Serializable]
        public class Data
        {
            public float master = 0.78f;
            public float sfx = 0.65f;
            public float music = 0.45f;
            public float voice = 0.80f;
            public int[] keys;
        }

        private const string PrefKey = "IronWasteland.Settings.v1";

        private static readonly Key[] DefaultKeys =
        {
            Key.W, Key.A, Key.S, Key.D,
            Key.J, Key.K, Key.L, Key.I,
            Key.Escape,
        };

        private static Data s_Data;

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Data = null;
            Changed = null;
        }

        private static Data D
        {
            get
            {
                if (s_Data == null) Load();
                return s_Data;
            }
        }

        // ============================================================
        // LOAD / SAVE
        // ============================================================

        public static void Load()
        {
            Data loaded = null;
            try
            {
                string json = PlayerPrefs.GetString(PrefKey, "");
                if (!string.IsNullOrEmpty(json)) loaded = JsonUtility.FromJson<Data>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{nameof(GameSettings)}] Không đọc được cài đặt, dùng mặc định. {e.Message}");
            }

            s_Data = loaded ?? new Data();
            NormalizeKeys();
            ApplyAudio();
        }

        public static void Save()
        {
            if (s_Data == null) return;
            PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(s_Data));
            PlayerPrefs.Save();
        }

        private static void NormalizeKeys()
        {
            int count = Enum.GetValues(typeof(GameAction)).Length;
            if (s_Data.keys == null || s_Data.keys.Length != count)
            {
                s_Data.keys = new int[count];
                for (int i = 0; i < count; i++) s_Data.keys[i] = (int)DefaultKeys[i];
            }
        }

        // ============================================================
        // ÂM THANH
        // ============================================================

        public static float GetVolume(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Master: return D.master;
                case AudioChannel.Sfx: return D.sfx;
                case AudioChannel.Music: return D.music;
                default: return D.voice;
            }
        }

        public static void SetVolume(AudioChannel channel, float value01)
        {
            value01 = Mathf.Clamp01(value01);
            switch (channel)
            {
                case AudioChannel.Master: D.master = value01; break;
                case AudioChannel.Sfx: D.sfx = value01; break;
                case AudioChannel.Music: D.music = value01; break;
                default: D.voice = value01; break;
            }

            ApplyAudio();
            Changed?.Invoke();
        }

        public static void ApplyAudio()
        {
            AudioListener.volume = D.master;
        }

        // ============================================================
        // PHÍM
        // ============================================================

        public static Key GetKey(GameAction action) => (Key)D.keys[(int)action];

        public static Key GetDefaultKey(GameAction action) => DefaultKeys[(int)action];

        /// <summary>Gán phím mới. Nếu phím đã dùng cho hành động khác thì hai hành động đổi chỗ cho nhau.</summary>
        public static void SetKey(GameAction action, Key key)
        {
            if (key == Key.None) return;

            int self = (int)action;
            int oldKey = D.keys[self];

            for (int i = 0; i < D.keys.Length; i++)
            {
                if (i != self && D.keys[i] == (int)key)
                {
                    D.keys[i] = oldKey;
                    break;
                }
            }

            D.keys[self] = (int)key;
            Changed?.Invoke();
        }

        public static void ResetAll()
        {
            s_Data = new Data();
            NormalizeKeys();
            ApplyAudio();
            Changed?.Invoke();
        }

        public static bool IsHeld(GameAction action, Keyboard keyboard)
        {
            Key key = GetKey(action);
            return keyboard != null && key != Key.None && keyboard[key].isPressed;
        }

        public static bool WasPressedThisFrame(GameAction action, Keyboard keyboard)
        {
            Key key = GetKey(action);
            return keyboard != null && key != Key.None && keyboard[key].wasPressedThisFrame;
        }

        public static bool WasReleasedThisFrame(GameAction action, Keyboard keyboard)
        {
            Key key = GetKey(action);
            return keyboard != null && key != Key.None && keyboard[key].wasReleasedThisFrame;
        }

        /// <summary>Nhãn ngắn hiển thị trên ô phím.</summary>
        public static string KeyLabel(Key key)
        {
            switch (key)
            {
                case Key.Escape: return "ESC";
                case Key.Space: return "SPACE";
                case Key.LeftShift: return "L-SHIFT";
                case Key.RightShift: return "R-SHIFT";
                case Key.LeftCtrl: return "L-CTRL";
                case Key.RightCtrl: return "R-CTRL";
                case Key.LeftAlt: return "L-ALT";
                case Key.RightAlt: return "R-ALT";
                case Key.Tab: return "TAB";
                case Key.Enter: return "ENTER";
                case Key.Backspace: return "BKSP";
                case Key.UpArrow: return "UP";
                case Key.DownArrow: return "DOWN";
                case Key.LeftArrow: return "LEFT";
                case Key.RightArrow: return "RIGHT";
                case Key.None: return "-";
            }

            string s = key.ToString();
            if (s.StartsWith("Digit")) s = s.Substring(5);
            return s.ToUpperInvariant();
        }
    }
}
