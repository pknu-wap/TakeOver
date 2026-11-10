using System;
using System.Collections;
using System.Collections.Generic;
using TakeOver.Events;
using TakeOver.NPC;
using UnityEngine;

namespace TakeOver.Story
{
    [Serializable]
    public sealed class StoryDecision
    {
        public string beatId;
        public int choice;
        public string interpretation;
    }

    /// <summary>대본·화면과 기존 게임 조작의 완료 조건을 연결한다. 이벤트·관계 규칙은 공용 서비스를 사용한다.</summary>
    public sealed class StoryDirector : MonoBehaviour
    {
        public const string CompanyId = "mirae-construction";
        public const string Representative = "npc-mirae-construction-ceo-kang-junhyeok";
        public const string FieldDirector = "npc-mirae-construction-field-director-park-dosik";
        public const string TutorialEventId = "inside_info_01";
        public const string FinancialDirector = "npc-mirae-construction-cfo-seo-jia";
        [SerializeField] private StorySequence sequence;
        public static StoryDirector Active { get; private set; }
        public static bool IsRunning => Active != null && !Active.Completed;
        public static bool CanAdvanceTurn => !IsRunning || Active.applyingSkippedAction || Active.Goal == StoryGoal.EndDay && !Active.busy && Active.clock.CurrentTurn == Active.initialTurn;
        public static bool IsGuidingEvent => IsRunning && (Active.Goal == StoryGoal.OpenEvent || Active.Goal == StoryGoal.CompleteEvent);
        public static bool IsInspectingProfile => IsRunning && Active.IsProfile;
        public static bool CanOpenEvent(string id) => !IsRunning || id == TutorialEventId &&
            (IsGuidingEvent && (!Active.busy || Active.preparing) || Active.applyingSkippedAction);
        public static bool CanNegotiate(string id) => !IsRunning || id == TutorialEventId &&
            (IsGuidingEvent && !Active.busy || Active.applyingSkippedAction);
        private bool IsBuying => Goal == StoryGoal.BuyShares || Goal == StoryGoal.BuyMore;
        private bool IsTrading => IsBuying || Goal == StoryGoal.SellShares;
        private bool IsCompanyReading => Goal == StoryGoal.ViewCompany || Goal == StoryGoal.CompareCompany || Goal == StoryGoal.ViewStake;
        private bool IsProfile => Goal == StoryGoal.ViewRepresentative || Goal == StoryGoal.ViewMemory || Goal == StoryGoal.ViewEventMemory;
        private bool NeedsConfirmation => IsProfile || IsCompanyReading;
        private Company GoalCompany => string.IsNullOrEmpty(Current?.companyId) ? company : FindFirstObjectByType<Companies>()?.getCompany(Current.companyId);
        public static bool CanTrade(Company company, bool buy) => !IsRunning ||
            (Active.IsTrading && !Active.busy || Active.applyingSkippedAction) && buy == Active.IsBuying && company != null && company.definition.companyID == CompanyId &&
            (Active.applyingSkippedAction || company.state.getStat(CompanyStat.PLAYER_SHARES) == Active.initialShares);
        public static bool CanTradeAmount(Company company, bool buy, int amount) => CanTrade(company, buy) && (!IsRunning || amount == 1);
        public static bool CanSelectCompany(Company company) => !IsRunning ||
            (Active.Goal == StoryGoal.SelectCompany || Active.IsTrading || Active.IsCompanyReading) && (!Active.busy || Active.preparing) &&
            company != null && company == Active.GoalCompany;
        public static bool CanNavigate(int tab) => !IsRunning || !Active.busy &&
            ((Active.Goal == StoryGoal.SelectCompany || Active.IsCompanyReading) && (tab == 1 || tab == 2) ||
             (Active.Goal == StoryGoal.EndDay || IsGuidingEvent) && tab == 0 || Active.IsProfile && tab == 3);
        public bool Completed { get; private set; }
        public bool Busy => busy;
        public bool SkipEntireIntroOnStart { get; set; }
        public bool CanSkipToChoice => !busy && !Completed && Current != null &&
            Goal == StoryGoal.None && !(Current.choices.Count > 0 && !choiceCommitted);
        public int BeatIndex => index;
        public StoryBeat Current => sequence != null && index >= 0 && index < sequence.beats.Count ? sequence.beats[index] : null;
        public StoryGoal Goal => Current?.goal ?? StoryGoal.None;
        public IReadOnlyList<StoryDecision> Decisions => decisions;
        public StoryView View => view;
        private readonly List<StoryDecision> decisions = new List<StoryDecision>();
        private StoryView view;
        private NpcStateRegistry registry;
        private Company company;
        private Player player;
        private GameTurnClock clock;
        private CompanyDetailPanel detail;
        private CompanyBrowserScreen browser;
        private NpcStatusScreen status;
        private int index = -1, exchangeStage;
        private bool busy, choiceCommitted, preparing;
        private float nextInputAt;
        private StoryChoice selectedChoice;
        private float initialShares, initialCash;
        private int initialTurn;
        private readonly string occurrence = Guid.NewGuid().ToString("N");
        private bool goalObserved;
        private bool applyingSkippedAction;
        private EventSystemController eventController;
        private ActionPointService actionPoints;
        private readonly HashSet<string> completedGoals = new HashSet<string>();
        private int beforeEventAp;
        private float beforeEventCash;
        private NpcRelationState beforeEventRelation;
        private bool hasEventSnapshot;
        private float lastTradeCashBefore, lastTradeSharesBefore;

        private void Awake()
        {
            if (Active != null) { Destroy(gameObject); return; }
            Active = this;
            view = gameObject.AddComponent<StoryView>();
            view.Build(sequence, this);
        }

        private IEnumerator Start()
        {
            // 기존 모든 Awake·Start(최초 주가 계산 포함)가 끝난 다음 대본을 시작한다.
            yield return null; yield return null;
            registry = FindFirstObjectByType<NpcStateRegistry>();
            clock = FindFirstObjectByType<GameTurnClock>();
            player = FindFirstObjectByType<Player>();
            company = FindFirstObjectByType<Companies>()?.getCompany(CompanyId);
            detail = FindFirstObjectByType<CompanyDetailPanel>(FindObjectsInactive.Include);
            browser = FindFirstObjectByType<CompanyBrowserScreen>(FindObjectsInactive.Include);
            status = FindFirstObjectByType<NpcStatusScreen>(FindObjectsInactive.Include);
            eventController = FindFirstObjectByType<EventSystemController>();
            actionPoints = FindFirstObjectByType<ActionPointService>();
            if (sequence == null || sequence.beats.Count == 0 || registry == null || clock == null || company == null || player == null || detail == null || browser == null || status == null || eventController == null || actionPoints == null)
            {
                Debug.LogError("도입부의 대본 또는 게임 연결이 누락되었습니다.", this);
                view.ShowError("도입부 연결을 확인할 수 없습니다. 메인 메뉴로 돌아가 다시 시작해 주세요.");
                yield break;
            }
            StartCoroutine(SkipEntireIntroOnStart ? SkipRoutine(true) : MoveNext());
        }

        public void Continue()
        {
            if (busy || Completed || Current == null || Time.unscaledTime < nextInputAt) return;
            nextInputAt = Time.unscaledTime + .18f;
            if (Goal != StoryGoal.None)
            {
                if (NeedsConfirmation && goalObserved)
                    StartCoroutine(MoveNext());
                return;
            }
            if (exchangeStage == 1)
            {
                exchangeStage = 2;
                view.ShowExchange(Current, selectedChoice.response, false, registry);
                return;
            }
            if (Current.choices.Count > 0 && !choiceCommitted) return;
            StartCoroutine(MoveNext());
        }

        public void Choose(int choice)
        {
            if (busy || Current == null || choiceCommitted || choice < 0 || choice >= Current.choices.Count) return;
            choiceCommitted = true;
            nextInputAt = Time.unscaledTime + .18f;
            CommitChoice(choice);
            exchangeStage = 1;
            view.ShowExchange(Current, selectedChoice.playerLine, true, registry);
            view.SetSkipAvailable(true);
        }

        private void CommitChoice(int choice)
        {
            selectedChoice = Current.choices[choice];
            decisions.Add(new StoryDecision { beatId = Current.id, choice = choice, interpretation = selectedChoice.interpretation });
            if (!string.IsNullOrEmpty(selectedChoice.targetNpcId))
                FindFirstObjectByType<NpcEventChannel>().Publish(new NpcEventPayload
                {
                    eventId = "intro-" + occurrence + "-" + Current.id,
                    companyId = CompanyId, actorId = "player", actionId = "intro-" + Current.id,
                    targetNpcIds = new List<string> { selectedChoice.targetNpcId },
                    memoryType = selectedChoice.memoryType, interpretation = selectedChoice.interpretation,
                    relationDelta = selectedChoice.relationDelta, durationTurns = 12,
                    publicity = NpcPublicity.Company, tags = new List<string> { "intro", Current.id }
                });
        }

        public void SkipToNextChoice()
        {
            if (!CanSkipToChoice || Time.unscaledTime < nextInputAt) return;
            StartCoroutine(SkipRoutine(false));
        }

        private IEnumerator SkipRoutine(bool entireIntro)
        {
            if (busy) yield break;
            busy = true;
            view.SetInput(false);
            yield return view.FadeTo(1, sequence.fadeSeconds);
            CloseGamePanels();
            // The current choice was already applied by Choose. Never replay it while skipping its reply.
            int first = entireIntro ? 0 : index;
            int target = sequence.beats.Count;
            for (int i = first; i < sequence.beats.Count; i++)
            {
                var beat = sequence.beats[i];
                if (!entireIntro && i > index && (beat.choices.Count > 0 || beat.goal != StoryGoal.None)) { target = i; break; }
                if (entireIntro) index = i;
                if (entireIntro && !ApplySkippedGoal(beat)) { target = i; break; }
                if (entireIntro && beat.choices.Count > 0)
                {
                    index = i;
                    CommitChoice(Mathf.Clamp(beat.skipDefaultChoice, 0, beat.choices.Count - 1));
                }
            }
            index = target;
            exchangeStage = 0; choiceCommitted = false; selectedChoice = null; goalObserved = false;
            if (target >= sequence.beats.Count) { yield return Finish(); yield break; }
            view.Show(Current, registry);
            if (Goal != StoryGoal.None) { preparing = true; PrepareGoal(); preparing = false; }
            yield return view.FadeTo(0, sequence.fadeSeconds);
            busy = false; nextInputAt = Time.unscaledTime + .18f;
            view.SetInput(true);
        }

        private bool ApplySkippedGoal(StoryBeat beat)
        {
            if (beat.goal == StoryGoal.None || completedGoals.Contains(beat.id)) return true;
            applyingSkippedAction = true;
            try
            {
                bool success = true;
                if (IsTrading)
                {
                    lastTradeCashBefore = player.state.cash;
                    lastTradeSharesBefore = company.state.getStat(CompanyStat.PLAYER_SHARES);
                    var trading = FindFirstObjectByType<ShareTradingSystem>();
                    success = IsBuying ? trading.buyShares(company, 1) : trading.sellShares(company, 1);
                }
                if (beat.goal == StoryGoal.EndDay) clock.AdvanceTurn();
                if (beat.goal == StoryGoal.CompleteEvent && !eventController.tryGetChoice(TutorialEventId, out _))
                {
                    SnapshotEvent();
                    eventController.openEvent(TutorialEventId);
                    eventController.startNegotiation();
                    eventController.confirmChoice(0);
                    eventController.closeResult();
                    success = eventController.tryGetChoice(TutorialEventId, out _);
                }
                if (!success) return false;
                completedGoals.Add(beat.id);
                return true;
            }
            finally { applyingSkippedAction = false; }
        }

        private void CloseGamePanels()
        {
            if (eventController != null && eventController.IsDetailOpen) eventController.closeDetail();
            if (detail != null && detail.gameObject.activeInHierarchy) detail.hide();
            if (status != null) status.Close();
            if (browser != null) browser.close();
        }

        private IEnumerator Finish()
        {
            Completed = true;
            var events = FindFirstObjectByType<EventSystemController>();
            if (events != null) events.refreshAvailability();
            FindFirstObjectByType<DashboardCardSlide>()?.RefreshCards();
            view.HideContent();
            yield return view.FadeTo(0, sequence.fadeSeconds);
            Destroy(gameObject);
        }

        private IEnumerator MoveNext()
        {
            if (busy) yield break;
            busy = true;
            view.SetInput(false);
            var previous = Current;
            if (previous != null && previous.goal != StoryGoal.None) completedGoals.Add(previous.id);
            var upcoming = index + 1 < sequence.beats.Count ? sequence.beats[index + 1] : null;
            // Opening the card hands over to the native event flow without closing or repainting it.
            if (previous?.goal == StoryGoal.OpenEvent && upcoming?.goal == StoryGoal.CompleteEvent)
            {
                index++;
                exchangeStage = 0; choiceCommitted = false; selectedChoice = null; goalObserved = false;
                view.Show(Current, registry);
                busy = false;
                view.SetInput(true);
                yield break;
            }
            bool sceneChange = previous == null || upcoming == null || previous.background != upcoming.background ||
                previous.sceneTitle != upcoming.sceneTitle || previous.goal != StoryGoal.None || upcoming.goal != StoryGoal.None ||
                previous.speaker == StorySpeaker.System || upcoming.speaker == StorySpeaker.System;
            if (sceneChange) yield return view.FadeTo(1, sequence.fadeSeconds);
            else yield return view.FadeDialogue(0, .12f);
            CloseGamePanels();
            index++;
            exchangeStage = 0; choiceCommitted = false; selectedChoice = null; goalObserved = false;
            if (index >= sequence.beats.Count)
            {
                yield return Finish();
                yield break;
            }
            view.Show(Current, registry);
            if (Goal != StoryGoal.None) { preparing = true; PrepareGoal(); preparing = false; }
            if (sceneChange) yield return view.FadeTo(0, sequence.fadeSeconds);
            else yield return view.FadeDialogue(1, .12f);
            busy = false;
            view.SetInput(true);
        }

        private void PrepareGoal()
        {
            initialShares = company.state.getStat(CompanyStat.PLAYER_SHARES);
            initialCash = player.state.cash;
            initialTurn = clock.CurrentTurn;
            if (Goal == StoryGoal.SelectCompany || IsTrading || IsCompanyReading)
            {
                detail.SetTutorialLayout(true);
                browser.open();
                FindFirstObjectByType<BrowserViewToggle>(FindObjectsInactive.Include)?.showMap();
                if (IsTrading || IsCompanyReading) detail.show(GoalCompany);
            }
            if (IsProfile) OpenProfile();
            if (IsGuidingEvent)
            {
                browser.close();
                eventController.refreshAvailability();
                FindFirstObjectByType<DashboardCardSlide>()?.RefreshCards();
                if (Goal == StoryGoal.OpenEvent) SnapshotEvent();
            }
            view.ShowObjective(Current.text, NeedsConfirmation, IsProfile, IsGuidingEvent);
        }

        private void SnapshotEvent()
        {
            beforeEventAp = actionPoints.Current;
            beforeEventCash = player.state.cash;
            if (registry.TryGet(FinancialDirector, out var state))
            {
                beforeEventRelation = state.relation;
                hasEventSnapshot = true;
            }
        }

        public void OpenProfile()
        {
            if (!IsProfile) return;
            status.Open(); status.SelectNpc(Goal == StoryGoal.ViewRepresentative ? Representative : Goal == StoryGoal.ViewMemory ? FieldDirector : FinancialDirector);
        }

        private void Update()
        {
            if (busy || Completed || Current == null || Goal == StoryGoal.None || company == null) return;
            bool done = false;
            switch (Goal)
            {
                case StoryGoal.SelectCompany:
                    view.SpotlightCompany();
                    done = detail.gameObject.activeInHierarchy && detail.current == company;
                    break;
                case StoryGoal.BuyShares:
                case StoryGoal.BuyMore:
                case StoryGoal.SellShares:
                    view.Spotlight(detail.transform.Find("Window") as RectTransform ?? (RectTransform)detail.transform);
                    float price = company.state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE);
                    int quantity = detail.EnteredQuantity;
                    float afterShares = initialShares + (IsBuying ? quantity : -quantity);
                    float afterCash = initialCash + (IsBuying ? -price * quantity : price * quantity);
                    view.ObjectiveDetail($"현재 주가 {price:N0} · 내 자금 {player.state.cash:N0}\n수량 {quantity}주 · 예상 {(IsBuying ? "매수 대금" : "매도 대금")} {price * quantity:N0}\n거래 후 자금 {afterCash:N0} · {afterShares:N0}주 ({afterShares / company.definition.totalShares * 100:0.##}%) / 이번에는 1주를 입력하세요.");
                    done = IsBuying ? company.state.getStat(CompanyStat.PLAYER_SHARES) > initialShares && player.state.cash < initialCash :
                        company.state.getStat(CompanyStat.PLAYER_SHARES) < initialShares && player.state.cash > initialCash;
                    if (done) { lastTradeCashBefore = initialCash; lastTradeSharesBefore = initialShares; }
                    if (!detail.gameObject.activeInHierarchy) detail.show(company);
                    break;
                case StoryGoal.ViewCompany:
                case StoryGoal.CompareCompany:
                case StoryGoal.ViewStake:
                    if (!detail.gameObject.activeInHierarchy) detail.show(GoalCompany);
                    view.Spotlight(detail.transform.Find("Window") as RectTransform ?? (RectTransform)detail.transform);
                    goalObserved |= detail.gameObject.activeInHierarchy && detail.current == GoalCompany;
                    view.SetGoalReady(goalObserved);
                    var targetCompany = GoalCompany;
                    view.ObjectiveDetail(Goal == StoryGoal.ViewStake ? $"{targetCompany.definition.companyName} · {targetCompany.state.getStat(CompanyStat.PLAYER_SHARES):N0}주 / 전체 {targetCompany.definition.totalShares:N0}주 · 지분 {targetCompany.state.getDerivedStat(CompanyDerivedStat.STAKE):0.##}%\n지분 조건을 잃으면 관련 이벤트가 닫힐 수 있습니다." :
                        $"{targetCompany.definition.companyName} · 매출 {targetCompany.state.getStat(CompanyStat.REVENUE):N0} · 순이익 {targetCompany.state.getDerivedStat(CompanyDerivedStat.NET_PROFIT):N0}\n회사 현금 {targetCompany.state.getStat(CompanyStat.CASH):N0} · 부채비율 {targetCompany.state.getDerivedStat(CompanyDerivedStat.DEBT_RATIO):P1} · 내 자금 {player.state.cash:N0}");
                    break;
                case StoryGoal.OpenEvent:
                    done = !eventController.IsClosed && eventController.ActiveEventId == TutorialEventId;
                    break;
                case StoryGoal.CompleteEvent:
                    done = eventController.tryGetChoice(TutorialEventId, out _) && eventController.IsClosed;
                    break;
                case StoryGoal.ViewRepresentative:
                case StoryGoal.ViewMemory:
                case StoryGoal.ViewEventMemory:
                    view.Spotlight(null);
                    string target = Goal == StoryGoal.ViewRepresentative ? Representative : Goal == StoryGoal.ViewMemory ? FieldDirector : FinancialDirector;
                    goalObserved |= status.gameObject.activeInHierarchy && status.SelectedNpcId == target;
                    view.SetGoalReady(goalObserved);
                    if (Goal != StoryGoal.ViewRepresentative && registry.TryGet(target, out var state) && state.memories.Count > 0)
                        view.ObjectiveDetail("최근 기억: " + state.memories[state.memories.Count - 1].interpretation);
                    break;
                case StoryGoal.EndDay:
                    view.SpotlightDayEnd();
                    done = clock.CurrentTurn == initialTurn + 1;
                    break;
            }
            if (done) StartCoroutine(MoveNext());
        }

        public string Resolve(string text)
        {
            return (text ?? "").Replace("{cash}", player != null ? player.state.cash.ToString("N0") : "")
                .Replace("{price}", company != null ? company.state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE).ToString("N0") : "")
                .Replace("{shares}", company != null ? company.state.getStat(CompanyStat.PLAYER_SHARES).ToString("N0") : "")
                .Replace("{turn}", clock != null ? clock.CurrentTurn.ToString() : "")
                .Replace("{ap}", actionPoints != null ? actionPoints.Current.ToString() : "")
                .Replace("{maxAp}", actionPoints != null ? actionPoints.Max.ToString() : "")
                .Replace("{apCost}", TutorialApCost.ToString())
                .Replace("{eventDays}", TutorialEventRemainingDays.ToString())
                .Replace("{eventResult}", EventResultSummary())
                .Replace("{tradeResult}", TradeResultSummary())
                .Replace("{stake}", company != null ? company.state.getDerivedStat(CompanyDerivedStat.STAKE).ToString("0.##") : "");
        }

        private int TutorialApCost
        {
            get
            {
                foreach (var info in eventController.getTodayEventInfos())
                    if (info.definition.getId() == TutorialEventId) return info.definition.getApCost();
                return beforeEventAp - actionPoints.Current;
            }
        }

        private int TutorialEventRemainingDays
        {
            get
            {
                foreach (var info in eventController.getTodayEventInfos())
                    if (info.definition.getId() == TutorialEventId) return info.remainingDays;
                return 0;
            }
        }

        private string EventResultSummary()
        {
            if (!hasEventSnapshot || !registry.TryGet(FinancialDirector, out var state)) return "";
            var a = beforeEventRelation; var b = state.relation;
            return $"면담 결과를 반영하였습니다.\n행동 포인트 {beforeEventAp} → {actionPoints.Current} · 자금 {beforeEventCash:N0} → {player.state.cash:N0}\n" +
                $"신뢰 {a.trust:0.#} → {b.trust:0.#} · 존중 {a.respect:0.#} → {b.respect:0.#} · 두려움 {a.fear:0.#} → {b.fear:0.#}\n" +
                $"적대감 {a.hostility:0.#} → {b.hostility:0.#} · 의존도 {a.dependency:0.#} → {b.dependency:0.#} · 관심 {a.interest:0.#} → {b.interest:0.#}";
        }

        private string TradeResultSummary()
        {
            if (company == null || player == null) return "";
            float shares = company.state.getStat(CompanyStat.PLAYER_SHARES);
            float quantity = Mathf.Abs(shares - lastTradeSharesBefore);
            string action = shares >= lastTradeSharesBefore ? "매수" : "매도";
            return $"{company.definition.companyName} 주식 {quantity:N0}주를 {action}하였습니다. 현재 보유 주식은 {shares:N0}주이며, 지분율은 {company.state.getDerivedStat(CompanyDerivedStat.STAKE):0.##}%입니다.\n남은 자금은 {player.state.cash:N0}원입니다.";
        }

        private void OnDestroy() { if (Active == this) Active = null; }
    }
}
