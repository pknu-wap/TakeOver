using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using TakeOver.Dashboard;
using TakeOver.Events;
using TakeOver.NPC;
using TakeOver.SaveSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class StoryDeadlineVerification
{
    public const string Output = "../QA_StoryDeadline/";
    static StoryDeadlineVerification() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "results.txt", "RUNNING\n");
        SessionState.SetBool("StoryDeadline.Verify", true);
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        var view = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
        view.position = new Rect(40, 40, 1280, 780); view.Show(); view.Focus();
        EditorApplication.isPlaying = true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool("StoryDeadline.Verify", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            var probe = new GameObject("Story deadline verification"); Object.DontDestroyOnLoad(probe); probe.AddComponent<StoryDeadlineProbe>();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool("StoryDeadline.Verify", false);
            EditorApplication.Exit(File.ReadAllText(Output + "results.txt").Contains("FAIL") ? 1 : 0);
        }
    }
}
public sealed class StoryDeadlineSaveManager : GameSaveManager
{
    protected override SaveFileStorage storage => new SaveFileStorage(Path.GetFullPath(StoryDeadlineVerification.Output + "save"));
}
public sealed class StoryDeadlineProbe : MonoBehaviour
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    EventSystemController game;
    GameTurnClock clock;
    Company company;
    StoryDeadlineBar bar;
    float timeout;
    void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        File.AppendAllText(StoryDeadlineVerification.Output + "results.txt", "PASS: " + description + "\n");
    }
    void OnEnable() { Application.logMessageReceived += Log; }
    void OnDisable() { Application.logMessageReceived -= Log; }
    void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            File.AppendAllText(StoryDeadlineVerification.Output + "results.txt", "FAIL runtime: " + message + "\n" + stack + "\n");
    }
    void Update()
    {
        if (timeout > 0 && Time.realtimeSinceStartup > timeout)
        { File.AppendAllText(StoryDeadlineVerification.Output + "results.txt", "FAIL: timeout\n"); EditorApplication.isPlaying = false; timeout = 0; }
    }
    IEnumerator Start()
    {
        timeout = Time.realtimeSinceStartup + 180;
        var stack = new Stack<IEnumerator>(); stack.Push(Verify());
        while (stack.Count > 0)
        {
            var routine = stack.Peek(); bool next = false; object current = null;
            try { next = routine.MoveNext(); if (next) current = routine.Current; }
            catch (Exception error) { File.AppendAllText(StoryDeadlineVerification.Output + "results.txt", "FAIL: " + error + "\n"); break; }
            if (!next) { stack.Pop(); continue; }
            if (current is IEnumerator nested) stack.Push(nested); else yield return current;
        }
        timeout = 0; EditorApplication.isPlaying = false;
    }
    IEnumerator Ready(bool reload = false)
    {
        if (reload) SceneManager.LoadScene("Game");
        yield return null; yield return null; yield return null; yield return null;
        game = Object.FindFirstObjectByType<EventSystemController>();
        clock = Object.FindFirstObjectByType<GameTurnClock>();
        company = Object.FindFirstObjectByType<Companies>().getCompany(EventSystemController.MandatoryCompanyId);
        bar = Object.FindFirstObjectByType<StoryDeadlineBar>();
    }
    void Stake(float percent)
    {
        company.state.setStat(CompanyStat.PLAYER_SHARES, company.state.totalShares * percent / 100f);
        company.calculator.calculateAboutShares(company);
        game.refreshAvailability();
    }
    void ToDay(int day) { while (clock.CurrentTurn < day) { int before = clock.CurrentTurn; clock.AdvanceTurn(); if (clock.CurrentTurn == before) throw new Exception("Unexpected turn guard before target day"); } }
    EventDefinition AddOrdinaryOffer()
    {
        var definition = Object.Instantiate(AssetDatabase.LoadAssetAtPath<EventDefinition>("Assets/EventSystem/Data/PressInterview.asset"));
        var data = JObject.Parse(JsonUtility.ToJson(definition)); data["eventId"] = "qa-preserved-ordinary"; data["validDays"] = 20;
        JsonUtility.FromJsonOverwrite(data.ToString(), definition);
        ((List<EventDefinition>)typeof(EventSystemController).GetField("events", Flags).GetValue(game)).Add(definition);
        return definition;
    }
    IEnumerator Verify()
    {
        yield return Ready();
        Check(bar != null && bar.Bar.gameObject.activeInHierarchy, "Mandatory bar exists below the top bar at game start");
        Check(bar.Requirement.text == "미래건설의 지분 10% 이상 인수하기" && bar.Countdown.text == "앞으로 4일", "Initial requirement and countdown show the requested wording");
        var source = bar.transform.Find("TopBar_Background").GetComponent<Image>();
        Check(bar.Bar.GetComponent<Image>().sprite == source.sprite && bar.Requirement.font != null, "Bar reuses actual top bar art and font");
        var divider = (RectTransform)bar.Bar.Find("Story deadline divider");
        Check(Mathf.Abs(divider.anchorMin.x - 7f / 11) < .0001f, "Divider gives the requested seven-to-four cell ratio");
        Check(bar.Requirement.preferredWidth < bar.Requirement.rectTransform.rect.width && bar.Countdown.preferredWidth < bar.Countdown.rectTransform.rect.width, "Both labels fit their cells");
        Check(!bar.Bar.GetComponent<Image>().raycastTarget && !bar.Requirement.raycastTarget, "Status bar does not block gameplay pointer input");
        yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.GetFullPath(StoryDeadlineVerification.Output + "deadline-bar.png")); yield return null;
        int signals = 0; StoryDeadlineFailureInfo info = null;
        game.BadEndingRequested += value => { signals++; info = value; };
        ToDay(4); Stake(9.9f); yield return null;
        Check(game.MandatoryState == MandatoryStoryState.Pending && signals == 0 && bar.Countdown.text == "앞으로 1일", "No failure before day five; day four has one day left");
        clock.AdvanceTurn();
        Check(clock.CurrentTurn == 5 && game.MandatoryState == MandatoryStoryState.Failed && signals == 0, "Entering day five below ten percent fails immediately before signaling");
        var blackout = GameObject.Find("Deadline blackout").GetComponent<Image>();
        Check(blackout.raycastTarget && blackout.color.a < 1 && game.getTodayEvents().Count == 0, "Failure starts fade and blocks input while ordinary cards are hidden");
        bool rejected = false;
        try { game.captureJson(); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Saving an unfinished fade is rejected");
        while (!game.IsMandatoryFailureReady) yield return null;
        Check(signals == 1 && blackout.color.a == 1 && info.eventId == EventSystemController.MandatoryEventId && info.failedDay == 5 && Mathf.Abs(info.stakeAtFailure - 9.9f) < .001f, "Bad ending request is emitted once only after a full blackout with failure context");
        clock.AdvanceTurn(); yield return null;
        Check(clock.CurrentTurn == 5 && signals == 1, "Failed state blocks further day advancement and duplicate requests");
        var manager = new GameObject("Isolated deadline save").AddComponent<StoryDeadlineSaveManager>();
        Check(manager.trySaveDayEnd(new DayStartData(6, FixedDaySeed.seed)), "Failed state saves through the whole-game manager: " + manager.lastError);
        Check(manager.tryLoad(out _), "Failed state loads through the whole-game manager: " + manager.lastError);
        yield return null; yield return null;
        Check(game.IsMandatoryFailureReady && signals == 2 && game.MandatoryState == MandatoryStoryState.Failed, "Loading failure restores blackout and redelivers the ending handoff once for the loaded session");

        yield return Ready(true);
        var ordinary = AddOrdinaryOffer();
        ToDay(4); Stake(10); yield return null;
        Check(game.getTodayEvents().Any(e => e.getId() == EventSystemController.MandatoryEventId) && game.getTodayEvents().Contains(ordinary), "Qualified story appears immediately on the same day alongside ordinary offers");
        string ordinaryTiming = JObject.Parse(game.captureJson())["occurrences"].First(e => (string)e["eventId"] == ordinary.getId()).ToString();
        clock.AdvanceTurn(); yield return null;
        Check(game.MandatoryState == MandatoryStoryState.Forced && game.getTodayEvents().Count == 1 && game.getTodayEvents()[0].getId() == EventSystemController.MandatoryEventId, "Day five at exactly ten percent shows only the mandatory event");
        Check(bar.Countdown.text == "앞으로 0일", "Forced day shows zero days remaining");
        game.openEvent(ordinary.getId());
        Check(game.IsClosed, "Hidden ordinary events cannot be opened through the controller");
        clock.AdvanceTurn();
        Check(clock.CurrentTurn == 5, "Required event blocks all turn API advancement before completion");
        manager = new GameObject("Forced deadline save").AddComponent<StoryDeadlineSaveManager>();
        Check(manager.trySaveDayEnd(new DayStartData(6, FixedDaySeed.seed)) && manager.tryLoad(out _), "Forced offer state round-trips through whole-game saving");
        Check(game.MandatoryState == MandatoryStoryState.Forced && game.getTodayEvents().Count == 1, "Load cannot bypass mandatory-only presentation");
        var legacy = JObject.Parse(game.captureJson()); legacy.Remove("mandatoryState"); legacy.Remove("mandatoryFailureStake");
        game.prepareLoad(legacy.ToString(), 2, new DayStartData(5, 1)); game.applyLoad(); game.rebuildAfterLoad();
        Check(game.MandatoryState == MandatoryStoryState.Forced && game.getTodayEvents().Count == 1, "Legacy event saves are evaluated against the same day-five rule");
        game.openEvent(EventSystemController.MandatoryEventId); game.startNegotiation();
        int ap = Object.FindFirstObjectByType<ActionPointService>().Current;
        game.confirmChoice(2); game.closeResult(); yield return null;
        Check(game.MandatoryState == MandatoryStoryState.Completed && !bar.Bar.gameObject.activeSelf && game.getTodayEvents().Contains(ordinary), "Completing the required event restores original offers and removes the objective");
        string restoredTiming = JObject.Parse(game.captureJson())["occurrences"].First(e => (string)e["eventId"] == ordinary.getId()).ToString();
        Check(ordinaryTiming == restoredTiming && Object.FindFirstObjectByType<ActionPointService>().Current == ap, "Restoring ordinary offers preserves their original timing and spends no additional AP");
        Stake(0); clock.AdvanceTurn();
        Check(clock.CurrentTurn == 6 && game.MandatoryState == MandatoryStoryState.Completed, "Normal day progression resumes after required event completion");

        yield return Ready(true);
        Stake(10); game.openEvent(EventSystemController.MandatoryEventId); game.startNegotiation(); game.confirmChoice(0); game.closeResult();
        Stake(0); int earlySignals = 0; game.BadEndingRequested += _ => earlySignals++;
        ToDay(5); yield return null;
        Check(game.MandatoryState == MandatoryStoryState.Completed && earlySignals == 0, "Earlier completion satisfies the obligation even if shares are later sold");
        Check(!bar.Bar.gameObject.activeSelf && GameObject.Find("Deadline blackout") == null, "Completed story does not show a deadline bar or ending transition");
        var invalid = JObject.Parse(game.captureJson()); invalid["mandatoryState"] = (int)MandatoryStoryState.Forced;
        rejected = false;
        try { game.prepareLoad(invalid.ToString(), game.saveVersion, new DayStartData(5, 1)); } catch (InvalidDataException) { rejected = true; }
        Check(rejected && game.MandatoryState == MandatoryStoryState.Completed, "Contradictory completion and deadline save state is rejected without mutation");
        File.AppendAllText(StoryDeadlineVerification.Output + "results.txt", "COMPLETE\n");
    }
}
