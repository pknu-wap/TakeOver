using System.IO;
using TakeOver.Events;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TakeOver.Events.Editor
{
    public static class EventWorkSceneBuilder
    {
        public const string rootPath = "Assets/EventSystem";
        public const string scenePath = rootPath + "/Scenes/EventWorkScene.unity";
        public const string eventPath = rootPath + "/Data/StockManipulation.asset";
        private static Font uiFont;

        [MenuItem("TakeOver/Events/Build Work Scene")]
        public static void build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            foreach (string folder in new[] { "Data", "Prefabs", "Scenes" })
            {
                if (!AssetDatabase.IsValidFolder(rootPath + "/" + folder))
                    AssetDatabase.CreateFolder(rootPath, folder);
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            uiFont = AssetDatabase.LoadAssetAtPath<Font>(rootPath + "/Fonts/NanumGothic-Regular.ttf");
            if (uiFont == null)
                throw new FileNotFoundException("나눔고딕 폰트를 찾을 수 없습니다.");

            EventDefinition definition = createDefinition();
            Camera camera = new GameObject("WorkCamera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.13f, 0.15f);

            GameObject canvasObject = new GameObject("EventWorkCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            new GameObject("UIEventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));

            GameObject detailPrefab = createDetailPrefab();
            GameObject negotiationPrefab = createNegotiationPrefab();
            GameObject detail = (GameObject)PrefabUtility.InstantiatePrefab(detailPrefab, canvasObject.transform);
            GameObject negotiation = (GameObject)PrefabUtility.InstantiatePrefab(negotiationPrefab, canvasObject.transform);

            GameObject systemObject = new GameObject("EventSystemController");
            EventSystemController system = systemObject.AddComponent<EventSystemController>();
            setObjects(system, "events", definition);
            setObject(system, "detailView", detail.GetComponent<EventDetailView>());
            setObject(system, "negotiationView", negotiation.GetComponent<EventNegotiationView>());
            EventDebugSubscriber subscriber = systemObject.AddComponent<EventDebugSubscriber>();
            setObject(subscriber, "eventSystem", system);

            createDashboard(canvasObject.transform, system, definition);
            detail.transform.SetAsLastSibling();
            negotiation.transform.SetAsLastSibling();
            detail.SetActive(false);
            negotiation.SetActive(false);
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Event work scene generated: " + scenePath);
        }

        private static EventDefinition createDefinition()
        {
            EventDefinition existing = AssetDatabase.LoadAssetAtPath<EventDefinition>(eventPath);
            if (existing != null)
                return existing;

            string imagePath = rootPath + "/Data/EventImage.asset";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);
            Sprite sprite = null;
            if (texture == null)
            {
                texture = new Texture2D(16, 16);
                texture.name = "EventImage";
                Color[] pixels = new Color[256];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color(0.48f, 0.52f, 0.57f);
                texture.SetPixels(pixels);
                texture.Apply();
                AssetDatabase.CreateAsset(texture, imagePath);
                sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f);
                sprite.name = "EventImageSprite";
                AssetDatabase.AddObjectToAsset(sprite, texture);
            }
            else
            {
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(imagePath))
                    if (asset is Sprite foundSprite)
                        sprite = foundSprite;
            }

            EventDefinition definition = ScriptableObject.CreateInstance<EventDefinition>();
            SerializedObject data = new SerializedObject(definition);
            data.FindProperty("eventId").stringValue = "stock_manipulation_01";
            data.FindProperty("title").stringValue = "은밀한 주가조작 제안";
            data.FindProperty("image").objectReferenceValue = sprite;
            data.FindProperty("description").stringValue = "M&A 브로커 윤하진이 찾아왔다.\n보유 종목의 주가를 끌어올릴 수 있다는 제안이다.\n그의 이야기를 듣고 수락하거나 거절할 수 있다.";
            data.FindProperty("apCost").intValue = 1;
            data.FindProperty("startButtonText").stringValue = "협상을 시작한다";
            data.FindProperty("speakerName").stringValue = "윤하진";
            data.FindProperty("speakerRole").stringValue = "M&A 브로커";
            data.FindProperty("dialogue").stringValue = "보유하신 종목 하나, 제가 주가를 끌어올릴 수 있습니다.\n위험은 따르겠지만 차익을 얻을 기회죠.\n제안을 받아들이시겠습니까?";
            SerializedProperty companyIds = data.FindProperty("companyIds");
            companyIds.arraySize = 1;
            companyIds.GetArrayElementAtIndex(0).stringValue = "dummy_company_01";
            SerializedProperty choices = data.FindProperty("choices");
            choices.arraySize = 2;
            setChoice(choices.GetArrayElementAtIndex(0), "수락", "좋습니다. 준비가 되면 다시 연락드리죠.", "dummy_accept");
            setChoice(choices.GetArrayElementAtIndex(1), "거절", "알겠습니다. 오늘 나눈 이야기는 없던 일로 하죠.", "dummy_reject");
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition, eventPath);
            return definition;
        }

        private static void setChoice(SerializedProperty choice, string text, string result, string value)
        {
            choice.FindPropertyRelative("choiceText").stringValue = text;
            choice.FindPropertyRelative("resultDialogue").stringValue = result;
            choice.FindPropertyRelative("resultValue").stringValue = value;
        }

        private static GameObject createDetailPrefab()
        {
            GameObject root = modal("EventDetail");
            RectTransform panel = panelRect(root.transform);
            label(panel, "Heading", "이벤트", 32, 25, 850, 35, 20);
            Text title = label(panel, "Title", "", 32, 75, 850, 50, 32);
            Image image = rect("EventImage", panel, 32, 148, 210, 250).gameObject.AddComponent<Image>();
            image.preserveAspect = false;
            Text description = label(panel, "Description", "", 275, 148, 610, 250, 25);
            Text cost = label(panel, "ApCost", "", 32, 425, 850, 45, 24);
            Button back = button(panel, "BackButton", "돌아간다", 32, 510, 230, 58);
            Button start = button(panel, "StartButton", "협상을 시작한다", 560, 510, 328, 58);
            EventDetailView view = root.AddComponent<EventDetailView>();
            setObject(view, "eventImage", image);
            setObject(view, "titleText", title);
            setObject(view, "descriptionText", description);
            setObject(view, "apCostText", cost);
            setObject(view, "startButtonText", start.GetComponentInChildren<Text>());
            setObject(view, "backButton", back);
            setObject(view, "startButton", start);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, rootPath + "/Prefabs/EventDetail.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject createNegotiationPrefab()
        {
            GameObject root = modal("EventNegotiation");
            RectTransform panel = panelRect(root.transform);
            Text speaker = label(panel, "Speaker", "", 32, 32, 850, 48, 28);
            Text dialogue = label(panel, "Dialogue", "", 32, 113, 850, 195, 26);
            Button[] choices = new Button[4];
            Text[] texts = new Text[4];
            for (int i = 0; i < choices.Length; i++)
            {
                choices[i] = button(panel, "Choice" + i, "", 32, 322 + i * 62, 856, 52);
                texts[i] = choices[i].GetComponentInChildren<Text>();
            }
            Button close = button(panel, "CloseButton", "닫기", 648, 510, 240, 58);
            EventNegotiationView view = root.AddComponent<EventNegotiationView>();
            setObject(view, "speakerText", speaker);
            setObject(view, "dialogueText", dialogue);
            setObjects(view, "choiceButtons", choices);
            setObjects(view, "choiceTexts", texts);
            setObject(view, "closeButton", close);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, rootPath + "/Prefabs/EventNegotiation.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void createDashboard(Transform parent, EventSystemController system, EventDefinition definition)
        {
            RectTransform root = stretch("DebugDashboard", parent);
            RectTransform panel = panelRect(root);
            label(panel, "Heading", "오늘의 이벤트", 32, 32, 640, 55, 32);
            Text day = label(panel, "Day", "1일차", 720, 32, 170, 50, 26);
            RectTransform cards = rect("Cards", panel, 32, 130, 856, 210);
            VerticalLayoutGroup layout = cards.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            Button template = button(panel, "CardTemplate", "", 0, 0, 856, 70);
            template.gameObject.AddComponent<LayoutElement>().preferredHeight = 70;
            template.gameObject.SetActive(false);
            Text empty = label(panel, "Empty", "오늘 남은 이벤트가 없어.", 32, 145, 850, 50, 24);
            Text result = label(panel, "Results", "", 32, 365, 850, 120, 23);
            Button next = button(panel, "NextDayButton", "다음 날", 648, 510, 240, 58);
            EventDebugDashboard dashboard = root.gameObject.AddComponent<EventDebugDashboard>();
            setObject(dashboard, "eventSystem", system);
            setObject(dashboard, "cardContainer", cards);
            setObject(dashboard, "cardTemplate", template);
            setObject(dashboard, "dayText", day);
            setObject(dashboard, "emptyText", empty);
            setObject(dashboard, "resultText", result);
            setObjects(dashboard, "observedEvents", definition);
            setObject(dashboard, "nextDayButton", next);
        }

        private static GameObject modal(string name)
        {
            RectTransform root = stretch(name, null);
            Image blocker = root.gameObject.AddComponent<Image>();
            blocker.color = new Color(0, 0, 0, 0.72f);
            return root.gameObject;
        }

        private static RectTransform panelRect(Transform parent)
        {
            RectTransform panel = rect("Panel", parent, 0, 0, 920, 600);
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one * 0.5f;
            panel.anchoredPosition = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = new Color(0.22f, 0.23f, 0.25f);
            return panel;
        }

        private static RectTransform stretch(string name, Transform parent)
        {
            RectTransform result = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            result.SetParent(parent, false);
            result.anchorMin = Vector2.zero;
            result.anchorMax = Vector2.one;
            result.offsetMin = result.offsetMax = Vector2.zero;
            return result;
        }

        private static RectTransform rect(string name, Transform parent, float x, float y, float width, float height)
        {
            RectTransform result = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            result.SetParent(parent, false);
            result.anchorMin = result.anchorMax = result.pivot = new Vector2(0, 1);
            result.anchoredPosition = new Vector2(x, -y);
            result.sizeDelta = new Vector2(width, height);
            return result;
        }

        private static Text label(Transform parent, string name, string value, float x, float y, float width, float height, int size)
        {
            Text text = rect(name, parent, x, y, width, height).gameObject.AddComponent<Text>();
            text.font = uiFont;
            text.fontSize = size;
            text.text = value;
            text.color = new Color(0.94f, 0.94f, 0.94f);
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Button button(Transform parent, string name, string value, float x, float y, float width, float height)
        {
            RectTransform root = rect(name, parent, x, y, width, height);
            Image image = root.gameObject.AddComponent<Image>();
            image.color = new Color(0.37f, 0.39f, 0.42f);
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Text text = label(root, "Text", value, 16, 4, width - 32, height - 8, 24);
            text.alignment = TextAnchor.MiddleCenter;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.offsetMin = new Vector2(16, 4);
            text.rectTransform.offsetMax = new Vector2(-16, -4);
            return button;
        }

        private static void setObject(Object target, string property, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void setObjects(Object target, string property, params Object[] values)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty array = serialized.FindProperty(property);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
