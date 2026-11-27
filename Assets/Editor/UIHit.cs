using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IronWasteland.EditorTools
{
    /// <summary>
    /// Tiện ích dùng chung cho các builder UI: tạo EventSystem và biến vùng bấm thiết kế thành Button thật.
    /// Mọi toạ độ vẫn dùng hệ thiết kế 1920x1080 gốc trên-trái để khớp với ảnh đã dựng.
    /// </summary>
    public static class UIHit
    {
        /// <summary>Chiều cao hệ thiết kế, dùng để lật trục y sang gốc dưới-trái của RectTransform.</summary>
        public const float DesignHeight = 1080f;

        /// <summary>Tạo EventSystem dùng InputSystemUIInputModule (giống Garage) và đưa vào scene đang dựng.</summary>
        public static EventSystem EnsureEventSystem(Scene scene, string name = "EventSystem")
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                EventSystem existing = root.GetComponentInChildren<EventSystem>(true);
                if (existing != null)
                {
                    return existing;
                }
            }

            GameObject go = new GameObject(name, typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(go, scene);
            return go.GetComponent<EventSystem>();
        }

        /// <summary>Gắn Button lên Graphic đã có sẵn, bật raycast và tắt hiệu ứng chuyển màu mặc định.</summary>
        public static Button Attach(Graphic graphic)
        {
            graphic.raycastTarget = true;
            Button button = graphic.GetComponent<Button>();
            if (button == null)
            {
                button = graphic.gameObject.AddComponent<Button>();
            }

            button.targetGraphic = graphic;
            button.transition = Selectable.Transition.None;
            return button;
        }

        /// <summary>Tạo Image trong suốt đúng rect thiết kế rồi gắn Button, dùng khi vùng bấm không có Graphic sẵn.</summary>
        public static Button AddZone(RectTransform parent, string name, float x, float y, float width, float height)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, DesignHeight - (y + height));
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;

            Image image = go.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            return button;
        }
    }
}
