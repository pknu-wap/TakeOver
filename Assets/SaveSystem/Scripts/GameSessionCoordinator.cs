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
        private GameSaveManager manager;
        private EventSystemController events;
        private GameTurnClock clock;
        private bool loading = true, failed, dirty;
        private int mode;
        private string retryRun, retryMission;
        private bool checkpointPending;
        public static GameSessionCoordinator Active { get; private set; }
        public bool Ready => !loading && !failed;
        public bool IsPersistenceBlocked => checkpointPending || manager != null && manager.hasPendingSave;
        public string LastError => manager != null ? manager.lastError : "저장 연결이 없습니다.";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            requestedMode = 0; requestedRun = requestedMission = null; Active = null;
            SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
        }
        public static void RequestNewGame() => requestedMode = 1;
        public static void RequestContinue() { requestedMode = 2; StoryLaunch.ShowLaunchCurtain(); }
        public static void RequestRetry(string run, string mission) { requestedMode = 3; requestedRun = run; requestedMission = mission; StoryLaunch.ShowLaunchCurtain(); }
        private static void Loaded(Scene scene, LoadSceneMode loadMode)
        {
            if (scene.name != "Game" || requestedMode == 0) return;
            var session = new GameObject("Game save session").AddComponent<GameSessionCoordinator>();
            session.mode = requestedMode; session.retryRun = requestedRun; session.retryMission = requestedMission; requestedMode = 0;
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
            if (mode == 2 && !manager.tryLoad(out _) || mode == 3 && !manager.TryLoadCampaignCheckpoint(retryRun, retryMission, out _))
            {
                failed = true; loading = false; StoryLaunch.DismissLaunchCurtain();
                var view = FindFirstObjectByType<EventNegotiationView>(FindObjectsInactive.Include);
                view.ShowDialogue(new DialoguePresentation { narration = true, showPlayer = false,
                    text = "저장을 불러오지 못했습니다.\n" + manager.lastError, choices = new[] { "확인" } },
                    null, _ => SceneManager.LoadScene("MainMenu"), null, () => false);
                view.SetDialogueInput(true); yield break;
            }
            loading = false; dirty = mode == 1 || mode == 3;
            if (events != null) events.todayEventsChanged += MarkDirty;
            if (clock != null) clock.DayStarted += DayStarted;
            StoryLaunch.DismissLaunchCurtain();
        }
        private void MarkDirty() => dirty = true;
        private void DayStarted(int day) => dirty = true;
        private void LateUpdate()
        {
            // Automated editor runs use isolated managers in their probes; never overwrite a player's save.
            if (Application.isBatchMode && string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY")) || !Ready || !dirty || StoryDirector.IsRunning || events == null || !events.IsClosed ||
                events.MandatoryState == MandatoryStoryState.Failed && !events.IsMandatoryFailureReady) return;
            dirty = false;
            if (manager.hasPendingSave) return;
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
            if (clock != null) clock.DayStarted -= DayStarted;
            if (Active == this) Active = null;
        }
    }
}
