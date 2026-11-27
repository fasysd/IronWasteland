using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

namespace IronWasteland.Tests
{
    /// <summary>
    /// Hợp đồng scene cho vùng bấm: mỗi vùng phải là Button thật, đúng rect thiết kế,
    /// có raycast, có EventSystem/GraphicRaycaster và có onClick nối sẵn.
    /// Test mở scene additive, không lưu gì.
    /// </summary>
    public class UIClickZoneSceneTests
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;
        private const float Tolerance = 0.5f;

        private Scene m_Scene;
        private bool m_ShouldClose;

        [TearDown]
        public void TearDown()
        {
            if (m_ShouldClose && m_Scene.IsValid() && m_Scene.isLoaded)
            {
                EditorSceneManager.CloseScene(m_Scene, true);
            }

            m_ShouldClose = false;
        }

        [Test]
        public void MainMenu_bon_muc_dieu_huong_la_button_dung_rect()
        {
            Scene scene = OpenScene(MainMenuScenePath);

            Assert.That(FindInScene<EventSystem>(scene), Is.Not.Null, "Scene thiếu EventSystem");
            Assert.That(FindInScene<GraphicRaycaster>(scene), Is.Not.Null, "Canvas thiếu GraphicRaycaster");

            List<Button> buttons = FindAllInScene<Button>(scene);
            Assert.That(buttons.Count, Is.EqualTo(4), "Số Button phải bằng số mục điều hướng");

            for (int i = 0; i < 4; i++)
            {
                string name = "Nav" + i;
                RectTransform rect = FindByName<RectTransform>(scene, name);
                Assert.That(rect, Is.Not.Null, "Thiếu vùng bấm " + name);
                AssertRect(rect, 24f, 484f + i * 120f, 750f, 112f);

                Button button = rect.GetComponent<Button>();
                Assert.That(button, Is.Not.Null, name + " chưa gắn Button");
                Assert.That(button.targetGraphic, Is.Not.Null, name + " chưa có targetGraphic");
                Assert.That(button.targetGraphic.raycastTarget, Is.True, name + " chưa bật raycastTarget");
                Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1), name + " chưa nối onClick");
                Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo("ActivateSection"));
            }
        }


        [Test]
        public void CoopLobby_cac_vung_bam_la_button_dung_rect()
        {
            Scene scene = OpenScene("Assets/Scenes/CoopLobby.unity");
            Canvas canvas = FindByName<Canvas>(scene, "CoopLobbyCanvas");
            Assert.That(canvas, Is.Not.Null, "Thiếu canvas CoopLobbyCanvas");
            Transform root = canvas.transform;

            Assert.That(FindInScene<EventSystem>(scene), Is.Not.Null, "Scene thiếu EventSystem");
            Assert.That(canvas.GetComponent<GraphicRaycaster>(), Is.Not.Null, "Canvas thiếu GraphicRaycaster");
            Assert.That(FindAllInScene<Button>(scene).FindAll(b => b.transform.IsChildOf(root)).Count, Is.EqualTo(16), "Số Button của lobby");

            AssertZone(root, "HitBack", 72f, 72f, 168f, 50f, "PressBack");
            AssertZone(root, "ButtonCreate", 140f, 866f, 320f, 68f, "PressCreate");
            AssertZone(root, "ButtonRefresh", 482f, 866f, 240f, 68f, "PressRefresh");
            AssertZone(root, "JoinCodeBox", 740f, 866f, 332f, 68f, "PressAddressField");
            AssertZone(root, "ButtonJoinCode", 1092f, 866f, 180f, 68f, "PressJoinAddress");
            AssertZone(root, "ButtonReady", 140f, 690f, 300f, 62f, "PressReady");
            AssertZone(root, "ButtonGarage", 520f, 690f, 300f, 62f, "PressGarage");
            AssertZone(root, "ButtonStart", 140f, 770f, 420f, 76f, "PressStart");
            AssertZone(root, "ButtonLeave", 140f, 862f, 260f, 54f, "PressLeave");
            AssertZone(root, "ButtonCopyAddress", 1316f, 540f, 300f, 44f, "PressCopyAddress");

            for (int i = 0; i < 6; i++)
            {
                AssertZone(root, "RoomRow" + (i + 1), 96f, 304f + i * 88f, 1140f, 74f, "PressRoom");
            }

            Assert.That(FindByName<TextMeshProUGUI>(root, "TextLobbyTitle").text, Is.EqualTo("PHÒNG LAN"));
            Assert.That(FindByName<TextMeshProUGUI>(root, "TextLobbySub").text, Does.Contain("MẠNG LAN"));
            Assert.That(FindByName<TextMeshProUGUI>(root, "TextJoinCode").text, Is.EqualTo("NHẬP IP HOST"));
            Assert.That(FindByName<TextMeshProUGUI>(root, "TextRoomCodeLabel").text, Is.EqualTo("PHÒNG LAN / IP HOST"));
            Assert.That(FindByName<TextMeshProUGUI>(root, "TextFlowStep3").text, Is.EqualTo("CHIA SẺ ĐỊA CHỈ HOST CHO ĐỒNG ĐỘI"));
        }


        [Test]
        public void GarageAccessory_cac_vung_bam_la_button_dung_rect()
        {
            Scene scene = OpenScene("Assets/Scenes/GarageAccessory.unity");
            Canvas canvas = FindByName<Canvas>(scene, "GarageAccessoryCanvas");
            Assert.That(canvas, Is.Not.Null, "Thiếu canvas GarageAccessoryCanvas");
            Transform root = canvas.transform;

            Assert.That(FindInScene<EventSystem>(scene), Is.Not.Null, "Scene thiếu EventSystem");
            Assert.That(canvas.GetComponent<GraphicRaycaster>(), Is.Not.Null, "Canvas thiếu GraphicRaycaster");
            Assert.That(FindAllInScene<Button>(scene).FindAll(b => b.transform.IsChildOf(root)).Count, Is.EqualTo(23), "Số Button của màn chọn phụ kiện");

            AssertZone(root, "HitClose", 1786f, 160f, 52f, 52f, "PressCancel");
            AssertZone(root, "ButtonCancel", 1210f, 900f, 290f, 52f, "PressCancel");
            AssertZone(root, "ButtonConfirm", 1526f, 900f, 296f, 52f, "PressConfirm");

            RectTransform content = FindByName<RectTransform>(root, "Content");
            Assert.That(content, Is.Not.Null, "Thiếu Content");
            for (int i = 0; i < 20; i++)
            {
                RectTransform card = FindByName<RectTransform>(content, "Card" + (i + 1).ToString("00"));
                Assert.That(card, Is.Not.Null, "Thiếu thẻ " + (i + 1));
                Assert.That(card.rect.width, Is.EqualTo(276f).Within(0.5f));
                Assert.That(card.rect.height, Is.EqualTo(596f).Within(0.5f));
                Assert.That(card.anchoredPosition.x, Is.EqualTo(i * 290.4f).Within(0.5f));

                Image background = card.GetComponent<Image>();
                Assert.That(background, Is.Not.Null, "Thẻ " + (i + 1) + " thiếu nền");
                Assert.That(background.raycastTarget, Is.True, "Nền thẻ " + (i + 1) + " chưa bật raycastTarget");
                Button button = card.GetComponent<Button>();
                Assert.That(button, Is.Not.Null, "Thẻ " + (i + 1) + " chưa gắn Button");
                Assert.That(button.targetGraphic, Is.SameAs(background));
                Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1));
                Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo("SelectCard"));
            }
        }

        [Test]
        public void StageSelect_cac_vung_bam_la_button_dung_rect()
        {
            Scene scene = OpenScene("Assets/Scenes/StageSelect.unity");
            Canvas canvas = FindByName<Canvas>(scene, "StageSelectCanvas");
            Assert.That(canvas, Is.Not.Null, "Thiếu canvas StageSelectCanvas");
            Transform root = canvas.transform;

            Assert.That(FindInScene<EventSystem>(scene), Is.Not.Null, "Scene thiếu EventSystem");
            Assert.That(canvas.GetComponent<GraphicRaycaster>(), Is.Not.Null, "Canvas thiếu GraphicRaycaster");
            Assert.That(FindAllInScene<Button>(scene).FindAll(b => b.transform.IsChildOf(root)).Count, Is.EqualTo(13), "Số Button của màn chọn ải");

            TextMeshProUGUI modeCoop = FindByName<TextMeshProUGUI>(root, "TextModeCoop");
            Assert.That(modeCoop, Is.Not.Null, "Thiếu nhãn chế độ coop");
            Assert.That(modeCoop.text, Is.EqualTo("CHƠI LAN"), "Nhãn chế độ phải là CHƠI LAN");

            bool foundController = false;
            foreach (MonoBehaviour behaviour in canvas.GetComponents<MonoBehaviour>())
            {
                SerializedObject serialized = new SerializedObject(behaviour);
                SerializedProperty lobbyScene = serialized.FindProperty("m_CoopLobbySceneName");
                if (lobbyScene == null) continue;
                foundController = true;
                Assert.That(lobbyScene.stringValue, Is.EqualTo("CoopLobby"), "CHƠI LAN phải mở scene CoopLobby");
            }

            Assert.That(foundController, Is.True, "Thiếu StageSelectController trên canvas");

            AssertZone(root, "HitBack", 72f, 94f, 168f, 50f, "PressBack");
            AssertZone(root, "HitStart", 1293f, 939f, 536f, 136f, "PressStart");
            AssertZone(root, "HitModeSingle", 1295f, 472f, 253f, 48f, "PressModeSingle");
            AssertZone(root, "HitModeCoop", 1565f, 472f, 265f, 48f, "PressModeCoop");

            float[] cards =
            {
                122.5f, 380.5f, 195f, 147f,
                347f, 381f, 194f, 146f,
                570.5f, 380.5f, 195f, 147f,
                794f, 380f, 196f, 148f,
                1018.5f, 380.5f, 195f, 147f,
                346f, 672f, 196f, 148f,
                570f, 672f, 196f, 148f,
                794.5f, 672.5f, 195f, 147f,
                1018.5f, 672.5f, 195f, 147f
            };
            for (int i = 0; i < 9; i++)
            {
                AssertZone(root, "HitCard" + (i + 1).ToString("00"), cards[i * 4], cards[i * 4 + 1], cards[i * 4 + 2], cards[i * 4 + 3], "SelectStage");
            }
        }

        [Test]
        public void StageSelect_du_9_nhan_trang_thai_dong()
        {
            Scene scene = OpenScene("Assets/Scenes/StageSelect.unity");
            Canvas canvas = FindByName<Canvas>(scene, "StageSelectCanvas");
            Assert.That(canvas, Is.Not.Null, "Thiếu canvas StageSelectCanvas");

            SerializedObject controller = null;
            SerializedProperty covers = null;
            SerializedProperty texts = null;
            foreach (MonoBehaviour behaviour in canvas.GetComponents<MonoBehaviour>())
            {
                SerializedObject serialized = new SerializedObject(behaviour);
                SerializedProperty candidate = serialized.FindProperty("m_StatusCovers");
                if (candidate == null) continue;
                controller = serialized;
                covers = candidate;
                texts = serialized.FindProperty("m_StatusTexts");
                break;
            }

            Assert.That(controller, Is.Not.Null, "Thiếu StageSelectController trên canvas");
            Assert.That(covers, Is.Not.Null, "Thiếu mảng m_StatusCovers");
            Assert.That(texts, Is.Not.Null, "Thiếu mảng m_StatusTexts");
            Assert.That(covers.arraySize, Is.EqualTo(9), "Phải có đủ 9 nền trạng thái");
            Assert.That(texts.arraySize, Is.EqualTo(9), "Phải có đủ 9 nhãn trạng thái");

            HashSet<Image> coverIds = new HashSet<Image>();
            HashSet<TextMeshProUGUI> textIds = new HashSet<TextMeshProUGUI>();
            for (int i = 0; i < 9; i++)
            {
                Image cover = covers.GetArrayElementAtIndex(i).objectReferenceValue as Image;
                TextMeshProUGUI text = texts.GetArrayElementAtIndex(i).objectReferenceValue as TextMeshProUGUI;
                Assert.That(cover, Is.Not.Null, "Thiếu nền trạng thái thẻ " + (i + 1));
                Assert.That(text, Is.Not.Null, "Thiếu nhãn trạng thái thẻ " + (i + 1));
                Assert.That(coverIds.Add(cover), Is.True, "Nền trạng thái bị trùng ở thẻ " + (i + 1));
                Assert.That(textIds.Add(text), Is.True, "Nhãn trạng thái bị trùng ở thẻ " + (i + 1));
                Assert.That(text.rectTransform.anchoredPosition.x, Is.EqualTo(cover.rectTransform.anchoredPosition.x).Within(0.1f), "Nhãn và nền lệch trục X ở thẻ " + (i + 1));
            }
        }

        [Test]
        public void StageSelect_du_9_nhan_muc_tieu_dong()
        {
            Scene scene = OpenScene("Assets/Scenes/StageSelect.unity");
            Canvas canvas = FindByName<Canvas>(scene, "StageSelectCanvas");
            Assert.That(canvas, Is.Not.Null, "Thiếu canvas StageSelectCanvas");

            SerializedObject controller = null;
            SerializedProperty covers = null;
            SerializedProperty texts = null;
            foreach (MonoBehaviour behaviour in canvas.GetComponents<MonoBehaviour>())
            {
                SerializedObject serialized = new SerializedObject(behaviour);
                SerializedProperty candidate = serialized.FindProperty("m_CardFooterCovers");
                if (candidate == null) continue;
                controller = serialized;
                covers = candidate;
                texts = serialized.FindProperty("m_CardFooterTexts");
                break;
            }

            Assert.That(controller, Is.Not.Null, "Thiếu StageSelectController trên canvas");
            Assert.That(covers, Is.Not.Null, "Thiếu mảng m_CardFooterCovers");
            Assert.That(texts, Is.Not.Null, "Thiếu mảng m_CardFooterTexts");
            Assert.That(covers.arraySize, Is.EqualTo(9), "Phải có đủ 9 nền chân thẻ");
            Assert.That(texts.arraySize, Is.EqualTo(9), "Phải có đủ 9 nhãn chân thẻ");

            HashSet<Image> coverIds = new HashSet<Image>();
            HashSet<TextMeshProUGUI> textIds = new HashSet<TextMeshProUGUI>();
            for (int i = 0; i < 9; i++)
            {
                Image cover = covers.GetArrayElementAtIndex(i).objectReferenceValue as Image;
                TextMeshProUGUI text = texts.GetArrayElementAtIndex(i).objectReferenceValue as TextMeshProUGUI;
                Assert.That(cover, Is.Not.Null, "Thiếu nền chân thẻ " + (i + 1));
                Assert.That(text, Is.Not.Null, "Thiếu nhãn chân thẻ " + (i + 1));
                Assert.That(coverIds.Add(cover), Is.True, "Nền chân thẻ bị trùng ở thẻ " + (i + 1));
                Assert.That(textIds.Add(text), Is.True, "Nhãn chân thẻ bị trùng ở thẻ " + (i + 1));
                Assert.That(text.rectTransform.anchoredPosition.x, Is.EqualTo(cover.rectTransform.anchoredPosition.x).Within(0.1f), "Nhãn và nền chân thẻ lệch trục X ở thẻ " + (i + 1));
                Assert.That(text.rectTransform.anchoredPosition.y, Is.EqualTo(cover.rectTransform.anchoredPosition.y + 24.4f).Within(0.1f), "Nhãn chân thẻ không nằm trong nền ở thẻ " + (i + 1));
            }
        }

        [Test]
        public void AccessorySalvage_cac_vung_bam_la_button_dung_rect()
        {
            Scene scene = OpenScene("Assets/Scenes/AccessorySalvage.unity");
            Canvas canvas = FindByName<Canvas>(scene, "AccessorySalvageCanvas");
            Assert.That(canvas, Is.Not.Null, "Thiếu canvas AccessorySalvageCanvas");
            Transform root = canvas.transform;

            Assert.That(FindInScene<EventSystem>(scene), Is.Not.Null, "Scene thiếu EventSystem");
            Assert.That(canvas.GetComponent<GraphicRaycaster>(), Is.Not.Null, "Canvas thiếu GraphicRaycaster");
            Assert.That(FindAllInScene<Button>(scene).FindAll(x => x.transform.IsChildOf(root)).Count, Is.EqualTo(26), "Số Button của màn phân rã");

            AssertZone(root, "Close", 1786f, 160f, 52f, 52f, "PressClose");
            AssertZone(root, "ButtonCancel", 1210f, 900f, 290f, 52f, "PressCancel");
            AssertZone(root, "ButtonConfirm", 1526f, 900f, 296f, 52f, "PressConfirm");
            AssertZone(root, "KeyMinus", 478f, 898f, 36f, 36f, "PressMinus");
            AssertZone(root, "KeyPlus", 610f, 898f, 36f, 36f, "PressPlus");
            AssertZone(root, "KeyMax", 656f, 898f, 62f, 36f, "PressMax");

            Transform content = FindByName<Transform>(root, "Content");
            Assert.That(content, Is.Not.Null, "Thiếu Content");
            for (int i = 0; i < 20; i++)
            {
                Button card = FindByName<Button>(content, "Card" + (i + 1).ToString("00"));
                Assert.That(card, Is.Not.Null, "Thiếu Button thẻ " + (i + 1));
                Assert.That(card.onClick.GetPersistentEventCount(), Is.GreaterThanOrEqualTo(1), "Thẻ " + (i + 1) + " chưa nối sự kiện");
                Assert.That(card.onClick.GetPersistentMethodName(0), Is.EqualTo("SelectCard"), "Thẻ " + (i + 1) + " nối sai hàm");
            }
        }

        [Test]
        public void Setting_ba_nut_roi_la_button()
        {
            Scene scene = OpenScene("Assets/Scenes/Setting.unity");
            Canvas canvas = FindByName<Canvas>(scene, "SettingCanvas");
            Assert.That(canvas, Is.Not.Null, "Thiếu canvas SettingCanvas");
            Transform root = canvas.transform;

            Assert.That(FindInScene<EventSystem>(scene), Is.Not.Null, "Scene thiếu EventSystem");
            Assert.That(canvas.GetComponent<GraphicRaycaster>(), Is.Not.Null, "Canvas thiếu GraphicRaycaster");
            Assert.That(FindAllInScene<Button>(scene).FindAll(x => x.transform.IsChildOf(root)).Count, Is.EqualTo(3), "Số Button của màn cài đặt");

            AssertButtonMethod(root, "BackButton", "PressBack");
            AssertButtonMethod(root, "ResetButton", "PressReset");
            AssertButtonMethod(root, "ApplyButton", "PressApply");
        }

        private static void AssertButtonMethod(Transform root, string name, string methodName)
        {
            Button button = FindByName<Button>(root, name);
            Assert.That(button, Is.Not.Null, "Thiếu Button " + name);
            Assert.That(button.onClick.GetPersistentEventCount(), Is.GreaterThanOrEqualTo(1), name + " chưa nối sự kiện");
            Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo(methodName), name + " nối sai hàm");
        }

        [Test]
        public void SkillPicker_cac_vung_bam_la_button_dung_rect()
        {
            Scene scene = OpenScene("Assets/Scenes/SkillPicker.unity");
            Canvas canvas = FindByName<Canvas>(scene, "SkillPickerCanvas");
            Assert.That(canvas, Is.Not.Null, "Thiếu canvas SkillPickerCanvas");
            Transform root = canvas.transform;

            Assert.That(FindInScene<EventSystem>(scene), Is.Not.Null, "Scene thiếu EventSystem");
            Assert.That(canvas.GetComponent<GraphicRaycaster>(), Is.Not.Null, "Canvas thiếu GraphicRaycaster");
            Assert.That(FindAllInScene<Button>(scene).FindAll(x => x.transform.IsChildOf(root)).Count, Is.EqualTo(7), "Số Button của màn chọn kỹ năng");

            AssertZone(root, "HitTab1", 432f, 306f, 236f, 48f, "PressTab1");
            AssertZone(root, "HitTab2", 678f, 306f, 236f, 48f, "PressTab2");
            AssertButtonMethod(root, "CloseButton", "PressClose");
            AssertButtonMethod(root, "FooterButton", "PressClose");
            for (int i = 0; i < 3; i++)
            {
                AssertButtonMethod(root, "CardBase" + i, "Choose");
            }
        }

        private static void AssertZone(Transform root, string name, float designX, float designY, float width, float height, string methodName)
        {
            Image image = FindByName<Image>(root, name);
            Assert.That(image, Is.Not.Null, "Thiếu vùng bấm " + name);
            AssertRect(image.rectTransform, designX, designY, width, height);

            Button button = image.GetComponent<Button>();
            Assert.That(button, Is.Not.Null, name + " chưa gắn Button");
            Assert.That(button.targetGraphic, Is.SameAs(image), name + " targetGraphic");
            Assert.That(image.raycastTarget, Is.True, name + " chưa bật raycastTarget");
            Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1), name + " chưa nối onClick");
            Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo(methodName));
        }

        private static T FindByName<T>(Transform root, string name) where T : Component
        {
            foreach (T candidate in root.GetComponentsInChildren<T>(true))
            {
                if (candidate.name == name)
                {
                    return candidate;
                }
            }

            return null;
        }

        private Scene OpenScene(string path)
        {
            Scene loaded = SceneManager.GetSceneByPath(path);
            if (loaded.IsValid() && loaded.isLoaded)
            {
                return loaded;
            }

            m_Scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            m_ShouldClose = true;
            return m_Scene;
        }

        private static void AssertRect(RectTransform rect, float designX, float designY, float width, float height)
        {
            string label = rect.name;
            // Chấp nhận cả anchor gốc trên-trái (ảnh dựng sẵn) lẫn gốc dưới-trái (vùng bấm trong suốt),
            // miễn là rect quy về đúng toạ độ thiết kế.
            Assert.That(rect.anchorMin, Is.EqualTo(rect.anchorMax), label + " đang giãn theo anchor");
            Assert.That(rect.sizeDelta.x, Is.EqualTo(width).Within(Tolerance), label + " width");
            Assert.That(rect.sizeDelta.y, Is.EqualTo(height).Within(Tolerance), label + " height");

            float parentWidth = DesignWidth;
            float parentHeight = DesignHeight;
            float pivotX = rect.anchorMin.x * parentWidth + rect.anchoredPosition.x;
            float pivotY = rect.anchorMin.y * parentHeight + rect.anchoredPosition.y;
            float left = pivotX - rect.pivot.x * rect.rect.width;
            float bottom = pivotY - rect.pivot.y * rect.rect.height;
            float top = DesignHeight - (bottom + rect.rect.height);
            Assert.That(left, Is.EqualTo(designX).Within(Tolerance), label + " x");
            Assert.That(top, Is.EqualTo(designY).Within(Tolerance), label + " y");
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static List<T> FindAllInScene<T>(Scene scene) where T : Component
        {
            List<T> results = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                results.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return results;
        }

        private static T FindByName<T>(Scene scene, string name) where T : Component
        {
            foreach (T candidate in FindAllInScene<T>(scene))
            {
                if (candidate.name == name)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}