using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TakeOver.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class FinancialPreviewFocusedVerification
{
    public static string Root => Path.GetFullPath(Environment.GetEnvironmentVariable("TAKEOVER_FINANCIAL_QA_OUTPUT") ?? "../QA_FinancialPreview/Focused") + Path.DirectorySeparatorChar;
    static FinancialPreviewFocusedVerification() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            throw new InvalidOperationException("Financial preview rendering verification requires a graphics device.");
        Directory.CreateDirectory(Root); File.WriteAllText(Root + "results.txt", "RUNNING\n");
        foreach (string name in new[] { "mirae.png", "haneul.png" })
            if (File.Exists(Root + name)) File.Delete(Root + name);
        SessionState.SetString("FinancialPreviewFocused.PreviousSave", Environment.GetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY") ?? "");
        Environment.SetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY", Path.GetFullPath(Root + "Save"));
        SessionState.SetBool("FinancialPreviewFocused", true);
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity"); EditorApplication.isPlaying = true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool("FinancialPreviewFocused", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode) new GameObject("Financial preview focus").AddComponent<FinancialPreviewFocusedProbe>();
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool("FinancialPreviewFocused", false);
            Environment.SetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY", SessionState.GetString("FinancialPreviewFocused.PreviousSave", ""));
            EditorApplication.Exit(File.ReadAllText(Root + "results.txt").Contains("FAIL") ? 1 : 0);
        }
    }
}
public sealed class FinancialPreviewFocusedProbe : MonoBehaviour
{
    FixedGameViewSize size;
    static void Check(bool pass, string label)
    { if (!pass) throw new Exception(label); File.AppendAllText(FinancialPreviewFocusedVerification.Root + "results.txt", "PASS: " + label + "\n"); }
    IEnumerator Start()
    {
        var run = Verify();
        while (true)
        {
            bool more; object next;
            try { more = run.MoveNext(); next = more ? run.Current : null; }
            catch (Exception error) { File.AppendAllText(FinancialPreviewFocusedVerification.Root + "results.txt", "FAIL: " + error); break; }
            if (!more) break;
            yield return next;
        }
        size?.Dispose(); File.AppendAllText(FinancialPreviewFocusedVerification.Root + "results.txt", "COMPLETE\n"); EditorApplication.isPlaying = false;
    }
    IEnumerator Verify()
    {
        size = new FixedGameViewSize(1280, 720);
        for (int i = 0; i < 10; i++) yield return null;
        var original = Object.FindFirstObjectByType<CompanyDetailPanel>(FindObjectsInactive.Include);
        var data = Object.Instantiate(AssetDatabase.LoadAssetAtPath<StorySequence>("Assets/Story/Data/IntroSequence.asset"));
        data.fadeSeconds = .05f;
        data.beats = new List<StoryBeat>(data.beats.Where(b => b.goal == StoryGoal.ViewCompany || b.goal == StoryGoal.CompareCompany || b.goal == StoryGoal.ViewStake));
        var readingBeats = data.beats.ToArray();
        data.beats.Add(new StoryBeat { id = "qa-end", speaker = StorySpeaker.System, text = "검증 종료", background = data.beats[0].background });
        var host = new GameObject("Focused tutorial host"); host.SetActive(false);
        var intro = Object.Instantiate(Resources.Load<GameObject>("Story/Intro"), host.transform, false);
        var director = intro.GetComponent<StoryDirector>();
        typeof(StoryDirector).GetField("sequence", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(director, data);
        host.SetActive(true);
        foreach (var beat in readingBeats)
        {
            var expected = beat.goal;
            float until = Time.realtimeSinceStartup + 20;
            while ((director.Goal != expected || director.Busy) && Time.realtimeSinceStartup < until) yield return null;
            Check(director.Goal == expected && !director.Busy, "Reading stage ready: " + expected);
            for (int i = 0; i < 12; i++) yield return null;
            var preview = Object.FindObjectsByType<CompanyDetailPanel>(FindObjectsSortMode.None).Single(p => p.IsTutorialPreview);
            Check(preview.GetComponent<CanvasGroup>().alpha == 1 && !original.gameObject.activeInHierarchy, "Only the fully visible copied table is rendered: " + expected);
            Check(UiLayoutAssertions.OnScreen(preview.transform.Find("Window") as RectTransform), "Copied table is fully on-screen: " + expected);
            var buttons = preview.GetComponentsInChildren<Button>(true).Where(b => b.name == "BuyButton" || b.name == "SellButton").ToArray();
            Check(buttons.Length == 2 && buttons.All(b => !b.IsInteractable()), "Both transaction buttons remain disabled: " + expected);
            Check(buttons.All(b => UiLayoutAssertions.OnScreen((RectTransform)b.transform)), "Disabled buttons remain visible in the unchanged layout: " + expected);
            string name = expected == StoryGoal.ViewStake ? "holdings-" + beat.id : expected == StoryGoal.ViewCompany ? "mirae" : "haneul";
            File.WriteAllLines(Path.GetFullPath(FinancialPreviewFocusedVerification.Root + name + "-graphics.txt"), preview.GetComponentsInChildren<Graphic>(true).Select(g =>
                g.name + " active=" + g.isActiveAndEnabled + " cull=" + g.canvasRenderer.cull + " color=" + g.color + " alpha=" + g.canvasRenderer.GetAlpha() + " depth=" + g.canvasRenderer.absoluteDepth + " material=" + g.materialForRendering?.name));
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(FinancialPreviewFocusedVerification.Root + name + ".png"));
            for (int i = 0; i < 8; i++) yield return null;
            string capturePath = FinancialPreviewFocusedVerification.Root + name + ".png";
            while (!File.Exists(capturePath)) yield return null;
            var capture = new Texture2D(2, 2);
            Check(capture.LoadImage(File.ReadAllBytes(capturePath)), "Rendered screenshot is available: " + expected);
            Color Sample(RectTransform rect)
            {
                var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                var point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(
                    new Vector3(rect.rect.xMin + rect.rect.width * .15f, rect.rect.yMin + rect.rect.height * .25f)));
                return capture.GetPixel(Mathf.Clamp(Mathf.RoundToInt(point.x), 0, capture.width - 1), Mathf.Clamp(Mathf.RoundToInt(point.y), 0, capture.height - 1));
            }
            Check(buttons.All(button =>
            {
                Color color = Sample((RectTransform)button.transform);
                return color.r > .15f && Mathf.Abs(color.r - color.g) < .05f && Mathf.Abs(color.g - color.b) < .05f;
            }), "Both disabled gray button backgrounds are actually drawn: " + expected);
            Check(UiLayoutAssertions.Field<MetricBox[]>(preview, "metricBoxes").All(metric =>
            {
                Color color = Sample((RectTransform)metric.transform);
                return color.b > .2f && color.b > color.r + .03f;
            }), "All six financial metric backgrounds are actually drawn: " + expected);
            Object.Destroy(capture);
            var confirm = director.GetComponentsInChildren<Button>(true).Single(b => b.name == "Confirm profile" && b.gameObject.activeInHierarchy);
            Check(UiLayoutAssertions.Reachable(confirm), "Confirmation is the accessible progression control: " + expected);
            UiLayoutAssertions.Click(confirm);
            yield return null;
        }
        for (int i = 0; i < 15; i++) yield return null;
        Check(!Object.FindObjectsByType<CompanyDetailPanel>(FindObjectsSortMode.None).Any(p => p.IsTutorialPreview), "The copied table is hidden outside the reading stages");
        Check(original.GetComponentsInChildren<Button>(true).Where(b => b.name == "BuyButton" || b.name == "SellButton").All(b => b.interactable), "Original transaction controls remain enabled after all reading stages");
        Object.Destroy(host); Object.Destroy(data);
        yield return null; yield return null;
        Check(!Object.FindObjectsByType<CompanyDetailPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(p => p.IsTutorialPreview), "The copied table is destroyed with its tutorial owner");
    }
}
