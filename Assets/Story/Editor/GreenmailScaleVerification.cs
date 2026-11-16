using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TakeOver.CompanyRules;
using TakeOver.Events;
using TakeOver.NPC;
using TakeOver.SaveSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class GreenmailScaleVerification
{
    public const string Output = "../QA_GreenmailScale/";
    static GreenmailScaleVerification() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "results.txt", "RUNNING\n");
        SessionState.SetBool("GreenmailScale.Verify", true);
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        EditorApplication.isPlaying = true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool("GreenmailScale.Verify", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            var probe = new GameObject("Greenmail scale verification"); Object.DontDestroyOnLoad(probe); probe.AddComponent<GreenmailScaleProbe>();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool("GreenmailScale.Verify", false);
            EditorApplication.Exit(File.ReadAllText(Output + "results.txt").Contains("FAIL") ? 1 : 0);
        }
    }
}
public sealed class GreenmailScaleSaveManager : GameSaveManager
{
    protected override SaveFileStorage storage => new SaveFileStorage(Path.GetFullPath(GreenmailScaleVerification.Output + "save"));
}
public sealed class GreenmailScaleProbe : MonoBehaviour
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    EventSystemController game;
    Companies companies;
    GameTurnClock clock;
    EventDefinition definition;
    float timeout;
    void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        File.AppendAllText(GreenmailScaleVerification.Output + "results.txt", "PASS: " + description + "\n");
    }
    void OnEnable() { Application.logMessageReceived += Log; }
    void OnDisable() { Application.logMessageReceived -= Log; }
    void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            File.AppendAllText(GreenmailScaleVerification.Output + "results.txt", "FAIL runtime: " + message + "\n" + stack + "\n");
    }
    void Update()
    {
        if (timeout > 0 && Time.realtimeSinceStartup > timeout)
        { File.AppendAllText(GreenmailScaleVerification.Output + "results.txt", "FAIL: timeout\n"); timeout = 0; EditorApplication.isPlaying = false; }
    }
    IEnumerator Start()
    {
        timeout = Time.realtimeSinceStartup + 180;
        var stack = new Stack<IEnumerator>(); stack.Push(Verify());
        while (stack.Count > 0)
        {
            var routine = stack.Peek(); bool next = false; object current = null;
            try { next = routine.MoveNext(); if (next) current = routine.Current; }
            catch (Exception error) { File.AppendAllText(GreenmailScaleVerification.Output + "results.txt", "FAIL: " + error + "\n"); break; }
            if (!next) { stack.Pop(); continue; }
            if (current is IEnumerator nested) stack.Push(nested); else yield return current;
        }
        timeout = 0; EditorApplication.isPlaying = false;
    }
    IEnumerator Ready(bool reload = true)
    {
        if (reload) SceneManager.LoadScene("Game");
        yield return null; yield return null; yield return null;
        game = Object.FindFirstObjectByType<EventSystemController>();
        companies = Object.FindFirstObjectByType<Companies>();
        clock = Object.FindFirstObjectByType<GameTurnClock>();
        definition = AssetDatabase.LoadAssetAtPath<EventDefinition>("Assets/EventSystem/Data/Greenmail.asset");
        foreach (var company in companies.CompanyList) Shares(company, 0);
    }
    void Shares(Company company, float shares)
    { company.state.setStat(CompanyStat.PLAYER_SHARES, shares); company.calculator.calculateAboutShares(company); }
    bool Meets(Company company) => (bool)typeof(EventSystemController).GetMethod("meetsConditions", Flags, null,
        new[] { typeof(EventDefinition), typeof(string) }, null).Invoke(game, new object[] { definition, company.definition.companyID });
    TodayEventInfo Offer => game.getTodayEventInfos().SingleOrDefault(info => info.definition.getId() == "greenmail_01");
    IEnumerator Verify()
    {
        yield return Ready(false);
        var boundary = ScriptableObject.CreateInstance<CompanyDefinition>();
        foreach (var entry in new[] { (999f, CompanyScale.Small, 35f), (1000f, CompanyScale.Medium, 30f), (9999f, CompanyScale.Medium, 30f), (10000f, CompanyScale.Large, 25f) })
        {
            boundary.initialBaseCompanyValue = entry.Item1;
            Check(CompanyScaleRules.TryGetScale(boundary, out var scale) && scale == entry.Item2 && CompanyScaleRules.TryGetGreenmailStake(boundary, out float required) && required == entry.Item3, "Value boundary maps to fixed scale and stake: " + entry.Item1);
        }
        foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
        {
            boundary.initialBaseCompanyValue = invalid;
            Check(!CompanyScaleRules.TryGetGreenmailStake(boundary, out _), "Invalid initial value does not silently qualify: " + invalid);
        }
        Object.Destroy(boundary);
        Check(!CompanyScaleRules.TryGetGreenmailStake(null, out _), "Missing company definition is rejected");
        string[] ids = companies.CompanyList.Select(c => c.definition.companyID).ToArray();
        Check(ids.Length == 9 && definition.getConditions().Single().kind == EventConditionKind.CompanyGreenmailStake, "Live greenmail asset uses scale condition for all nine registered companies");
        int small = 0, medium = 0, large = 0;
        foreach (string id in ids)
        {
            yield return Ready();
            var company = companies.getCompany(id);
            Check(CompanyScaleRules.TryGetScale(company.definition, out var scale) && CompanyScaleRules.TryGetGreenmailStake(company.definition, out _), "Registered company has a valid fixed scale: " + id);
            CompanyScaleRules.TryGetGreenmailStake(company.definition, out float threshold);
            if (scale == CompanyScale.Small) small++; else if (scale == CompanyScale.Medium) medium++; else large++;
            int requiredShares = Mathf.CeilToInt(company.state.totalShares * threshold / 100);
            Shares(company, requiredShares - 1);
            Check(!Meets(company), "One share below threshold is ineligible: " + id);
            clock.AdvanceTurn();
            Check(Offer == null, "No real greenmail card below threshold: " + id);
            Shares(company, requiredShares);
            Check(Meets(company), "Exact threshold qualifies: " + id + "/" + threshold);
            Shares(company, requiredShares + 1);
            Check(Meets(company), "One share above threshold qualifies: " + id);
            company.state.setStat(CompanyStat.BASE_COMPANY_VALUE, 1000000);
            company.state.setDerivedStat(CompanyDerivedStat.SHARE_PRICE, .01f);
            Check(CompanyScaleRules.TryGetGreenmailStake(company.definition, out float stable) && stable == threshold && Meets(company), "Runtime value and price cannot change the threshold: " + id);
            Shares(company, requiredShares); clock.AdvanceTurn();
            Check(Offer != null && Offer.companyId == id && !string.IsNullOrEmpty(Offer.speakerNpcId), "Actual event selects the qualified company and its representative: " + id);
        }
        Check(small == 4 && medium == 2 && large == 3, "Current distribution is four small, two medium and three large companies");

        // 대표 규모별로 실제 수락 효과와 전체 저장을 확인한다.
        foreach (string id in new[] { "haneul-food", "polaris-finance", "daeseong-industry" })
        {
            yield return Ready();
            var company = companies.getCompany(id);
            CompanyScaleRules.TryGetGreenmailStake(company.definition, out float threshold);
            int held = Mathf.CeilToInt(company.state.totalShares * threshold / 100);
            Shares(company, held); clock.AdvanceTurn();
            var player = Object.FindFirstObjectByType<Player>(); var trading = Object.FindFirstObjectByType<ShareTradingSystem>();
            float price = company.state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE), cash = player.state.cash;
            int delivered = 0; game.choiceConfirmed += _ => delivered++;
            game.openEvent("greenmail_01"); game.startNegotiation(); game.confirmChoice(0); game.confirmChoice(0);
            float expected = cash + price * held * (1 + definition.getChoices()[0].getBuybackPremiumRatio());
            Check(delivered == 1 && company.state.getBaseStat(CompanyStat.PLAYER_SHARES) == 0 && Mathf.Abs(player.state.cash - expected) < Mathf.Max(.02f, Mathf.Abs(expected) * .00001f), "Accept sells all shares and pays authored premium exactly once: " + id);
            Check(company.state.greenmailBuyBlocked && !trading.buyShares(company, 1), "Accepted company is permanently blocked from rebuying: " + id);
            var other = companies.CompanyList.First(c => c != company);
            Check(trading.buyShares(other, 1), "Buy restriction does not affect another company: " + id);
            game.closeResult();
            var manager = new GameObject("Greenmail save verification").AddComponent<GreenmailScaleSaveManager>();
            float savedCash = player.state.cash;
            Check(manager.trySaveDayEnd(new DayStartData(clock.CurrentTurn + 1, FixedDaySeed.seed)), "Greenmail result saves through whole-game manager: " + id + " / " + manager.lastError);
            typeof(CompanyState).GetProperty("greenmailBuyBlocked").SetValue(company.state, false); Shares(company, held); player.state.addCash(-player.state.cash);
            Check(manager.tryLoad(out _) && company.state.greenmailBuyBlocked && company.state.getBaseStat(CompanyStat.PLAYER_SHARES) == 0 && player.state.cash == savedCash && delivered == 1, "Load restores cash, sold holdings and rebuy ban without replay: " + id);
        }

        yield return Ready();
        var target = companies.getCompany("haneul-food"); Shares(target, target.state.totalShares * .35f); clock.AdvanceTurn();
        var actor = Object.FindFirstObjectByType<Player>(); var trader = Object.FindFirstObjectByType<ShareTradingSystem>();
        float oldCash = actor.state.cash, oldShares = target.state.getBaseStat(CompanyStat.PLAYER_SHARES);
        game.openEvent("greenmail_01"); game.startNegotiation(); game.confirmChoice(1); game.closeResult();
        Check(actor.state.cash == oldCash && target.state.getBaseStat(CompanyStat.PLAYER_SHARES) == oldShares && !target.state.greenmailBuyBlocked, "Reject leaves money, holdings and buying rights intact");
        Check(trader.buyShares(target, 1) && trader.sellShares(target, 1), "Rejected company remains tradable");
        var second = companies.getCompany("polaris-finance"); Shares(second, second.state.totalShares * .3f); clock.AdvanceTurn();
        Check(Offer != null && Offer.companyId == second.definition.companyID, "Completion for one company does not suppress another scale's eligible offer");

        yield return Ready();
        target = companies.getCompany("haneul-food"); Shares(target, target.state.totalShares * .35f); clock.AdvanceTurn();
        var points = Object.FindFirstObjectByType<ActionPointService>(); game.openEvent("greenmail_01");
        int beforeAp = points.Current; Shares(target, target.state.totalShares * .34f); game.startNegotiation();
        Check(game.IsClosed && points.Current == beforeAp, "Falling below the new threshold before meeting cancels without AP cost");

        yield return Ready();
        target = companies.getCompany("haneul-food"); Shares(target, target.state.totalShares * .35f); clock.AdvanceTurn();
        game.openEvent("greenmail_01"); game.startNegotiation();
        actor = Object.FindFirstObjectByType<Player>(); oldCash = actor.state.cash;
        Shares(target, target.state.totalShares * .34f);
        int confirmed = 0; game.choiceConfirmed += _ => confirmed++;
        game.confirmChoice(0);
        Check(game.IsClosed && confirmed == 0 && actor.state.cash == oldCash && !target.state.greenmailBuyBlocked && !game.tryGetChoice("greenmail_01", out _), "Final confirmation rechecks ownership without partial cash, history or ban effects");

        yield return Ready();
        target = companies.getCompany("haneul-food"); Shares(target, target.state.totalShares * .35f); clock.AdvanceTurn();
        game.openEvent("greenmail_01"); points = Object.FindFirstObjectByType<ActionPointService>(); beforeAp = points.Current;
        clock.AdvanceTurn(); clock.AdvanceTurn(); game.startNegotiation();
        Check(game.IsClosed && points.Current == beforeAp, "Expired detail cannot start a greenmail meeting or consume AP");
        File.AppendAllText(GreenmailScaleVerification.Output + "results.txt", "COMPLETE\n");
    }
}
