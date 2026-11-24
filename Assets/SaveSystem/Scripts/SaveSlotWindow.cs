using TakeOver.Events;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TakeOver.SaveSystem
{
    public sealed class SaveSlotWindow : MonoBehaviour
    {
        private static SaveSlotWindow active;
        private EventNegotiationView template;
        private EventDetailView informationStyle;
        private GameObject panel, prompt;
        private Text message;
        private Text automaticLabel;
        private Button automaticLoad;
        private readonly Text[] labels = new Text[GameSaveManager.ManualSlotCount];
        private readonly Button[] loads = new Button[GameSaveManager.ManualSlotCount];

        public static void Show(EventNegotiationView source, bool loadOnly = false, EventDetailView style = null)
        {
            if (active != null || source == null) return;
            if (!loadOnly && GameSessionCoordinator.Active?.CanUseManualSaves != true)
            { Object.FindFirstObjectByType<FloatingMessage>()?.Show("진행 중인 대화나 안내를 마친 뒤 이용해 주세요."); return; }
            var root = new GameObject("Save and load", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32764;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root.GetComponent<Image>().color = Color.clear;
            active = root.AddComponent<SaveSlotWindow>(); active.template = source;
            active.informationStyle = style;
            active.Build(loadOnly);
        }
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one;
        }
        private void Build(bool loadOnly)
        {
            panel = template.CreateInformationPanel(transform, out var heading, out message);
            MakeOpaque(panel);
            Place((RectTransform)panel.transform, Vector2.zero, new Vector2(820, 540));
            heading.text = loadOnly ? "이어하기" : "저장과 불러오기";
            message.text = loadOnly ? "이어서 진행할 저장을 선택해 주세요." : "수동저장은 자동저장과 별개로 보관됩니다. 사건 전에 저장하면 같은 시점부터 다시 진행할 수 있습니다.";
            message.fontSize = 18;
            message.rectTransform.sizeDelta = new Vector2(-36, 50);
            var ruleSource = heading.transform.parent.Find("Underline");
            for (int boundary = 0; boundary <= labels.Length + 1; boundary++)
            {
                var rule = Instantiate(ruleSource, panel.transform, false);
                rule.name = "Save slot boundary " + boundary;
                Place((RectTransform)rule, new Vector2(0, 140 - boundary * 80), new Vector2(784, ((RectTransform)ruleSource).rect.height));
                foreach (var graphic in rule.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            }
            automaticLabel = Instantiate(message, panel.transform, false);
            automaticLabel.name = "Automatic save slot"; automaticLabel.fontSize = 20;
            Place(automaticLabel.rectTransform, new Vector2(-150, 100), new Vector2(450, 56));
            automaticLoad = Button("Automatic save load", "불러오기", new Vector2(310, 100), () => ConfirmLoad(0));
            for (int i = 0; i < labels.Length; i++)
            {
                int slot = i + 1; float y = 20 - i * 80;
                var text = Instantiate(message, panel.transform, false);
                text.name = "Save slot " + slot; text.fontSize = 20;
                Place(text.rectTransform, new Vector2(-150, y), new Vector2(450, 56));
                labels[i] = text;
                if (!loadOnly) Button("Save slot " + slot + " save", "저장", new Vector2(165, y), () => Save(slot));
                loads[i] = Button("Save slot " + slot + " load", "불러오기", new Vector2(310, y), () => ConfirmLoad(slot));
            }
            Button("Close saves", "닫기", new Vector2(0, -222), () => Destroy(gameObject));
            Refresh();
        }
        private Button Button(string name, string label, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            var button = template.CreateActionButton(panel.transform, name, label, () => action());
            informationStyle?.CopyActionStyle(button.targetGraphic as Image);
            Place((RectTransform)button.transform, position, new Vector2(130, 42)); return button;
        }
        private void Refresh()
        {
            automaticLabel.text = GameSaveManager.DescribeAutomaticSlot();
            automaticLoad.interactable = GameSaveManager.HasAutomaticSlot();
            for (int i = 0; i < labels.Length; i++)
            { labels[i].text = GameSaveManager.DescribeManualSlot(i + 1); loads[i].interactable = GameSaveManager.HasManualSlot(i + 1); }
        }
        private void Save(int slot)
        {
            if (GameSaveManager.HasManualSlot(slot)) Confirm($"{slot}번 저장을 현재 진행으로 덮어씁니다.", () => Write(slot));
            else Write(slot);
        }
        private void Write(int slot)
        {
            bool saved = GameSessionCoordinator.Active?.SaveManualSlot(slot) == true;
            message.text = saved ? $"{slot}번에 저장했습니다." : "저장하지 못했습니다. 진행 중인 작업과 저장 공간을 확인해 주세요.";
            Refresh();
        }
        private void ConfirmLoad(int slot)
        {
            Confirm(slot == 0 ? "자동저장한 시점부터 다시 진행합니다." : $"수동저장 {slot}의 시점부터 다시 진행합니다.", () =>
            {
                if (slot == 0) GameSessionCoordinator.RequestContinue();
                else GameSessionCoordinator.RequestManualLoad(slot);
                Destroy(gameObject); SceneManager.LoadScene("Game");
            });
        }
        private void Confirm(string text, System.Action accepted)
        {
            if (prompt != null) return;
            prompt = template.CreateInformationPanel(transform, out var heading, out var body);
            MakeOpaque(prompt);
            Place((RectTransform)prompt.transform, Vector2.zero, new Vector2(600, 220));
            heading.text = "확인"; body.text = text; body.rectTransform.sizeDelta = new Vector2(-36, 60);
            panel.SetActive(false);
            void ClosePrompt() { Destroy(prompt); prompt = null; panel.SetActive(true); }
            var yes = template.CreateActionButton(prompt.transform, "Confirm save action", "확인", () => { ClosePrompt(); accepted(); });
            var no = template.CreateActionButton(prompt.transform, "Cancel save action", "취소", ClosePrompt);
            informationStyle?.CopyActionStyle(yes.targetGraphic as Image);
            informationStyle?.CopyActionStyle(no.targetGraphic as Image);
            Place((RectTransform)yes.transform, new Vector2(-120, -68), new Vector2(180, 42));
            Place((RectTransform)no.transform, new Vector2(120, -68), new Vector2(180, 42));
        }
        private void MakeOpaque(GameObject instance)
        {
            var image = instance.GetComponent<Image>();
            informationStyle?.CopyInformationPanelStyle(image);
            if (image != null) { var color = image.color; color.a = 1f; image.color = color; }
            foreach (var group in instance.GetComponentsInChildren<CanvasGroup>(true)) group.alpha = 1f;
        }
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (prompt != null) { Destroy(prompt); prompt = null; panel.SetActive(true); }
                else Destroy(gameObject);
            }
        }
        private void OnDestroy() { if (active == this) active = null; }
    }
}
