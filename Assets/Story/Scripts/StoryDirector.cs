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
        [SerializeField] private StorySequence sequence;
        public static StoryDirector Active { get; private set; }
        public static bool IsRunning => Active != null && !Active.Completed;
        public static bool CanAdvanceTurn => !IsRunning || Active.applyingSkippedAction || Active.Goal == StoryGoal.EndDay && !Active.busy && Active.clock.CurrentTurn == Active.initialTurn;
        public static bool CanTrade(Company company, bool buy) => !IsRunning ||
            (Active.Goal == StoryGoal.BuyShares && !Active.busy || Active.applyingSkippedAction) && buy && company != null && company.definition.companyID == CompanyId &&
            (Active.applyingSkippedAction || company.state.getStat(CompanyStat.PLAYER_SHARES) == Active.initialShares);
        public static bool CanSelectCompany(Company company) => !IsRunning ||
            (Active.Goal == StoryGoal.SelectCompany || Active.Goal == StoryGoal.BuyShares) && (!Active.busy || Active.preparing) &&
            company != null && company.definition.companyID == CompanyId;
        public static bool CanNavigate(int tab) => !IsRunning || !Active.busy &&
            (Active.Goal == StoryGoal.SelectCompany && tab == 1 || Active.Goal == StoryGoal.EndDay && tab == 0);
        public bool Completed { get; private set; }
        public bool Busy => busy;
        public bool SkipEntireIntroOnStart { get; set; }
        public bool CanSkipToChoice => !busy && !Completed && Current != null &&
            !(Current.choices.Count > 0 && !choiceCommitted);
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
        private float introStartShares;
        private int introStartTurn;

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
            if (sequence == null || sequence.beats.Count == 0 || registry == null || clock == null || company == null || player == null || detail == null || browser == null || status == null)
            {
                Debug.LogError("도입부의 대본 또는 게임 연결이 누락되었습니다.", this);
                view.ShowError("도입부 연결을 확인할 수 없습니다. 메인 메뉴로 돌아가 다시 시작해 주세요.");
                yield break;
            }
            introStartShares = company.state.getStat(CompanyStat.PLAYER_SHARES);
            introStartTurn = clock.CurrentTurn;
            StartCoroutine(SkipEntireIntroOnStart ? SkipRoutine(true) : MoveNext());
        }

        public void Continue()
        {
            if (busy || Completed || Current == null || Time.unscaledTime < nextInputAt) return;
            nextInputAt = Time.unscaledTime + .18f;
            if (Goal != StoryGoal.None)
            {
                if ((Goal == StoryGoal.ViewRepresentative || Goal == StoryGoal.ViewMemory) && goalObserved)
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
                if (!entireIntro && i > index && beat.choices.Count > 0) { target = i; break; }
                if (!ApplySkippedGoal(beat)) { target = i; break; }
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
            applyingSkippedAction = true;
            try
            {
                if (beat.goal == StoryGoal.BuyShares && company.state.getStat(CompanyStat.PLAYER_SHARES) <= introStartShares)
                    return FindFirstObjectByType<ShareTradingSystem>().buyShares(company, 1);
                if (beat.goal == StoryGoal.EndDay && clock.CurrentTurn == introStartTurn) clock.AdvanceTurn();
                return true;
            }
            finally { applyingSkippedAction = false; }
        }

        private void CloseGamePanels()
        {
            if (detail != null && detail.gameObject.activeInHierarchy) detail.hide();
            if (status != null) status.Close();
            if (browser != null) browser.close();
        }

        private IEnumerator Finish()
        {
            Completed = true;
            var events = FindFirstObjectByType<EventSystemController>();
            if (events != null) events.refreshAvailability();
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
            var upcoming = index + 1 < sequence.beats.Count ? sequence.beats[index + 1] : null;
            bool sceneChange = previous == null || upcoming == null || previous.background != upcoming.background ||
                previous.sceneTitle != upcoming.sceneTitle || previous.goal != StoryGoal.None || upcoming.goal != StoryGoal.None;
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
            if (Goal == StoryGoal.SelectCompany || Goal == StoryGoal.BuyShares)
            {
                browser.open();
                FindFirstObjectByType<BrowserViewToggle>(FindObjectsInactive.Include)?.showMap();
                if (Goal == StoryGoal.BuyShares) detail.show(company);
            }
            if (Goal == StoryGoal.ViewRepresentative || Goal == StoryGoal.ViewMemory)
                OpenProfile();
            view.ShowObjective(Current.text, Goal == StoryGoal.ViewRepresentative || Goal == StoryGoal.ViewMemory);
        }

        public void OpenProfile()
        {
            if (Goal != StoryGoal.ViewRepresentative && Goal != StoryGoal.ViewMemory) return;
            status.Open(); status.SelectNpc(Goal == StoryGoal.ViewRepresentative ? Representative : FieldDirector);
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
                    view.Spotlight(detail.transform.Find("Window") as RectTransform ?? (RectTransform)detail.transform);
                    float price = company.state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE);
                    view.ObjectiveDetail($"현재 주가 {price:N2} · 보유 자금 {player.state.cash:N2}\n수량을 입력하고 매수하세요. 실패·취소 시 다시 시도할 수 있습니다.");
                    done = company.state.getStat(CompanyStat.PLAYER_SHARES) > initialShares && player.state.cash < initialCash;
                    if (!detail.gameObject.activeInHierarchy) detail.show(company);
                    break;
                case StoryGoal.ViewRepresentative:
                case StoryGoal.ViewMemory:
                    view.Spotlight(null);
                    string target = Goal == StoryGoal.ViewRepresentative ? Representative : FieldDirector;
                    goalObserved |= status.gameObject.activeInHierarchy && status.SelectedNpcId == target;
                    view.SetGoalReady(goalObserved);
                    if (Goal == StoryGoal.ViewMemory && registry.TryGet(target, out var state) && state.memories.Count > 0)
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
            return (text ?? "").Replace("{cash}", player != null ? player.state.cash.ToString("N2") : "")
                .Replace("{price}", company != null ? company.state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE).ToString("N2") : "")
                .Replace("{shares}", company != null ? company.state.getStat(CompanyStat.PLAYER_SHARES).ToString("N0") : "")
                .Replace("{turn}", clock != null ? clock.CurrentTurn.ToString() : "");
        }

        private void OnDestroy() { if (Active == this) Active = null; }
    }
}
