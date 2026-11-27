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
        foreach (var entry in new[] { (999f, CompanyScale.Small, 35f), (1000f, CompanyScale.Medium, 30f), (9999f, CompanyScale.Medium, 30f), (10000f, CompanyScale.Large, 25f) })
        {
            Check(CompanyScaleRules.TryGetScale(entry.Item1, out var scale) && scale == entry.Item2 && CompanyScaleRules.TryGetGreenmailStake(entry.Item1, out float required) && required == entry.Item3, "Value boundary maps to current scale and stake: " + entry.Item1);
        }
        foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
        {
            Check(!CompanyScaleRules.TryGetGreenmailStake(invalid, out _), "Invalid current value does not silently qualify: " + invalid);
        }
        Check(!new EventWorldReader(companies, null).tryGetGreenmailStake("missing-company", out _, out _), "Missing company is rejected");
        string[] ids = companies.CompanyList.Select(c => c.definition.companyID).ToArray();
        Check(ids.Length == 9 && definition.getConditions().Single().kind == EventConditionKind.CompanyGreenmailStake, "Live greenmail asset uses scale condition for all nine registered companies");
        int small = 0, medium = 0, large = 0;
        foreach (string id in ids)
        {
            yield return Ready();
            var company = companies.getCompany(id);
            Check(CompanyScaleRules.TryGetScale(company.state.getDerivedStat(CompanyDerivedStat.MARKET_VALUE), out var scale) && CompanyScaleRules.TryGetGreenmailStake(company.state.getDerivedStat(CompanyDerivedStat.MARKET_VALUE), out _), "Registered company has a valid current scale: " + id);
            CompanyScaleRules.TryGetGreenmailStake(company.state.getDerivedStat(CompanyDerivedStat.MARKET_VALUE), out float threshold);
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
            Shares(company, requiredShares); clock.AdvanceTurn();
            Check(Offer != null && Offer.companyId == id && !string.IsNullOrEmpty(Offer.speakerNpcId), "Actual event selects the qualified company and its representative: " + id);
        }
        Check(small + medium + large == 9, "All nine companies have a valid runtime valuation");

        // 신규 5곳을 포함한 모든 회사의 성장·축소를 실제 후보 생성 경로로 검증한다.
        foreach (string id in ids)
        {
            foreach (var entry in new[] { (999f, 35f), (1000f, 30f), (9999f, 30f), (10000f, 25f) })
            {
                yield return Ready();
                var company = companies.getCompany(id);
                company.state.setDerivedStat(CompanyDerivedStat.MARKET_VALUE, entry.Item1);
                Shares(company, company.state.totalShares * .30f);
                Check(new EventWorldReader(companies, null).tryGetGreenmailStake(id, out _, out float required) && required == entry.Item2,
                    "Current valuation controls threshold regardless of initial scale: " + id + "/" + entry.Item1);
                game.onDayStarted(clock.CurrentTurn + 1);
                Check((Offer != null && Offer.companyId == id) == (entry.Item2 <= 30),
                    "Current value controls actual card at 30 percent holdings: " + id + "/" + entry.Item1);
                company.state.setDerivedStat(CompanyDerivedStat.MARKET_VALUE, 999);
                Check(Meets(company) == (entry.Item2 <= 30), "Issued offer keeps its required stake after shrinking: " + id);
                company.state.setDerivedStat(CompanyDerivedStat.MARKET_VALUE, 10000);
                Check(Meets(company), "Growth preserves eligibility under the issued or newly evaluated terms: " + id);
            }
        }

        // 경제 갱신 구독 순서와 무관하게 모든 갱신이 끝난 뒤 판정한다.
        yield return Ready();
        var growing = companies.getCompany("haneul-food");
        Shares(growing, growing.state.totalShares * .30f);
        Action<int> growOnTurn = _ => growing.state.setDerivedStat(CompanyDerivedStat.MARKET_VALUE, 10000);
        clock.TurnAdvanced += growOnTurn;
        clock.AdvanceTurn();
        clock.TurnAdvanced -= growOnTurn;
        Check(Offer != null && Offer.companyId == growing.definition.companyID, "Daily candidate uses valuation after all economic turn updates");

        // 신규 5곳을 포함해 모든 회사의 실제 수락 효과와 전체 저장을 확인한다.
        foreach (string id in ids)
        {
            yield return Ready();
            var company = companies.getCompany(id);
            CompanyScaleRules.TryGetGreenmailStake(company.state.getDerivedStat(CompanyDerivedStat.MARKET_VALUE), out float threshold);
            int held = Mathf.CeilToInt(company.state.totalShares * threshold / 100);
            Shares(company, held); clock.AdvanceTurn();
            var player = Object.FindFirstObjectByType<Player>(); var trading = Object.FindFirstObjectByType<ShareTradingSystem>();
            double price = company.state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE), cash = player.state.cash;
            int delivered = 0; game.choiceConfirmed += _ => delivered++;
            game.openEvent("greenmail_01"); game.startNegotiation(); game.confirmChoice(0); game.confirmChoice(0);
            double expected = cash + price * held * (1 + definition.getChoices()[0].getBuybackPremiumRatio());
            Check(delivered == 1 && company.state.getBaseStat(CompanyStat.PLAYER_SHARES) == 0 && System.Math.Abs(player.state.cash - expected) < System.Math.Max(.02d, System.Math.Abs(expected) * .00001d), "Accept sells all shares and pays authored premium exactly once: " + id);
            Check(company.state.greenmailBuyBlocked && !trading.buyShares(company, 1), "Accepted company is permanently blocked from rebuying: " + id);
            var other = companies.CompanyList.First(c => c != company);
            Check(trading.buyShares(other, 1), "Buy restriction does not affect another company: " + id);
            game.closeResult();
            var manager = new GameObject("Greenmail save verification").AddComponent<GreenmailScaleSaveManager>();
            double savedCash = player.state.cash;
            Check(manager.trySaveDayEnd(new DayStartData(clock.CurrentTurn + 1, FixedDaySeed.seed)), "Greenmail result saves through whole-game manager: " + id + " / " + manager.lastError);
            typeof(CompanyState).GetProperty("greenmailBuyBlocked").SetValue(company.state, false); Shares(company, held); player.state.addCash(-player.state.cash);
            Check(manager.tryLoad(out _) && company.state.greenmailBuyBlocked && company.state.getBaseStat(CompanyStat.PLAYER_SHARES) == 0 && player.state.cash == savedCash && delivered == 1, "Load restores cash, sold holdings and rebuy ban without replay: " + id);
        }

        yield return Ready();
        var target = companies.getCompany("haneul-food"); Shares(target, target.state.totalShares * .35f); clock.AdvanceTurn();
        var actor = Object.FindFirstObjectByType<Player>(); var trader = Object.FindFirstObjectByType<ShareTradingSystem>();
        double oldCash = actor.state.cash; float oldShares = target.state.getBaseStat(CompanyStat.PLAYER_SHARES);
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

        // 발행 시점 기준과 현재 거래 조건을 분리한다.
        yield return Ready();
        target = companies.getCompany("sinseong-development");
        target.state.setDerivedStat(CompanyDerivedStat.MARKET_VALUE, 10001);
        Shares(target, target.state.totalShares * .25f);
        clock.RestoreTurn(2); game.onDayStarted(2);
        Check(Offer != null && Offer.companyId == target.definition.companyID, "Large company issues a 25 percent offer");
        target.marketState.eventPriceMultiplier = .5f;
        target.calculator.recalculate(target, Object.FindFirstObjectByType<Marketdata>());
        game.refreshAvailability();
        Check(Offer != null && Meets(target), "Price drop after issue cannot withdraw an otherwise valid offer");
        var snapshot = JsonUtility.FromJson<EventSystemController.EventSaveData>(game.captureJson());
        Check(snapshot.occurrences.Single(x => x.eventId == "greenmail_01").greenmailStake == 25, "Save captures issued stake rather than current scale");
        var manager2 = new GameObject("Offer snapshot save verification").AddComponent<GreenmailScaleSaveManager>();
        Check(manager2.trySaveDayEnd(new DayStartData(3, FixedDaySeed.seed)), "Pending offer saves through whole-game manager");
        Check(manager2.tryLoad(out _) && Offer != null && Meets(target) && target.state.getDerivedStat(CompanyDerivedStat.MARKET_VALUE) < 10000,
            "Whole-game reload preserves offer after a persisted scale decrease");
        game.openEvent("greenmail_01");
        target.state.setDerivedStat(CompanyDerivedStat.MARKET_VALUE, 999);
        game.startNegotiation();
        Check(!game.IsClosed && game.getNegotiationChoiceCount() > 0, "Scale decrease before meeting does not cancel a valid issued offer");
        actor = Object.FindFirstObjectByType<Player>(); oldCash = actor.state.cash;
        game.confirmChoice(0); game.confirmChoice(0);
        Check(target.state.greenmailBuyBlocked && target.state.getBaseStat(CompanyStat.PLAYER_SHARES) == 0 && actor.state.cash > oldCash,
            "Issued stake remains valid at final confirmation and sale happens once");
        game.closeResult();

        // 저장 손상은 적용 전에 거부한다. 이전 저장에는 복원 시점 값으로 요구 지분을 채운다.
        yield return Ready();
        target = companies.getCompany("sinseong-development");
        target.state.setDerivedStat(CompanyDerivedStat.MARKET_VALUE, 10001);
        Shares(target, target.state.totalShares * .25f); clock.RestoreTurn(2); game.onDayStarted(2);
        string validOfferSave = game.captureJson();
        var corrupt = JsonUtility.FromJson<EventSystemController.EventSaveData>(validOfferSave);
        corrupt.occurrences.Single(x => x.eventId == "greenmail_01").greenmailStake = 1;
        bool rejected = false;
        try { game.prepareLoad(JsonUtility.ToJson(corrupt), game.saveVersion, new DayStartData(2, FixedDaySeed.seed)); }
        catch (InvalidDataException) { rejected = true; }
        Check(rejected && game.captureJson() == validOfferSave, "Corrupt issued stake is rejected without changing the current offer");
        var missingStake = Newtonsoft.Json.Linq.JObject.Parse(validOfferSave);
        foreach (var item in missingStake["occurrences"])
            if ((string)item["eventId"] == "greenmail_01") ((Newtonsoft.Json.Linq.JObject)item).Remove("greenmailStake");
        rejected = false;
        try { game.prepareLoad(missingStake.ToString(), game.saveVersion, new DayStartData(2, FixedDaySeed.seed)); }
        catch (InvalidDataException) { rejected = true; }
        Check(rejected && game.captureJson() == validOfferSave, "Missing v4 issued stake is rejected without changing the current offer");
        game.prepareLoad(validOfferSave, 3, new DayStartData(2, FixedDaySeed.seed)); game.applyLoad(); game.rebuildAfterLoad();
        Check(Offer != null && JsonUtility.FromJson<EventSystemController.EventSaveData>(game.captureJson()).occurrences.Single(x => x.eventId == "greenmail_01").greenmailStake == 25,
            "Legacy v3 offer initializes its stake after trading state restoration");

        target.marketState.eventPriceMultiplier = .5f;
        target.calculator.recalculate(target, Object.FindFirstObjectByType<Marketdata>());
        var randomBeforeLoad = UnityEngine.Random.state;
        game.prepareLoad(missingStake.ToString(), 3, new DayStartData(2, FixedDaySeed.seed)); game.applyLoad(); game.rebuildAfterLoad();
        var migrated = JsonUtility.FromJson<EventSystemController.EventSaveData>(game.captureJson());
        Check(Offer == null && migrated.occurrences.Single(x => x.eventId == "greenmail_01").greenmailStake == 30,
            "Legacy save without authored stake uses restored current scale rather than the old initial scale");
        Check(UnityEngine.Random.state.Equals(randomBeforeLoad), "Legacy offer migration consumes no randomness");
        Shares(target, target.state.totalShares * .30f); game.refreshAvailability();
        Check(Offer != null, "Migrated pending offer becomes usable when restored required holdings are met");
        string migratedJson = game.captureJson();
        game.prepareLoad(migratedJson, game.saveVersion, new DayStartData(2, FixedDaySeed.seed)); game.applyLoad(); game.rebuildAfterLoad();
        Check(game.captureJson() == migratedJson && Offer != null, "Migrated offer resaves and reloads as v4 without changing its terms");

        foreach (var status in new[] { CompanyStatus.Normal, CompanyStatus.Watchlisted, CompanyStatus.Delisted, CompanyStatus.Liquidation })
        {
            target.state.status = status;
            bool eligible = new EventWorldReader(companies, null).tryGetGreenmailStake(target.definition.companyID, out _, out _);
            Check(eligible == (status == CompanyStatus.Normal || status == CompanyStatus.Watchlisted), "Company trading status controls greenmail eligibility: " + status);
        }
        target.state.status = CompanyStatus.Normal;
        foreach (float price in new[] { 0, -1, float.NaN, float.PositiveInfinity })
        {
            target.state.setDerivedStat(CompanyDerivedStat.SHARE_PRICE, price);
            Check(!new EventWorldReader(companies, null).tryGetGreenmailStake(target.definition.companyID, out _, out _), "Nontradable price cannot produce a completed greenmail transaction: " + price);
        }
        target.state.setDerivedStat(CompanyDerivedStat.SHARE_PRICE, 1);
        typeof(CompanyState).GetProperty("greenmailBuyBlocked").SetValue(target.state, true);
        Check(!new EventWorldReader(companies, null).tryGetGreenmailStake(target.definition.companyID, out _, out _), "Already bought-back company cannot issue another offer");

        yield return Ready();
        target = companies.getCompany("haneul-food"); Shares(target, target.state.totalShares * .35f); clock.AdvanceTurn();
        game.openEvent("greenmail_01"); game.startNegotiation();
        target.state.status = CompanyStatus.Liquidation;
        actor = Object.FindFirstObjectByType<Player>(); oldCash = actor.state.cash;
        oldShares = target.state.getBaseStat(CompanyStat.PLAYER_SHARES);
        game.confirmChoice(0);
        Check(game.IsClosed && actor.state.cash == oldCash && target.state.getBaseStat(CompanyStat.PLAYER_SHARES) == oldShares && !target.state.greenmailBuyBlocked && !game.tryGetChoice("greenmail_01", out _),
            "Liquidation during a meeting cancels before cash, holdings, completion or buy ban effects");

        // 같은 회사의 무응답 제안은 기한과 재발행 간격을 지키고, 새 제안은 새 규모로 계산한다.
        yield return Ready();
        target = companies.getCompany("haneul-food"); Shares(target, target.state.totalShares * .35f);
        clock.RestoreTurn(2); game.onDayStarted(2);
        Check(Offer != null, "Small company offer starts its two-day validity window");
        target.state.setDerivedStat(CompanyDerivedStat.MARKET_VALUE, 10000);
        Shares(target, target.state.totalShares * .30f);
        Check(!Meets(target), "Growing company cannot weaken the required stake of an already issued offer");
        Shares(target, target.state.totalShares * .35f);
        clock.RestoreTurn(3); game.onDayStarted(3);
        Check(Offer != null && Offer.remainingDays == 1, "Unanswered offer survives only through its second day");
        clock.RestoreTurn(4); game.onDayStarted(4);
        Check(Offer == null, "Offer expires exactly after its two-day validity window");
        Shares(target, target.state.totalShares * .25f);
        for (int day = 5; day < 11; day++)
        {
            clock.RestoreTurn(day); game.onDayStarted(day);
            Check(Offer == null, "Same-company offer stays absent during seven-day expiry cooldown: day " + day);
        }
        clock.RestoreTurn(11); game.onDayStarted(11);
        Check(Offer != null && JsonUtility.FromJson<EventSystemController.EventSaveData>(game.captureJson()).occurrences.Single(x => x.eventId == "greenmail_01").greenmailStake == 25,
            "First reissue after cooldown uses the new current company scale");

        foreach (string id in ids)
        {
            yield return Ready();
            target = companies.getCompany(id);
            CompanyScaleRules.TryGetGreenmailStake(target.state.getDerivedStat(CompanyDerivedStat.MARKET_VALUE), out float threshold);
            Shares(target, Mathf.CeilToInt(target.state.totalShares * threshold / 100)); clock.AdvanceTurn();
            actor = Object.FindFirstObjectByType<Player>(); oldCash = actor.state.cash;
            oldShares = target.state.getBaseStat(CompanyStat.PLAYER_SHARES);
            int notifications = 0; game.choiceConfirmed += _ => notifications++;
            game.openEvent("greenmail_01"); game.startNegotiation(); game.confirmChoice(1); game.confirmChoice(1); game.closeResult();
            Check(notifications == 1 && actor.state.cash == oldCash && target.state.getBaseStat(CompanyStat.PLAYER_SHARES) == oldShares && !target.state.greenmailBuyBlocked,
                "Reject preserves holdings, cash and buying rights and notifies exactly once: " + id);
            Check(game.tryGetChoice("greenmail_01", id, out int rejectedChoice) && rejectedChoice == 1,
                "Reject records completion only for the offered company: " + id);
            clock.AdvanceTurn();
            Check(Offer == null, "Completed rejection cannot reissue another offer for the same company: " + id);
        }
        File.AppendAllText(GreenmailScaleVerification.Output + "results.txt", "COMPLETE\n");
    }
}
