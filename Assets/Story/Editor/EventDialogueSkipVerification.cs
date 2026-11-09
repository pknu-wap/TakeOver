using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TakeOver.Events;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TakeOver.Story.Editor
{
    [InitializeOnLoad]
    public static class EventDialogueSkipVerification
    {
        private const string Key = "TakeOver.EventDialogueSkip";
        internal static string Output => Path.GetFullPath("../QA_EventDialogueSkip");
        static EventDialogueSkipVerification() { EditorApplication.playModeStateChanged += Changed; }
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "skip-results.txt"), "RUNNING\n");
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            var game = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
            game.position = new Rect(40, 40, 1280, 780); game.Show(); game.Focus();
            EditorApplication.isPlaying = true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                var go = new GameObject("Event skip verification");
                Object.DontDestroyOnLoad(go); go.AddComponent<EventDialogueSkipProbe>();
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                if (Application.isBatchMode) EditorApplication.Exit(File.ReadAllText(Path.Combine(Output, "skip-results.txt")).Contains("FAIL") ? 1 : 0);
            }
        }
    }

    public sealed class EventDialogueSkipProbe : MonoBehaviour
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private string Results => Path.Combine(EventDialogueSkipVerification.Output, "skip-results.txt");
        private float deadline;
        private string state;
        private static T Get<T>(object value, string field) => (T)value.GetType().GetField(field, Fields).GetValue(value);
        private static string State(EventSystemController controller) => Get<object>(controller, "screenState").ToString();
        private void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            File.AppendAllText(Results, "PASS: " + message + "\n");
        }
        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                File.AppendAllText(Results, "FAIL runtime: " + message + "\n" + stack + "\n");
        }
        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            SceneManager.sceneLoaded -= StorySkipState.Seed;
        }
        private void Update()
        {
            if (deadline > 0 && Time.realtimeSinceStartup > deadline)
            { File.AppendAllText(Results, "FAIL: Timeout\n"); deadline = 0; EditorApplication.isPlaying = false; }
        }
        private IEnumerator Start()
        {
            deadline = Time.realtimeSinceStartup + 300;
            Application.logMessageReceived += OnLog;
            SceneManager.sceneLoaded += StorySkipState.Seed;
            var routines = new Stack<IEnumerator>(); routines.Push(Verify());
            while (routines.Count > 0)
            {
                bool next; object value = null;
                var run = routines.Peek();
                try { next = run.MoveNext(); if (next) value = run.Current; }
                catch (Exception error) { File.AppendAllText(Results, "FAIL: " + error + "\n"); break; }
                if (!next) { routines.Pop(); continue; }
                if (value is IEnumerator nested) routines.Push(nested);
                else yield return value;
            }
            deadline = 0; EditorApplication.isPlaying = false;
        }
        private static void Click(Button button)
        {
            if (!button.IsInteractable() || !button.gameObject.activeInHierarchy) throw new Exception("Unavailable: " + button.name);
            var canvas = button.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var rect = (RectTransform)button.transform;
            var point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            if (hits.Count == 0 || !(hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)))
                throw new Exception("Occluded: " + button.name + " by " + (hits.Count > 0 ? hits[0].gameObject.name : "nothing"));
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        private static Button Skip(EventNegotiationView view) => view.GetComponentsInChildren<Button>(true).Single(b => b.name == "Skip to choice");
        private static Button Choice(EventNegotiationView view, int index) => Get<List<Button>>(view, "choices")[index];
        private static bool Skipping(EventNegotiationView view) => Get<bool>(view, "skipping");
        private IEnumerator Setup(EventDefinition definition)
        {
            SceneManager.LoadScene("Game"); yield return null; yield return null; yield return null;
            foreach (var company in Object.FindFirstObjectByType<Companies>().CompanyList)
            {
                company.state.setStat(CompanyStat.PLAYER_SHARES, company.state.totalShares * .6f);
                company.state.setStat(CompanyStat.REPUTATION_B2C, 10);
                company.calculator.calculateAboutShares(company);
            }
            var controller = Object.FindFirstObjectByType<EventSystemController>();
            Object.FindFirstObjectByType<TakeOver.NPC.GameTurnClock>().AdvanceTurn();
            Check(controller.getTodayEvents().Contains(definition), "Fixture exposes actual event " + definition.getId());
        }
        private IEnumerator RunChoice(EventDefinition definition, int choice, bool skip)
        {
            yield return Setup(definition);
            var controller = Object.FindFirstObjectByType<EventSystemController>();
            var view = Object.FindFirstObjectByType<EventNegotiationView>(FindObjectsInactive.Include);
            var ap = Object.FindFirstObjectByType<ActionPointService>();
            int apBefore = ap.Current, notifications = 0;
            controller.choiceConfirmed += _ => notifications++;
            controller.openEvent(definition.getId()); controller.startNegotiation();
            yield return null; Canvas.ForceUpdateCanvases();
            Check(State(controller) == "Negotiation" && ap.Current == apBefore - definition.getApCost(), "Negotiation consumes AP once " + definition.getId());
            Check(Skip(view).gameObject.activeInHierarchy && !Skip(view).interactable, "Skip visible and disabled at unanswered choice");
            string waiting = StorySkipState.Capture(); view.SkipToNextChoice();
            Check(State(controller) == "Negotiation" && StorySkipState.Capture() == waiting && notifications == 0, "Skip cannot answer or alter an unanswered choice");
            Click(Choice(view, choice));
            if (definition.getChoiceGeneration() == EventChoiceGeneration.OwnedCompanies && choice == 0)
            {
                Check(State(controller) == "CompanyExchange" && notifications == 0, "Company preselection does not apply result");
                waiting = StorySkipState.Capture();
                if (skip)
                {
                    Click(Skip(view)); view.SkipToNextChoice();
                    Check(Skipping(view) && !Skip(view).interactable, "Repeated skip locks one transition");
                    while (State(controller) == "CompanyExchange") yield return null;
                    Check(Get<Image>(view, "skipCurtain").color.a > .99f, "Company choices prepared under full blackout");
                    while (Skipping(view)) yield return null;
                }
                else while (State(controller) == "CompanyExchange") yield return null;
                Check(State(controller) == "CompanyChoice" && !Skip(view).interactable, "Stops at company choice without automatic selection");
                Check(StorySkipState.Capture() == waiting && notifications == 0, "Company exchange skip changes no cash, shares, AP, NPC state");
                yield return null; Canvas.ForceUpdateCanvases();
                if (skip)
                {
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(EventDialogueSkipVerification.Output, "company-choice-after-skip.png"));
                    yield return null;
                }
                Click(Choice(view, 0));
            }
            Check(State(controller) == "Result" && notifications == 1, "User selection applies result exactly once");
            waiting = StorySkipState.Capture();
            if (skip)
            {
                Click(Skip(view)); view.SkipToNextChoice();
                while (Skipping(view)) yield return null;
                Check(State(controller) == "Closed" && !view.gameObject.activeSelf, "Last choice skip closes dialogue after fade");
                Check(!Get<Canvas>(view, "skipCanvas").gameObject.activeSelf, "Fade releases background input after final result");
            }
            else
            {
                while (!Get<Button>(view, "closeButton").gameObject.activeSelf) yield return null;
                yield return null; Canvas.ForceUpdateCanvases();
                Click(Get<Button>(view, "closeButton"));
                Check(State(controller) == "Closed", "Normal result close remains functional");
            }
            Check(StorySkipState.Capture() == waiting && notifications == 1, "Closing/skipping does not replay rewards or NPC memories");
            Check(ap.Current == apBefore - definition.getApCost(), "No additional AP consumed by skip");
            Check(controller.tryGetChoice(definition.getId(), out int selected) && selected == choice, "Existing selected-choice history retained");
            state = StorySkipState.Capture() + "\n" + string.Join("|", controller.getTodayEventInfos().Select(i => i.definition.getId() + ":" + i.companyId));
        }
        private IEnumerator Verify()
        {
            var definitions = AssetDatabase.FindAssets("t:EventDefinition", new[] { "Assets/EventSystem/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<EventDefinition>(AssetDatabase.GUIDToAssetPath(g))).OrderBy(d => d.getId()).ToArray();
            Check(definitions.Length == 7, "All seven existing event definitions covered");
            foreach (var definition in definitions)
            {
                string original = EditorJsonUtility.ToJson(definition);
                for (int choice = 0; choice < definition.getChoices().Count; choice++)
                {
                    yield return RunChoice(definition, choice, false);
                    string normal = state;
                    yield return RunChoice(definition, choice, true);
                    File.WriteAllText(Path.Combine(EventDialogueSkipVerification.Output, definition.getId() + "-" + choice + "-normal.json"), normal);
                    File.WriteAllText(Path.Combine(EventDialogueSkipVerification.Output, definition.getId() + "-" + choice + "-skip.json"), state);
                    Check(normal == state, "Normal/skip complete state identical " + definition.getId() + "/" + choice);
                }
                Check(original == EditorJsonUtility.ToJson(definition), "Source event asset unchanged " + definition.getId());
            }
            // Cancellation/reopening and AP failure cover lifecycle paths outside the happy sequence.
            var stock = definitions.Single(d => d.getChoiceGeneration() == EventChoiceGeneration.OwnedCompanies);
            yield return Setup(stock);
            var controller = Object.FindFirstObjectByType<EventSystemController>();
            var view = Object.FindFirstObjectByType<EventNegotiationView>(FindObjectsInactive.Include);
            controller.openEvent("inside_info_01"); controller.startNegotiation(); yield return null; Canvas.ForceUpdateCanvases();
            Click(Choice(view, 0));
            while (!Get<Button>(view, "closeButton").gameObject.activeSelf) yield return null;
            yield return null; Canvas.ForceUpdateCanvases();
            Check(Skip(view).interactable, "Skip remains available after result animation completes");
            Click(Skip(view)); while (Skipping(view)) yield return null;
            controller.openEvent("press_interview_01"); controller.startNegotiation(); yield return null; Canvas.ForceUpdateCanvases();
            Check(!Skip(view).interactable && view.GetComponentsInChildren<Button>(true).Count(b => b.name == "Skip to choice") == 1, "Reopening same view resets skip state and keeps one button");
            Click(Choice(view, 0)); Click(Skip(view)); while (Skipping(view)) yield return null;
            Check(State(controller) == "Closed", "Reused view skips a second event successfully");
            controller.openEvent(stock.getId()); controller.startNegotiation(); yield return null; Canvas.ForceUpdateCanvases();
            Click(Choice(view, 0)); Click(Skip(view));
            view.hide(); yield return new WaitForSecondsRealtime(.8f);
            Check(State(controller) == "CompanyExchange" && !Skipping(view), "External hide cancels pending skip callback");
            Check(!Get<Canvas>(view, "skipCanvas").gameObject.activeSelf, "Cancelled skip removes blackout");
            SceneManager.LoadScene("Game"); yield return null; yield return null; yield return null;
            Check(GameObject.Find("Event skip blackout") == null && !StoryDirector.IsRunning, "Scene reload removes blackout and does not start intro");
            controller = Object.FindFirstObjectByType<EventSystemController>();
            var ap = Object.FindFirstObjectByType<ActionPointService>(); ap.TryConsume(ap.Current);
            var simple = controller.getTodayEvents().First();
            controller.openEvent(simple.getId()); controller.startNegotiation();
            Check(State(controller) == "Detail" && ap.Current == 0, "Insufficient AP still prevents negotiation");
            File.AppendAllText(Results, "COMPLETE\n");
        }
    }
}
