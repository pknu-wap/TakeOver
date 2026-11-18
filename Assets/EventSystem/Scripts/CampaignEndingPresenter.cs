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
        public EventNegotiationView View => view;
        public bool AtEnd => owner != null && owner.EndingLineIndex >= owner.CurrentEnding.lines.Count;
        public void Build(EventSystemController controller, EventNegotiationView template)
        {
            owner = controller;
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
            if (AtEnd)
            {
                var saves = FindFirstObjectByType<GameSaveManager>();
                bool retry = owner.CampaignRevision == 2 && saves != null && saves.HasCampaignCheckpoint(owner.CampaignRunId, owner.LastDeadlineFailure.missionId);
                view.ShowDialogue(new DialoguePresentation { narration = true, showPlayer = false,
                    text = ending.title + "\n\n" + ending.companyOutcome + (retry ? "\n\n재도전하면 이 미션의 준비 시작 시점으로 돌아갑니다." : "\n\n복원 가능한 미션 준비 저장이 없습니다."),
                    choices = retry ? new[] { "재도전", "타이틀" } : new[] { "타이틀" } }, null, choice => {
                        if (retry && choice == 0) { GameSessionCoordinator.RequestRetry(owner.CampaignRunId, owner.LastDeadlineFailure.missionId); SceneManager.LoadScene("Game"); }
                        else SceneManager.LoadScene("MainMenu");
                    }, null, () => false);
            }
            else
            {
                var line = ending.lines[owner.EndingLineIndex];
                var registry = FindFirstObjectByType<NpcStateRegistry>();
                var profile = !string.IsNullOrEmpty(line.npcId) && registry.TryGetProfile(line.npcId, out var seed) ? seed.profile : null;
                string details = owner.EndingLineIndex == 0 ? string.Join(" / ", owner.LastDeadlineFailure.goals.Select(g =>
                    (FindFirstObjectByType<Companies>().getCompany(g.companyId)?.definition.companyName ?? g.companyId) + " " + g.actual.ToString("0.#") + "% (목표 " + g.required.ToString("0.#") + "%)")) + "\n\n" : "";
                view.ShowDialogue(new DialoguePresentation { speaker = line.player ? "주인공" : profile?.displayName ?? "",
                    text = details + line.text, player = line.player, narration = line.narration, showPlayer = !line.narration,
                    npcStanding = profile?.standingIllustration, npcDimmed = profile?.dimmedStandingIllustration },
                    () => owner.AdvanceEnding(false), null, () => owner.AdvanceEnding(true), () => true);
            }
            view.SetDialogueInput(true);
            if (AtEnd) view.SkipButton.gameObject.SetActive(false);
        }
    }
}
