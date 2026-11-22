using UnityEngine;
using UnityEngine.UI;
using TakeOver.Events;
using TMPro;
using System.Linq;

namespace TakeOver.Story
{
    /// <summary>명시적 새 게임 요청만 소비한다. Game 씬 직접 실행·저장 복원은 도입부를 시작하지 않는다.</summary>
    public static class StoryLaunch
    {
        private static bool requested;
        private static bool skipRequested;
        private static GameObject curtain;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            requested = false; skipRequested = false; curtain = null;
        }

        public static void RequestNewGame(bool skipIntro = false)
        {
            requested = true;
            skipRequested = skipIntro;
            TakeOver.SaveSystem.GameSessionCoordinator.RequestNewGame();
            ShowLaunchCurtain();
        }

        public static void ShowLaunchCurtain()
        {
            if (curtain != null) return;
            curtain = new GameObject("New game blackout", typeof(Canvas));
            Object.DontDestroyOnLoad(curtain);
            curtain.AddComponent<GraphicRaycaster>();
            var canvas = curtain.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var image = new GameObject("Black", typeof(RectTransform), typeof(Image));
            image.transform.SetParent(curtain.transform, false);
            var rect = (RectTransform)image.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            image.GetComponent<Image>().color = Color.black;
        }

        public static void DismissLaunchCurtain() { if (curtain != null) Object.Destroy(curtain); curtain = null; }

        public static bool ShowNamePrompt(System.Action completed)
        {
            if (!requested) return false;
            var template = Object.FindFirstObjectByType<EventNegotiationView>(FindObjectsInactive.Include);
            var player = Object.FindFirstObjectByType<Player>();
            var scaler = curtain.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            var panel = template.CreateInformationPanel(curtain.transform, out Text heading, out Text body);
            panel.name = "Protagonist name";
            Place((RectTransform)panel.transform, Vector2.zero, new Vector2(540, 212));
            heading.text = "주인공 이름";
            body.text = "주인공의 이름을 입력하세요.";

            var fieldRoot = template.CreateActionButton(panel.transform, "Name input", "", null);
            var fieldLabel = fieldRoot.GetComponentInChildren<Text>(true);
            var fieldGraphic = fieldRoot.targetGraphic;
            var fieldObject = fieldRoot.gameObject;
            Object.DestroyImmediate(fieldRoot);
            Place((RectTransform)fieldObject.transform, new Vector2(0, -11), new Vector2(500, 42));
            var viewport = new GameObject("Text viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(fieldObject.transform, false);
            Place((RectTransform)viewport.transform, Vector2.zero, new Vector2(468, 36));
            var label = new GameObject("Name", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(viewport.transform, false);
            Place(label.rectTransform, Vector2.zero, new Vector2(468, 36));
            label.font = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(text => text.font).FirstOrDefault(font => font != null && font.name.StartsWith("NotoSansKR")) ?? TMP_Settings.defaultFontAsset;
            label.fontSize = 22;
            label.color = fieldLabel.color;
            label.richText = false;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            fieldLabel.gameObject.SetActive(false);
            var input = fieldObject.AddComponent<TMP_InputField>();
            input.targetGraphic = fieldGraphic;
            input.textViewport = (RectTransform)viewport.transform;
            input.textComponent = label;
            input.richText = false;
            input.characterLimit = 20;
            input.navigation = new Navigation { mode = Navigation.Mode.None };

            void Submit()
            {
                if (!requested || string.IsNullOrWhiteSpace(input.text)) return;
                player.SetName(input.text);
                panel.SetActive(false);
                StartIntro();
                completed();
            }

            var confirm = template.CreateActionButton(panel.transform, "Confirm name", "확인", Submit);
            Place((RectTransform)confirm.transform, new Vector2(201, -65), new Vector2(98, 40));
            confirm.interactable = false;
            input.onValueChanged.AddListener(value => confirm.interactable = !string.IsNullOrWhiteSpace(value));
            input.onSubmit.AddListener(_ => Submit());
            Canvas.ForceUpdateCanvases();
            input.Select();
            input.ActivateInputField();
            return true;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        internal static GameObject IntroPrefab => Resources.Load<GameObject>("Story/Intro");

        private static void StartIntro()
        {
            requested = false;
            var prefab = IntroPrefab;
            if (prefab == null) Debug.LogError("새 게임 도입부 프리팹이 없습니다: Resources/Story/Intro");
            else Object.Instantiate(prefab).GetComponent<StoryDirector>().SkipEntireIntroOnStart = skipRequested;
            skipRequested = false;
            DismissLaunchCurtain();
        }
    }
}
