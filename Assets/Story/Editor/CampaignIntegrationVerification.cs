using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using TakeOver.CompanyRules;
using TakeOver.Documents;
using TakeOver.Events;
using TakeOver.NPC;
using TakeOver.SaveSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CampaignIntegrationVerification
{
    public const string Output = "../QA_PairedCampaign/";
    static CampaignIntegrationVerification() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        SessionState.SetString("Campaign.PreviousSaveRoot", Environment.GetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY") ?? "");
        Environment.SetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY", Path.GetFullPath(Output + "save"));
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "results.txt", "RUNNING\n");
        File.WriteAllText(Output + "balance.txt", "");
        SessionState.SetBool("Campaign.Verify", true);
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        var view = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")); view.position = new Rect(40, 40, 1280, 780); view.Show();
        EditorApplication.isPlaying = true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool("Campaign.Verify", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        { var go = new GameObject("Campaign integration verification"); Object.DontDestroyOnLoad(go); go.AddComponent<CampaignIntegrationProbe>(); }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool("Campaign.Verify", false);
            string previous = SessionState.GetString("Campaign.PreviousSaveRoot", ""); Environment.SetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY", previous == "" ? null : previous);
            EditorApplication.Exit(File.ReadAllText(Output + "results.txt").Contains("FAIL") ? 1 : 0);
        }
    }
}
public sealed class CampaignIsolatedSaveManager : GameSaveManager
{
    protected override SaveFileStorage storage => new SaveFileStorage(Path.GetFullPath(CampaignIntegrationVerification.Output + "save"));
}
public sealed partial class CampaignIntegrationProbe : MonoBehaviour
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private EventSystemController game;
    private GameTurnClock clock;
    private Companies companies;
    private ShareTradingSystem trading;
    private Player player;
    private CampaignIsolatedSaveManager saves;
    private readonly string[] companyIds = { "mirae-construction", "haneul-food", "dongbang-trading", "hansung-industry" };
    private readonly int[] buyDays = { 1, 7, 15, 23 };
    private readonly int[] dueDays = { 5, 12, 20, 28 };
    private float timeout;
    private int assertions;
    private void Check(bool value, string message)
    { if (!value) throw new Exception(message); assertions++; File.AppendAllText(CampaignIntegrationVerification.Output + "results.txt", "PASS: " + message + "\n"); }
    private void OnEnable() => Application.logMessageReceived += Log;
    private void OnDisable() => Application.logMessageReceived -= Log;
    private void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) File.AppendAllText(CampaignIntegrationVerification.Output + "results.txt", "FAIL runtime: " + message + "\n" + stack + "\n"); }
    private void Update()
    { if (timeout > 0 && Time.realtimeSinceStartup > timeout) { File.AppendAllText(CampaignIntegrationVerification.Output + "results.txt", "FAIL: timeout\n"); timeout = 0; EditorApplication.isPlaying = false; } }
    private IEnumerator Start()
    {
        timeout = Time.realtimeSinceStartup + 600;
        var stack = new Stack<IEnumerator>(); stack.Push(Verify());
        while (stack.Count > 0)
        {
            var routine = stack.Peek(); bool next; object value = null;
            try { next = routine.MoveNext(); if (next) value = routine.Current; }
            catch (Exception error) { File.AppendAllText(CampaignIntegrationVerification.Output + "results.txt", "FAIL: " + error + "\n"); break; }
            if (!next) { stack.Pop(); continue; }
            if (value is IEnumerator nested) stack.Push(nested); else yield return value;
        }
        timeout = 0; EditorApplication.isPlaying = false;
    }
    private IEnumerator Ready(int seed, bool reload = true)
    {
        if (reload) SceneManager.LoadScene("Game");
        yield return null; yield return null; yield return null; yield return null;
        game = Object.FindFirstObjectByType<EventSystemController>(); clock = Object.FindFirstObjectByType<GameTurnClock>();
        companies = Object.FindFirstObjectByType<Companies>(); trading = Object.FindFirstObjectByType<ShareTradingSystem>(); player = Object.FindFirstObjectByType<Player>();
        saves = new GameObject("Isolated campaign save").AddComponent<CampaignIsolatedSaveManager>();
        FixedDaySeed.changeSeed(seed); UnityEngine.Random.InitState(seed);
        var initial = JsonUtility.FromJson<EventSystemController.EventSaveData>(game.captureJson()); initial.tutorialRewardGranted = true;
        game.prepareLoad(JsonUtility.ToJson(initial), game.saveVersion, new DayStartData(clock.CurrentTurn, seed)); game.applyLoad(); game.rebuildAfterLoad();
    }
    private void Buy(string id, float percent)
    {
        var company = companies.getCompany(id);
        int amount = Mathf.Max(0, Mathf.CeilToInt(company.state.totalShares * percent / 100f - company.state.getBaseStat(CompanyStat.PLAYER_SHARES)));
        Check(amount == 0 || trading.buyShares(company, amount), "Real share purchase succeeds: " + id + " day " + clock.CurrentTurn);
        game.refreshAvailability();
    }
    private IEnumerator Complete(string id, int choice = 0, bool capture = false)
    {
        var info = game.getTodayEventInfos().Single(item => item.definition.getId() == id);
        int before = Object.FindFirstObjectByType<ActionPointService>().Current;
        game.openEvent(id); game.startNegotiation(); yield return null;
        Check(game.getNegotiationChoiceCount() > choice, "Authored choices are connected: " + id);
        var view = Object.FindFirstObjectByType<EventNegotiationView>();
        if (info.definition.OpeningLines.Count > 0 || info.definition.Variant(info.companyId) != null)
        {
            view.SkipToNextChoice(); yield return null;
            int guard = 100;
            while (view.HasMorePages && --guard > 0) { view.ContinueButton.onClick.Invoke(); yield return null; }
        }
        if (capture) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.GetFullPath(CampaignIntegrationVerification.Output + id + ".png")); yield return null; }
        game.confirmChoice(choice); yield return null;
        if (info.definition.getChoiceGeneration() == EventChoiceGeneration.OwnedCompanies && choice == 0)
        {
            view.SkipToNextChoice(); yield return null;
            Check(game.getNegotiationChoiceCount() > 0, "Owned company selection is connected");
            game.confirmChoice(0); yield return null;
        }
        int memories = Object.FindFirstObjectByType<NpcStateRegistry>().States.Sum(state => state.memories.Count);
        float cash = player.state.cash;
        game.confirmChoice(choice);
        Check(player.state.cash == cash && memories == Object.FindFirstObjectByType<NpcStateRegistry>().States.Sum(state => state.memories.Count), "Double confirmation does not duplicate effects: " + id);
        game.closeResult(); game.closeResult(); yield return null;
        Check(game.IsClosed && Object.FindFirstObjectByType<ActionPointService>().Current == before - info.definition.getApCost(), "One AP charge and closed result: " + id);
    }
    private IEnumerator CompleteForced(bool capture = false)
    {
        int guard = 20;
        while (game.MandatoryState == MandatoryStoryState.Forced && --guard > 0)
        {
            Check(game.getTodayEvents().Count == game.PendingMandatoryEvents.Count && game.getTodayEvents().Count <= 2, "Only pending mandatory bundle cards are presented");
            bool paired = game.PendingMandatoryEvents.Count == 2;
            if (paired) {
                Check(!trading.sellShares(companies.getCompany(companyIds[0]), 1), "Mandatory bundle blocks real share trading");
                Check(!game.CanAdvanceTurn(clock.CurrentTurn), "Mandatory bundle blocks day advance");
                if (capture) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.GetFullPath(CampaignIntegrationVerification.Output + "bundle-" + clock.CurrentTurn + ".png")); yield return null; }
            }
            var id = (FixedDaySeed.seed % 2 == 0 ? game.getTodayEvents().First() : game.getTodayEvents().Last()).getId();
            int choice = Mathf.Abs(FixedDaySeed.seed - 11) % game.getTodayEvents().Single(e => e.getId() == id).getChoices().Count;
            yield return Complete(id, choice, capture);
            if (paired) {
                Check(game.MandatoryState == MandatoryStoryState.Forced && game.getTodayEvents().Count == 1, "First result leaves only the second mandatory card");
                string before = game.captureJson();
                Check(saves.trySaveDayEnd(new DayStartData(clock.CurrentTurn, FixedDaySeed.seed)) && saves.tryLoad(out _), "Half-completed bundle saves and restores: " + saves.lastError);
                yield return null;
                Check(before == game.captureJson(), "Bundle load preserves completed choice and one remaining card");
            }
        }
        Check(guard > 0, "Forced sequence terminates");
    }
    private IEnumerator PlayTo(int finalDay, int skipCompany = -1, bool capture = false)
    {
        for (int day = clock.CurrentTurn; day <= finalDay; day++)
        {
            yield return CompleteForced(capture && (day == 20 || day == 28 || day == 32 || day == 40 || day == 42));
            Check(companyIds.All(id => game.CompanyStoryCompleted(id) == (id == companyIds[0] || id == companyIds[2] ? day >= 33 : day >= 41)), "Greenmail completion gates follow the new final scenes, day " + day);
            Check(saves.TryWriteCampaignCheckpoint(game), "Mission preparation checkpoint writes: " + saves.lastError);
            for (int i = 0; i < companyIds.Length; i++) if (day == buyDays[i] && i != skipCompany) Buy(companyIds[i], 10);
            if (day == 15) Buy(companyIds[0], 30);
            if (day == 23) Buy(companyIds[1], 30);
            if (day == 29) Buy(companyIds[2], 20);
            if (day == 34) Buy(companyIds[3], 20);
            if (day == 7 || day == 15 || day == 23)
                Check(!game.getTodayEvents().Any(item => item.getId() == (day == 7 ? "main_haneul_01" : day == 15 ? "main_dongbang_01" : "main_hansung_01")), "Early ten-percent holdings do not launch the new company main");
            if (game.MandatoryState == MandatoryStoryState.Failed) yield break;
            yield return CompleteForced(capture && (day == 12 || day == 28 || day == 29));
            if (day == 13 || day == 21)
            {
                string before = game.captureJson(); float cash = player.state.cash;
                Check(saves.trySaveDayEnd(new DayStartData(clock.CurrentTurn, FixedDaySeed.seed)), "Campaign save succeeds day " + day + ": " + saves.lastError);
                Check(saves.tryLoad(out _), "Campaign load succeeds day " + day + ": " + saves.lastError);
                yield return null;
                Check(before == game.captureJson() && cash == player.state.cash, "Load preserves campaign and offers without replay day " + day);
            }
            if (FixedDaySeed.seed == 11 && new[] {4,11,19,27,31,39}.Contains(day)) {
                Check(saves.trySaveDayEnd(new DayStartData(day, FixedDaySeed.seed)), "Pre-deadline baseline saves");
                File.Copy(saves.savePath, CampaignIntegrationVerification.Output + "before-" + (day+1) + ".json", true);
            }
            if (day == finalDay) yield break;
            var inbox = Object.FindFirstObjectByType<DocumentSystem>().Inbox;
            foreach (var doc in inbox.Open.OfType<IApprovalDocument>().ToList()) inbox.Resolve(doc, false);
            int previous = clock.CurrentTurn; clock.AdvanceTurn(); yield return null;
            Check(clock.CurrentTurn == previous + 1, "Day advances without a story softlock: " + day);
        }
    }
    private IEnumerator Verify()
    {
        yield return Ready(11, false);
        Check(game.StoryEvents.Count == 21 && Resources.Load<GeneralEventDatabase>("Events/GeneralEventDatabase").Events.Count == 26, "Story and general databases contain the complete authored content");
        var registry = Object.FindFirstObjectByType<NpcStateRegistry>();
        Check(registry.States.Count == 30, "All thirty NPCs initialize");
        foreach (var definition in Resources.Load<GeneralEventDatabase>("Events/GeneralEventDatabase").Events.Concat(game.StoryEvents))
            foreach (var line in definition.OpeningLines.Concat(definition.getChoices().SelectMany(choice => choice.ResultLines)))
                Check(string.IsNullOrEmpty(line.npcId) || registry.TryGetProfile(line.npcId, out _), "Registered dialogue speaker: " + line.npcId);
        Check(!game.getTodayEventInfos().Any(item => item.definition.getId() == "greenmail_01" || item.definition.Repeatable && item.definition.hasCompanyConditions()), "Company side events and greenmail are locked before first main");
        for (int seed = 11; seed <= 14; seed++)
        {
            if (seed != 11) yield return Ready(seed);
            var documents = Object.FindFirstObjectByType<DocumentSystem>();
            var report = new SnapshotDocument("qa-campaign", "저장 검증 보고서", "검증", new[] { new DocumentInfo("기록", "유지") });
            documents.Inbox.Add(report); documents.Inbox.MarkRead(report);
            documents.Inbox.Add(new SnapshotDocument("qa-pending", "미확인 보고서", "검증", new[] { new DocumentInfo("상태", "대기") }));
            Object.FindFirstObjectByType<NewsFeed>().addNews(1, "저장 검증 뉴스", new[] { "mirae-construction" });
            yield return PlayTo(42, -1, seed == 11);
            Check(companyIds.All(game.CompanyStoryCompleted) && game.MandatoryState == MandatoryStoryState.Completed, "All four company stories complete with distinct gates, seed " + seed);
            Check(player.state.cash > 0, "Four investment objectives leave positive cash, seed " + seed + ": " + player.state.cash);
            File.AppendAllText(CampaignIntegrationVerification.Output + "balance.txt", $"seed={seed} day=42 cash={player.state.cash}\n");
            string before = game.captureJson();
            Check(saves.trySaveDayEnd(new DayStartData(clock.CurrentTurn, FixedDaySeed.seed)), "Final whole-game save succeeds: " + saves.lastError);
            yield return Ready(seed);
            Check(saves.tryLoad(out _), "Fresh-scene continue restores the whole game: " + saves.lastError); yield return null;
            Check(clock.CurrentTurn == 42 && before == game.captureJson(), "Fresh-scene load restores exact campaign, choices, pending offers and cooldowns");
            Check(Object.FindFirstObjectByType<DocumentSystem>().Inbox.History.Any(item => item.document.TypeId == "qa-campaign") && Object.FindFirstObjectByType<DocumentSystem>().Inbox.Open.Any(item => item.TypeId == "qa-pending"), "Document history and unread documents survive fresh-scene continue");
            Check(Object.FindFirstObjectByType<NewsFeed>().Entries.Any(item => item.title == "저장 검증 뉴스"), "News survives fresh-scene continue");
        }
        yield return VerifyPairedFailures();
        yield return Ready(99);
        yield return PlayTo(42);
        foreach (var id in companyIds)
        {
            Buy(id, 51);
            var company = companies.getCompany(id);
            company.state.setStat(CompanyStat.COST, company.state.getStat(CompanyStat.REVENUE) + 100);
            company.state.setStat(CompanyStat.REPUTATION_B2C, 20);
        }
        game.refreshAvailability();
        Check(saves.trySaveDayEnd(new DayStartData(42, FixedDaySeed.seed)), "Repeatable-choice baseline save succeeds");
        var definitions = Resources.Load<GeneralEventDatabase>("Events/GeneralEventDatabase").Events.Where(item => item.Repeatable).ToList();
        int scenes = 0, branches = 0;
        foreach (var definition in definitions)
        {
            var targets = definition.getId() == "management_defense_01" || definition.getId() == "restructuring_01" ? companyIds : new[] { definition.hasCompanyConditions() ? definition.getCompanyIds().FirstOrDefault() ?? companyIds[0] : "" };
            foreach (string cid in targets)
            {
                scenes++;
                var choices = definition.Variant(cid)?.choices ?? definition.getChoices();
                for (int choice = 0; choice < choices.Count; choice++)
                {
                    Check(saves.tryLoad(out _), "Branch baseline restores"); yield return null;
                    var data = JsonUtility.FromJson<EventSystemController.EventSaveData>(game.captureJson());
                    data.occurrences.RemoveAll(item => item.eventId == definition.getId());
                    data.cooldowns.RemoveAll(item => item.eventId == definition.getId());
                    data.occurrences.Add(new EventSystemController.TimingData { eventId = definition.getId(), companyId = cid, day = 42, serial = ++data.lastOccurrenceSerial });
                    game.prepareLoad(JsonUtility.ToJson(data), game.saveVersion, new DayStartData(42, FixedDaySeed.seed)); game.applyLoad(); game.rebuildAfterLoad();
                    int beforeMemories = Object.FindFirstObjectByType<NpcStateRegistry>().States.Sum(state => state.memories.Count);
                    float cash = player.state.cash;
                    yield return Complete(definition.getId(), choice, scenes == 1 && choice == 0);
                    Check(game.RepeatHistory.Count == data.repeatHistory.Count + 1, "One unique repeat record: " + definition.getId() + ":" + cid + ":" + choice);
                    Check(Mathf.Abs(player.state.cash - cash - choices[choice].getCashDelta()) < .01f, "Authored cash delta applied once");
                    Check(Object.FindFirstObjectByType<NpcStateRegistry>().States.Sum(state => state.memories.Count) == beforeMemories + 1, "NPC event connection adds one authored observation memory");
                    string snapshot = game.captureJson();
                    game.prepareLoad(snapshot, game.saveVersion, new DayStartData(42, FixedDaySeed.seed)); game.applyLoad(); game.rebuildAfterLoad();
                    Check(snapshot == game.captureJson(), "Completed repeat record and pending offers round-trip without reroll");
                    branches++;
                }
            }
        }
        Check(scenes == 27 && branches == 75, "All 27 repeatable scenes and 75 outcomes execute through real NPC/event connections");
        Check(saves.tryLoad(out _), "Daily-repeat stress baseline restores"); yield return null;
        var seen = new HashSet<string>();
        int offers = 0;
        for (int step = 0; step < 192; step++)
        {
            foreach (var id in companyIds)
            {
                var company = companies.getCompany(id);
                company.state.setStat(CompanyStat.COST, company.state.getStat(CompanyStat.REVENUE) + 100);
                company.state.setStat(CompanyStat.REPUTATION_B2C, 20);
            }
            var inbox = Object.FindFirstObjectByType<DocumentSystem>().Inbox;
            foreach (var document in inbox.Open.OfType<IApprovalDocument>().ToList()) inbox.Resolve(document, false);
            clock.AdvanceTurn(); yield return null;
            var state = JsonUtility.FromJson<EventSystemController.EventSaveData>(game.captureJson());
            Check(state.occurrences.Select(item => item.eventId).Distinct().Count() == state.occurrences.Count, "Shared event definitions never create ambiguous simultaneous company offers");
            Check(state.occurrences.Count(item => item.day == clock.CurrentTurn && item.serial > 0) <= 2, "At most two new daily repeat offers");
            foreach (var info in game.getTodayEventInfos().Where(item => item.definition.Repeatable).ToList())
            {
                string key = info.definition.getId() + ":" + (string.IsNullOrEmpty(info.definition.RepeatGroup) ? info.companyId : "");
                seen.Add(key); offers++;
                int answer = info.definition.getId() == "stock_manipulation_01" ? 1 : info.definition.getId() == "political_donation_01" ? 2 : 0;
                yield return Complete(info.definition.getId(), answer);
            }
            string stable = game.captureJson(); game.onDayStarted(clock.CurrentTurn); game.refreshAvailability();
            Check(stable == game.captureJson(), "Same-day refresh does not reroll repeat events");
            if (step == 40)
            {
                Check(saves.trySaveDayEnd(new DayStartData(clock.CurrentTurn, FixedDaySeed.seed)) && saves.tryLoad(out _), "Live repeat history saves and restores: " + saves.lastError);
                yield return null;
                Check(stable == game.captureJson(), "Load keeps exact live history and cooldowns");
            }
        }
        Check(offers > 30 && seen.Count >= 24 && game.RepeatHistory.Select(item => item.serial).Distinct().Count() == game.RepeatHistory.Count, "Daily probability produces repeated, unique occurrences across the eligible event pool");
        File.WriteAllText(CampaignIntegrationVerification.Output + "repeat-observation.txt", $"192 stress days / {offers} offers / {seen.Count} of 27 eligible scene variants observed\n");
        // Explicitly reject duplicated occurrence serials before mutating the running scene.
        var corrupt = JObject.Parse(game.captureJson());
        corrupt["repeatHistory"].Last["serial"] = 0;
        string unchanged = game.captureJson(); bool rejected = false;
        try { game.prepareLoad(corrupt.ToString(), game.saveVersion, new DayStartData(42, FixedDaySeed.seed)); } catch (InvalidDataException) { rejected = true; }
        Check(rejected && unchanged == game.captureJson(), "Invalid repeat history is rejected without runtime mutation");
        Check(saves.trySaveDayEnd(new DayStartData(clock.CurrentTurn, FixedDaySeed.seed)), "Save for actual title continue succeeds");
        int continuedDay = clock.CurrentTurn; string continuedEvents = game.captureJson();
        SceneManager.LoadScene("MainMenu"); yield return null; yield return null;
        var menu = Object.FindFirstObjectByType<TakeOver.MainMenu.MainMenuController>();
        Check(menu != null, "Title scene opens after the campaign");
        menu.startContinue();
        while (SceneManager.GetActiveScene().name != "Game" || GameSessionCoordinator.Active == null || !GameSessionCoordinator.Active.Ready) yield return null;
        game = Object.FindFirstObjectByType<EventSystemController>(); clock = Object.FindFirstObjectByType<GameTurnClock>();
        Check(!TakeOver.Story.StoryDirector.IsRunning && clock.CurrentTurn == continuedDay && continuedEvents == game.captureJson(), "Actual title continue loads the saved scene without replaying tutorial or events");
        File.AppendAllText(CampaignIntegrationVerification.Output + "results.txt", $"COMPLETE {assertions} PASS\n");
    }
}
