using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TakeOver.MainMenu;
using TakeOver.NPC;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TakeOver.Story.Editor
{
    [InitializeOnLoad]
    public static class StoryBoundaryVerification
    {
        private const string Key = "TakeOver.StoryBoundary";
        internal static string Output => Path.GetFullPath("../QA_OverallReview/boundary-results.txt");
        static StoryBoundaryVerification() { EditorApplication.playModeStateChanged += Changed; }
        public static void Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            File.WriteAllText(Output, "RUNNING\n");
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            EditorApplication.isPlaying = true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                var go = new GameObject("Boundary verification");
                Object.DontDestroyOnLoad(go); go.AddComponent<StoryBoundaryProbe>();
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                if (Application.isBatchMode) EditorApplication.Exit(File.ReadAllText(Output).Contains("FAIL") ? 1 : 0);
            }
        }
    }

    public sealed class StoryBoundaryProbe : MonoBehaviour
    {
        private float deadline;
        private void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            File.AppendAllText(StoryBoundaryVerification.Output, "PASS: " + message + "\n");
        }
        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                File.AppendAllText(StoryBoundaryVerification.Output, "FAIL runtime: " + message + "\n" + stack + "\n");
        }
        private void OnDestroy() { Application.logMessageReceived -= OnLog; }
        private void Update()
        {
            if (deadline > 0 && Time.realtimeSinceStartup > deadline)
            {
                File.AppendAllText(StoryBoundaryVerification.Output, "FAIL: Timeout\n");
                deadline = 0; EditorApplication.isPlaying = false;
            }
        }
        private IEnumerator Start()
        {
            deadline = Time.realtimeSinceStartup + 150;
            Application.logMessageReceived += OnLog;
            var run = Verify();
            while (true)
            {
                bool next; object value = null;
                try { next = run.MoveNext(); if (next) value = run.Current; }
                catch (Exception error) { File.AppendAllText(StoryBoundaryVerification.Output, "FAIL: " + error + "\n"); break; }
                if (!next) break;
                yield return value;
            }
            deadline = 0; EditorApplication.isPlaying = false;
        }
        private static IEnumerator Ready()
        {
            while (StoryDirector.Active == null || StoryDirector.Active.Current == null || StoryDirector.Active.Busy) yield return null;
            yield return new WaitForSecondsRealtime(.23f);
        }
        private IEnumerator Verify()
        {
            yield return null; yield return null;
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            menu.startNewGame(); menu.startNewGame();
            yield return Ready();
            Check(Object.FindObjectsByType<StoryDirector>(FindObjectsSortMode.None).Length == 1, "Rapid new-game calls create one director");
            var d = StoryDirector.Active;
            var seq = AssetDatabase.LoadAssetAtPath<StorySequence>("Assets/Story/Data/IntroSequence.asset");
            var first = seq.beats.FindIndex(b => b.choices.Count > 0);
            d.SkipToNextChoice(); yield return Ready();
            Check(d.BeatIndex == first, "Skip stops at first choice");
            d.Choose(0); d.Choose(1);
            Check(d.Decisions.Count == 1, "Rapid conflicting choices commit once");
            yield return new WaitForSecondsRealtime(.23f);

            // Force a real insufficient-funds failure; skipping must stop at the unresolved goal.
            var player = Object.FindFirstObjectByType<Player>();
            float cash = player.state.cash; player.state.addCash(-cash);
            d.SkipToNextChoice(); yield return Ready();
            Check(d.Goal == StoryGoal.BuyShares, "Insufficient funds stop skip at purchase goal");
            var company = Object.FindFirstObjectByType<Companies>().getCompany(StoryDirector.CompanyId);
            Check(company.state.getStat(CompanyStat.PLAYER_SHARES) == 0 && player.state.cash == 0, "Failed skip purchase preserves cash and shares");
            Check(d.Decisions.Count == 1, "Failed skip commits no later decisions");
            d.SkipToNextChoice(); yield return Ready();
            Check(d.Goal == StoryGoal.BuyShares && d.Decisions.Count == 1, "Repeated failed skip stays recoverable");
            player.state.addCash(cash);
            var trading = Object.FindFirstObjectByType<ShareTradingSystem>();
            Check(!trading.buyShares(company, 0) && !trading.buyShares(company, -1), "Invalid purchase quantities rejected");
            Check(trading.buyShares(company, 1), "Manual purchase succeeds after failure recovery");
            // Begin skip before the director's next Update sees the completed purchase.
            d.SkipToNextChoice(); yield return Ready();
            Check(d.Current.choices.Count > 0 && company.state.getStat(CompanyStat.PLAYER_SHARES) == 1, "Same-frame purchase then skip does not purchase twice");
            Check(d.Decisions.Count == 1, "Recovery still waits for second answer");
            d.Choose(1); yield return new WaitForSecondsRealtime(.23f);
            d.SkipToNextChoice(); yield return Ready();
            d.Choose(2); yield return new WaitForSecondsRealtime(.23f);

            // Walk the remaining script to test manual end-day followed by same-frame skip.
            while (d.Goal != StoryGoal.EndDay)
            {
                if (d.Goal == StoryGoal.ViewRepresentative || d.Goal == StoryGoal.ViewMemory)
                { d.OpenProfile(); yield return null; }
                d.Continue(); yield return Ready();
            }
            var clock = Object.FindFirstObjectByType<GameTurnClock>();
            int day = clock.CurrentTurn;
            clock.AdvanceTurn(); clock.AdvanceTurn();
            Check(clock.CurrentTurn == day + 1, "Repeated same-frame end day advances once during tutorial");
            d.SkipToNextChoice();
            while (StoryDirector.Active != null) yield return null;
            Check(clock.CurrentTurn == day + 1, "End-day then skip does not advance twice");
            var registry = Object.FindFirstObjectByType<NpcStateRegistry>();
            Check(registry.States.Sum(n => n.memories.Count) == 3, "Boundary recovery retains exactly three choice memories");
            Check(trading.buyShares(company, 1) && trading.sellShares(company, 1), "Trading releases after boundary completion");
            clock.AdvanceTurn(); Check(clock.CurrentTurn == day + 2, "Normal next day works after intro release");

            // Scene interruption must not leave the static intro guard locked.
            SceneManager.LoadScene("MainMenu"); yield return null; yield return null;
            Object.FindFirstObjectByType<MainMenuController>().startNewGame(); yield return Ready();
            SceneManager.LoadScene("Game"); yield return null; yield return null; yield return null;
            Check(!StoryDirector.IsRunning && StoryDirector.CanAdvanceTurn, "Reloading Game directly clears interrupted intro guards");
            File.AppendAllText(StoryBoundaryVerification.Output, "COMPLETE\n");
        }
    }
}
