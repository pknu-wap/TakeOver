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
        internal static string Output => Path.GetFullPath("../QA_ContextUI/boundary-results.txt");
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
            while (StoryDirector.Active != null && StoryDirector.Active.Busy) yield return null;
        }
        private static IEnumerator AdvanceTo(StoryGoal target)
        {
            while (true)
            {
                yield return Ready();
                var d = StoryDirector.Active;
                if (d.Goal == target) yield break;
                if (d.Current.choices.Count > 0)
                {
                    d.Choose(0); d.Choose(1);
                    yield return new WaitForSecondsRealtime(.24f);
                    d.SkipToNextChoice();
                }
                else if (d.Goal == StoryGoal.ViewRepresentative || d.Goal == StoryGoal.ViewMemory)
                    d.Continue();
                else d.SkipToNextChoice();
                yield return null;
            }
        }

        private IEnumerator Verify()
        {
            yield return null; yield return null;
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            menu.startNewGame(); menu.startNewGame();
            yield return Ready();
            Check(Object.FindObjectsByType<StoryDirector>(FindObjectsSortMode.None).Length == 1, "Rapid new-game calls create one director");
            var d = StoryDirector.Active;
            d.SkipToNextChoice(); yield return Ready();
            Check(d.Goal == StoryGoal.SelectCompany, "Dialogue skip stops at the first mandatory company selection");
            var company = Object.FindFirstObjectByType<Companies>().getCompany(StoryDirector.CompanyId);
            Object.FindFirstObjectByType<CompanyDetailPanel>(FindObjectsInactive.Include).show(company);
            yield return AdvanceTo(StoryGoal.BuyShares);
            var player = Object.FindFirstObjectByType<Player>();
            float cash = player.state.cash; player.state.addCash(-cash);
            var trading = Object.FindFirstObjectByType<ShareTradingSystem>();
            Check(!trading.buyShares(company, 1), "Insufficient funds reject actual purchase");
            d.SkipToNextChoice(); yield return Ready();
            Check(d.Goal == StoryGoal.BuyShares && player.state.cash == 0 && company.state.getStat(CompanyStat.PLAYER_SHARES) == 0,
                "Failed purchase and repeated skip cannot bypass mandatory trade");
            player.state.addCash(cash);
            Check(!trading.buyShares(company, 0) && !trading.buyShares(company, -1) && !trading.buyShares(company, 2), "Invalid or non-lesson quantities rejected");
            Check(trading.buyShares(company, 1), "Purchase recovers after restoring actual funds");
            d.SkipToNextChoice();
            Check(!trading.buyShares(company, 1), "Purchase followed by same-frame skip cannot repeat the transaction");
            yield return AdvanceTo(StoryGoal.EndDay);
            var clock = Object.FindFirstObjectByType<GameTurnClock>();
            int day = clock.CurrentTurn;
            clock.AdvanceTurn(); clock.AdvanceTurn(); d.SkipToNextChoice();
            Check(clock.CurrentTurn == day + 1, "End-day and same-frame skip advance only once");
            yield return Ready();
            Check(StoryDirector.IsRunning && clock.CurrentTurn == day + 1, "Second-day lessons remain required after end-day skip");
            var registry = Object.FindFirstObjectByType<NpcStateRegistry>();
            Check(registry.States.Sum(n => n.memories.Count) == 3, "Boundary recovery retains the three introductory choices exactly once");

            // Scene interruption must not leave the static intro guard locked.
            SceneManager.LoadScene("MainMenu"); yield return null; yield return null;
            Object.FindFirstObjectByType<MainMenuController>().startNewGame(); yield return Ready();
            SceneManager.LoadScene("Game"); yield return null; yield return null; yield return null;
            Check(!StoryDirector.IsRunning && StoryDirector.CanAdvanceTurn, "Reloading Game directly clears interrupted intro guards");
            File.AppendAllText(StoryBoundaryVerification.Output, "COMPLETE\n");
        }
    }
}
