using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TakeOver.NPC;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TakeOver.Events
{
    public enum MandatoryStoryState { Pending, Forced, Failed, Completed }
    [Serializable] public sealed class StoryDeadlineFailureInfo
    {
        public string eventId, companyId, endingSignalId, missionId;
        public int failedDay;
        public float stakeAtFailure;
        public List<CampaignStakeSnapshot> goals = new List<CampaignStakeSnapshot>();
    }
    public sealed partial class EventSystemController : ITurnAdvanceGuard
    {
        public const string MandatoryEventId = "inside_info_01";
        public const string MandatoryCompanyId = "mirae-construction";
        public const int MandatoryFailureDay = 5;
        public const string MandatoryEndingSignal = "mirae-cooperation-deadline-failed";
        [SerializeField] private UnityEvent<string> onBadEndingRequested = new UnityEvent<string>();
        public event Action<StoryDeadlineFailureInfo> BadEndingRequested;
        public event Action MandatoryDeadlineChanged;
        public MandatoryStoryState MandatoryState => mandatoryState;
        public int CampaignRevision => campaignRevision;
        public string CampaignRunId => campaignRunId;
        public IReadOnlyList<string> PendingMandatoryEvents => pendingMandatoryEvents.AsReadOnly();
        public bool IsCampaignTradeLocked => mandatoryState == MandatoryStoryState.Forced || mandatoryState == MandatoryStoryState.Failed;
        public bool IsMandatoryFailureReady { get; private set; }
        public StoryDeadlineFailureInfo LastDeadlineFailure { get; private set; }
        public CampaignEnding CurrentEnding => ResolveEnding();
        public int CurrentEndingNumber => campaignPlan.endings.IndexOf(CurrentEnding) + 1;
        public int EndingLineIndex => endingLineIndex;
        public string CompanyMandateStatus => mandatoryState == MandatoryStoryState.Failed ? CurrentEnding?.companyOutcome ?? "독립 운용 종료" : "독립 투자 검토 진행";
        private EarlyCampaignPlan campaignPlan;
        private int campaignRevision = 2;
        private string campaignRunId = Guid.NewGuid().ToString("N");
        private readonly List<string> pendingMandatoryEvents = new List<string>();
        private List<CampaignStakeSnapshot> failureGoals = new List<CampaignStakeSnapshot>();
        private string failedMissionId = "", failedEndingSignal = "";
        private int endingLineIndex;
        private MandatoryStoryState mandatoryState;
        private string mandatoryEventId = "";
        private int campaignDayOffset;
        private float mandatoryFailureStake;
        private Image deadlineCurtain;
        private Coroutine deadlineTransition;
        private CampaignEndingPresenter endingPresenter;
        private bool IsMandatory(EventDefinition item) => item != null && (item.getId() == MandatoryEventId || item.IsStory && item.CampaignDay > 0);
        private int DueDay(EventDefinition item) => (item.getId() == MandatoryEventId ? 5 : item.CampaignDay) + (item.getId() == MandatoryEventId ? 0 : campaignDayOffset);
        private string MissionCompany(EventDefinition item) => item == null ? "" : item.getId() == MandatoryEventId ? MandatoryCompanyId : item.getCompanyIds().FirstOrDefault() ?? "";
        private bool PrerequisiteMet(EventDefinition item) => (string.IsNullOrEmpty(item.PrerequisiteEventId) || selectedChoices.ContainsKey(item.PrerequisiteEventId)) && item.PrerequisiteEventIds.All(selectedChoices.ContainsKey);
        private int OccurrenceExpiry(EventDefinition item, int firstDay) => IsMandatory(item) ? int.MaxValue : firstDay + item.getValidDays();
        private bool ShowMandatoryOccurrence(EventDefinition item) => mandatoryState != MandatoryStoryState.Failed && (mandatoryState != MandatoryStoryState.Forced || pendingMandatoryEvents.Contains(item.getId()));
        private bool EnsureMandatoryOffer()
        {
            if (mandatoryState == MandatoryStoryState.Failed) return false;
            var definition = events.Find(e => e.getId() == MandatoryEventId);
            if (definition == null || selectedChoices.ContainsKey(MandatoryEventId) || !meetsConditions(definition, MandatoryCompanyId) || occurrences.Exists(o => o.definition == definition)) return false;
            occurrences.Add(new Occurrence { definition = definition, companyId = MandatoryCompanyId, firstDay = Mathf.Max(1, currentDay) }); return true;
        }
        private void OnMandatoryDayStarted(int day)
        {
            EvaluateMandatoryDeadline(day);
            var mission = ActiveMission;
            if (mission != null && mission.dueDay + campaignDayOffset - day == 1)
            {
                string note = "내일 시작에 투자 목표 판정: " + MissionRequirement.Replace("\n", " / ");
                FindFirstObjectByType<FloatingMessage>()?.Show("내일 시작에 투자 목표를 판정합니다.");
                FindFirstObjectByType<NewsFeed>()?.addNews(day, note, mission.goals.Select(g => g.companyId).ToArray());
            }
        }
        private void EvaluateMandatoryDeadline(int day)
        {
            if (mandatoryState == MandatoryStoryState.Failed || mandatoryState == MandatoryStoryState.Forced || screenState == ScreenState.Result) return;
            mandatoryEventId = ""; mandatoryState = ActiveMission != null ? MandatoryStoryState.Pending : MandatoryStoryState.Completed;
            var due = events.Where(IsMandatory).Where(e => !selectedChoices.ContainsKey(e.getId()) && PrerequisiteMet(e) && day >= DueDay(e)).OrderBy(DueDay).FirstOrDefault();
            if (due != null)
            {
                FindFirstObjectByType<FloatingMessage>()?.Hide();
                var mission = MissionFor(due);
                if (mission != null)
                {
                    var snapshot = mission.goals.Select(g => new CampaignStakeSnapshot { companyId = g.companyId, required = g.percent, actual = Stake(g.companyId) }).ToList();
                    int mask = 0;
                    for (int i = 0; i < snapshot.Count; i++) if (!float.IsFinite(snapshot[i].actual) || snapshot[i].actual + .0001f < snapshot[i].required) mask |= 1 << i;
                    if (mask > 0) { FailMission(mission, snapshot, mask); return; }
                }
                pendingMandatoryEvents.Clear();
                pendingMandatoryEvents.AddRange(mission?.eventIds.Where(id => !selectedChoices.ContainsKey(id)) ?? new[] { due.getId() });
                foreach (string id in pendingMandatoryEvents)
                {
                    var item = events.Single(e => e.getId() == id);
                    if (!PrerequisiteMet(item)) throw new InvalidOperationException("필수 묶음의 선행 사건이 다릅니다: " + id);
                    if (!occurrences.Any(o => o.definition == item)) occurrences.Add(new Occurrence { definition = item, companyId = MissionCompany(item), firstDay = day });
                }
                mandatoryState = MandatoryStoryState.Forced; mandatoryEventId = pendingMandatoryEvents[0]; InterruptForMandatoryDeadline();
            }
            EnsureMandatoryOffer(); refreshAvailability(); MandatoryDeadlineChanged?.Invoke();
        }
        private void FailMission(CampaignMission mission, List<CampaignStakeSnapshot> snapshot, int mask)
        {
            failedMissionId = mission.id; failureGoals = snapshot; failedEndingSignal = mission.signals[mask - 1]; endingLineIndex = 0;
            mandatoryFailureStake = snapshot.First(g => g.actual + .0001f < g.required).actual;
            mandatoryState = MandatoryStoryState.Failed; mandatoryEventId = mission.eventIds[0]; pendingMandatoryEvents.Clear();
            InterruptForMandatoryDeadline(); refreshAvailability(); StartDeadlineFailure(false); MandatoryDeadlineChanged?.Invoke();
        }
        private void InterruptForMandatoryDeadline()
        {
            EventSceneTransition.Cancel();
            detailView.hide(); negotiationView.hide(); activeInfo = null; negotiationChoices.Clear(); screenState = ScreenState.Closed;
        }
        private void CompleteMandatoryPresentation()
        {
            if (mandatoryState == MandatoryStoryState.Failed) return;
            pendingMandatoryEvents.RemoveAll(selectedChoices.ContainsKey);
            if (pendingMandatoryEvents.Count > 0) { mandatoryEventId = pendingMandatoryEvents[0]; refreshAvailability(); return; }
            mandatoryState = MandatoryStoryState.Completed; mandatoryEventId = ""; EvaluateMandatoryDeadline(currentDay);
        }
        public bool CanAdvanceTurn(int day) => screenState == ScreenState.Closed && !IsCampaignTradeLocked;
        public void OnTurnAdvanceBlocked() => FindFirstObjectByType<FloatingMessage>()?.Show(mandatoryState == MandatoryStoryState.Forced ? "필수 사건을 모두 완료해 주십시오." : mandatoryState == MandatoryStoryState.Failed ? "엔딩에서 재도전하거나 타이틀로 돌아갈 수 있습니다." : "진행 중인 만남을 먼저 마쳐 주십시오.");
        private void StartDeadlineFailure(bool restored)
        {
            if (deadlineTransition != null) StopCoroutine(deadlineTransition);
            if (endingPresenter != null) Destroy(endingPresenter.gameObject);
            if (deadlineCurtain != null) Destroy(deadlineCurtain.transform.parent.gameObject);
            IsMandatoryFailureReady = false; LastDeadlineFailure = null;
            var overlay = new GameObject("Story deadline transition", typeof(Canvas), typeof(GraphicRaycaster)); overlay.transform.SetParent(transform, false);
            var canvas = overlay.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32766;
            var black = new GameObject("Deadline blackout", typeof(RectTransform), typeof(Image)); black.transform.SetParent(overlay.transform, false);
            deadlineCurtain = black.GetComponent<Image>(); deadlineCurtain.raycastTarget = true;
            var rect = deadlineCurtain.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            deadlineCurtain.color = new Color(0, 0, 0, restored ? 1 : 0); deadlineTransition = StartCoroutine(FadeDeadlineFailure(restored));
        }
        private IEnumerator FadeDeadlineFailure(bool restored)
        {
            if (!restored) for (float elapsed = 0; elapsed < .65f; elapsed += Time.unscaledDeltaTime)
            { deadlineCurtain.color = new Color(0, 0, 0, Mathf.SmoothStep(0, 1, elapsed / .65f)); yield return null; }
            else yield return null;
            deadlineCurtain.color = Color.black;
            var mission = missions.Single(m => m.id == failedMissionId);
            var failed = failureGoals.First(g => g.actual + .0001f < g.required);
            LastDeadlineFailure = new StoryDeadlineFailureInfo { eventId = mandatoryEventId, missionId = failedMissionId, companyId = failed.companyId, endingSignalId = failedEndingSignal,
                failedDay = mission.dueDay + campaignDayOffset, stakeAtFailure = mandatoryFailureStake, goals = failureGoals.Select(g => new CampaignStakeSnapshot { companyId = g.companyId, required = g.required, actual = g.actual }).ToList() };
            IsMandatoryFailureReady = true; deadlineTransition = null;
            BadEndingRequested?.Invoke(LastDeadlineFailure); onBadEndingRequested.Invoke(LastDeadlineFailure.endingSignalId);
            endingPresenter = new GameObject("Campaign ending").AddComponent<CampaignEndingPresenter>(); endingPresenter.Build(this, negotiationView);
            ReleaseDeadlineCurtainForEnding(); todayEventsChanged?.Invoke();
        }
        private CampaignEnding ResolveEnding()
        {
            string key = failedEndingSignal;
            if (key == "dongbang-trading-investment-deadline-failed") key = "early-a1-dongbang-stake-failed";
            if (key == "hansung-industry-investment-deadline-failed") key = "early-b1-hansung-stake-failed";
            return campaignPlan?.endings.FirstOrDefault(e => e.signal == key);
        }
        public void AdvanceEnding(bool skip)
        {
            if (mandatoryState != MandatoryStoryState.Failed || CurrentEnding == null) return;
            if (EventSceneTransition.IsBusy) return;
            if (skip && endingLineIndex < CurrentEnding.lines.Count)
            {
                int scene = CurrentEnding.lines[endingLineIndex].scene;
                do { endingLineIndex++; } while (endingLineIndex < CurrentEnding.lines.Count && CurrentEnding.lines[endingLineIndex].scene == scene);
            }
            else endingLineIndex = Mathf.Min(endingLineIndex + 1, CurrentEnding.lines.Count);
            endingPresenter?.Present(); todayEventsChanged?.Invoke();
        }
        public void ReleaseDeadlineCurtainForEnding()
        {
            if (IsMandatoryFailureReady && deadlineCurtain != null) { Destroy(deadlineCurtain.transform.parent.gameObject); deadlineCurtain = null; }
        }
        private void RestoreMandatoryPresentation()
        {
            EventSceneTransition.Cancel();
            if (deadlineTransition != null) StopCoroutine(deadlineTransition);
            deadlineTransition = null;
            if (endingPresenter != null) Destroy(endingPresenter.gameObject); endingPresenter = null;
            if (deadlineCurtain != null) Destroy(deadlineCurtain.transform.parent.gameObject);
            deadlineCurtain = null; IsMandatoryFailureReady = false; LastDeadlineFailure = null; lastMissionDisplay = null;
            if (mandatoryState == MandatoryStoryState.Failed) StartDeadlineFailure(true);
            else if (mandatoryState != MandatoryStoryState.Forced) EvaluateMandatoryDeadline(currentDay);
            EnsureMandatoryOffer(); MandatoryDeadlineChanged?.Invoke();
        }
    }
}
