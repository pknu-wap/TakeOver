using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TakeOver.Events;
using TakeOver.Journal;
using TakeOver.NPC;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class DialogueInteractionVerification
{
    public static readonly string Output = Path.GetFullPath("../QA_DialogueInteraction");
    private const string Key = "TakeOver.DialogueInteractionQA";
    public static bool StoryOnly => SessionState.GetBool(Key + ".StoryOnly", false);
    public static void RunStoryOnly() { SessionState.SetBool(Key + ".StoryOnly", true); Run(); }
    public static void RunAndBuild() { SessionState.SetBool(Key + ".Build", true); Run(); }
    static DialogueInteractionVerification() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/results.txt", "RUNNING\n");
        Environment.SetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY", Output + "/Save");
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        EditorApplication.isPlaying = true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode) new GameObject("Dialogue interaction QA").AddComponent<DialogueInteractionProbe>();
        if (state != PlayModeStateChange.EnteredEditMode) return;
        SessionState.SetBool(Key, false);
        SessionState.SetBool(Key + ".StoryOnly", false);
        bool failed = File.ReadAllText(Output + "/results.txt").Contains("FAIL");
        bool build = SessionState.GetBool(Key + ".Build", false);
        SessionState.SetBool(Key + ".Build", false);
        if (!failed && build)
        {
            Environment.SetEnvironmentVariable("TAKEOVER_QA_OUTPUT", Path.GetFullPath("../output/dialogue-interaction-build"));
            StoryEventVerification.Build();
        }
        else EditorApplication.Exit(failed ? 1 : 0);
    }
}

public sealed class DialogueInteractionProbe : MonoBehaviour
{
    private FixedGameViewSize size;
    private Keyboard keyboard;
    private float deadline;
    private int passed;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private void Check(bool value, string label)
    {
        if (!value) throw new Exception(label);
        passed++; File.AppendAllText(DialogueInteractionVerification.Output + "/results.txt", "PASS " + label + "\n");
    }
    private void OnEnable() { Application.logMessageReceived += Log; }
    private void OnDisable() { Application.logMessageReceived -= Log; }
    private void Log(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            File.AppendAllText(DialogueInteractionVerification.Output + "/results.txt", "FAIL " + message + "\n" + trace + "\n");
    }
    private IEnumerator Start()
    {
        deadline = Time.realtimeSinceStartup + 120;
        var routine = Verify();
        while (true)
        {
            bool next;
            try { next = routine.MoveNext(); }
            catch (Exception error) { File.AppendAllText(DialogueInteractionVerification.Output + "/results.txt", "FAIL " + error + "\n"); break; }
            if (!next) break;
            yield return routine.Current;
        }
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        size?.Dispose();
        File.AppendAllText(DialogueInteractionVerification.Output + "/results.txt", "COMPLETE PASS=" + passed + "\n");
        EditorApplication.isPlaying = false;
    }
    private void Update()
    {
        if (deadline > 0 && Time.realtimeSinceStartup > deadline)
        { deadline = 0; File.AppendAllText(DialogueInteractionVerification.Output + "/results.txt", "FAIL timeout\n"); EditorApplication.isPlaying = false; }
    }
    private void Key(Key key, bool down) => InputSystem.QueueStateEvent(keyboard, down ? new KeyboardState(key) : new KeyboardState());
    private IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(DialogueInteractionVerification.Output + "/" + name + ".png");
        yield return null;
    }
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Flags).GetValue(target);
    private sealed class DayBlocker : ITurnBlocker
    {
        public bool IsBlocking(out string reason) { reason = "QA day lock"; return true; }
    }
    private IEnumerator Verify()
    {
        for (int i = 0; i < 30; i++) yield return null;
        keyboard = InputSystem.AddDevice<Keyboard>();
        if (DialogueInteractionVerification.StoryOnly)
        {
            var story = VerifyStory();
            while (story.MoveNext()) yield return story.Current;
            yield break;
        }
        var original = Object.FindFirstObjectByType<EventNegotiationView>(FindObjectsInactive.Include);
        var view = Object.Instantiate(original, original.transform.parent);
        var registry = Object.FindFirstObjectByType<NpcStateRegistry>();
        registry.TryGetProfile(TakeOver.Story.StoryDirector.Representative, out var profile);
        int advanced = 0, chosen = -1;
        foreach (int width in new[] {1280, 1920})
        {
            size = new FixedGameViewSize(width, width * 9 / 16);
            for (int i = 0; i < 8; i++) yield return null;
            view.ShowDialogue(new DialoguePresentation { speaker = profile.GetSpeakerLabel(),
                npcStanding = profile.profile.standingIllustration,
                text = "오늘 회의에서는 회사의 미래를 함께 이야기해 보겠습니다.\n선택하기 전에 조건을 충분히 확인해 주세요." },
                () => advanced++, index => chosen = index, null, () => false);
            view.SetDialogueInput(true); yield return null; yield return null;
            Check(!view.ContinueButton.gameObject.activeSelf, width + " visible continue button removed");
            Check(view.transform.Find("Dialogue Log").GetComponent<Button>().GetComponent<Image>() == null, width + " Log is text only");
            Check(view.CanAdvanceDialogue, width + " line ready");
            yield return Capture("dialogue-" + width);
            Key(UnityEngine.InputSystem.Key.Space, true); yield return null; yield return null;
            Key(UnityEngine.InputSystem.Key.Space, false); yield return null;
            Check(advanced == (width == 1280 ? 1 : 3), width + " space advances once");
            var surface = view.transform.Find("Dialogue click area").GetComponent<Button>();
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, surface.transform.position) };
            var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && hits[0].gameObject == surface.gameObject, width + " dialogue body raycast reaches click surface");
            ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
            Check(advanced == (width == 1280 ? 2 : 4), width + " dialogue surface click advances once");
            view.SetDialogueInput(false);
            view.RequestDialogueAdvance();
            Check(!view.CanAdvanceDialogue, width + " disabled dialogue rejects input");
            view.SetDialogueInput(true);
            view.ShowDialogue(new DialoguePresentation { player = true, speaker = "주인공", text = "말씀하신 조건을 확인했습니다.\n검토한 내용을 바탕으로 결정하겠습니다." }, () => advanced++, index => chosen = index, null, () => false);
            view.SetDialogueInput(true); yield return null; yield return null;
            view.OpenDialogueLog(); yield return null; yield return null;
            Check(!view.CanAdvanceDialogue, width + " log blocks advancement");
            var log = view.GetComponentInChildren<DialogueLogWindow>();
            Check(ReadLog(log).Contains("오늘 회의"), width + " log includes displayed speaker and text");
            Check(log.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Log separator ")) == view.DialogueLogEntries.Count, width + " every dialogue entry has a separator");
            Check(log.transform.Find("Event style information").GetComponent<Image>().color.a < 1, width + " log background is translucent");
            yield return Capture("log-" + width);
            Key(UnityEngine.InputSystem.Key.Escape, true); yield return null; yield return null;
            Key(UnityEngine.InputSystem.Key.Escape, false); yield return null;
            Check(view.GetComponentInChildren<DialogueLogWindow>() == null, width + " escape closes log");
            Check(!Object.FindFirstObjectByType<EscMenu>().isOpen && Time.timeScale > 0, width + " log escape does not also open pause menu");
            view.ShowDialogue(new DialoguePresentation { speaker = "선택", text = "어떻게 하시겠습니까?", choices = new[] { "동의한다", "거절한다" } },
                () => advanced++, index => chosen = index, null, () => false);
            view.SetDialogueInput(true); yield return null; yield return null;
            Check(!view.CanAdvanceDialogue && !surface.gameObject.activeSelf, width + " choices disable line continuation");
            Key(UnityEngine.InputSystem.Key.Space, true); yield return null; yield return null;
            Key(UnityEngine.InputSystem.Key.Space, false); yield return null;
            Check(chosen == -1, width + " space never selects answer");
            yield return Capture("choices-" + width);
            view.GetComponentsInChildren<Button>().First(b => b.name == "Choice1").onClick.Invoke();
            Check(chosen == 1, width + " choice click dispatches selected answer"); chosen = -1;
            view.ShowDialogue(new DialoguePresentation { narration = true, text = string.Join("\n", Enumerable.Repeat("긴 대사가 여러 페이지로 나뉘어도 같은 입력으로 읽을 수 있습니다.", 30)) },
                () => {}, _ => {}, null, () => false);
            view.SetDialogueInput(true); yield return null; yield return null;
            Check(view.HasMorePages, width + " long line is paginated");
            string before = view.CurrentDialogue.text;
            view.RequestDialogueAdvance(); yield return null;
            Check(Field<int>(view, "page") == 1, width + " continuation moves one page");
            view.hide(); yield return null;
            size.Dispose(); size = null;
        }
        // Scope follows a dialogue scene, rather than the Unity Game scene or a single line.
        var firstScene = new object(); var secondScene = new object();
        view.ShowDialogue(new DialoguePresentation { sceneKey = firstScene, speaker = "A", text = "첫 장면 첫 대사" }, () => {}, _ => {}, null, () => false);
        view.ShowDialogue(new DialoguePresentation { sceneKey = firstScene, speaker = "B", text = "첫 장면 둘째 대사" }, () => {}, _ => {}, null, () => false);
        Check(view.DialogueLogText.Contains("첫 장면 첫 대사") && view.DialogueLogText.Contains("첫 장면 둘째 대사"), "same scene retains different speakers");
        view.ShowDialogue(new DialoguePresentation { sceneKey = secondScene, text = "둘째 장면 대사" }, () => {}, _ => {}, null, () => false);
        Check(view.DialogueLogText.Contains("둘째 장면") && !view.DialogueLogText.Contains("첫 장면"), "scene key change clears previous dialogue with same backdrop");
        view.hide();
        Check(string.IsNullOrEmpty(view.DialogueLogText), "closing conversation clears its log");
        view.ShowDialogue(new DialoguePresentation { sceneKey = secondScene, text = "다시 연 장면" }, () => {}, _ => {}, null, () => false);
        Check(!view.DialogueLogText.Contains("둘째 장면"), "reopening does not restore previous scene log");
        view.hide();
        var encounter = new object();
        string npcA = TakeOver.Story.StoryDirector.Representative, npcB = TakeOver.Story.StoryDirector.FinancialDirector;
        view.ShowDialogue(new DialoguePresentation { sceneKey = encounter, npcId = npcA, text = "같은 상대의 첫 대사" }, () => {}, _ => {}, null, () => false);
        view.SetDialogueInput(true); yield return null; yield return null;
        view.ShowDialogue(new DialoguePresentation { sceneKey = encounter, npcId = npcA, text = "같은 상대의 다음 대사" }, () => {}, _ => {}, null, () => false);
        view.SetDialogueInput(true);
        Check(!EventSceneTransition.IsBusy, "same NPC consecutive lines have no blackout");
        view.ShowDialogue(new DialoguePresentation { sceneKey = encounter, npcId = npcA, player = true, text = "주인공의 답변" }, () => {}, _ => {}, null, () => false);
        view.SetDialogueInput(true);
        Check(!EventSceneTransition.IsBusy, "NPC to player speaking turn has no blackout");
        view.ShowDialogue(new DialoguePresentation { sceneKey = encounter, npcId = npcB, text = "교체된 상대의 대사" }, () => {}, _ => {}, null, () => false);
        view.SetDialogueInput(true);
        Check(EventSceneTransition.IsBusy, "different NPC in same scene starts short blackout");
        yield return new WaitForSecondsRealtime(.35f);
        Check(!EventSceneTransition.IsBusy && view.CurrentDialogue.text == "교체된 상대의 대사" && view.CanAdvanceDialogue, "NPC blackout presents new line and restores input");
        bool skipped = false;
        view.ShowDialogue(new DialoguePresentation { sceneKey = encounter, npcId = npcB, text = "같은 상대 스킵" }, () => {}, _ => {}, () => skipped = true, () => true, fadeSkip: true);
        view.SetDialogueInput(true); yield return null; yield return null;
        view.SkipToNextChoice();
        Check(EventSceneTransition.IsBusy, "skip retains blackout even without NPC change");
        yield return new WaitForSecondsRealtime(.8f);
        Check(skipped && !EventSceneTransition.IsBusy, "skip callback still completes after blackout");
        view.hide();
        // Exercise the older event exchange coroutine as well as shared story rendering.
        view.gameObject.SetActive(true);
        typeof(EventNegotiationView).GetField("dialogueMode", Flags).SetValue(view, false);
        int exchanged = 0;
        view.showExchange("다음 조건을 확인한 뒤 답변해 주세요.", "조건을 먼저 듣겠습니다.", () => exchanged++);
        yield return new WaitForSecondsRealtime(.7f);
        Check(view.CanAdvanceDialogue, "native player reply uses same surface");
        view.RequestDialogueAdvance();
        yield return new WaitForSecondsRealtime(.9f);
        Check(exchanged == 1, "native exchange finishes at answer boundary");
        Check(view.DialogueLogText.Contains("조건을 먼저 듣겠습니다."), "native reply recorded in scene log");
        view.hide();
        var navigation = Object.FindFirstObjectByType<TopBarNavigationController>();
        Check(navigation.Navigate(DashboardTab.Log), "journal opens");
        var journal = Object.FindFirstObjectByType<JournalScreen>();
        var person = new JournalPerson { npcId = TakeOver.Story.StoryDirector.Representative, displayName = "강준혁" };
        // Use the journal's actual chip pool path, including portrait-free people.
        typeof(JournalScreen).GetMethod("showPeople", Flags).Invoke(journal, new object[] { new System.Collections.Generic.List<JournalPerson> { person } });
        Field<GameObject>(journal, "cardRoot").SetActive(true);
        var chip = journal.GetComponentInChildren<JournalPersonChip>();
        Check(chip.GetComponent<Button>().interactable, "registered person chip clickable without portrait");
        chip.GetComponent<Button>().onClick.Invoke(); yield return null;
        Check(navigation.CurrentTab == DashboardTab.Npcs, "journal chip navigates to NPC tab");
        Check(Object.FindFirstObjectByType<NpcStatusScreen>().SelectedNpcId == person.npcId, "journal chip selects exact NPC");
        yield return Capture("journal-to-npc");
        Check(!navigation.OpenNpc("missing-npc"), "unregistered NPC cannot change selection");
        navigation.ReturnHome();
        var clock = Object.FindFirstObjectByType<GameTurnClock>();
        int turn = clock.CurrentTurn;
        var blocker = new DayBlocker(); clock.RegisterBlocker(blocker);
        Object.FindFirstObjectByType<DaySkipController>().OnClickNextDay();
        Check(!EventSceneTransition.IsBusy && clock.CurrentTurn == turn, "blocked day does not play transition or settle");
        clock.UnregisterBlocker(blocker);
        Object.FindFirstObjectByType<DaySkipController>().OnClickNextDay();
        Check(EventSceneTransition.IsBusy && clock.CurrentTurn == turn, "day change starts fade before settlement");
        Object.FindFirstObjectByType<DaySkipController>().OnClickNextDay();
        yield return new WaitForSecondsRealtime(.2f);
        Check(EventSceneTransition.Opacity > 0 && clock.CurrentTurn == turn, "fade darkens old day");
        yield return Capture("day-fade");
        yield return new WaitForSecondsRealtime(.75f);
        Check(clock.CurrentTurn == turn + 1 && !EventSceneTransition.IsBusy, "day settles once and fade returns");
        yield return Capture("day-after");
        Object.Destroy(view.gameObject);
        yield return null;
        var storyCheck = VerifyStory();
        while (storyCheck.MoveNext()) yield return storyCheck.Current;
    }

    private IEnumerator VerifyStory()
    {
        // A real StoryDirector creates its own shared renderer from the native template.
        var sequence = ScriptableObject.CreateInstance<TakeOver.Story.StorySequence>();
        sequence.fadeSeconds = .05f;
        sequence.beats.Add(new TakeOver.Story.StoryBeat { id = "qa-1", sceneTitle = "QA", speaker = TakeOver.Story.StorySpeaker.Npc,
            npcId = TakeOver.Story.StoryDirector.Representative, text = "대본의 첫 번째 대사입니다." });
        sequence.beats.Add(new TakeOver.Story.StoryBeat { id = "qa-2", sceneTitle = "QA", speaker = TakeOver.Story.StorySpeaker.Player,
            text = "대본의 두 번째 대사입니다." });
        sequence.beats.Add(new TakeOver.Story.StoryBeat { id = "qa-npc-change", sceneTitle = "QA", speaker = TakeOver.Story.StorySpeaker.Npc,
            npcId = TakeOver.Story.StoryDirector.FinancialDirector, text = "같은 장면에 새 인물이 등장합니다." });
        sequence.beats.Add(new TakeOver.Story.StoryBeat { id = "qa-3", sceneTitle = "Another scene", speaker = TakeOver.Story.StorySpeaker.Npc,
            npcId = TakeOver.Story.StoryDirector.Representative, text = "새로운 장면의 대사입니다." });
        var storyRoot = new GameObject("QA story"); storyRoot.SetActive(false);
        var director = storyRoot.AddComponent<TakeOver.Story.StoryDirector>();
        typeof(TakeOver.Story.StoryDirector).GetField("sequence", Flags).SetValue(director, sequence);
        storyRoot.SetActive(true);
        yield return new WaitForSecondsRealtime(1f);
        var shared = director.View.SharedDialogue;
        File.AppendAllText(DialogueInteractionVerification.Output + "/results.txt", $"STORY index={director.BeatIndex} busy={director.Busy} active={shared.isActiveAndEnabled} canAdvance={shared.CanAdvanceDialogue} input={Field<bool>(shared, "dialogueInput")} continue={Field<bool>(shared, "continueVisible")} transition={EventSceneTransition.IsBusy} timeScale={Time.timeScale} bodyActive={shared.CurrentDialogue.gameObject.activeInHierarchy}\n");
        yield return Capture("story-before");
        Check(director.BeatIndex == 0 && director.View.SharedDialogue.CanAdvanceDialogue, "actual story clone is ready for space input");
        Check(director.View.SharedDialogue.transform.Cast<Transform>().Count(t => t.name == "Dialogue Log") == 1, "story clone has a single Log control");
        Key(UnityEngine.InputSystem.Key.Space, true); yield return null; yield return null;
        Key(UnityEngine.InputSystem.Key.Space, false);
        bool uninterrupted = true;
        float until = Time.unscaledTime + .5f;
        while (Time.unscaledTime < until)
        {
            uninterrupted &= shared.GetComponent<CanvasGroup>().alpha >= .999f && director.View.FadeAlpha <= .001f && !EventSceneTransition.IsBusy;
            yield return null;
        }
        Check(uninterrupted, "actual tutorial NPC to player line stays visible on every frame");
        Check(director.BeatIndex == 1 && director.View.Dialogue.text.Contains("두 번째"), "actual story space advances NPC to player line");
        Check(shared.DialogueLogText.Contains("첫 번째") && shared.DialogueLogText.Contains("두 번째"), "actual story keeps both speakers within current scene");
        yield return Capture("story-player");
        shared.RequestDialogueAdvance();
        Check(EventSceneTransition.IsBusy, "actual tutorial NPC replacement starts blackout");
        yield return new WaitForSecondsRealtime(.35f);
        Check(director.BeatIndex == 2 && shared.CurrentDialogue.text.Contains("새 인물") && !EventSceneTransition.IsBusy, "actual tutorial NPC replacement completes");
        shared.RequestDialogueAdvance();
        yield return new WaitForSecondsRealtime(.5f);
        Check(director.BeatIndex == 3 && shared.DialogueLogText.Contains("새로운 장면") && !shared.DialogueLogText.Contains("첫 번째") && !shared.DialogueLogText.Contains("두 번째"), "actual story scene boundary clears earlier lines even with same background");
        shared.OpenDialogueLog(); yield return null; yield return null;
        var logText = ReadLog(shared.GetComponentInChildren<DialogueLogWindow>());
        Check(logText.Contains("새로운 장면") && !logText.Contains("두 번째"), "visible Log window contains only current scene");
        yield return Capture("current-scene-log");
        Object.Destroy(storyRoot); Object.Destroy(sequence);
    }
    private static string ReadLog(DialogueLogWindow window) => string.Join("\n", window.GetComponentInChildren<ScrollRect>().content.GetComponentsInChildren<Text>().Select(t => t.text));
}
