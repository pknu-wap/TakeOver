using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TakeOver.Events;
using TakeOver.NPC;
using TakeOver.SaveSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class EventPersistenceVerification
{
    public const string Output = "../QA_EventPersistence/";
    static EventPersistenceVerification() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "results.txt", "RUNNING\n");
        SessionState.SetBool("EventPersistenceVerification", true);
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        EditorApplication.isPlaying = true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool("EventPersistenceVerification", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode) new GameObject("Event persistence regression").AddComponent<EventPersistenceProbe>();
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool("EventPersistenceVerification", false);
            EditorApplication.Exit(File.ReadAllText(Output + "results.txt").Contains("FAIL") ? 1 : 0);
        }
    }
}
public sealed class EventPersistenceTestSaveManager : GameSaveManager
{
    protected override SaveFileStorage storage => new SaveFileStorage(Path.GetFullPath(EventPersistenceVerification.Output + "save"));
}
public sealed class EventPersistenceTestGuard : MonoBehaviour, ITurnAdvanceGuard
{
    public bool blocked = true;
    public bool notified;
    public bool CanAdvanceTurn(int currentTurn) => !blocked;
    public void OnTurnAdvanceBlocked() => notified = true;
}
public sealed class EventPersistenceProbe : MonoBehaviour
{
    void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        File.AppendAllText(EventPersistenceVerification.Output + "results.txt", "PASS: " + message + "\n");
    }
    void OnEnable() { Application.logMessageReceived += Log; }
    void OnDisable() { Application.logMessageReceived -= Log; }
    void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            File.AppendAllText(EventPersistenceVerification.Output + "results.txt", "FAIL runtime: " + message + "\n" + stack + "\n");
    }
    IEnumerator Start()
    {
        yield return null; yield return null; yield return null;
        try { Verify(); }
        catch (Exception error) { File.AppendAllText(EventPersistenceVerification.Output + "results.txt", "FAIL: " + error + "\n"); }
        EditorApplication.isPlaying = false;
    }
    void Verify()
    {
        var game = Object.FindFirstObjectByType<EventSystemController>();
        var clock = Object.FindFirstObjectByType<GameTurnClock>();
        var player = Object.FindFirstObjectByType<Player>();
        var npc = Object.FindFirstObjectByType<NpcSaveService>();
        var ap = Object.FindFirstObjectByType<ActionPointService>();
        var companies = Object.FindFirstObjectByType<Companies>();
        var company = companies.getCompany(TakeOver.Story.StoryDirector.CompanyId);
        const string eventId = "political_donation_01";
        string initialEvents = game.captureJson();
        string initialCards = string.Join("|", game.getTodayEventInfos().Select(x => x.definition.getId() + ":" + x.companyId + ":" + x.remainingDays));
        Check(game.getTodayEventInfos().Count > 0, "Initial save contains active offers with remaining deadlines");
        game.onDayStarted(game.getCurrentDay() + 5);
        var randomBefore = UnityEngine.Random.state;
        game.prepareLoad(initialEvents, 1, new DayStartData(clock.CurrentTurn, FixedDaySeed.seed));
        game.applyLoad(); game.rebuildAfterLoad();
        Check(initialEvents == game.captureJson() && initialCards == string.Join("|", game.getTodayEventInfos().Select(x => x.definition.getId() + ":" + x.companyId + ":" + x.remainingDays)), "Active company, first day and remaining deadlines restore without rerolling");
        Check(UnityEngine.Random.state.Equals(randomBefore), "Card restoration consumes no randomness");
        player.state.addCash(50 - player.state.cash);
        var baselineNpc = npc.captureJson();
        int confirmed = 0;
        game.choiceConfirmed += _ => confirmed++;
        game.openEvent(eventId); game.startNegotiation();
        bool duringMeetingRejected = false;
        try { game.captureJson(); } catch (InvalidOperationException) { duringMeetingRejected = true; }
        Check(duringMeetingRejected, "Unanswered meeting cannot be saved after spending AP");
        Check(!game.IsClosed && game.ActiveEventId == eventId, "Paid event opens through real scene controller");
        var view = Object.FindFirstObjectByType<EventNegotiationView>();
        var buttons = (List<Button>)typeof(EventNegotiationView).GetField("choices", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
        buttons[0].onClick.Invoke();
        Check(player.state.cash == 50 && confirmed == 0 && !game.tryGetChoice(eventId, out _), "Insufficient cash commits no money, completion or notification");
        Check(npc.captureJson() == baselineNpc, "Rejected paid choice changes no NPC relations or memories");
        Check(buttons.All(x => x.interactable), "Failed UI submission unlocks all answer buttons");
        var notice = Object.FindFirstObjectByType<FloatingMessage>();
        var label = (Text)typeof(FloatingMessage).GetField("label", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(notice);
        Check(label.text.Contains("부족"), "Insufficient cash shows formal feedback");
        buttons[1].onClick.Invoke();
        game.confirmChoice(1);
        Check(player.state.cash == 0 && confirmed == 1 && game.tryGetChoice(eventId, out var answer) && answer == 1, "Exact balance pays once and commits affordable alternative");
        game.closeResult();
        var guard = new GameObject("Test deadline gate").AddComponent<EventPersistenceTestGuard>();
        int before = clock.CurrentTurn;
        int apBefore = ap.Current;
        Object.FindFirstObjectByType<DaySkipController>().OnClickNextDay();
        Check(clock.CurrentTurn == before && ap.Current == apBefore && guard.notified, "Blocked day leaves turn and AP untouched and invokes redirection hook");
        guard.blocked = false;
        clock.AdvanceTurn();
        Check(clock.CurrentTurn == before + 1, "Cleared deadline allows normal daily processing");
        for (int i = 0; i < 4; i++) clock.AdvanceTurn();
        typeof(CompanyState).GetProperty("greenmailBuyBlocked").SetValue(company.state, true);
        ap.TryConsume(3);
        var manager = new GameObject("Test whole-game save").AddComponent<EventPersistenceTestSaveManager>();
        int savedTurn = clock.CurrentTurn;
        var nextDay = new DayStartData(savedTurn + 1, FixedDaySeed.seed);
        Check(manager.trySaveDayEnd(nextDay), "Whole GameSaveManager writes scene participants: " + manager.lastError);
        var storage = new SaveFileStorage(Path.GetFullPath(EventPersistenceVerification.Output + "save"));
        var file = JsonUtility.FromJson<SaveFileData>(storage.read());
        Check(new HashSet<string>(file.sections.Select(x => x.id)).SetEquals(new[] { "trading", "events", "npc", "action-points" }), "Trading, event, NPC and AP sections are all registered");
        string expectedEvents = game.captureJson(), expectedNpc = npc.captureJson();
        int expectedAp = ap.Current;
        var savedEvents = JsonUtility.FromJson<EventSystemController.EventSaveData>(expectedEvents);
        Check(savedEvents.cooldowns.Count > 0, "Expired offers retain cooldown entries");
        int turnNotifications = 0;
        clock.TurnAdvanced += _ => turnNotifications++;
        clock.AdvanceTurn(); player.state.addCash(123 - player.state.cash);
        typeof(CompanyState).GetProperty("greenmailBuyBlocked").SetValue(company.state, false); ap.Restore();
        turnNotifications = 0;
        Check(manager.tryLoad(out var day), "Whole GameSaveManager loads all sections: " + manager.lastError);
        Check(game.captureJson() == expectedEvents && npc.captureJson() == expectedNpc, "Event occurrence, expiry, cooldown, choice and NPC states round trip exactly");
        Check(player.state.cash == 0 && company.state.greenmailBuyBlocked && ap.Current == expectedAp, "Cash, permanent buy ban and spent AP survive whole-game load");
        Check(clock.CurrentTurn == savedTurn && day.day == savedTurn + 1 && turnNotifications == 0 && confirmed == 1, "Load neither advances time nor replays rewards");
        Check(!game.getTodayEvents().Any(x => x.getId() == eventId), "Completed event does not reappear after load");
        var eventSection = file.sections.Single(x => x.id == "events");
        var invalid = JsonUtility.FromJson<EventSystemController.EventSaveData>(eventSection.json);
        invalid.choices[0].choice = 999;
        eventSection.json = JsonUtility.ToJson(invalid);
        storage.write(JsonUtility.ToJson(file));
        player.state.addCash(321 - player.state.cash);
        Check(!manager.tryLoad(out _) && player.state.cash == 321 && game.captureJson() == expectedEvents, "Invalid event choice rejects entire load before any module mutation");
        invalid.choices[0].choice = savedEvents.choices[0].choice;
        invalid.occurrences.Add(new EventSystemController.TimingData { eventId = "unknown", companyId = "", day = savedTurn });
        bool rejected = false;
        try { game.prepareLoad(JsonUtility.ToJson(invalid), 1, nextDay); } catch (InvalidDataException) { rejected = true; }
        Check(rejected && game.captureJson() == expectedEvents, "Unknown event ID is rejected without changing runtime state");
        rejected = false;
        try { game.prepareLoad("{\"day\":1}", 1, nextDay); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "Missing required event lists are rejected");
        file.sections.Remove(eventSection);
        storage.write(JsonUtility.ToJson(file));
        Check(!manager.tryLoad(out _) && player.state.cash == 321, "Legacy save missing event state is rejected rather than replaying completed events");
        // 정본 테스트 파일은 정상 상태로 남긴다.
        eventSection.json = expectedEvents; file.sections.Add(eventSection); storage.write(JsonUtility.ToJson(file));
        Check(manager.tryLoad(out _), "Valid save remains loadable after rejected files");
        File.AppendAllText(EventPersistenceVerification.Output + "results.txt", "COMPLETE\n");
    }
}
