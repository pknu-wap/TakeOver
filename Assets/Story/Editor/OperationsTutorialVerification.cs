using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TakeOver.Employees;
using TakeOver.Operations;
using TakeOver.SaveSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TakeOver.Story.Editor
{
    [InitializeOnLoad]
    public static class OperationsTutorialVerification
    {
        public static string Output => Path.GetFullPath(Environment.GetEnvironmentVariable("TAKEOVER_QA_OUTPUT") ?? "../QA_OperationsTutorial");
        private const string Key = "OperationsTutorialVerification";
        static OperationsTutorialVerification() { EditorApplication.playModeStateChanged += Changed; }
        public static void Run()
        {
            if (!File.Exists(OperationsTutorialPrefabBuilder.Path)) OperationsTutorialPrefabBuilder.Rebuild();
            Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "results.txt"), "RUNNING\n");
            Environment.SetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY", Path.Combine(Output, "Save"));
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Show();
            EditorApplication.isPlaying = true;
        }
        public static void RebuildAndRun()
        {
            OperationsTutorialPrefabBuilder.Rebuild();
            Run();
        }
        public static void SyncAndRun()
        {
            OperationsTutorialAuthoring.Sync();
            Run();
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) new GameObject("Operations tutorial verification").AddComponent<OperationsTutorialProbe>();
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(File.ReadAllText(Path.Combine(Output, "results.txt")).Contains("FAIL") ? 1 : 0);
            }
        }
    }

    public sealed class OperationsTutorialProbe : MonoBehaviour
    {
        private FixedGameViewSize size;
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Fields).GetValue(target);
        private static void Check(bool pass, string message)
        {
            if (!pass) throw new InvalidOperationException(message);
            File.AppendAllText(Path.Combine(OperationsTutorialVerification.Output, "results.txt"), "PASS: " + message + "\n");
        }
        private void OnEnable() => Application.logMessageReceived += Log;
        private void OnDisable() => Application.logMessageReceived -= Log;
        private void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                File.AppendAllText(Path.Combine(OperationsTutorialVerification.Output, "results.txt"), "FAIL runtime: " + message + "\n" + stack + "\n");
        }
        private IEnumerator Start()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(Verify());
            while (stack.Count > 0)
            {
                IEnumerator step = stack.Peek(); bool more;
                try { more = step.MoveNext(); }
                catch (Exception error)
                { File.AppendAllText(Path.Combine(OperationsTutorialVerification.Output, "results.txt"), "FAIL: " + error + "\n"); break; }
                if (!more) { stack.Pop(); continue; }
                if (step.Current is IEnumerator nested) stack.Push(nested); else yield return step.Current;
            }
            size?.Dispose(); File.AppendAllText(Path.Combine(OperationsTutorialVerification.Output, "results.txt"), "COMPLETE\n");
            EditorApplication.isPlaying = false;
        }
        private static Dictionary<string, string> Snapshot() => Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OfType<ISaveParticipant>().Where(p => new[] { "employees", "documents", "startups", "trading", "action-points" }.Contains(p.saveId))
            .ToDictionary(p => p.saveId, p => p.captureJson());
        private static string OriginalUiState(OperationsScreen screen) => string.Join("\n", screen.GetComponentsInChildren<RectTransform>(true)
            .Select(t => t.name + ":" + t.gameObject.activeSelf + ":" + t.localPosition + ":" + t.localScale + ":" + t.sizeDelta + ":" + t.anchorMin + ":" + t.anchorMax + ":" + t.pivot));
        private static bool Inside(RectTransform element, RectTransform frame)
        {
            var corners = new Vector3[4]; element.GetWorldCorners(corners);
            var bounds = frame.rect; bounds.xMin -= 1; bounds.xMax += 1; bounds.yMin -= 1; bounds.yMax += 1;
            return corners.All(c => bounds.Contains(frame.InverseTransformPoint(c)));
        }
        private IEnumerator Verify()
        {
            for (int i = 0; i < 20; i++) yield return null;
            var originalOperations = Object.FindFirstObjectByType<OperationsScreen>(FindObjectsInactive.Include);
            string originalUi = OriginalUiState(originalOperations);
            var snapshots = Snapshot();
            foreach (var width in new[] { 1280, 1920 })
            {
                size = new FixedGameViewSize(width, width * 9 / 16);
                for (int i = 0; i < 10; i++) yield return null;
                var data = Object.Instantiate(AssetDatabase.LoadAssetAtPath<StorySequence>("Assets/Story/Data/IntroSequence.asset"));
                data.fadeSeconds = .05f;
                data.beats = data.beats.Where(b => (int)b.goal >= (int)StoryGoal.ViewEmployees && (int)b.goal <= (int)StoryGoal.ViewStartups).ToList();
                var beats = data.beats.ToArray();
                Check(beats.Length == 6, "All six real operations tutorial screens are covered");
                data.beats.Add(new StoryBeat { id = "operations-qa-end", speaker = StorySpeaker.System, text = "검증 종료", background = data.beats[0].background });
                var host = new GameObject("Operations tutorial host"); host.SetActive(false);
                var intro = Object.Instantiate(Resources.Load<GameObject>("Story/Intro"), host.transform, false);
                var director = intro.GetComponent<StoryDirector>();
                typeof(StoryDirector).GetField("sequence", Fields).SetValue(director, data); host.SetActive(true);
                foreach (var beat in beats)
                {
                    float deadline = Time.realtimeSinceStartup + 20;
                    while ((director.Current?.id != beat.id || director.Busy) && Time.realtimeSinceStartup < deadline) yield return null;
                    Check(director.Current?.id == beat.id && !director.Busy, "Operations stage ready: " + beat.id + "/" + width);
                    for (int i = 0; i < 12; i++) yield return null;
                    Canvas.ForceUpdateCanvases();
                    var preview = Object.FindFirstObjectByType<OperationsTutorialView>();
                    Check(preview != null && preview.Ready, "Dedicated tutorial copy is ready: " + beat.id);
                    var operations = preview.Screen;
                    var visibleText = operations.GetComponentsInChildren<Text>().Select(t => t.text)
                        .Concat(operations.GetComponentsInChildren<TMPro.TMP_Text>().Select(t => t.text));
                    Check(visibleText.All(t => !new[] { "설명용", "미리보기", "예시", "??" }.Any(marker => t.Contains(marker))),
                        "Tutorial copy has no review labels or corrupted placeholder text: " + beat.id);
                    Check(operations != originalOperations, "Preview uses a separate operations instance");
                    Check(operations.GetComponentsInChildren<Canvas>().All(c => !c.overrideSorting), "Preview has no active desktop sorting canvases");
                    Check(operations.GetComponentsInChildren<CanvasScaler>(true).Length == 0, "Preview has no independent screen scaling");
                    Check(OriginalUiState(originalOperations) == originalUi, "Live operations hierarchy is unchanged: " + beat.id);
                    Check(Mathf.Abs(preview.Content.rect.width / preview.Content.rect.height - 16f / 9f) < .001f,
                        "Entire authored frame retains the original 16:9 proportions");
                    foreach (var tabButton in Field<Button[]>(operations, "subTabButtons"))
                        Check(tabButton.gameObject.activeInHierarchy && Inside((RectTransform)tabButton.transform, preview.Content),
                            "Original tab remains visible inside the copied frame: " + tabButton.name);
                    var explanation = director.View.InformationRect;
                    var previewCorners = new Vector3[4]; var explanationCorners = new Vector3[4];
                    preview.Content.GetWorldCorners(previewCorners); explanation.GetWorldCorners(explanationCorners);
                    Check(previewCorners[0].x > explanationCorners[2].x + 4, "Copied frame does not overlap the explanation");
                    Check(UiLayoutAssertions.OnScreen(operations.TutorialContent), "Whole example fits the viewport: " + beat.id);
                    Check(operations.GetComponentsInChildren<Selectable>().All(s => !s.IsInteractable()), "Example controls cannot transact or navigate: " + beat.id);
                    foreach (var control in operations.GetComponentsInChildren<Selectable>().Where(s => s.targetGraphic != null))
                        Check(control.targetGraphic.canvasRenderer.GetColor().a >= control.colors.normalColor.a - .02f,
                            "Read-only control keeps its normal opacity: " + beat.id + "/" + control.name);
                    if (beat.goal == StoryGoal.ViewEmployees)
                    {
                        var staff = operations.GetComponentInChildren<EmployeeTabView>();
                        Check(staff.IsTutorialPreview && staff.GetComponentsInChildren<EmployeeRowView>().Any(r => r.BoundEmployee.Name == "김민준"), "Employee example row is populated");
                        Check(staff.GetComponentsInChildren<ApplicantRowView>().Length == 1, "Applicant example row is populated");
                        Check(staff.GetComponentsInChildren<EmployeeRowView>().All(r => Inside((RectTransform)r.transform, preview.Content))
                            && staff.GetComponentsInChildren<ApplicantRowView>().All(r => Inside((RectTransform)r.transform, preview.Content)),
                            "Employee and applicant rows stay inside their copied frame");
                    }
                    else if (beat.goal == StoryGoal.ViewStartups)
                    {
                        var startups = operations.GetComponentInChildren<StartupTabView>();
                        Check(startups.IsTutorialPreview && startups.GetComponentInChildren<StartupLineGraph>().pointCount >= 2, "Startup metrics and graph remain visible");
                    }
                    else
                    {
                        var tab = operations.GetComponentInChildren<DocumentTabView>();
                        Check(tab.IsTutorialPreview && tab.GetComponentsInChildren<DocumentCard>().Length == 1, "Example document fills the real inbox list");
                        var detail = tab.GetComponentInChildren<DocumentDetailView>();
                        Check(detail != null && Field<Button>(detail, "approve").gameObject.activeInHierarchy, "Document decision area stays visible");
                        Check(Field<Text>(detail, "deadline").text == "D-7", "Tutorial document uses the normal deadline display");
                        Check(detail.GetComponentsInChildren<DocumentInfoRow>().Length >= (beat.goal == StoryGoal.ViewDiscovery ? 1 : 4), "Example document contains its key information rows");
                        Check(Inside(Field<Text>(detail, "title").rectTransform, preview.Content)
                            && Inside((RectTransform)Field<Button>(detail, "approve").transform, preview.Content), "Document title and decision area stay inside the copied frame");
                        Field<Button>(detail, "approve").onClick.Invoke();
                        Check(Snapshot().All(p => snapshots.TryGetValue(p.Key, out var before) && before == p.Value), "Preview approval has no real state effects");
                    }
                    Check(Snapshot().All(p => snapshots.TryGetValue(p.Key, out var before) && before == p.Value), "Employee/document/startup/money/AP state is unchanged: " + beat.id);
                    ScreenCapture.CaptureScreenshot(Path.Combine(OperationsTutorialVerification.Output, width + "-" + beat.id + ".png"));
                    yield return null;
                    var confirm = director.GetComponentsInChildren<Button>(true).Single(b => b.name == "Confirm profile" && b.gameObject.activeInHierarchy);
                    Check(UiLayoutAssertions.Reachable(confirm), "Only the actual explanation confirmation progresses the tutorial");
                    UiLayoutAssertions.Click(confirm); yield return null;
                }
                for (int i = 0; i < 15; i++) yield return null;
                Check(!Object.FindObjectsByType<EmployeeTabView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(v => v.IsTutorialPreview)
                    && !Object.FindObjectsByType<DocumentTabView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(v => v.IsTutorialPreview)
                    && !Object.FindObjectsByType<StartupTabView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(v => v.IsTutorialPreview), "All preview flags are cleared between tutorial and normal UI");
                Check(Object.FindFirstObjectByType<OperationsTutorialView>() == null, "Dedicated preview is destroyed after the lesson");
                Check(OriginalUiState(originalOperations) == originalUi, "Original operations hierarchy remains intact after all stages");
                Object.Destroy(host); Object.Destroy(data); yield return null; yield return null;
                size.Dispose(); size = null;
            }
            Check(Snapshot().All(p => snapshots.TryGetValue(p.Key, out var before) && before == p.Value), "Both resolutions leave all gameplay save data unchanged");
        }
    }
}
