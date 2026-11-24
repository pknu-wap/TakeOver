using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TakeOver.Documents;
using TakeOver.Employees;
using TakeOver.Events;
using TakeOver.NPC;
using TakeOver.SaveSystem;
using TakeOver.Startups;
using TakeOver.Story.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ReviewFixVerification
{
    public static string Root => Path.GetFullPath(Environment.GetEnvironmentVariable("TAKEOVER_REVIEW_FIX_QA_OUTPUT") ?? "../QA_ReviewFixes/Focused") + Path.DirectorySeparatorChar;
    static ReviewFixVerification() { EditorApplication.playModeStateChanged += Changed; }
    public static bool PricesOnly => SessionState.GetBool("ReviewFix.PricesOnly", false);
    public static void RunPrices() { SessionState.SetBool("ReviewFix.PricesOnly", true); Run(); }
    public static void Run()
    {
        Directory.CreateDirectory(Root); File.WriteAllText(Root + "results.txt", "RUNNING\n");
        Environment.SetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY", Root + "Save");
        SessionState.SetBool("ReviewFix", true);
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        if (!PricesOnly)
        {
            var gameView = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
            gameView.position = new Rect(40, 40, 1280, 780); gameView.Show(); gameView.Focus();
        }
        EditorApplication.isPlaying = true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool("ReviewFix", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        { var root = new GameObject("Review fixes verification"); Object.DontDestroyOnLoad(root); root.AddComponent<ReviewFixProbe>(); }
        if (state == PlayModeStateChange.EnteredEditMode)
        { SessionState.SetBool("ReviewFix", false); SessionState.SetBool("ReviewFix.PricesOnly", false); EditorApplication.Exit(File.ReadAllText(Root + "results.txt").Contains("FAIL") ? 1 : 0); }
    }
}
public sealed class ReviewFixProbe : MonoBehaviour
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    string Results => ReviewFixVerification.Root + "results.txt";
    static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    void Check(bool pass, string text)
    { if (!pass) throw new Exception(text); File.AppendAllText(Results, "PASS: " + text + "\n"); }
    IEnumerator Start()
    {
        var stack = new Stack<IEnumerator>(); stack.Push(Run());
        while (stack.Count > 0)
        {
            var current = stack.Peek(); bool more; object value = null;
            try { more = current.MoveNext(); if (more) value = current.Current; }
            catch (Exception error) { File.AppendAllText(Results, "FAIL: " + error + "\n"); break; }
            if (!more) stack.Pop(); else if (value is IEnumerator child) stack.Push(child); else yield return value;
        }
        File.AppendAllText(Results, "COMPLETE\n"); EditorApplication.isPlaying = false;
    }
    IEnumerator Frames(int n = 12) { for (int i = 0; i < n; i++) yield return null; }
    IEnumerator Fade() { while (EventSceneTransition.IsBusy) yield return null; yield return null; }
    IEnumerator Capture(string name)
    { yield return Frames(2); ScreenCapture.CaptureScreenshot(ReviewFixVerification.Root + name + ".png"); yield return Frames(2); }
    IEnumerator Run()
    {
        yield return Frames();
        if (ReviewFixVerification.PricesOnly) { yield return VerifyPrices(); yield break; }
        var game = Object.FindFirstObjectByType<EventSystemController>();
        var companies = Object.FindFirstObjectByType<Companies>();
        foreach (var company in companies.CompanyList)
        { company.state.setStat(CompanyStat.PLAYER_SHARES, company.state.totalShares * .6f); company.state.setStat(CompanyStat.REPUTATION_B2C, 10); company.calculator.calculateAboutShares(company); }
        Object.FindFirstObjectByType<GameTurnClock>().AdvanceTurn();
        foreach (var scripted in new[] { false, true })
        {
            var definition = game.getTodayEvents().First(item => (item.OpeningLines.Count > 0) == scripted && !item.IsLinearDialogue && item.getChoiceGeneration() != EventChoiceGeneration.OwnedCompanies);
            game.openEvent(definition.getId()); game.startNegotiation(); yield return Fade();
            var view = Object.FindFirstObjectByType<EventNegotiationView>();
            if (view.SkipButton.IsInteractable()) { view.SkipToNextChoice(); yield return Fade(); }
            Check(Get<List<Button>>(view, "choices").Count > 0, "Introduction skip stops at unanswered choices: " + definition.getId());
            game.confirmChoice(0); yield return Frames(3);
            float resultCash = Object.FindFirstObjectByType<Player>().state.cash;
            int resultMemories = Object.FindFirstObjectByType<NpcStateRegistry>().States.Sum(npc => npc.memories.Count);
            Check(view.SkipButton.IsInteractable(), "Result skip is available: " + definition.getId());
            view.SkipToNextChoice(); yield return Fade();
            Check(game.IsClosed && Object.FindFirstObjectByType<Player>().state.cash == resultCash &&
                Object.FindFirstObjectByType<NpcStateRegistry>().States.Sum(npc => npc.memories.Count) == resultMemories,
                "Result skip closes once without repeating effects: " + definition.getId() + " closed=" + game.IsClosed);
        }
        var manager = Object.FindFirstObjectByType<GameSaveManager>() ?? game.gameObject.AddComponent<GameSaveManager>();
        var clock = Object.FindFirstObjectByType<GameTurnClock>();
        Check(manager.trySaveDayEnd(new DayStartData(clock.CurrentTurn, FixedDaySeed.seed)), "Baseline saves");
        GameSessionCoordinator.RequestContinue(); SceneManager.LoadScene("Game"); yield return Frames();
        while (GameSessionCoordinator.Active == null || !GameSessionCoordinator.Active.Ready) yield return null;
        yield return Frames(); manager = Object.FindFirstObjectByType<GameSaveManager>();
        var trader = Object.FindFirstObjectByType<ShareTradingSystem>(); var player = Object.FindFirstObjectByType<Player>();
        companies = Object.FindFirstObjectByType<Companies>(); var target = companies.CompanyList.First();
        string original = File.ReadAllText(manager.savePath); float shares = target.state.getStat(CompanyStat.PLAYER_SHARES);
        float quote = target.state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE);
        Check(quote > 0 && Mathf.Abs(quote - target.state.getDerivedStat(CompanyDerivedStat.MARKET_VALUE) / target.state.totalShares) < .000001f,
            "Tradable quote retains market value per share without rounding or a minimum-price floor");
        float cashBeforePurchase = player.state.cash;
        Check(trader.buyShares(target, 1), "Trade succeeds"); float cash = player.state.cash; yield return Frames();
        Check(Mathf.Abs(cash - (cashBeforePurchase - quote)) < .001f, "Trade charges the unrounded quote");
        Check(original != File.ReadAllText(manager.savePath), "Trade updates autosave without another action");
        Check(manager.tryLoad(out _) && player.state.cash == cash && target.state.getStat(CompanyStat.PLAYER_SHARES) == shares + 1, "Continue retains purchased shares and cash"); yield return Frames();
        var inbox = Object.FindFirstObjectByType<DocumentSystem>().Inbox;
        var report = new SnapshotDocument("qa-reading", "검수 보고서", "검수 담당", new[] { new DocumentInfo("내용", "읽기 상태 검증") });
        inbox.Add(report); yield return Frames(); original = File.ReadAllText(manager.savePath);
        inbox.MarkRead(report); yield return Frames();
        Check(original != File.ReadAllText(manager.savePath), "Reading a report updates autosave");
        Check(manager.tryLoad(out _) && !inbox.Open.Any(item => item.TypeId == "qa-reading"), "A read report remains read after loading"); yield return Frames();

        Check(GameSessionCoordinator.Active.SaveManualSlot(1), "Manual slot saves while the session is idle");
        cash = player.state.cash; var assigned = Object.FindFirstObjectByType<EventSystemController>().captureJson(); int seed = FixedDaySeed.seed;
        Check(trader.buyShares(target, 1), "Later trade updates only the active progress"); yield return Frames();
        Check(manager.TryLoadManualSlot(1, out _) && player.state.cash == cash && assigned == Object.FindFirstObjectByType<EventSystemController>().captureJson() && seed == FixedDaySeed.seed,
            "Manual slot restores its original cash, exact assigned events and random seed"); yield return Frames();
        var template = Object.FindFirstObjectByType<EventNegotiationView>(FindObjectsInactive.Include);
        SaveSlotWindow.Show(template); yield return Frames(); yield return Capture("manual-save-menu");
        var load = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(button => button.name == "Save slot 1 load");
        StoryPlayProbe.Click(load); yield return Frames();
        StoryPlayProbe.Click(Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(button => button.name == "Confirm save action"));
        yield return Frames(); while (GameSessionCoordinator.Active == null || !GameSessionCoordinator.Active.Ready) yield return null;
        Check(Object.FindFirstObjectByType<Player>().state.cash == cash, "Actual load button reloads the manual slot through a new game scene");
        yield return Frames(); manager = Object.FindFirstObjectByType<GameSaveManager>();
        template = Object.FindFirstObjectByType<EventNegotiationView>(FindObjectsInactive.Include);

        companies = Object.FindFirstObjectByType<Companies>(); var startup = companies.StartupList.First();
        var employees = Object.FindFirstObjectByType<EmployeeService>(); var jobs = Object.FindFirstObjectByType<JobService>();
        Check(employees.StartRecruit(), "Recruitment can start"); clock = Object.FindFirstObjectByType<GameTurnClock>();
        for (int i = 0; employees.Applicants.Count == 0 && i < 4; i++) { clock.AdvanceTurn(); yield return Frames(); }
        var applicant = employees.Applicants.First(); Check(employees.Hire(applicant), "An applicant can be hired");
        Check(jobs.Assign(applicant, JobType.Discovery), "Employee begins an existing assignment");
        var support = new SupportRequestDocument(startup, clock.CurrentTurn + 7, employees, jobs, companies);
        support.SetChoice("employee", applicant.Id); inbox = Object.FindFirstObjectByType<DocumentSystem>().Inbox;
        inbox.Add(support); inbox.Resolve(support, true); yield return Frames();
        Check(jobs.GetJob(applicant) == JobType.Support && jobs.HasSupportFor(startup), "Approval moves the selected employee to the requested startup");
        string supportedName = startup.definition.companyName;
        Check(manager.trySaveDayEnd(new DayStartData(clock.CurrentTurn, FixedDaySeed.seed)) && manager.tryLoad(out _), "Support assignment saves and restores");
        applicant = employees.Employees.Single(employee => employee.Id == applicant.Id);
        Check(jobs.GetJob(applicant) == JobType.Support && jobs.SupportTargetName(applicant) == supportedName, "Restored employee retains the support company");
        jobs.Clear(applicant); Check(jobs.GetJob(applicant) == JobType.None, "Support can be released explicitly");
        var promoted = companies.StartupList.First();
        Check(jobs.AssignSupport(applicant, promoted) && companies.PromoteStartup(promoted), "A supported company can be listed");
        yield return Frames();
        Check(jobs.GetJob(applicant) == JobType.None, "Listing automatically releases support without leaving an invalid save target");
        yield return Frames();
        var navigation = Object.FindFirstObjectByType<TopBarNavigationController>(); navigation.Navigate(DashboardTab.Operations);
        var operations = Object.FindFirstObjectByType<TakeOver.Operations.OperationsScreen>(); operations.ShowTutorialPage(false, false); yield return Frames();
        var startupSystem = Object.FindFirstObjectByType<StartupSystem>(); startup = companies.StartupList.First(); startupSystem.tryGetRecord(startup.definition.companyID, out var record);
        player = Object.FindFirstObjectByType<Player>();
        var funding = new StartupFundingDocument(startup, record, player, companies, 100, clock.CurrentTurn + 7); inbox.Add(funding);
        var tab = operations.GetComponentInChildren<TakeOver.Operations.DocumentTabView>();
        tab.GetType().GetMethod("select", Private).Invoke(tab, new object[] { funding, null }); yield return Frames();
        var detail = operations.GetComponentInChildren<TakeOver.Operations.DocumentDetailView>();
        string visible = string.Join("\n", detail.GetComponentsInChildren<Text>().Select(text => text.text));
        Check(funding.BodyLines.All(visible.Contains), "Funding request displays its reason and the consequence of declining"); yield return Capture("document-body");
        navigation.Navigate(DashboardTab.Map); yield return new WaitForSecondsRealtime(.5f); yield return Capture("map");
        Check(Object.FindFirstObjectByType<CompanyBrowserScreen>().transform.Find("ListBackground").gameObject.activeSelf,
            "Map uses its opaque browser background to cover the dashboard controls beneath it");
        Check(UiLayoutAssertions.MapCoversViewport(Object.FindFirstObjectByType<CompanyMapView>()),
            "Map artwork covers the viewport without uncovered strips");
        navigation.Navigate(DashboardTab.News); yield return Frames(); yield return Capture("news");
        navigation.Navigate(DashboardTab.Home);
        player.SetName(new string('\uAC00', 20));
        template.ShowDialogue(new DialoguePresentation { player = true, speaker = player.ProtagonistName, text = "이름 표시 확인" }, null, null, null, () => false);
        template.SetDialogueInput(true); yield return Frames();
        Check(template.CurrentSpeaker.preferredWidth <= template.CurrentSpeaker.rectTransform.rect.width, "Twenty-character player name fits its frame");
        yield return Capture("long-name");
        template.hide();
        var contracts = Object.FindFirstObjectByType<StartupContractService>();
        startup = companies.StartupList.First();
        Check(contracts.requestContract(startup), "Investment review schedules a future contract");
        int contractDay = clock.CurrentTurn + 1; string contractCompany = startup.definition.companyID;
        Check(manager.trySaveDayEnd(new DayStartData(clock.CurrentTurn, FixedDaySeed.seed)) && manager.tryLoad(out _), "Pending contract round-trips through a save");
        startupSystem.tryGetRecord(contractCompany, out var restoredContract);
        Check(restoredContract.pendingContractDay == contractDay, "Continue preserves the requested contract's due day");
        SceneManager.LoadScene("MainMenu"); yield return Frames(); yield return Capture("title-manual-load");
        StoryPlayProbe.Click(Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(button => button.name == "ContinueButton")); yield return Frames();
        StoryPlayProbe.Click(Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(button => button.name == "Save slot 1 load")); yield return Frames();
        StoryPlayProbe.Click(Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(button => button.name == "Confirm save action")); yield return Frames();
        while (GameSessionCoordinator.Active == null || !GameSessionCoordinator.Active.Ready) yield return null;
        Check(Object.FindFirstObjectByType<GameTurnClock>().CurrentTurn == 2, "The title continue picker restores the selected earlier day");
    }

    IEnumerator VerifyPrices()
    {
        var companies = Object.FindFirstObjectByType<Companies>();
        var player = Object.FindFirstObjectByType<Player>();
        var trader = Object.FindFirstObjectByType<ShareTradingSystem>();
        var market = Object.FindFirstObjectByType<Marketdata>();
        foreach (var company in companies.CompanyList)
        {
            float expected = company.state.getDerivedStat(CompanyDerivedStat.MARKET_VALUE) / company.state.totalShares;
            float actual = company.state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE);
            Check(Mathf.Abs(actual - expected) <= Mathf.Max(.000001f, Mathf.Abs(expected) * .000001f),
                "Quote matches unrounded market value per share: " + company.definition.companyID);
        }
        var target = companies.getCompany("haneul-food");
        float quote = target.state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE);
        Check(quote > 0 && quote < 1, "A valid fractional quote below one is not floored");
        float initialCash = player.state.cash, shares = target.state.getStat(CompanyStat.PLAYER_SHARES);
        Check(trader.buyShares(target, 7), "Buy seven shares at the fractional quote");
        Check(Mathf.Abs(player.state.cash - (initialCash - quote * 7)) < .002f &&
            target.state.getStat(CompanyStat.PLAYER_SHARES) == shares + 7, "Purchase charges quote times quantity and grants exactly seven shares");
        float beforeSale = player.state.cash;
        Check(trader.sellShares(target, 3), "Sell three shares at the fractional quote");
        Check(Mathf.Abs(player.state.cash - (beforeSale + quote * 3)) < .002f &&
            target.state.getStat(CompanyStat.PLAYER_SHARES) == shares + 4, "Sale pays quote times quantity and removes exactly three shares");
        var effects = Object.FindFirstObjectByType<EventStockmanipulationAccept>();
        var clock = Object.FindFirstObjectByType<GameTurnClock>();
        var otherPrices = companies.CompanyList.Where(c => c != target).ToDictionary(c => c, c => c.state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE));
        Check(effects.applyEventEffect(target.definition.companyID, 1, .35f), "Schedule a 35 percent rise for the selected company");
        var execute = typeof(EventStockmanipulationAccept).GetMethod("executeScheduledEffects", Private);
        execute.Invoke(effects, new object[] { clock.CurrentTurn });
        Check(target.state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE) == quote, "Scheduled rise does not apply before its due day");
        execute.Invoke(effects, new object[] { clock.CurrentTurn + 1 });
        float risen = target.state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE);
        Check(risen > quote && Mathf.Abs(risen - quote * 1.35f) < .000001f, "A 35 percent rise changes the actual fractional trading quote by 35 percent");
        Check(otherPrices.All(pair => pair.Key.state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE) == pair.Value), "The price effect leaves other companies unchanged");
        beforeSale = player.state.cash;
        Check(trader.sellShares(target, 4), "Sell the remaining four purchased shares after the rise");
        Check(Mathf.Abs(player.state.cash - (beforeSale + risen * 4)) < .002f &&
            Mathf.Abs(player.state.cash - (initialCash + quote * .35f * 4)) < .002f, "The rise produces the expected realized sale profit");
        float savedCash = player.state.cash;
        var manager = Object.FindFirstObjectByType<GameSaveManager>() ?? gameObject.AddComponent<GameSaveManager>();
        Check(manager.trySaveDayEnd(new DayStartData(clock.CurrentTurn, FixedDaySeed.seed)), "Save the fractional quote and applied price effect");
        player.state.addCash(-player.state.cash);
        target.marketState.eventPriceMultiplier = 1;
        target.calculator.recalculate(target, market);
        Check(manager.tryLoad(out _), "Restore the fractional-price snapshot: " + manager.lastError);
        Check(player.state.cash == savedCash && target.state.getStat(CompanyStat.PLAYER_SHARES) == shares &&
            Mathf.Abs(target.state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE) - risen) < .000001f,
            "Save/load preserves actual cash, holdings and the unrounded risen price");
        yield return null;
    }
}
