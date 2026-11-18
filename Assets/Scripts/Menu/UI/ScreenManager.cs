using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace IronWasteland.Menu
{
    /// <summary>
    /// Quản lý màn hình + popup của Menu.
    /// - Màn hình: chỉ một màn hiện tại, có lịch sử để "Quay lại".
    /// - Popup: xếp chồng lên màn hình hiện tại.
    /// - ESC: đóng popup trên cùng; nếu không có thì để màn hình tự xử lý, mặc định là quay lại.
    /// </summary>
    public class ScreenManager : MonoBehaviour
    {
        private readonly Dictionary<Type, MenuView> m_Views = new Dictionary<Type, MenuView>();
        private readonly Stack<MenuScreen> m_History = new Stack<MenuScreen>();
        private readonly List<MenuPopup> m_Popups = new List<MenuPopup>();

        private MenuContext m_Ctx;
        private MenuScreen m_Current;

        private RectTransform m_ToastRoot;
        private TextMeshProUGUI m_ToastText;
        private Image m_ToastBg;
        private Coroutine m_ToastRoutine;

        public MenuScreen Current => m_Current;

        // ============================================================
        // KHỞI TẠO
        // ============================================================

        public void Init(MenuContext ctx)
        {
            m_Ctx = ctx;
            BuildToast();
        }

        /// <summary>Tạo một màn hình / popup và đăng ký. Gọi lúc khởi động.</summary>
        public T Register<T>() where T : MenuView
        {
            RectTransform rt = UIKit.NewRect(typeof(T).Name, m_Ctx.CanvasRoot);
            var view = rt.gameObject.AddComponent<T>();
            m_Views[typeof(T)] = view;
            view.Initialize(m_Ctx);

            m_ToastRoot.SetAsLastSibling();
            return view;
        }

        public T Get<T>() where T : MenuView
        {
            if (m_Views.TryGetValue(typeof(T), out MenuView v)) return (T)v;
            Debug.LogError($"[{nameof(ScreenManager)}] Chưa đăng ký {typeof(T).Name}.");
            return null;
        }

        // ============================================================
        // MÀN HÌNH
        // ============================================================

        public void Show<T>() where T : MenuScreen => ShowScreen(Get<T>(), true);

        public void ShowScreen(MenuScreen screen, bool pushCurrent)
        {
            if (screen == null) return;

            CloseAllPopups();

            // Đi tới một màn đã có trong lịch sử => quay về đó thay vì chồng thêm.
            if (pushCurrent && m_History.Contains(screen))
            {
                while (m_History.Peek() != screen) m_History.Pop();
                m_History.Pop();
                pushCurrent = false;
            }

            if (m_Current != null)
            {
                if (pushCurrent) m_History.Push(m_Current);
                m_Current.OnHidden();
                m_Current.gameObject.SetActive(false);
            }

            m_Current = screen;
            screen.gameObject.SetActive(true);
            screen.OnShown();
        }

        public void Back()
        {
            CloseAllPopups();
            if (m_History.Count == 0) return;
            ShowScreen(m_History.Pop(), false);
        }

        public void GoHome()
        {
            m_History.Clear();
            ShowScreen(Get<MainMenuScreen>(), false);
        }

        // ============================================================
        // POPUP
        // ============================================================

        public void OpenPopup(MenuPopup popup)
        {
            if (popup == null || m_Popups.Contains(popup)) return;

            m_Popups.Add(popup);
            popup.Root.SetAsLastSibling();
            popup.gameObject.SetActive(true);
            popup.OnShown();
            m_ToastRoot.SetAsLastSibling();
        }

        public void ClosePopup(MenuPopup popup)
        {
            if (popup == null || !m_Popups.Remove(popup)) return;
            popup.OnHidden();
            popup.gameObject.SetActive(false);
        }

        public void CloseAllPopups()
        {
            for (int i = m_Popups.Count - 1; i >= 0; i--) ClosePopup(m_Popups[i]);
        }

        // ============================================================
        // ESC
        // ============================================================

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

            if (m_Popups.Count > 0)
            {
                MenuPopup top = m_Popups[m_Popups.Count - 1];
                if (!top.OnBack()) ClosePopup(top);
                return;
            }

            if (m_Current == null) return;
            if (m_Current.OnBack()) return;
            if (m_History.Count > 0) Back();
        }

        // ============================================================
        // THÔNG BÁO NHANH
        // ============================================================

        private void BuildToast()
        {
            m_ToastRoot = UIKit.NewRect("Toast", m_Ctx.CanvasRoot);
            m_ToastRoot.anchorMin = new Vector2(0.5f, 0f);
            m_ToastRoot.anchorMax = new Vector2(0.5f, 0f);
            m_ToastRoot.pivot = new Vector2(0.5f, 0f);
            m_ToastRoot.anchoredPosition = new Vector2(0f, 80f);
            m_ToastRoot.sizeDelta = new Vector2(860f, 60f);

            m_ToastBg = m_ToastRoot.gameObject.AddComponent<Image>();
            m_ToastBg.color = UITheme.PanelDark;
            m_ToastBg.raycastTarget = false;

            var outline = m_ToastRoot.gameObject.AddComponent<Outline>();
            outline.effectColor = UITheme.Orange;
            outline.effectDistance = new Vector2(1.5f, 1.5f);

            m_ToastText = UIKit.Label(m_ToastRoot, "", 0, 0, 860, 60, 22, UITheme.Text,
                TextAlignmentOptions.Center, FontStyles.Bold, false, true);
            UIKit.Stretch(m_ToastText.rectTransform, 16f, 0f, 16f, 0f);

            m_ToastRoot.gameObject.SetActive(false);
        }

        /// <summary>Hiện một dòng thông báo ngắn ở cuối màn hình.</summary>
        public void Toast(string message, bool good = false)
        {
            if (m_ToastRoot == null) return;

            m_ToastText.text = message;
            m_ToastRoot.GetComponent<Outline>().effectColor = good ? UITheme.Green : UITheme.Orange;
            m_ToastRoot.gameObject.SetActive(true);
            m_ToastRoot.SetAsLastSibling();

            if (m_ToastRoutine != null) StopCoroutine(m_ToastRoutine);
            m_ToastRoutine = StartCoroutine(HideToastAfter(2.4f));
        }

        private IEnumerator HideToastAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            m_ToastRoot.gameObject.SetActive(false);
            m_ToastRoutine = null;
        }
    }
}
