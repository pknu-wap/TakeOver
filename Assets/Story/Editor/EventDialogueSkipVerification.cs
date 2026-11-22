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
        internal static string Output => Path.GetFullPath(Environment.GetEnvironmentVariable("TAKEOVER_QA_OUTPUT") ?? "../QA_NarrativeRevision/EventRegression");
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
        private static bool Skipping(EventNegotiationView view) => EventSceneTransition.IsBusy;
        private static void EnsureOffer(EventDefinition definition, EventSystemController controller)
        {
            if (controller.getTodayEvents().Contains(definition)) return;
            var occurrenceType = typeof(EventSystemController).GetNestedType("Occurrence", BindingFlags.NonPublic);
            var offer = Activator.CreateInstance(occurrenceType, true);
            occurrenceType.GetField("definition").SetValue(offer, definition);
            occurrenceType.GetField("companyId").SetValue(offer, definition.hasCompanyConditions() ? "haneul-food" : "");
            occurrenceType.GetField("firstDay").SetValue(offer, Object.FindFirstObjectByType<TakeOver.NPC.GameTurnClock>().CurrentTurn);
            int serial = Get<int>(controller, "lastOccurrenceSerial") + 1;
            occurrenceType.GetField("serial").SetValue(offer, serial);
            typeof(EventSystemController).GetField("lastOccurrenceSerial", Fields).SetValue(controller, serial);
            ((System.Collections.IList)typeof(EventSystemController).GetField("occurrences", Fields).GetValue(controller)).Add(offer);
            typeof(EventSystemController).GetMethod("rebuildToday", Fields).Invoke(controller, null);
        }
        private IEnumerator ReachChoices(EventNegotiationView view)
        {
            while (Get<List<Button>>(view, "choices").Count == 0)
            {
                if (Skip(view).interactable) Click(Skip(view));
                else if (view.ContinueButton.gameObject.activeInHierarchy && view.ContinueButton.interactable) Click(view.ContinueButton);
                while (EventSceneTransition.IsBusy) yield return null;
                yield return null;
            }
        }
        private IEnumerator Setup(EventDefinition definition)
        {
            SceneManager.LoadScene("Game"); yield return null; yield return null; yield return null;
            foreach (var company in Object.FindFirstObjectByType<Companies>().CompanyList)
            {
                company.state.setStat(CompanyStat.PLAYER_SHARES, company.state.totalShares * .6f);
                company.state.setStat(CompanyStat.REPUTATION_B2C, 10);
                if(definition.getId()=="restructuring_01")company.state.setStat(CompanyStat.COST,company.state.getStat(CompanyStat.REVENUE)+100);
                company.calculator.calculateAboutShares(company);
            }
            var controller = Object.FindFirstObjectByType<EventSystemController>();
            // Seed only the independent food-company story gates; Mirae's introduction remains unanswered.
            var completed = Get<Dictionary<string, int>>(controller, "selectedChoices");
            completed["main_haneul_01"] = 0;
            completed["main_pair_b_complete"] = 0;
            Object.FindFirstObjectByType<TakeOver.NPC.GameTurnClock>().AdvanceTurn();
            if (!controller.getTodayEvents().Contains(definition))
            {
                // Dialogue verification uses a fixed eligible offer, independent of the daily draw probability.
                var occurrenceType = typeof(EventSystemController).GetNestedType("Occurrence", BindingFlags.NonPublic);
                var offer = Activator.CreateInstance(occurrenceType, true);
                occurrenceType.GetField("definition").SetValue(offer, definition);
                occurrenceType.GetField("companyId").SetValue(offer, definition.hasCompanyConditions() ? "haneul-food" : "");
                occurrenceType.GetField("firstDay").SetValue(offer, Object.FindFirstObjectByType<TakeOver.NPC.GameTurnClock>().CurrentTurn);
                int serial = Get<int>(controller, "lastOccurrenceSerial") + 1;
                occurrenceType.GetField("serial").SetValue(offer, serial);
                typeof(EventSystemController).GetField("lastOccurrenceSerial", Fields).SetValue(controller, serial);
                ((System.Collections.IList)typeof(EventSystemController).GetField("occurrences", Fields).GetValue(controller)).Add(offer);
                typeof(EventSystemController).GetMethod("rebuildToday", Fields).Invoke(controller, null);
            }
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
            yield return null; while (EventSceneTransition.IsBusy) yield return null; Canvas.ForceUpdateCanvases();
            Check(State(controller) == "Negotiation" && ap.Current == apBefore - definition.getApCost(), "Negotiation consumes AP once " + definition.getId());
            if (Get<bool>(view, "dialogueMode"))
            {
                yield return RunScriptedChoice(definition, choice, skip, controller, view, apBefore, () => notifications);
                yield break;
            }
            var question = Get<Text>(view, "dialogueText");
            string beforeReading = StorySkipState.Capture();
            var readPages = new List<string>();
            while (view.HasMorePages)
            {
                Check(Get<List<Button>>(view, "choices").Count == 0 && Skip(view).interactable, "Long introduction waits for reading or skip before showing choices");
                Check(question.preferredHeight <= question.rectTransform.rect.height + .5f, "Introduction page fits shared event body");
                readPages.Add(question.text);
                if (definition.getId() == "inside_info_01" && choice == 0 && !skip && readPages.Count == 1)
                {
                    if (Application.isBatchMode) yield return null; else yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(EventDialogueSkipVerification.Output, "cooperation-introduction.png"));
                    yield return null;
                }
                if (skip)
                {
                    Click(Skip(view)); while (Skipping(view)) yield return null;
                }
                else Click(Get<Button>(view, "closeButton"));
                yield return null;
            }
            readPages.Add(question.text);
            if (definition.getId() == "inside_info_01" && !skip)
                Check(new string(string.Concat(readPages).Where(c => !char.IsWhiteSpace(c)).ToArray()) == new string(definition.getDialogue().Where(c => !char.IsWhiteSpace(c)).ToArray()), "Every authored introduction character is reachable through pages");
            Check(StorySkipState.Capture() == beforeReading && notifications == 0, "Reading or skipping introduction applies no effects and spends no extra AP");
            Check(question.rectTransform.rect.width >= 800 && question.preferredHeight <= question.rectTransform.rect.height + .5f,
                "Native event question uses readable shared width without clipping " + definition.getId());
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
                    Check(EventSceneTransition.Opacity > .99f, "Company choices prepared under full blackout");
                    while (Skipping(view)) yield return null;
                }
                else while (State(controller) == "CompanyExchange")
                {
                    yield return new WaitForSecondsRealtime(.7f);
                    if (State(controller) != "CompanyExchange") break;
                    var advance = Get<Button>(view, "closeButton");
                    if (advance.gameObject.activeInHierarchy && advance.interactable) Click(advance);
                    yield return null;
                }
                Check(State(controller) == "CompanyChoice" && !Skip(view).interactable, "Stops at company choice without automatic selection");
                Check(StorySkipState.Capture() == waiting && notifications == 0, "Company exchange skip changes no cash, shares, AP, NPC state");
                yield return null; Canvas.ForceUpdateCanvases();
                if (skip)
                {
                    if (Application.isBatchMode) yield return null; else yield return new WaitForEndOfFrame();
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
                Check(!EventSceneTransition.IsBusy, "Fade releases background input after final result");
            }
            else
            {
                yield return new WaitForSecondsRealtime(.7f);
                while (State(controller) == "Result" && view.CurrentDialogue != Get<Text>(view, "responseText"))
                {
                    var advance = Get<Button>(view, "closeButton");
                    if (advance.gameObject.activeInHierarchy && advance.interactable) Click(advance);
                    yield return new WaitForSecondsRealtime(.35f);
                }
                while (!Get<Button>(view, "closeButton").gameObject.activeSelf) yield return null;
                yield return null; Canvas.ForceUpdateCanvases();
                var response = Get<Text>(view, "responseText");
                var playerLine = Get<Text>(view, "playerLineText");
                var responsePages = new List<string>();
                while (view.HasMorePages)
                {
                    responsePages.Add(response.text);
                    Check(response.preferredHeight <= response.rectTransform.rect.height + .5f, "Result page fits shared event body");
                    Click(Get<Button>(view, "closeButton")); yield return null;
                }
                responsePages.Add(response.text);
                if (definition.getId() == "inside_info_01")
                    Check(new string(string.Concat(responsePages).Where(c => !char.IsWhiteSpace(c)).ToArray()) == new string(definition.getChoices()[choice].getResultDialogue().Where(c => !char.IsWhiteSpace(c)).ToArray()), "Every authored result character is reachable without replaying effects");
                Check(response.preferredHeight <= response.rectTransform.rect.height + .5f,
                    "Native NPC response is not clipped " + definition.getId()+"/"+choice);
                if (Get<CanvasGroup>(view,"playerBlock").gameObject.activeInHierarchy)
                {
                    Check(playerLine.rectTransform.rect.width >= 600 && playerLine.preferredHeight <= playerLine.rectTransform.rect.height + .5f,
                        "Native player reply no longer wraps in a narrow column " + definition.getId()+"/"+choice);
                    var line=(RectTransform)Get<CanvasGroup>(view,"playerBlock").transform.Find("Underline");
                    var ruleCorners=new Vector3[4];var bodyCorners=new Vector3[4];
                    line.GetWorldCorners(ruleCorners);playerLine.rectTransform.GetWorldCorners(bodyCorners);
                    Check(Mathf.Abs(ruleCorners[2].x-bodyCorners[2].x)<1,"Native player reply ends at speaker underline");
                    var upper = new Vector3[4]; var lower = new Vector3[4];
                    response.rectTransform.GetWorldCorners(upper); playerLine.rectTransform.GetWorldCorners(lower);
                    Check(upper[0].y >= lower[1].y, "Native NPC/player text rows do not overlap " + definition.getId()+"/"+choice);
                }
                if(definition.getId()=="inside_info_01" && choice==0)
                {
                    if (Application.isBatchMode) yield return null; else yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(EventDialogueSkipVerification.Output,"native-response-readable.png"));
                    yield return null;
                }
                Click(Get<Button>(view, "closeButton"));
                while (EventSceneTransition.IsBusy) yield return null;
                Check(State(controller) == "Closed", "Normal result close remains functional");
            }
            Check(StorySkipState.Capture() == waiting && notifications == 1, "Closing/skipping does not replay rewards or NPC memories");
            Check(ap.Current == apBefore - definition.getApCost(), "No additional AP consumed by skip");
            Check(controller.tryGetChoice(definition.getId(), out int selected) && selected == choice, "Existing selected-choice history retained");
            state = StorySkipState.Capture() + "\n" + string.Join("|", controller.getTodayEventInfos().Select(i => i.definition.getId() + ":" + i.companyId));
        }
        private IEnumerator RunScriptedChoice(EventDefinition definition, int choice, bool skip, EventSystemController controller,
            EventNegotiationView view, int apBefore, Func<int> notifications)
        {
            string before = StorySkipState.Capture();
            while (Get<List<Button>>(view, "choices").Count == 0)
            {
                Check(view.CurrentDialogue.preferredHeight <= view.CurrentDialogue.rectTransform.rect.height + 1,
                    "Scripted introduction page fits: " + definition.getId());
                if (skip) Click(Skip(view)); else Click(view.ContinueButton);
                while (EventSceneTransition.IsBusy) yield return null;
                yield return null;
            }
            Check(!Skip(view).interactable && notifications() == 0 && StorySkipState.Capture() == before,
                "Scripted introduction stops at an unanswered choice without effects");
            Click(Choice(view, choice)); yield return null;
            if (State(controller) == "CompanyExchange" || State(controller) == "CompanyChoice")
            {
                while (Get<List<Button>>(view, "choices").Count == 0)
                {
                    if (skip) Click(Skip(view)); else Click(view.ContinueButton);
                    while (EventSceneTransition.IsBusy) yield return null;
                    yield return null;
                }
                Check(notifications() == 0 && !Skip(view).interactable, "Scripted company choice is never selected automatically");
                Click(Choice(view, 0)); yield return null;
            }
            Check(State(controller) == "Result" && notifications() == 1, "Scripted answer applies effects exactly once");
            string committed = StorySkipState.Capture();
            if (skip)
            {
                Click(Skip(view)); while (EventSceneTransition.IsBusy) yield return null;
            }
            else while (!controller.IsClosed)
            {
                Check(view.CurrentDialogue.preferredHeight <= view.CurrentDialogue.rectTransform.rect.height + 1,
                    "Scripted result page fits: " + definition.getId());
                Click(view.ContinueButton); while (EventSceneTransition.IsBusy) yield return null;
                yield return null;
            }
            Check(controller.IsClosed && !view.gameObject.activeSelf && notifications() == 1 && StorySkipState.Capture() == committed,
                "Scripted result closes without duplicate rewards");
            Check(Object.FindFirstObjectByType<ActionPointService>().Current == apBefore - definition.getApCost(), "Scripted skip spends no additional AP");
            state = StorySkipState.Capture() + "\n" + string.Join("|", controller.getTodayEventInfos().Select(i => i.definition.getId() + ":" + i.companyId));
        }
        private IEnumerator Verify()
        {
            var definitions = AssetDatabase.FindAssets("t:EventDefinition", new[] { "Assets/EventSystem/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<EventDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => Path.GetDirectoryName(AssetDatabase.GetAssetPath(d)).Replace('\\', '/') == "Assets/EventSystem/Data")
                .OrderBy(d => d.getId()).ToArray();
            Check(definitions.Length == 7, "All seven existing event definitions covered");
            var cooperation = definitions.Single(d => d.getId() == "inside_info_01");
            Check(cooperation.getTitle() == "미래건설의 협력 제안" && !string.IsNullOrWhiteSpace(cooperation.getDialogue()), "Cooperation introduction has authored content");
            Check(cooperation.getChoices().Select(c => c.getResultValue()).SequenceEqual(new[] { "review_cooperation", "prepare_takeover", "decline_cooperation" }), "Three distinct player intentions are authored");
            yield return Setup(cooperation);
            var eligibility = Object.FindFirstObjectByType<EventSystemController>();
            var mirae = Object.FindFirstObjectByType<Companies>().getCompany(StoryDirector.CompanyId);
            mirae.state.setStat(CompanyStat.PLAYER_SHARES, mirae.state.totalShares * .099f); mirae.calculator.calculateAboutShares(mirae); eligibility.refreshAvailability();
            Check(!eligibility.getTodayEvents().Contains(cooperation), "Mirae story is unavailable at 9.9 percent");
            mirae.state.setStat(CompanyStat.PLAYER_SHARES, mirae.state.totalShares * .1f); mirae.calculator.calculateAboutShares(mirae); eligibility.refreshAvailability();
            Check(eligibility.getTodayEvents().Contains(cooperation), "Mirae story remains available at exactly 10 percent");
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
            controller.openEvent("inside_info_01"); controller.startNegotiation(); yield return null; while (EventSceneTransition.IsBusy) yield return null; Canvas.ForceUpdateCanvases();
            if (view.HasMorePages) { Click(Skip(view)); while (Skipping(view)) yield return null; }
            Click(Choice(view, 0));
            while (!Get<Button>(view, "closeButton").gameObject.activeSelf) yield return null;
            yield return null; Canvas.ForceUpdateCanvases();
            Check(Skip(view).interactable, "Skip remains available after result animation completes");
            Click(Skip(view)); while (Skipping(view)) yield return null;
            EnsureOffer(definitions.Single(item => item.getId() == "press_interview_01"), controller);
            controller.openEvent("press_interview_01"); controller.startNegotiation(); yield return null; while (EventSceneTransition.IsBusy) yield return null; Canvas.ForceUpdateCanvases();
            yield return ReachChoices(view);
            Check(!Skip(view).interactable && view.GetComponentsInChildren<Button>(true).Count(b => b.name == "Skip to choice") == 1, "Reopening same view resets skip state and keeps one button");
            Click(Choice(view, 0)); Click(Skip(view)); while (Skipping(view)) yield return null;
            Check(State(controller) == "Closed", "Reused view skips a second event successfully");
            controller.openEvent(stock.getId()); controller.startNegotiation(); yield return null; while (EventSceneTransition.IsBusy) yield return null; Canvas.ForceUpdateCanvases();
            yield return ReachChoices(view);
            Click(Choice(view, 0)); string stateBeforeHide = State(controller); Click(Skip(view));
            view.hide(); yield return new WaitForSecondsRealtime(.8f);
            Check(State(controller) == stateBeforeHide && !Skipping(view), "External hide cancels pending skip callback");
            Check(!EventSceneTransition.IsBusy, "Cancelled skip removes blackout");
            yield return Setup(cooperation);
            Check(GameObject.Find("Event scene fade") == null && !StoryDirector.IsRunning, "Scene reload removes blackout and does not start intro");
            controller = Object.FindFirstObjectByType<EventSystemController>();
            var ap = Object.FindFirstObjectByType<ActionPointService>(); ap.TryConsume(ap.Current);
            var simple = cooperation;
            controller.openEvent(simple.getId()); controller.startNegotiation();
            Check(State(controller) == "Detail" && ap.Current == 0, "Insufficient AP still prevents negotiation");
            File.AppendAllText(Results, "COMPLETE\n");
        }
    }
}
