using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TakeOver.Events;
using TakeOver.Help;
using TakeOver.NPC;
using TakeOver.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Runs independently of save/load verification, at actual fixed Game View resolutions.
[InitializeOnLoad]
public static class UiLayoutVerification
{
    public static string Root => HelpVerification.Output;
    static UiLayoutVerification() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            throw new InvalidOperationException("UI resolution verification requires a graphics device; run Unity without -nographics.");
        Directory.CreateDirectory(Root); File.WriteAllText(Root + "results.txt", "RUNNING\n");
        SessionState.SetString("UiLayout.PreviousSave", Environment.GetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY") ?? "");
        Environment.SetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY", Path.GetFullPath(Root + "Save"));
        SessionState.SetBool("UiLayout.Running", true);
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        EditorApplication.isPlaying = true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool("UiLayout.Running", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            var probe = new GameObject("UI layout verification"); Object.DontDestroyOnLoad(probe); probe.AddComponent<UiLayoutProbe>();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool("UiLayout.Running", false);
            Environment.SetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY", SessionState.GetString("UiLayout.PreviousSave", ""));
            EditorApplication.Exit(File.ReadAllText(Root + "results.txt").Contains("FAIL") ? 1 : 0);
        }
    }
}

public static class UiLayoutAssertions
{
    public const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    public static Rect Bounds(RectTransform target, RectTransform relative)
    {
        var corners = new Vector3[4]; target.GetWorldCorners(corners);
        var lower = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var upper = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var corner in corners)
        {
            Vector2 point = relative.InverseTransformPoint(corner);
            lower = Vector2.Min(lower, point); upper = Vector2.Max(upper, point);
        }
        return Rect.MinMaxRect(lower.x, lower.y, upper.x, upper.y);
    }
    public static bool Contains(Rect outer, Rect inner, float tolerance = 1) =>
        inner.xMin >= outer.xMin - tolerance && inner.yMin >= outer.yMin - tolerance &&
        inner.xMax <= outer.xMax + tolerance && inner.yMax <= outer.yMax + tolerance;
    public static bool OnScreen(RectTransform rect)
    {
        var root = rect.GetComponentInParent<Canvas>().rootCanvas;
        var camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        return corners.All(corner =>
        {
            var point = RectTransformUtility.WorldToScreenPoint(camera, corner);
            return point.x >= -1 && point.y >= -1 && point.x <= Screen.width + 1 && point.y <= Screen.height + 1;
        });
    }
    public static PointerEventData Pointer(RectTransform rect)
    {
        var root = rect.GetComponentInParent<Canvas>().rootCanvas;
        var camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        return new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(camera,
            rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
    }
    public static bool Reachable(Button button)
    {
        if (!button.isActiveAndEnabled || !button.IsInteractable() || !OnScreen((RectTransform)button.transform)) return false;
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(Pointer((RectTransform)button.transform), hits);
        return hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform));
    }
    public static void Click(Button button)
    {
        if (!Reachable(button)) throw new Exception("Button is off-screen or occluded: " + button.name);
        ExecuteEvents.Execute(button.gameObject, Pointer((RectTransform)button.transform), ExecuteEvents.pointerClickHandler);
    }
    public static bool TextFits(Text body) => body.preferredHeight <= body.rectTransform.rect.height + 1;
    public static Rect TextBounds(Text text, RectTransform relative)
    {
        var vertices = text.cachedTextGenerator.verts;
        if (vertices.Count <= 4) throw new Exception("Text has no rendered glyphs: " + text.name);
        var lower = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var upper = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < vertices.Count - 4; i++)
        {
            Vector2 point = relative.InverseTransformPoint(text.rectTransform.TransformPoint(vertices[i].position / text.pixelsPerUnit));
            lower = Vector2.Min(lower, point); upper = Vector2.Max(upper, point);
        }
        return Rect.MinMaxRect(lower.x, lower.y, upper.x, upper.y);
    }
    public static bool MapCoversViewport(CompanyMapView map)
    {
        var content = Field<RectTransform>(map, "nodeParent");
        var viewport = (RectTransform)content.parent;
        return Contains(Bounds(content, viewport), viewport.rect);
    }
}

internal sealed class FixedGameViewSize : IDisposable
{
    readonly EditorWindow view;
    readonly object group;
    readonly PropertyInfo selected;
    readonly int previous, customIndex, builtIn;
    public FixedGameViewSize(int width, int height)
    {
        var assembly = typeof(UnityEditor.Editor).Assembly;
        var viewType = assembly.GetType("UnityEditor.GameView", true);
        view = EditorWindow.GetWindow(viewType);
        selected = viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        previous = (int)selected.GetValue(view);
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
        var sizes = sizesType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
        var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType", true);
        group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
        customIndex = (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
        builtIn = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
        var sizeType = assembly.GetType("UnityEditor.GameViewSize", true);
        var kind = Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType", true), "FixedResolution");
        var size = Activator.CreateInstance(sizeType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new object[] { kind, width, height, "TakeOver UI verification" }, null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        selected.SetValue(view, builtIn + customIndex); view.Show(); view.Focus(); view.Repaint();
    }
    public void Dispose()
    {
        selected.SetValue(view, previous);
        group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { builtIn + customIndex });
        view.Repaint();
    }
}

public sealed class UiLayoutProbe : MonoBehaviour
{
    float deadline;
    FixedGameViewSize resolution;
    void OnEnable() { Application.logMessageReceived += OnLog; }
    void OnDisable() { Application.logMessageReceived -= OnLog; }
    void OnLog(string text, string trace, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) File.AppendAllText(UiLayoutVerification.Root + "results.txt", "FAIL runtime: " + text + "\n" + trace + "\n"); }
    void Update()
    { if (deadline > 0 && Time.realtimeSinceStartup > deadline) { File.AppendAllText(UiLayoutVerification.Root + "results.txt", "FAIL: timeout\n"); Finish(); } }
    void Check(bool pass, string text)
    { if (!pass) throw new Exception(text); File.AppendAllText(UiLayoutVerification.Root + "results.txt", "PASS: " + text + "\n"); }
    void Finish() { deadline = 0; resolution?.Dispose(); resolution = null; EditorApplication.isPlaying = false; }
    IEnumerator Start()
    {
        deadline = Time.realtimeSinceStartup + 180;
        var stack = new Stack<IEnumerator>(); stack.Push(Verify());
        while (stack.Count > 0)
        {
            var current = stack.Peek(); bool more; object next;
            try { more = current.MoveNext(); next = more ? current.Current : null; }
            catch (Exception error) { File.AppendAllText(UiLayoutVerification.Root + "results.txt", "FAIL: " + error + "\n"); break; }
            if (!more) stack.Pop(); else if (next is IEnumerator child) stack.Push(child); else yield return next;
        }
        File.AppendAllText(UiLayoutVerification.Root + "results.txt", "COMPLETE\n"); Finish();
    }
    IEnumerator Frames(int count = 3) { for (int i = 0; i < count; i++) yield return null; Canvas.ForceUpdateCanvases(); }
    IEnumerator Capture(string name)
    {
        ScreenCapture.CaptureScreenshot(Path.Combine(UiLayoutVerification.Root, name + ".png"));
        yield return Frames(4);
    }
    Button Named(string name) => Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == name);
    IEnumerator Verify()
    {
        yield return Frames(10);
        Check(HelpTopics.Items.All(topic => !string.IsNullOrWhiteSpace(topic.title) && !string.IsNullOrWhiteSpace(topic.text) &&
            topic.title != "주인공 이름" && topic.title != "다시 시작하기" &&
            !System.Text.RegularExpressions.Regex.IsMatch(topic.text, @"\d+일차|\d+%")), "Help authoring is complete and omits tutorial spoilers, without an arbitrary length cap");
        foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
        {
            resolution = new FixedGameViewSize(size.x, size.y); yield return Frames(8);
            Check(Screen.width == size.x && Screen.height == size.y, "Actual Game View resolution " + size);
            yield return VerifyHelp(size);
            yield return VerifyMap(size);
            yield return VerifyHover(size);
            resolution.Dispose(); resolution = null;
        }
        var registry = Object.FindFirstObjectByType<NpcStateRegistry>();
        var status = Object.FindFirstObjectByType<NpcStatusScreen>(FindObjectsInactive.Include);
        Object.FindFirstObjectByType<TopBarNavigationController>().Navigate(DashboardTab.Npcs);
        yield return Frames();
        var portrait = status.ProfileRect.GetComponentsInChildren<Image>(true).Single(i => i.name == "StandingIllustration");
        status.SelectNpc("npc-blueharbor-retail-data-director-han-dogyeong");
        Check(!portrait.enabled && portrait.sprite == null, "Unillustrated NPC does not show substituted art");
        Check(registry.Profiles.All(npc => npc.profile.personalitySummary?.Contains("행동 양식:") != true &&
            npc.profile.gameplayRoleSummary?.Contains("선호:") != true), "NPC introductions use narrative prose");
        status.SelectNpc(StoryDirector.Representative); Check(portrait.enabled && portrait.sprite != null, "Registered standing art remains visible"); status.Close();
        var news = Object.FindFirstObjectByType<NewsFeed>(); int count = news.Entries.Count;
        news.SendMessage("addTestNews");
        Check(news.Entries.Count == count + 3 && news.Entries.All(entry => !string.IsNullOrWhiteSpace(entry.description)), "News cards have article text");
        yield return VerifyAfterTutorial();
    }
    IEnumerator VerifyAfterTutorial()
    {
        // Keep the former HelpVerification coverage of the dialogue clone being destroyed on completion.
        SceneManager.LoadScene("MainMenu"); yield return Frames();
        Object.FindFirstObjectByType<TakeOver.MainMenu.MainMenuController>().startWithTutorialChoice(true);
        yield return SessionSaveVerification.EnterProtagonistName("도움말 검증");
        while (GameObject.Find("Tutorial completion notice") == null) yield return null;
        Check(!StoryDirector.IsRunning, "Actual tutorial skip finishes before showing its reward notice");
        UiLayoutAssertions.Click(Named("Confirm tutorial reward")); yield return Frames();
        UiLayoutAssertions.Click(Named("Open help")); yield return Frames();
        var help = Object.FindFirstObjectByType<HelpScreen>();
        Check(help.IsOpen, "Help opens after the actual tutorial dialogue clone is destroyed");
        int imageTopic = Array.FindIndex(HelpTopics.Items, topic => !string.IsNullOrEmpty(topic.image));
        UiLayoutAssertions.Click(Named("Help topic " + imageTopic)); yield return Frames();
        for (int cycle = 0; cycle < 3; cycle++)
        {
            UiLayoutAssertions.Click(UiLayoutAssertions.Field<Image>(help, "photo").GetComponent<Button>()); yield return Frames();
            UiLayoutAssertions.Click(Named("Close enlarged image")); yield return Frames();
            Check(GameObject.Find("Help enlarged image") == null, "Photo opens and closes repeatedly after actual tutorial completion: " + cycle);
        }
        UiLayoutAssertions.Click(Named("Close help")); yield return Frames();
        Check(!help.IsOpen, "Help closes after actual tutorial completion");
        foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
        {
            resolution = new FixedGameViewSize(size.x, size.y); yield return Frames(8);
            foreach (bool loadOnly in new[] { false, true })
            {
                TakeOver.SaveSystem.SaveSlotWindow.Show(Object.FindFirstObjectByType<EventNegotiationView>(FindObjectsInactive.Include), loadOnly);
                yield return Frames();
                yield return Capture((loadOnly ? "load-slots-" : "save-slots-") + size.x + "x" + size.y);
                Check(UiLayoutAssertions.Reachable(Named("Close saves")) &&
                    (loadOnly || Enumerable.Range(1, 3).All(slot => UiLayoutAssertions.Reachable(Named("Save slot " + slot + " save")))),
                    "Slot separators leave save and close controls reachable: " + loadOnly + " / " + size);
                UiLayoutAssertions.Click(Named("Close saves")); yield return Frames();
            }
            resolution.Dispose(); resolution = null;
        }
    }
    IEnumerator VerifyHelp(Vector2Int size)
    {
        var nav = Object.FindFirstObjectByType<TopBarNavigationController>(); nav.OpenHelp(); yield return Frames();
        var help = Object.FindFirstObjectByType<HelpScreen>();
        var scroll = UiLayoutAssertions.Field<ScrollRect>(help, "scroll");
        var body = UiLayoutAssertions.Field<Text>(help, "body");
        var title = UiLayoutAssertions.Field<Text>(help, "heading");
        var close = Named("Close help");
        var panel = (RectTransform)close.transform.parent;
        yield return Capture("help-" + size.x + "x" + size.y);
        Check(UiLayoutAssertions.OnScreen(panel) && !UiLayoutAssertions.TextBounds(title, panel).Overlaps(
            UiLayoutAssertions.Bounds((RectTransform)close.transform, panel)), "Help panel and non-overlapping header controls fit " + size);
        Check(panel.GetComponent<Image>().sprite != null && panel.GetComponent<Image>().type == Image.Type.Sliced &&
            (close.targetGraphic as Image)?.sprite != null, "Help keeps the shared event panel and action artwork " + size);
        foreach (var index in Enumerable.Range(0, HelpTopics.Items.Length))
        {
            UiLayoutAssertions.Click(Named("Help topic " + index)); yield return Frames();
            Check(UiLayoutAssertions.TextFits(body), "Full help content has sufficient layout height: " + HelpTopics.Items[index].title + " / " + size);
            Check(UiLayoutAssertions.OnScreen(scroll.viewport), "Help reading area stays on-screen: " + index + " / " + size);
            if (scroll.content.rect.height > scroll.viewport.rect.height + 1) yield return ScrollToBottom(scroll, size.ToString());
            Check(UiLayoutAssertions.Reachable(close), "Help close button remains reachable: " + index + " / " + size);
        }
        // Exercise the actual scroll handler even when today's short help topics fit without scrolling.
        string authored = body.text;
        body.text = string.Join("\n", Enumerable.Repeat("긴 설명도 마지막 줄까지 읽을 수 있어야 합니다. 검증용 문단입니다.", 40));
        yield return Frames();
        Check(scroll.content.rect.height > scroll.viewport.rect.height, "Long-content fixture actually overflows the viewport " + size);
        yield return ScrollToBottom(scroll, "long-content fixture / " + size);
        var fitter = body.GetComponent<ContentSizeFitter>(); fitter.enabled = false;
        var originalHeight = body.rectTransform.rect.height;
        body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 10);
        Check(!UiLayoutAssertions.TextFits(body), "Readability check rejects a clipped text fixture " + size);
        body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, originalHeight); fitter.enabled = true; body.text = authored;
        yield return Frames();
        var blocker = new GameObject("QA click obstruction", typeof(RectTransform), typeof(Image));
        blocker.transform.SetParent(panel, false); var blockRect = (RectTransform)blocker.transform;
        blockRect.anchorMin = Vector2.zero; blockRect.anchorMax = Vector2.one; blockRect.offsetMin = blockRect.offsetMax = Vector2.zero;
        yield return Frames();
        Check(!UiLayoutAssertions.Reachable(close), "Click check rejects an obscured close button " + size);
        blocker.SetActive(false); Object.Destroy(blocker);
        for (int i = 0; i < HelpTopics.Items.Length; i++)
        {
            UiLayoutAssertions.Click(Named("Help topic " + i)); yield return Frames();
            var photo = UiLayoutAssertions.Field<Image>(help, "photo");
            Check(string.IsNullOrEmpty(HelpTopics.Items[i].image) || photo.sprite != null, "Configured help image exists: " + i);
            if (!photo.gameObject.activeSelf) continue;
            UiLayoutAssertions.Click(photo.GetComponent<Button>()); yield return Frames();
            Check(UiLayoutAssertions.OnScreen(GameObject.Find("Help enlarged image").GetComponent<RectTransform>()), "Enlarged image overlay fits " + size);
            UiLayoutAssertions.Click(Named("Close enlarged image")); yield return Frames();
            Check(GameObject.Find("Help enlarged image") == null, "Actual pointer click closes the enlarged image " + size);
        }
        UiLayoutAssertions.Click(close); yield return Frames(); Check(!help.IsOpen, "Actual close click dismisses help " + size);
        HelpScreen.ShowTutorialCompletion(); yield return Frames();
        var confirm = Named("Confirm tutorial reward");
        var rewardTitle = confirm.transform.parent.GetComponentsInChildren<Text>().Single(t => t.text == "튜토리얼 완료");
        var notice = (RectTransform)confirm.transform.parent;
        Check(UiLayoutAssertions.OnScreen(notice) && UiLayoutAssertions.TextFits(rewardTitle) &&
            !UiLayoutAssertions.TextBounds(rewardTitle, notice).Overlaps(UiLayoutAssertions.Bounds((RectTransform)confirm.transform, notice)),
            "Tutorial reward notice fits with readable title and reachable confirmation " + size);
        UiLayoutAssertions.Click(confirm); yield return Frames();
        UiLayoutAssertions.Click(Named("Open help")); yield return Frames();
        Check(help.IsOpen, "Tutorial help notice opens help through its actual button " + size);
        UiLayoutAssertions.Click(Named("Close help")); yield return Frames();
    }
    IEnumerator ScrollToBottom(ScrollRect scroll, string context)
    {
        scroll.verticalNormalizedPosition = 1; scroll.StopMovement(); yield return Frames();
        var pointer = UiLayoutAssertions.Pointer(scroll.viewport); pointer.scrollDelta = new Vector2(0, -10000);
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.scrollHandler) != null,
            "Wheel input reaches the reading scroll view: " + context);
        yield return Frames();
        float bottom = UiLayoutAssertions.Bounds(scroll.content, scroll.viewport).yMin;
        Check(scroll.verticalNormalizedPosition < .01f && Mathf.Abs(bottom - scroll.viewport.rect.yMin) < 2,
            "Wheel scroll exposes the final content line: " + context);
    }
    IEnumerator VerifyMap(Vector2Int size)
    {
        var nav = Object.FindFirstObjectByType<TopBarNavigationController>(); nav.Navigate(DashboardTab.Map); yield return Frames(5);
        var map = Object.FindFirstObjectByType<CompanyMapView>();
        Check(map != null && UiLayoutAssertions.MapCoversViewport(map), "Map artwork covers the viewport " + size);
        var content = UiLayoutAssertions.Field<RectTransform>(map, "nodeParent"); var viewport = (RectTransform)content.parent;
        var nodes = map.GetComponentsInChildren<CompanyMapNode>();
        Check(nodes.Length == Object.FindFirstObjectByType<Companies>().CompanyList.Count, "Every listed company has a map node " + size);
        var detail = Object.FindFirstObjectByType<CompanyDetailPanel>(FindObjectsInactive.Include);
        foreach (var node in nodes)
        {
            Check(node.GetComponentsInChildren<Graphic>().Where(g => g.isActiveAndEnabled).All(g =>
                UiLayoutAssertions.Contains(viewport.rect, UiLayoutAssertions.Bounds(g.rectTransform, viewport))), "Node artwork and logo fit the map viewport: " + node.company.definition.companyID + " / " + size);
            UiLayoutAssertions.Click(node.GetComponentInChildren<Button>()); yield return Frames();
            Check(detail.gameObject.activeInHierarchy, "Map pin click opens company details: " + node.company.definition.companyID);
            detail.hide(); yield return Frames();
        }
        var story = new GameObject("Tutorial spotlight layout probe").AddComponent<StoryView>();
        story.Build(AssetDatabase.LoadAssetAtPath<StorySequence>("Assets/Story/Data/IntroSequence.asset"), null);
        story.transform.Find("Letterbox").gameObject.SetActive(false); story.transform.Find("Fade curtain").gameObject.SetActive(false);
        typeof(StoryView).GetField("gated", UiLayoutAssertions.Private).SetValue(story, true);
        yield return Frames(); story.SpotlightCompany(); Canvas.ForceUpdateCanvases();
        var outline = UiLayoutAssertions.Field<Image>(story, "outline").rectTransform;
        yield return Capture("map-highlight-" + size.x + "x" + size.y);
        var target = nodes.Single(n => n.company.definition.companyID == StoryDirector.CompanyId);
        Check(target.GetComponentsInChildren<Graphic>().Where(g => g.isActiveAndEnabled).All(g =>
            UiLayoutAssertions.Contains(outline.rect, UiLayoutAssertions.Bounds(g.rectTransform, outline), 0)), "Tutorial frame encloses the entire Mirae logo and label " + size);
        Check(UiLayoutAssertions.Reachable(target.GetComponentInChildren<Button>()), "Tutorial masks leave the highlighted company clickable " + size);
        var topBar = Object.FindFirstObjectByType<TopBarNavigationController>().transform.Find("TopBar_Background") as RectTransform;
        foreach (bool profile in new[] { false, true })
        {
            typeof(StoryView).GetMethod("ShowInformation", UiLayoutAssertions.Private).Invoke(story, new object[] {
                "안내", "상단바와 겹치지 않는 튜토리얼 설명창 검증", profile ? new Rect(24, 100, 312, 536) : new Rect(24, 160, 312, 520), true });
            yield return Frames();
            yield return Capture("tutorial-side-" + (profile ? "profile-" : "general-") + size.x + "x" + size.y);
            var panelCorners = new Vector3[4]; var barCorners = new Vector3[4];
            story.InformationRect.GetWorldCorners(panelCorners); topBar.GetWorldCorners(barCorners);
            Check(panelCorners[2].y <= barCorners[0].y - 1,
                "Tutorial side panel stays below the top bar: profile=" + profile + " / " + size + " / panelTop=" + panelCorners[2].y + " barBottom=" + barCorners[0].y);
            Check(UiLayoutAssertions.OnScreen(story.InformationRect), "Lowered tutorial side panel stays fully on screen " + profile + " / " + size);
        }
        story.gameObject.SetActive(false); Object.Destroy(story.gameObject); nav.Navigate(DashboardTab.Home); yield return Frames();
    }

    void Hover(Button button, bool inside)
    {
        if (inside) Check(UiLayoutAssertions.Reachable(button), "Hover target receives pointer raycasts: " + button.name);
        var pointer = UiLayoutAssertions.Pointer((RectTransform)button.transform);
        if (inside) ExecuteEvents.ExecuteHierarchy(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        else ExecuteEvents.ExecuteHierarchy(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
    }
    static bool SameColor(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a) < .001f;
    IEnumerator VerifyHover(Vector2Int size)
    {
        var nav = Object.FindFirstObjectByType<TopBarNavigationController>(); nav.Navigate(DashboardTab.Npcs); yield return Frames();
        var status = Object.FindFirstObjectByType<NpcStatusScreen>();
        var profile = status.ProfileRect.GetComponent<NpcProfilePanelView>();
        var rows = UiLayoutAssertions.Field<List<NpcListItemView>>(profile, "_spawnedItems");
        var roster = Object.FindFirstObjectByType<NpcStateRegistry>().Profiles;
        var orderedProfiles = rows.Select(row => roster.Single(seed => seed.profile.displayName ==
            UiLayoutAssertions.Field<TMPro.TextMeshProUGUI>(row, "nameText").text)).ToArray();
        Check(orderedProfiles.Length == roster.Count && orderedProfiles.Select(seed => seed.npcId).Distinct().Count() == roster.Count,
            "NPC sorting retains every registered character exactly once " + size);
        Check(orderedProfiles.Take(5).All(seed => seed.companyId == "external-network") &&
            orderedProfiles.Skip(5).All(seed => seed.companyId != "external-network"), "All five neutral external NPCs appear first " + size);
        var expectedCompanies = new[] { "external-network", "mirae-construction", "haneul-food", "dongbang-trading", "hansung-industry",
            "aurora-foodtech", "blueharbor-retail", "daeseong-industry", "polaris-finance", "sinseong-development" };
        Check(orderedProfiles.Select(seed => seed.companyId).SequenceEqual(expectedCompanies.SelectMany(company =>
            roster.Where(seed => seed.companyId == company).Select(seed => seed.companyId))),
            "NPC groups follow mission appearance, then remaining company IDs alphabetically " + size);
        UiLayoutAssertions.Click(rows[0].GetComponent<Button>()); yield return Frames();
        var description = UiLayoutAssertions.Field<TMPro.TextMeshProUGUI>(profile, "descriptionText");
        var profileScroll = UiLayoutAssertions.Field<ScrollRect>(profile, "profileDetailsScroll");
        var profileHeader = UiLayoutAssertions.Field<TMPro.TextMeshProUGUI>(profile, "introHeaderText");
        Check(description.text.StartsWith("<b>성향</b>") && description.text.Contains("<b>기억</b>"), "Profile scrolling starts with traits and includes memories " + size);
        Check(profileScroll.viewport.GetComponentsInChildren<Image>().Count(image => image.name.StartsWith("Profile details border")) == 4 &&
            !profileHeader.transform.IsChildOf(profileScroll.viewport), "Profile has a thin scroll frame below its fixed introduction title " + size);
        Check(description.transform.Find("Traits section divider") != null && description.transform.Find("Memories section divider") != null,
            "Traits and memory separators scroll with the profile content " + size);
        Check(rows.All(row => row.GetComponent<Button>().IsInteractable()) && (profile.GetComponent<RectMask2D>() == null || !profile.GetComponent<RectMask2D>().enabled),
            "Normal NPC tab keeps every NPC selectable and has no tutorial-only standing clip " + size);
        Vector3 headerPosition = profileHeader.transform.position, textPosition = description.transform.position;
        yield return Capture("npc-profile-top-" + size.x + "x" + size.y);
        yield return ScrollToBottom(profileScroll, "NPC profile " + size);
        Check(profileHeader.transform.position == headerPosition && description.transform.position.y > textPosition.y,
            "Traits and remaining sections move inside the frame while the main title stays fixed " + size);
        yield return Capture("npc-profile-scrolled-" + size.x + "x" + size.y);
        profileScroll.verticalNormalizedPosition = 1; profileScroll.StopMovement(); yield return Frames();
        var selected = rows[0]; var candidate = rows[1];
        var selectedBg = UiLayoutAssertions.Field<Image>(selected, "backgroundImage");
        var candidateBg = UiLayoutAssertions.Field<Image>(candidate, "backgroundImage");
        Color selectedColor = selectedBg.color, normalColor = candidateBg.color;
        string selectedId = status.SelectedNpcId;
        var candidateRect = (RectTransform)candidate.transform; Vector2 rowSize = candidateRect.sizeDelta;
        Hover(candidate.GetComponent<Button>(), true); yield return Frames();
        Check(candidateBg.color.r > selectedColor.r && candidateBg.color.g > selectedColor.g && candidateBg.color.b > selectedColor.b &&
            candidateBg.color.grayscale < normalColor.grayscale, "NPC hover uses a slightly lighter shade of the selected navy " + size);
        Check(candidateBg.color.grayscale > Color.Lerp(selectedColor, Color.white, .14f).grayscale,
            "NPC hover is lighter than the previous fourteen-percent tint " + size);
        Check(status.SelectedNpcId == selectedId && candidateRect.sizeDelta == rowSize, "NPC hover changes neither selection nor row layout " + size);
        Check(SameColor(UiLayoutAssertions.Field<TMPro.TextMeshProUGUI>(candidate, "nameText").color, Color.white), "Hovered NPC label stays readable on navy " + size);
        yield return Capture("npc-hover-" + size.x + "x" + size.y);
        Hover(candidate.GetComponent<Button>(), false); yield return Frames();
        Check(SameColor(candidateBg.color, normalColor), "Unselected NPC restores its normal color on pointer exit " + size);
        Hover(selected.GetComponent<Button>(), true); yield return Frames();
        Check(!SameColor(selectedBg.color, selectedColor), "Selected NPC also responds to pointer hover " + size);
        Hover(selected.GetComponent<Button>(), false); yield return Frames();
        Check(SameColor(selectedBg.color, selectedColor), "Selected NPC retains its selection after pointer exit " + size);
        Hover(candidate.GetComponent<Button>(), true); UiLayoutAssertions.Click(candidate.GetComponent<Button>()); yield return Frames();
        Check(status.SelectedNpcId != selectedId, "Hover does not prevent the real NPC selection click " + size);
        Hover(candidate.GetComponent<Button>(), false); yield return Frames();
        Check(SameColor(candidateBg.color, selectedColor) && SameColor(selectedBg.color, normalColor), "Click transfers selection without leaving stale hover colors " + size);
        Hover(candidate.GetComponent<Button>(), true); nav.Navigate(DashboardTab.Home); yield return Frames();
        nav.Navigate(DashboardTab.Npcs); yield return Frames();
        Check(!UiLayoutAssertions.Field<List<NpcListItemView>>(profile, "_spawnedItems").Any(row => UiLayoutAssertions.Field<bool>(row, "hovered")), "Closing and reopening the NPC tab clears hover state " + size);
        nav.Navigate(DashboardTab.Home); yield return Frames();

        var helpTab = Named("HelpTab");
        var tabLabel = helpTab.GetComponentInChildren<TMPro.TMP_Text>(); Color normalTabLabel = tabLabel.color;
        Hover(helpTab, true); yield return Frames();
        Check(!SameColor(tabLabel.color, normalTabLabel) && nav.CurrentTab == DashboardTab.Home,
            "Top-bar Help hover highlights the label without opening the tab " + size);
        yield return Capture("help-tab-hover-" + size.x + "x" + size.y);
        Hover(helpTab, false); yield return Frames();
        Check(SameColor(tabLabel.color, normalTabLabel), "Top-bar Help restores its original label on pointer exit " + size);
        UiLayoutAssertions.Click(helpTab); yield return Frames();
        var help = Object.FindFirstObjectByType<HelpScreen>(); var body = UiLayoutAssertions.Field<Text>(help, "body");
        var topic = Named("Help topic 3"); var topicLabel = topic.GetComponentInChildren<Text>();
        Color normalTopicLabel = topicLabel.color; string originalBody = body.text;
        Hover(topic, true); yield return Frames();
        Check(topic.targetGraphic.GetComponent<Outline>().enabled && !SameColor(topicLabel.color, normalTopicLabel), "Help topic hover adds the shared blue glow and label accent " + size);
        Check(body.text == originalBody, "Hovering a help topic does not select it " + size);
        yield return Capture("help-topic-hover-" + size.x + "x" + size.y);
        UiLayoutAssertions.Click(topic); yield return Frames();
        Check(body.text.StartsWith(HelpTopics.Items[3].title), "Help topic still opens through its actual click " + size);
        Hover(topic, false); yield return Frames();
        Check(topic.targetGraphic.GetComponent<Outline>().enabled && !SameColor(topicLabel.color, normalTopicLabel), "Selected help topic stays lit after pointer exit " + size);
        yield return Capture("help-topic-selected-" + size.x + "x" + size.y);
        var otherTopic = Named("Help topic 4");
        Color selectedLabel = topicLabel.color;
        Hover(otherTopic, true); yield return Frames();
        Check(SameColor(topicLabel.color, selectedLabel) && !SameColor(selectedLabel, otherTopic.GetComponentInChildren<Text>().color), "Selection stays distinct while hovering another help topic " + size);
        UiLayoutAssertions.Click(otherTopic); Hover(otherTopic, false); yield return Frames();
        Check(SameColor(topicLabel.color, normalTopicLabel) && otherTopic.targetGraphic.GetComponent<Outline>().enabled, "Persistent help highlight follows the selected topic " + size);
        topic.interactable = false; HoverDisabled(topic); yield return Frames();
        Check(!UiLayoutAssertions.Field<bool>(topic.GetComponent<TakeOver.UI.ButtonHoverFeedback>(), "hovered"), "Disabled help buttons do not show hover feedback " + size);
        topic.interactable = true; Hover(topic, true); help.Close(); yield return Frames(); help.Open(); yield return Frames();
        Check(!UiLayoutAssertions.Field<bool>(topic.GetComponent<TakeOver.UI.ButtonHoverFeedback>(), "hovered") &&
            SameColor(topicLabel.color, normalTopicLabel), "Closing help while hovered clears the visual state " + size);
        nav.Navigate(DashboardTab.Home); yield return Frames();
    }
    void HoverDisabled(Button button)
    {
        ExecuteEvents.ExecuteHierarchy(button.gameObject, UiLayoutAssertions.Pointer((RectTransform)button.transform), ExecuteEvents.pointerEnterHandler);
    }
}
