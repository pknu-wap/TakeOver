using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TakeOver.Events;
using TakeOver.MainMenu;
using TakeOver.NPC;
using TakeOver.SaveSystem;
using TakeOver.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SessionSaveVerification
{
    public const string Root = "../QA_PairedCampaign/Session/";
    static SessionSaveVerification() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        Directory.CreateDirectory(Root); File.WriteAllText(Root + "results.txt", "RUNNING\n");
        SessionState.SetString("Campaign.SessionPreviousRoot", Environment.GetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY") ?? "");
        Environment.SetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY", Path.GetFullPath(Root + "save"));
        SessionState.SetBool("Campaign.SessionSave", true);
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); EditorApplication.isPlaying = true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool("Campaign.SessionSave", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        { var go = new GameObject("Session save verification"); Object.DontDestroyOnLoad(go); go.AddComponent<SessionSaveProbe>(); }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool("Campaign.SessionSave", false);
            Environment.SetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY", SessionState.GetString("Campaign.SessionPreviousRoot", ""));
            EditorApplication.Exit(File.ReadAllText(Root + "results.txt").Contains("FAIL") ? 1 : 0);
        }
    }
}
public sealed class SessionSaveProbe : MonoBehaviour
{
    float timeout;
    void Check(bool value, string text) { if (!value) throw new Exception(text); File.AppendAllText(SessionSaveVerification.Root + "results.txt", "PASS: " + text + "\n"); }
    void Update() { if (timeout > 0 && Time.realtimeSinceStartup > timeout) { File.AppendAllText(SessionSaveVerification.Root + "results.txt", "FAIL: timeout\n"); EditorApplication.isPlaying = false; timeout = 0; } }
    IEnumerator Start()
    {
        timeout = Time.realtimeSinceStartup + 100;
        var test = Verify();
        while (true)
        {
            bool next; object value = null;
            try { next = test.MoveNext(); if (next) value = test.Current; }
            catch (Exception error) { File.AppendAllText(SessionSaveVerification.Root + "results.txt", "FAIL: " + error); break; }
            if (!next) break;
            yield return value;
        }
        timeout = 0; EditorApplication.isPlaying = false;
    }
    IEnumerator Verify()
    {
        yield return null; yield return null;
        Object.FindFirstObjectByType<MainMenuController>().startWithTutorialChoice(true);
        while (GameSessionCoordinator.Active == null || !GameSessionCoordinator.Active.Ready || StoryDirector.Active == null) yield return null;
        while (StoryDirector.IsRunning) yield return null;
        foreach (string name in new[] { "Confirm tutorial reward", "Confirm help notice" })
        {
            yield return null;
            var button = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == name);
            if (button != null) button.onClick.Invoke();
        }
        for (int i = 0; i < 5; i++) yield return null;
        var manager = Object.FindFirstObjectByType<GameSaveManager>();
        var clock = Object.FindFirstObjectByType<GameTurnClock>();
        var game = Object.FindFirstObjectByType<EventSystemController>();
        string root = GameSaveManager.SaveDirectory, path = manager.savePath;
        Check(clock.CurrentTurn == 2 && File.Exists(path), "Actual new-game session automatically saves tutorial completion");
        Check(manager.HasCampaignCheckpoint(game.CampaignRunId, "mirae-initial"), "Real tutorial completion creates an independent preparation checkpoint");
        Check(manager.tryReadDayStart(out var day) && day.day == 2, "Automatic save contains the completed tutorial day");
        string blocker = Path.GetFullPath(SessionSaveVerification.Root + "blocked-file"); File.WriteAllText(blocker, "fixture");
        Environment.SetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY", blocker);
        clock.AdvanceTurn(); for (int i = 0; i < 4; i++) yield return null;
        Check(clock.CurrentTurn == 3 && manager.hasPendingSave, "A write failure retains the completed day snapshot");
        var trade = Object.FindFirstObjectByType<ShareTradingSystem>();
        var initialCompany = Object.FindFirstObjectByType<Companies>().getCompany("mirae-construction");
        Check(!trade.buyShares(initialCompany, 1) && !trade.sellShares(initialCompany, 1), "Pending save blocks trades that would invalidate its snapshot");
        var offered = game.getTodayEvents().FirstOrDefault(); if (offered != null) game.openEvent(offered.getId());
        Check(game.IsClosed, "Pending save blocks new event mutations");
        float cash = Object.FindFirstObjectByType<Player>().state.cash;
        clock.AdvanceTurn(); yield return null;
        Check(clock.CurrentTurn == 3 && Object.FindFirstObjectByType<Player>().state.cash == cash, "Failed save blocks a second economy tick");
        Environment.SetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY", root);
        clock.AdvanceTurn(); yield return null;
        Check(clock.CurrentTurn == 3 && !manager.hasPendingSave && manager.tryReadDayStart(out day) && day.day == 3, "Retry writes the same day without advancing or rerunning settlement");
        clock.AdvanceTurn(); for (int i = 0; i < 4; i++) yield return null;
        Check(clock.CurrentTurn == 4 && manager.tryReadDayStart(out day) && day.day == 4, "Daily progression and automatic saving resume after retry");
        string expected = game.captureJson(); cash = Object.FindFirstObjectByType<Player>().state.cash;
        SceneManager.LoadScene("MainMenu"); yield return null; yield return null;
        Object.FindFirstObjectByType<MainMenuController>().startContinue();
        while (GameSessionCoordinator.Active == null || !GameSessionCoordinator.Active.Ready) yield return null;
        yield return null;
        game = Object.FindFirstObjectByType<EventSystemController>(); manager = Object.FindFirstObjectByType<GameSaveManager>();
        Check(Object.FindFirstObjectByType<GameTurnClock>().CurrentTurn == 4 && cash == Object.FindFirstObjectByType<Player>().state.cash && expected == game.captureJson(), "Actual Continue restores the automatic save without replay");

        // Current whole-game format plus a legacy event section: retain old Mirae completion,
        // start the added campaign at day 6 relative to an already-advanced old playthrough.
        var oldFile = JObject.Parse(File.ReadAllText(CampaignTestFixtures.PathFor("legacy-v5-progress.json")));
        oldFile["formatVersion"] = 3;
        var sections = (JArray)oldFile["sections"];
        foreach (var item in sections.Where(s => (string)s["id"] == "news" || (string)s["id"] == "documents").ToArray()) item.Remove();
        var eventSection = sections.Single(s => (string)s["id"] == "events"); eventSection["version"] = 4;
        var payload = (JObject)eventSection["data"];
        payload["mandatoryState"] = (int)MandatoryStoryState.Completed; payload["mandatoryFailureStake"] = 0;
        foreach (string key in new[] { "mandatoryEventId", "campaignDayOffset", "repeatHistory", "lastOccurrenceSerial", "politicalFavorGranted" }) payload.Remove(key);
        foreach (string key in new[] { "choices", "companyChoices", "storyResults" })
            foreach (var item in ((JArray)payload[key]).Where(s => ((string)s["eventId"]).StartsWith("main_", StringComparison.Ordinal)).ToArray()) item.Remove();
        payload["occurrences"] = new JArray(); payload["cooldowns"] = new JArray();
        File.WriteAllText(path, oldFile.ToString());
        Check(manager.tryLoad(out _), "Legacy whole-game v3/event v4 save migrates: " + manager.lastError);
        Check(game.MandatoryState == MandatoryStoryState.Forced && game.getTodayEvents().Single().getId() == "main_mirae_02", "Old completed Mirae save starts the added arc without immediately failing later missions");
        var forced = JsonUtility.FromJson<EventSystemController.EventSaveData>(game.captureJson());
        forced.occurrences.Clear(); bool rejected = false; string before = game.captureJson();
        try { game.prepareLoad(JsonUtility.ToJson(forced), game.saveVersion, new DayStartData(forced.day, FixedDaySeed.seed)); } catch (InvalidDataException) { rejected = true; }
        Check(rejected && before == game.captureJson(), "Missing forced-story offer is rejected before live state changes");
        File.WriteAllText(path, "{broken");
        Check(!manager.tryLoad(out _) && before == game.captureJson(), "Corrupt save is rejected without mutating the live event state");
        File.AppendAllText(SessionSaveVerification.Root + "results.txt", "COMPLETE\n");
    }
}
