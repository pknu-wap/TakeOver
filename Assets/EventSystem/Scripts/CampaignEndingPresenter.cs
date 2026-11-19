using System.Linq;
using TakeOver.NPC;
using TakeOver.SaveSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TakeOver.Events
{
    public sealed class CampaignEndingPresenter : MonoBehaviour
    {
        private EventSystemController owner;
        private EventNegotiationView view;
        private int shownScene = -1;
        private CampaignEndingVisuals visuals;
        public EventNegotiationView View => view;
        public bool AtEnd => owner != null && owner.EndingLineIndex >= owner.CurrentEnding.lines.Count;
        public void Build(EventSystemController controller, EventNegotiationView template)
        {
            owner = controller;
            visuals = Resources.Load<CampaignEndingVisuals>("Story/EndingVisuals");
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32500;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            var source = template.GetComponentInParent<CanvasScaler>();
            scaler.uiScaleMode = source.uiScaleMode; scaler.referenceResolution = source.referenceResolution;
            scaler.screenMatchMode = source.screenMatchMode; scaler.matchWidthOrHeight = source.matchWidthOrHeight;
            gameObject.AddComponent<GraphicRaycaster>();
            var black = new GameObject("Ending input background", typeof(RectTransform), typeof(Image)); black.transform.SetParent(transform, false);
            var rect = (RectTransform)black.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            black.GetComponent<Image>().color = Color.black;
            view = Instantiate(template.gameObject, transform, false).GetComponent<EventNegotiationView>();
            view.name = "Shared ending dialogue"; Present();
        }
        public void Present()
        {
            var ending = owner.CurrentEnding;
            if (ending == null) throw new System.InvalidOperationException("실패 신호에 맞는 엔딩이 없습니다.");
            int scene = AtEnd ? 4 : ending.lines[owner.EndingLineIndex].scene;
            if (scene != shownScene)
            {
                bool first = shownScene < 0;
                if (EventSceneTransition.IsBusy) return;
                EventSceneTransition.Play(() => { shownScene = scene; Render(); }, first, .65f);
            }
            else Render();
        }
        private void Render()
        {
            var ending = owner.CurrentEnding;
            Sprite background = visuals != null && visuals.backgrounds.Length > 0
                ? visuals.backgrounds[Mathf.Clamp(shownScene, 0, visuals.backgrounds.Length - 1)] : null;
            if (AtEnd)
            {
                var saves = FindFirstObjectByType<GameSaveManager>();
                bool retry = owner.CampaignRevision == 2 && saves != null && saves.HasCampaignCheckpoint(owner.CampaignRunId, owner.LastDeadlineFailure.missionId);
                view.ShowDialogue(new DialoguePresentation { narration = true, showPlayer = false, background = background, backgroundTint = new Color(.25f,.28f,.34f,1),
                    text = $"엔딩 {owner.CurrentEndingNumber}: {ending.title}",
                    choices = retry ? new[] { "재도전", "타이틀" } : new[] { "타이틀" } }, null, choice => {
                        if (retry && choice == 0) { GameSessionCoordinator.RequestRetry(owner.CampaignRunId, owner.LastDeadlineFailure.missionId); SceneManager.LoadScene("Game"); }
                        else SceneManager.LoadScene("MainMenu");
                    }, null, () => false);
            }
            else
            {
                var line = ending.lines[owner.EndingLineIndex];
                view.ShowDialogue(new DialoguePresentation { background = background, backgroundTint = new Color(.6f,.65f,.72f,1),
                    text = line.text, narration = true, showPlayer = false },
                    () => owner.AdvanceEnding(false), null, () => owner.AdvanceEnding(true), () => true);
            }
            view.SetDialogueInput(true);
            if (AtEnd) view.SkipButton.gameObject.SetActive(false);
        }
    }
}
