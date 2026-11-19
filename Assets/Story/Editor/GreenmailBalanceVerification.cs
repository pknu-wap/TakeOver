using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
public static class GreenmailBalanceVerification
{
    public const string Output = "../QA_GreenmailBalance/";
    static GreenmailBalanceVerification() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "results.txt", "RUNNING\n");
        SessionState.SetBool("GreenmailBalance.Verify", true);
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        EditorApplication.isPlaying = true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool("GreenmailBalance.Verify", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            var probe = new GameObject("Greenmail balance verification");
            Object.DontDestroyOnLoad(probe); probe.AddComponent<GreenmailBalanceProbe>();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool("GreenmailBalance.Verify", false);
            EditorApplication.Exit(File.ReadAllText(Output + "results.txt").Contains("FAIL") ? 1 : 0);
        }
    }
}

public sealed class GreenmailBalanceProbe : MonoBehaviour
{
    EventSystemController game;
    Companies companies;
    GameTurnClock clock;
    void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        File.AppendAllText(GreenmailBalanceVerification.Output + "results.txt", "PASS: " + description + "\n");
    }
    void OnEnable() { Application.logMessageReceived += Log; }
    void OnDisable() { Application.logMessageReceived -= Log; }
    void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            File.AppendAllText(GreenmailBalanceVerification.Output + "results.txt", "FAIL runtime: " + message + "\n" + stack + "\n");
    }
    IEnumerator Start()
    {
        var stack = new Stack<IEnumerator>(); stack.Push(Survey());
        while (stack.Count > 0)
        {
            var routine = stack.Peek(); bool next = false; object current = null;
            try { next = routine.MoveNext(); if (next) current = routine.Current; }
            catch (Exception error) { File.AppendAllText(GreenmailBalanceVerification.Output + "results.txt", "FAIL: " + error + "\n"); break; }
            if (!next) { stack.Pop(); continue; }
            if (current is IEnumerator nested) stack.Push(nested); else yield return current;
        }
        EditorApplication.isPlaying = false;
    }
    IEnumerator Ready()
    {
        SceneManager.LoadScene("Game");
        yield return null; yield return null; yield return null;
        game = Object.FindFirstObjectByType<EventSystemController>();
        companies = Object.FindFirstObjectByType<Companies>();
        clock = Object.FindFirstObjectByType<GameTurnClock>();
        // 필수 스토리 완료 후를 재현해 경제 관측이 이야기 기한으로 중단되지 않게 한다.
        var data = JsonUtility.FromJson<EventSystemController.EventSaveData>(game.captureJson());
        data.mandatoryState = MandatoryStoryState.Completed;
        data.choices.Add(new EventSystemController.ChoiceData { eventId = EventSystemController.MandatoryEventId, companyId = "", choice = 0 });
        data.companyChoices.Add(new EventSystemController.ChoiceData { eventId = EventSystemController.MandatoryEventId, companyId = EventSystemController.MandatoryCompanyId, choice = 0 });
        data.storyResults.Add(new EventSystemController.StoryResultData { eventId = EventSystemController.MandatoryEventId, succeeded = true });
        data.occurrences.Clear();
        game.prepareLoad(JsonUtility.ToJson(data), game.saveVersion, new DayStartData(1, 1234));
        game.applyLoad(); game.rebuildAfterLoad();
    }
    IEnumerator Survey()
    {
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        string path = GreenmailBalanceVerification.Output;
        using (var csv = new StreamWriter(path + "survey.csv"))
        {
            csv.WriteLine("seed,holding,day,company,value,required,eligible,status,offer,remaining");
            int offers = 0, priceCancellations = 0, observedOfferNextDays = 0, badPrices = 0, nontradableOffers = 0;
            foreach (int holding in new[] { 25, 30, 35 })
            foreach (int seed in Enumerable.Range(1, 12))
            {
                yield return Ready();
                foreach (var company in companies.CompanyList)
                {
                    company.state.setStat(CompanyStat.PLAYER_SHARES, company.state.totalShares * holding / 100f);
                    company.calculator.calculateAboutShares(company);
                }
                string previousCompany = null; int previousRemaining = 0;
                for (int day = 2; day <= 85; day++)
                {
                    FixedDaySeed.changeSeed(seed * 100003 + day * 7919);
                    UnityEngine.Random.InitState(seed * 100003 + day * 7919);
                    clock.AdvanceTurn();
                    CheckTurn(clock.CurrentTurn == day, seed, holding, day);
                    var offer = game.getTodayEventInfos().SingleOrDefault(x => x.definition.getId() == "greenmail_01");
                    bool retained = offer != null && offer.companyId == previousCompany && previousRemaining > 1;
                    if (offer != null && !retained) offers++;
                    if (previousCompany != null && previousRemaining > 1)
                    {
                        observedOfferNextDays++;
                        var previous = companies.getCompany(previousCompany);
                        CompanyScaleRules.TryGetGreenmailStake(previous.state.getDerivedStat(CompanyDerivedStat.MARKET_VALUE), out float required);
                        if (!retained && holding < required) priceCancellations++;
                    }
                    foreach (var company in companies.CompanyList)
                    {
                        float value = company.state.getDerivedStat(CompanyDerivedStat.MARKET_VALUE);
                        bool valid = CompanyScaleRules.TryGetGreenmailStake(value, out float required);
                        if (!valid) badPrices++;
                        bool shown = offer != null && offer.companyId == company.definition.companyID;
                        if (shown && (company.state.status == CompanyStatus.Liquidation || company.state.status == CompanyStatus.Delisted)) nontradableOffers++;
                        csv.WriteLine(string.Join(",", seed, holding, day, company.definition.companyID,
                            value.ToString("R", CultureInfo.InvariantCulture), required, valid && holding >= required ? 1 : 0,
                            company.state.status, shown ? 1 : 0, shown ? offer.remainingDays : 0));
                    }
                    previousCompany = offer?.companyId; previousRemaining = offer?.remainingDays ?? 0;
                    if (day % 7 == 0) { csv.Flush(); yield return null; }
                }
                Check(true, "84 economic turns recorded: seed=" + seed + ", holdings=" + holding);
            }
            File.WriteAllText(path + "survey-summary.txt", "offers=" + offers + "\nnextDayOfferObservations=" + observedOfferNextDays
                + "\npriceCancellations=" + priceCancellations + "\ninvalidValuations=" + badPrices + "\nnontradableOffers=" + nontradableOffers + "\n");
            Check(badPrices == 0, "All surveyed runtime company values remain valid");
            Check(priceCancellations == 0, "Price movements alone never withdraw issued offers");
            Check(nontradableOffers == 0, "Liquidation and delisted companies never issue greenmail offers");
        }
        File.AppendAllText(path + "results.txt", "COMPLETE\n");
    }
    void CheckTurn(bool value, int seed, int holding, int day)
    {
        if (!value) throw new Exception("Turn blocked: " + seed + "/" + holding + "/" + day);
    }
}
