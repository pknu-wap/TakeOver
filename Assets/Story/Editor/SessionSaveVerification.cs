using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using TakeOver.Events;
using TakeOver.Documents;
using TakeOver.Employees;
using TakeOver.Help;
using TakeOver.Journal;
using TakeOver.MainMenu;
using TakeOver.NPC;
using TakeOver.SaveSystem;
using TakeOver.Story;
using TakeOver.Story.Editor;
using TakeOver.Startups;
using TMPro;
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

    internal static IEnumerator EnterProtagonistName(string name)
    {
        TMP_InputField input;
        while ((input = Object.FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None)
            .SingleOrDefault(field => field.name == "Name input")) == null) yield return null;
        yield return null;
        var confirm = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(button => button.name == "Confirm name");
        if (confirm.interactable || StoryDirector.Active != null || GameSessionCoordinator.Active.Ready)
            throw new InvalidOperationException("The name prompt must block both the introduction and the session until a name is set.");
        input.text = "  ";
        if (confirm.interactable) throw new InvalidOperationException("Whitespace should not enable confirmation.");
        input.text = name;
        if (!confirm.interactable) throw new InvalidOperationException("A valid name should enable confirmation.");
        StoryPlayProbe.Click(confirm);
        if (Object.FindFirstObjectByType<Player>().ProtagonistName != name.Trim())
            throw new InvalidOperationException("The confirmed protagonist name was not applied.");
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
        var namePrompt = SessionSaveVerification.EnterProtagonistName("  한서윤  ");
        while (namePrompt.MoveNext()) yield return null;
        Check(true, "Skipping the tutorial requires a name against the launch blackout");
        while (GameSessionCoordinator.Active == null || !GameSessionCoordinator.Active.Ready || StoryDirector.Active == null) yield return null;
        while (StoryDirector.Active != null) yield return null;
        foreach (string name in new[] { "Confirm tutorial reward", "Confirm help notice" })
        {
            yield return null;
            var button = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == name);
            Check(button != null, "Tutorial notice is visible: " + name);
            if (name == "Confirm tutorial reward")
            {
                var title = button.transform.parent.GetComponentsInChildren<Text>().Single(label => label.text == "튜토리얼 완료");
                Check(title.rectTransform.anchoredPosition.y == -22, "Shared tutorial notice title uses the lowered top inset");
            }
            button.onClick.Invoke();
        }
        for (int i = 0; i < 5; i++) yield return null;
        var manager = Object.FindFirstObjectByType<GameSaveManager>();
        var clock = Object.FindFirstObjectByType<GameTurnClock>();
        var game = Object.FindFirstObjectByType<EventSystemController>();
        var registry = Object.FindFirstObjectByType<NpcStateRegistry>();
        var tutorialJournal = JournalEntryBuilder.buildStoryEntries(game, registry);
        Check(tutorialJournal.Count == 8 && tutorialJournal.Count(item => item.resolvedDay == 1) == 5 &&
            tutorialJournal.Single(item => item.eventId == StoryDirector.TutorialEventId).resolvedDay == 2,
            "Skipped tutorial records seven conversations and the day-two financial review in the journal");
        var journalChoices = tutorialJournal.Select(item => (item.eventId, item.resolvedDay, item.keyPoints[0])).ToArray();
        Check(HelpTopics.Items.All(topic => topic.title != "주인공 이름" && topic.text.Length <= 130),
            "Help contains short system explanations and omits the protagonist-name topic");

        var status = Object.FindFirstObjectByType<NpcStatusScreen>(FindObjectsInactive.Include);
        status.Open();
        var panel = status.ProfileRect;
        var portrait = panel.GetComponentsInChildren<Image>(true).Single(image => image.name == "StandingIllustration");
        status.SelectNpc("npc-blueharbor-retail-data-director-han-dogyeong");
        Check(!portrait.enabled && portrait.sprite == null, "Han Dogyeong has no substituted standing sprite");
        Check(registry.Profiles.All(npc => npc.profile.personalitySummary?.Contains("행동 양식:") != true &&
            npc.profile.gameplayRoleSummary?.Contains("선호:") != true), "Registered profiles use narrative introductions");
        status.SelectNpc(StoryDirector.Representative);
        Check(portrait.enabled && portrait.sprite != null, "Characters with registered art still display it");
        status.Close();

        var newsFeed = Object.FindFirstObjectByType<NewsFeed>();
        int publishedCount = newsFeed.Entries.Count;
        newsFeed.SendMessage("addTestNews");
        Check(newsFeed.Entries.Count == publishedCount + 3 &&
            newsFeed.Entries.All(entry => !string.IsNullOrWhiteSpace(entry.description)),
            "Market and sample news entries have article text");
        string root = GameSaveManager.SaveDirectory, path = manager.savePath;
        Check(clock.CurrentTurn == 2 && File.Exists(path), "Actual new-game session automatically saves tutorial completion");
        Check(manager.HasCampaignCheckpoint(game.CampaignRunId, "mirae-initial"), "Real tutorial completion creates an independent preparation checkpoint");
        Check(manager.tryReadDayStart(out var day) && day.day == 2, "Automatic save contains the completed tutorial day");
        var employees = Object.FindFirstObjectByType<EmployeeService>();
        Check(employees.StartRecruit(), "A recruitment starts before the save failure");
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
        clock.AdvanceTurn();
        Check(clock.CurrentTurn == 4 && manager.tryReadDayStart(out day) && day.day == 4,
            "Entering the next day persists its offers before returning from day advance");
        for (int i = 0; i < 4; i++) yield return null;
        var applicant = employees.Applicants.First();
        Check(employees.Hire(applicant), "Hiring an applicant marks the session for saving");
        var jobs = Object.FindFirstObjectByType<JobService>();
        Check(jobs.Assign(applicant, JobType.Discovery), "An employee can start a discovery assignment");
        int assignmentDays = jobs.GetRemainingDays(applicant);
        Object.FindFirstObjectByType<StartupDiscoveryService>().SendMessage("createTestReport");
        var report = Object.FindFirstObjectByType<DocumentSystem>().Inbox.Open.OfType<StartupDiscoveryDocument>().First();
        var startupId = report.CompanyId;
        var startupName = report.CompanyName;
        var startupSystem = Object.FindFirstObjectByType<StartupSystem>();
        startupSystem.tryGetRecord(startupId, out var startup);
        startup.revenueHistory.Add(1234f);
        for (int i = 0; i < 3; i++) yield return null;
        string expected = game.captureJson(); cash = Object.FindFirstObjectByType<Player>().state.cash;
        SceneManager.LoadScene("MainMenu"); yield return null; yield return null;
        var continueButton = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .Single(button => button.name == "ContinueButton");
        Check(continueButton.interactable, "Title Continue button is enabled when an automatic save exists");
        continueButton.onClick.Invoke();
        while (GameSessionCoordinator.Active == null || !GameSessionCoordinator.Active.Ready)
        {
            if (GameSessionCoordinator.Active != null && !string.IsNullOrEmpty(GameSessionCoordinator.Active.LastError))
                throw new InvalidOperationException(GameSessionCoordinator.Active.LastError);
            yield return null;
        }
        yield return null;
        game = Object.FindFirstObjectByType<EventSystemController>(); manager = Object.FindFirstObjectByType<GameSaveManager>();
        registry = Object.FindFirstObjectByType<NpcStateRegistry>();
        Check(JournalEntryBuilder.buildStoryEntries(game, registry)
            .Select(item => (item.eventId, item.resolvedDay, item.keyPoints[0])).SequenceEqual(journalChoices),
            "Continue preserves the tutorial's journal order, dates and exact choices");
        Check(Object.FindFirstObjectByType<GameTurnClock>().CurrentTurn == 4 && cash == Object.FindFirstObjectByType<Player>().state.cash && expected == game.captureJson(), "Actual Continue restores the automatic save without replay");
        Check(Object.FindFirstObjectByType<Player>().ProtagonistName == "한서윤", "Continue restores the custom name");
        var restoredEmployee = Object.FindFirstObjectByType<EmployeeService>().Employees.Single();
        jobs = Object.FindFirstObjectByType<JobService>();
        Check(restoredEmployee.Id == applicant.Id && restoredEmployee.Name == applicant.Name &&
            restoredEmployee.ContractEndTurn == applicant.ContractEndTurn && jobs.GetJob(restoredEmployee) == JobType.Discovery &&
            jobs.GetRemainingDays(restoredEmployee) == assignmentDays, "Continue restores the hired employee, contract and assigned job");
        var restored = Object.FindFirstObjectByType<DocumentSystem>().Inbox.Open.OfType<StartupDiscoveryDocument>()
            .First(item => item.CompanyId == startupId);
        startupSystem = Object.FindFirstObjectByType<StartupSystem>();
        Check(startupSystem.tryGetRecord(startupId, out var restoredStartup) && restoredStartup.revenueHistory.Last() == 1234f &&
            restored.CompanyName == startupName && restored.TargetCompany == Object.FindFirstObjectByType<Companies>().getCompany(startupId),
            "Continue binds a saved discovery report to the original startup and preserves its history");

        var browser = Object.FindFirstObjectByType<CompanyBrowserScreen>(FindObjectsInactive.Include);
        browser.open();
        yield return null;
        var map = browser.GetComponentInChildren<CompanyMapView>();
        Check(map != null, "The company browser opens in map view");
        var nodes = map.GetComponentsInChildren<CompanyMapNode>();
        Check(nodes.Length == Object.FindFirstObjectByType<Companies>().CompanyList.Count, "The default map shows every listed company");
        var details = browser.GetComponentInChildren<CompanyDetailPanel>(true);
        foreach (var node in nodes)
        {
            StoryPlayProbe.Click(node.GetComponentInChildren<Button>());
            details.hide();
        }
        Check(true, "Every default map pin receives an actual screen-coordinate click");
        browser.close();

        string baseline = File.ReadAllText(path);
        var eventSave = VerifyEventSaveLoad(game, manager);
        while (eventSave.MoveNext()) yield return eventSave.Current;
        game = Object.FindFirstObjectByType<EventSystemController>();
        manager = Object.FindFirstObjectByType<GameSaveManager>();
        registry = Object.FindFirstObjectByType<NpcStateRegistry>();
        File.WriteAllText(path, baseline);
        Check(manager.tryLoad(out _), "Restore the baseline after testing event save/load: " + manager.lastError);

        var legacyRoster = JObject.Parse(File.ReadAllText(path));
        legacyRoster["formatVersion"] = 4;
        var rosterSections = (JArray)legacyRoster["sections"];
        rosterSections.Single(section => (string)section["id"] == "employees").Remove();
        var tradingSection = rosterSections.Single(section => (string)section["id"] == "trading");
        var tradingPayload = (JObject)tradingSection["data"];
        tradingPayload.Remove("hasStartupRoster");
        foreach (var company in ((JArray)tradingPayload["companies"]).Where(company => (bool?)company["generated"] == true).ToArray()) company.Remove();
        var legacyEvents = (JObject)rosterSections.Single(section => (string)section["id"] == "events")["data"];
        legacyEvents.Remove("tutorialDecisions");
        legacyEvents.Remove("tutorialVisitDay");
        File.WriteAllText(path, legacyRoster.ToString());
        Check(manager.tryLoad(out _), "A v4 save without an original startup roster still loads: " + manager.lastError);
        Check(Object.FindFirstObjectByType<DocumentSystem>().Inbox.Open.OfType<SnapshotDocument>()
            .Any(item => item.TypeId == StartupDiscoveryDocument.typeId && item.Title == restored.Title),
            "A legacy discovery report is readable without targeting a different startup with a recycled ID");
        Check(JournalEntryBuilder.buildStoryEntries(game, registry).Single().eventId == StoryDirector.TutorialEventId,
            "A legacy tutorial save displays its previously recorded financial review");

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

    private static string[] Cards()
    {
        var slide = Object.FindFirstObjectByType<DashboardCardSlide>();
        var field = typeof(DashboardCardSlide).GetField("displayEventList", BindingFlags.Instance | BindingFlags.NonPublic);
        return ((System.Collections.Generic.List<TodayEventInfo>)field.GetValue(slide))
            .Select(info => $"{info.definition.getId()}:{info.companyId}:{info.remainingDays}").ToArray();
    }

    private IEnumerator VerifyEventSaveLoad(EventSystemController game, GameSaveManager manager)
    {
        const string eventId = "neutral-lee-first-interview";
        var originalCards = Cards();
        var actions = Object.FindFirstObjectByType<ActionPointService>();
        int beforeAp = actions.Current;
        string beforeEvent = game.captureJson();
        string marketBefore = Object.FindFirstObjectByType<TradingSaveAdapter>().captureJson();
        var marketPrices = Object.FindFirstObjectByType<Companies>().CompanyList
            .Select(company => company.state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE)).ToArray();
        Check(originalCards.Length > 1 && game.getTodayEvents().Any(item => item.getId() == eventId), "Day four has several assigned cards to compare");
        game.openEvent(eventId);
        game.startNegotiation();
        while (EventSceneTransition.IsBusy) yield return null;
        Check(actions.Current < beforeAp, "Before-event save is written as soon as the player spends AP");

        SceneManager.LoadScene("MainMenu"); yield return null; yield return null;
        Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(button => button.name == "ContinueButton").onClick.Invoke();
        while (GameSessionCoordinator.Active == null || !GameSessionCoordinator.Active.Ready) yield return null;
        yield return null;
        game = Object.FindFirstObjectByType<EventSystemController>();
        actions = Object.FindFirstObjectByType<ActionPointService>();
        Check(game.captureJson() == beforeEvent && Cards().SequenceEqual(originalCards) && actions.Current == beforeAp,
            "Interrupting a negotiation and continuing preserves AP, exact offers and visible order");
        Check(Object.FindFirstObjectByType<TradingSaveAdapter>().captureJson() == marketBefore &&
            Object.FindFirstObjectByType<Companies>().CompanyList.Select(company => company.state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE))
                .Zip(marketPrices, (after, before) => Mathf.Approximately(after, before)).All(equal => equal),
            "Active and delayed market effects restore without changing the prices used by offer conditions");

        game.openEvent(eventId);
        game.startNegotiation();
        while (EventSceneTransition.IsBusy) yield return null;
        game.confirmChoice(0);
        while (EventSceneTransition.IsBusy) yield return null;
        game.closeResult();
        while (EventSceneTransition.IsBusy) yield return null;
        Check(game.IsClosed && game.tryGetChoice(eventId, out var chosen) && chosen == 0 &&
            Cards().SequenceEqual(originalCards.Where(card => !card.StartsWith(eventId + ":", StringComparison.Ordinal))),
            "Completing one offer leaves the other cards in their original relative order");

        SceneManager.LoadScene("MainMenu"); yield return null; yield return null;
        Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(button => button.name == "ContinueButton").onClick.Invoke();
        while (GameSessionCoordinator.Active == null || !GameSessionCoordinator.Active.Ready) yield return null;
        yield return null;
        game = Object.FindFirstObjectByType<EventSystemController>();
        Check(game.tryGetChoice(eventId, out chosen) && chosen == 0 &&
            Cards().SequenceEqual(originalCards.Where(card => !card.StartsWith(eventId + ":", StringComparison.Ordinal))),
            "Ordinary Continue preserves the committed result and remaining cards");

    }
}
