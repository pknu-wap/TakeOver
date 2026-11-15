using System;
using System.Collections;
using TakeOver.NPC;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TakeOver.Events
{
    public enum MandatoryStoryState { Pending, Forced, Failed, Completed }

    [Serializable]
    public sealed class StoryDeadlineFailureInfo
    {
        public string eventId;
        public string companyId;
        public string endingSignalId;
        public int failedDay;
        public float stakeAtFailure;
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
        public MandatoryStoryState MandatoryState => selectedChoices.ContainsKey(MandatoryEventId) ? MandatoryStoryState.Completed : mandatoryState;
        public int MandatoryDaysRemaining => Mathf.Max(0, MandatoryFailureDay - (turnClock != null ? turnClock.CurrentTurn : currentDay));
        public bool IsMandatoryFailureReady { get; private set; }
        public StoryDeadlineFailureInfo LastDeadlineFailure { get; private set; }
        private MandatoryStoryState mandatoryState;
        private float mandatoryFailureStake;
        private Image deadlineCurtain;
        private Coroutine deadlineTransition;
        private float lastMandatoryShares = float.NaN;

        private bool IsMandatory(EventDefinition definition) => definition.getId() == MandatoryEventId;
        private int OccurrenceExpiry(EventDefinition definition, int firstDay) =>
            IsMandatory(definition) ? Mathf.Max(MandatoryFailureDay + 1, firstDay + 1) : firstDay + definition.getValidDays();
        private bool ShowMandatoryOccurrence(EventDefinition definition) => mandatoryState != MandatoryStoryState.Failed &&
            (mandatoryState != MandatoryStoryState.Forced || IsMandatory(definition));
        private float MandatoryStake()
        {
            var company = FindFirstObjectByType<Companies>()?.getCompany(MandatoryCompanyId);
            return company != null && company.state.totalShares > 0
                ? company.state.getBaseStat(CompanyStat.PLAYER_SHARES) * 100f / company.state.totalShares : 0;
        }
        private void Update()
        {
            if (mandatoryState == MandatoryStoryState.Failed || selectedChoices.ContainsKey(MandatoryEventId)) return;
            float shares = MandatoryStake();
            if (shares == lastMandatoryShares) return;
            lastMandatoryShares = shares;
            if (currentDay > 0) { EnsureMandatoryOffer(); refreshAvailability(); }
            MandatoryDeadlineChanged?.Invoke();
        }
        private bool EnsureMandatoryOffer()
        {
            if (mandatoryState == MandatoryStoryState.Failed || selectedChoices.ContainsKey(MandatoryEventId)) return false;
            var definition = events.Find(IsMandatory);
            if (definition == null || !meetsConditions(definition, MandatoryCompanyId) || occurrences.Exists(item => item.definition == definition)) return false;
            occurrences.Add(new Occurrence { definition = definition, companyId = MandatoryCompanyId, firstDay = Mathf.Max(1, currentDay) });
            return true;
        }
        private void OnMandatoryDayStarted(int day)
        {
            EvaluateMandatoryDeadline(day);
        }
        private void EvaluateMandatoryDeadline(int day)
        {
            if (selectedChoices.ContainsKey(MandatoryEventId))
            {
                mandatoryState = MandatoryStoryState.Completed;
                MandatoryDeadlineChanged?.Invoke();
                return;
            }
            if (mandatoryState == MandatoryStoryState.Failed || day < MandatoryFailureDay) return;
            if (mandatoryState == MandatoryStoryState.Forced) return;
            float stake = MandatoryStake();
            if (float.IsNaN(stake) || float.IsInfinity(stake) || stake < 10)
            {
                mandatoryFailureStake = float.IsNaN(stake) || float.IsInfinity(stake) ? 0 : stake;
                mandatoryState = MandatoryStoryState.Failed;
                InterruptForMandatoryDeadline();
                refreshAvailability();
                StartDeadlineFailure(false);
            }
            else
            {
                mandatoryState = MandatoryStoryState.Forced;
                InterruptForMandatoryDeadline();
                EnsureMandatoryOffer();
                refreshAvailability();
            }
            MandatoryDeadlineChanged?.Invoke();
        }
        private void InterruptForMandatoryDeadline()
        {
            detailView.hide(); negotiationView.hide();
            activeInfo = null; negotiationChoices.Clear(); screenState = ScreenState.Closed;
        }
        private void CompleteMandatoryPresentation()
        {
            if (mandatoryState == MandatoryStoryState.Completed) return;
            if (!selectedChoices.ContainsKey(MandatoryEventId)) return;
            mandatoryState = MandatoryStoryState.Completed;
            refreshAvailability();
            MandatoryDeadlineChanged?.Invoke();
        }
        public bool CanAdvanceTurn(int day) => mandatoryState != MandatoryStoryState.Forced && mandatoryState != MandatoryStoryState.Failed;
        public void OnTurnAdvanceBlocked()
        {
            if (mandatoryState == MandatoryStoryState.Forced)
                FindFirstObjectByType<FloatingMessage>()?.Show("미래건설의 필수 이벤트를 먼저 완료해 주십시오.");
        }
        private void StartDeadlineFailure(bool restored)
        {
            if (deadlineTransition != null) StopCoroutine(deadlineTransition);
            if (deadlineCurtain != null) Destroy(deadlineCurtain.transform.parent.gameObject);
            IsMandatoryFailureReady = false;
            LastDeadlineFailure = null;
            var overlay = new GameObject("Story deadline transition", typeof(Canvas), typeof(GraphicRaycaster));
            overlay.transform.SetParent(transform, false);
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32766;
            var black = new GameObject("Deadline blackout", typeof(RectTransform), typeof(Image));
            black.transform.SetParent(overlay.transform, false);
            deadlineCurtain = black.GetComponent<Image>(); deadlineCurtain.raycastTarget = true;
            var rect = deadlineCurtain.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            deadlineCurtain.color = new Color(0, 0, 0, restored ? 1 : 0);
            deadlineTransition = StartCoroutine(FadeDeadlineFailure(restored));
        }
        private IEnumerator FadeDeadlineFailure(bool restored)
        {
            if (!restored)
            {
                const float duration = .65f;
                for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                {
                    deadlineCurtain.color = new Color(0, 0, 0, Mathf.SmoothStep(0, 1, elapsed / duration));
                    yield return null;
                }
            }
            else yield return null;
            deadlineCurtain.color = Color.black;
            LastDeadlineFailure = new StoryDeadlineFailureInfo
            {
                eventId = MandatoryEventId, companyId = MandatoryCompanyId, endingSignalId = MandatoryEndingSignal,
                failedDay = MandatoryFailureDay, stakeAtFailure = mandatoryFailureStake
            };
            IsMandatoryFailureReady = true;
            deadlineTransition = null;
            BadEndingRequested?.Invoke(LastDeadlineFailure);
            onBadEndingRequested.Invoke(MandatoryEndingSignal);
        }
        /// <summary>실제 엔딩 화면이 준비된 다음 연결 담당자가 호출한다. 일반 게임 진행 잠금은 유지한다.</summary>
        public void ReleaseDeadlineCurtainForEnding()
        {
            if (IsMandatoryFailureReady && deadlineCurtain != null)
            {
                Destroy(deadlineCurtain.transform.parent.gameObject);
                deadlineCurtain = null;
            }
        }
        private void RestoreMandatoryPresentation()
        {
            if (deadlineTransition != null) StopCoroutine(deadlineTransition);
            deadlineTransition = null;
            if (deadlineCurtain != null) Destroy(deadlineCurtain.transform.parent.gameObject);
            deadlineCurtain = null; IsMandatoryFailureReady = false; LastDeadlineFailure = null;
            lastMandatoryShares = float.NaN;
            if (mandatoryState == MandatoryStoryState.Failed) StartDeadlineFailure(true);
            else
            {
                EvaluateMandatoryDeadline(currentDay);
                EnsureMandatoryOffer();
            }
            MandatoryDeadlineChanged?.Invoke();
        }
    }
}
