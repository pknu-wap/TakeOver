using System.Collections;
using TakeOver.Events;
using TakeOver.NPC;
using TakeOver.Story;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TakeOver.SaveSystem
{
    public sealed class GameSessionCoordinator : MonoBehaviour, ITurnAdvanceGuard
    {
        private static int requestedMode;
        private static string requestedRun, requestedMission;
        private static int requestedSlot;
        private int manualSlot;
        private GameSaveManager manager;
        private EventSystemController events;
        private GameTurnClock clock;
        private TakeOver.Employees.EmployeeService employees;
        private TakeOver.Documents.DocumentInbox documents;
        private bool loading = true, failed, dirty;
        private int mode;
        private string retryRun, retryMission;
        private bool checkpointPending;
        public static GameSessionCoordinator Active { get; private set; }
        public bool Ready => !loading && !failed;
        internal bool IsRestoring => loading && mode != 1;
        public bool IsPersistenceBlocked => checkpointPending || manager != null && manager.hasPendingSave;
        public string LastError => manager != null ? manager.lastError : "저장 연결이 없습니다.";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            requestedMode = 0; requestedSlot = 0; requestedRun = requestedMission = null; Active = null;
            SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
        }
        public static void RequestNewGame() => requestedMode = 1;
        public static void RequestContinue() { requestedMode = 2; StoryLaunch.ShowLaunchCurtain(); }
        public static void RequestManualLoad(int slot) { requestedMode = 4; requestedSlot = slot; StoryLaunch.ShowLaunchCurtain(); }
        public static void RequestRetry(string run, string mission) { requestedMode = 3; requestedRun = run; requestedMission = mission; StoryLaunch.ShowLaunchCurtain(); }
        private static void Loaded(Scene scene, LoadSceneMode loadMode)
        {
            if (scene.name != "Game" || requestedMode == 0) return;
            var session = new GameObject("Game save session").AddComponent<GameSessionCoordinator>();
            session.mode = requestedMode; session.retryRun = requestedRun; session.retryMission = requestedMission; requestedMode = 0;
            session.manualSlot = requestedSlot;
        }
        private void Awake()
        {
            Active = this;
            manager = FindFirstObjectByType<GameSaveManager>();
            if (manager == null) manager = gameObject.AddComponent<GameSaveManager>();
        }
        private IEnumerator Start()
        {
            yield return null; yield return null; yield return null;
            manager = FindFirstObjectByType<GameSaveManager>();
            if (manager == null) manager = gameObject.AddComponent<GameSaveManager>();
            clock = FindFirstObjectByType<GameTurnClock>(); events = FindFirstObjectByType<EventSystemController>();
            if (mode == 2 && !manager.tryLoad(out _) || mode == 3 && !manager.TryLoadCampaignCheckpoint(retryRun, retryMission, out _) ||
                mode == 4 && !manager.TryLoadManualSlot(manualSlot, out _))
            {
                failed = true; loading = false; StoryLaunch.DismissLaunchCurtain();
                var view = FindFirstObjectByType<EventNegotiationView>(FindObjectsInactive.Include);
                view.ShowDialogue(new DialoguePresentation { narration = true, showPlayer = false,
                    text = "저장을 불러오지 못했습니다.\n" + manager.lastError, choices = new[] { "확인" } },
                    null, _ => SceneManager.LoadScene("MainMenu"), null, () => false);
                view.SetDialogueInput(true); yield break;
            }
            if (mode == 1 && StoryLaunch.ShowNamePrompt(FinishLoading)) yield break;
            FinishLoading();
        }

        private void FinishLoading()
        {
            loading = false; dirty = mode == 1 || mode == 3 || mode == 4;
            if (events != null) events.todayEventsChanged += MarkDirty;
            employees = FindFirstObjectByType<TakeOver.Employees.EmployeeService>();
            if (employees != null) employees.Changed += MarkDirty;
            documents = FindFirstObjectByType<TakeOver.Documents.DocumentSystem>()?.Inbox;
            if (documents != null) documents.Changed += MarkDirty;
            StoryLaunch.DismissLaunchCurtain();
        }
        private void MarkDirty() => dirty = true;
        public bool CanUseManualSaves => Ready && !IsPersistenceBlocked && !StoryDirector.IsRunning &&
            events != null && events.IsClosed && !EventSceneTransition.IsBusy &&
            !(FindFirstObjectByType<TakeOver.Startups.StartupContractService>()?.IsBlocking(out _) ?? false);
        public bool SaveManualSlot(int slot) => CanUseManualSaves &&
            manager.TrySaveManualSlot(slot, new DayStartData(clock.CurrentTurn, FixedDaySeed.seed));
        private static bool SavingEnabled => !Application.isBatchMode ||
            !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY"));

        internal bool SaveBeforeEvent()
        {
            if (!SavingEnabled || StoryDirector.IsRunning) return true;
            return Ready && !IsPersistenceBlocked &&
                manager.TrySaveBeforeEvent(new DayStartData(clock.CurrentTurn, FixedDaySeed.seed));
        }

        internal void SaveSettledState()
        {
            dirty = true;
            WriteIfReady();
        }

        private void LateUpdate() => WriteIfReady();

        private void WriteIfReady()
        {
            // Automated editor runs use isolated managers in their probes; never overwrite a player's save.
            if (!SavingEnabled || !Ready || !dirty || StoryDirector.IsRunning || events == null || !events.IsClosed ||
                events.MandatoryState == MandatoryStoryState.Failed && !events.IsMandatoryFailureReady) return;
            if (manager.hasPendingSave) return;
            dirty = false;
            if (!manager.trySaveDayEnd(new DayStartData(clock.CurrentTurn, FixedDaySeed.seed)))
                FindFirstObjectByType<FloatingMessage>()?.Show("자동 저장에 실패했습니다. 하루 마감에서 다시 시도합니다.");
            else
            {
                checkpointPending = !manager.TryWriteCampaignCheckpoint(events);
                if (checkpointPending) FindFirstObjectByType<FloatingMessage>()?.Show("재도전 준비 저장에 실패했습니다. 하루 마감에서 다시 시도합니다.");
            }
        }
        public bool CanAdvanceTurn(int currentTurn) => Ready && !checkpointPending && (manager == null || !manager.hasPendingSave);
        public void OnTurnAdvanceBlocked()
        {
            if (checkpointPending)
            {
                checkpointPending = !manager.TryWriteCampaignCheckpoint(events);
                FindFirstObjectByType<FloatingMessage>()?.Show(checkpointPending ? "재도전 준비 저장에 실패했습니다. 저장 공간을 확인해 주십시오." : "준비 저장을 완료했습니다. 다시 하루를 마감할 수 있습니다.");
                return;
            }
            if (manager != null && manager.hasPendingSave)
            {
                bool saved = manager.tryRetrySave(out _);
                if (saved) checkpointPending = !manager.TryWriteCampaignCheckpoint(events);
                FindFirstObjectByType<FloatingMessage>()?.Show(saved ? "저장을 완료했습니다. 다시 하루를 마감할 수 있습니다." : "저장 재시도에 실패했습니다. 저장 공간을 확인해 주십시오.");
            }
        }
        private void OnDestroy()
        {
            if (events != null) events.todayEventsChanged -= MarkDirty;
            if (employees != null) employees.Changed -= MarkDirty;
            if (documents != null) documents.Changed -= MarkDirty;
            if (Active == this) Active = null;
        }
    }
}
