using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using TakeOver.Events;
using TakeOver.NPC;
using TakeOver.SaveSystem;
using TakeOver.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class StoryEventVerification
{
    public const string Output = "../QA_StoryEvents/";
    static StoryEventVerification() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "results.txt", "RUNNING\n");
        SessionState.SetBool("StoryEvents.Verify", true);
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        var gameView = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
        gameView.position = new Rect(40, 40, 1280, 780); gameView.Show(); gameView.Focus();
        EditorApplication.isPlaying = true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool("StoryEvents.Verify", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode) new GameObject("Story event verification").AddComponent<StoryEventProbe>();
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool("StoryEvents.Verify", false);
            EditorApplication.Exit(File.ReadAllText(Output + "results.txt").Contains("FAIL") ? 1 : 0);
        }
    }
    public static void Build()
    {
        string buildOutput = (Environment.GetEnvironmentVariable("TAKEOVER_QA_OUTPUT") ?? Output).TrimEnd('/', '\\') + "/";
        Directory.CreateDirectory(buildOutput);
        var report = UnityEditor.BuildPipeline.BuildPlayer(new UnityEditor.BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Game.unity" },
            locationPathName = buildOutput + "Build/TakeOver.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        File.WriteAllText(buildOutput + "build-results.txt", $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\n");
        EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }
}

public sealed class StoryEventProbe : MonoBehaviour
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        File.AppendAllText(StoryEventVerification.Output + "results.txt", "PASS: " + message + "\n");
    }
    void OnEnable() { Application.logMessageReceived += Log; }
    void OnDisable() { Application.logMessageReceived -= Log; }
    void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            File.AppendAllText(StoryEventVerification.Output + "results.txt", "FAIL runtime: " + message + "\n" + stack + "\n");
    }
    IEnumerator Start()
    {
        yield return null; yield return null; yield return null;
        try { Verify(); }
        catch (Exception error) { File.AppendAllText(StoryEventVerification.Output + "results.txt", "FAIL: " + error + "\n"); }
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(StoryEventVerification.Output + "reward-notice.png"));
        yield return null;
        try
        {
            var confirm = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == "Confirm tutorial reward");
            confirm.onClick.Invoke();
        }
        catch (Exception error) { File.AppendAllText(StoryEventVerification.Output + "results.txt", "FAIL: " + error + "\n"); }
        yield return null;
        try { Check(GameObject.Find("Help introduction") != null && GameObject.Find("Tutorial completion notice") == null, "Reward confirmation transitions to help location notice"); }
        catch (Exception error) { File.AppendAllText(StoryEventVerification.Output + "results.txt", "FAIL: " + error + "\n"); }
        File.AppendAllText(StoryEventVerification.Output + "results.txt", "COMPLETE\n");
        EditorApplication.isPlaying = false;
    }
    EventDefinition Fixture(string id, bool success, EventCondition[] entrance = null)
    {
        var definition = Object.Instantiate(AssetDatabase.LoadAssetAtPath<EventDefinition>("Assets/EventSystem/Data/InsideInfo.asset"));
        var data = JObject.Parse(JsonUtility.ToJson(definition));
        data["eventId"] = id; data["title"] = id; data["apCost"] = 0; data["validDays"] = 30;
        data["conditions"] = JArray.FromObject(entrance ?? Array.Empty<EventCondition>());
        data["companyIds"] = new JArray(StoryDirector.CompanyId);
        data["choiceGeneration"] = 0;
        var payload = new NpcEventPayload
        {
            targetNpcIds = new List<string> { StoryDirector.TutorialContact },
            actorId = "player", actionId = id, interpretation = id,
            memoryType = NpcMemoryType.Custom, durationTurns = 10,
            relationDelta = new NpcRelationDelta { trust = 2 }
        };
        var good = JObject.Parse(JsonUtility.ToJson(payload));
        payload.relationDelta = new NpcRelationDelta { trust = -3 };
        var bad = JObject.Parse(JsonUtility.ToJson(payload));
        data["choices"] = new JArray(new JObject
        {
            ["choiceText"] = "조건 확인", ["choiceDescription"] = "검증용", ["playerLine"] = "확인하겠습니다.",
            ["resultDialogue"] = "조건을 만족했습니다.", ["resultValue"] = "qa-success", ["cashDelta"] = 7, ["npcResult"] = good,
            ["resultConditions"] = new JArray(JObject.FromObject(new EventCondition
            {
                kind = EventConditionKind.CompanyStake, companyId = StoryDirector.CompanyId, value = success ? 5 : 80
            })),
            ["failureOutcome"] = new JObject
            {
                ["resultDialogue"] = "조건을 만족하지 못했습니다.", ["resultValue"] = "qa-failure", ["cashDelta"] = -3, ["npcResult"] = bad
            }
        });
        JsonUtility.FromJsonOverwrite(data.ToString(), definition);
        return definition;
    }
    void Verify()
    {
        var game = Object.FindFirstObjectByType<EventSystemController>();
        var company = Object.FindFirstObjectByType<Companies>().getCompany(StoryDirector.CompanyId);
        var player = Object.FindFirstObjectByType<Player>();
        var registry = Object.FindFirstObjectByType<NpcStateRegistry>();
        var trading = Object.FindFirstObjectByType<TradingSaveAdapter>();
        var clock = Object.FindFirstObjectByType<GameTurnClock>();
        var day = new DayStartData(clock.CurrentTurn, 1);
        Check(game.StoryEvents.Count == 1 && game.StoryEvents[0].getId() == "inside_info_01", "Dedicated story DB supplies original main-story event exactly once");
        Check(!game.TutorialRewardGranted && !StoryDirector.IsRunning, "Direct game entry does not grant tutorial reward");
        var before = game.captureJson();
        float owned = company.state.getStat(CompanyStat.PLAYER_SHARES), cash = player.state.cash;
        Check(!game.GrantTutorialReward(Object.FindFirstObjectByType<Companies>().CompanyList.First(c => c != company)), "Other company cannot receive the tutorial reward");
        Check(game.GrantTutorialReward(company), "Tutorial reward grant succeeds");
        Check(company.state.getStat(CompanyStat.PLAYER_SHARES) == owned + 50 && Mathf.Abs(company.state.getDerivedStat(CompanyDerivedStat.STAKE) - 5) < .001f, "Reward adds exactly 50 shares and updates stake immediately");
        Check(player.state.cash == cash, "Reward does not spend cash");
        Check(game.GrantTutorialReward(company) && company.state.getStat(CompanyStat.PLAYER_SHARES) == owned + 50, "Repeated completion cannot duplicate reward");
        string rewarded = game.captureJson(), rewardedTrading = trading.captureJson();
        game.prepareLoad(before, 2, day); game.applyLoad(); game.rebuildAfterLoad();
        trading.prepareLoad(rewardedTrading, trading.saveVersion, day); trading.applyLoad(); trading.rebuildAfterLoad();
        game.prepareLoad(rewarded, 2, day); game.applyLoad(); game.rebuildAfterLoad();
        Check(game.TutorialRewardGranted && game.GrantTutorialReward(company) && company.state.getStat(CompanyStat.PLAYER_SHARES) == owned + 50, "Trading and reward record restore without duplicate grant");

        var definitions = (List<EventDefinition>)typeof(EventSystemController).GetField("events", Flags).GetValue(game);
        var yes = Fixture("qa-story-yes", true);
        var no = Fixture("qa-story-no", true);
        var follow = Fixture("qa-story-follow", true, new[]
        {
            new EventCondition { kind = EventConditionKind.EventCompleted, requiredEventId = "qa-story-yes" },
            new EventCondition { kind = EventConditionKind.EventChoice, requiredEventId = "qa-story-yes", requiredChoiceIndex = 0 },
            new EventCondition { kind = EventConditionKind.StorySucceeded, requiredEventId = "qa-story-yes" },
            new EventCondition { kind = EventConditionKind.StoryFailed, requiredEventId = "qa-story-no" },
            new EventCondition { kind = EventConditionKind.Day, value = game.getCurrentDay() + 2 }
        });
        var never = Fixture("qa-story-never", true, new[] { new EventCondition { kind = EventConditionKind.EventCompleted, requiredEventId = "unregistered-event" } });
        definitions.AddRange(new[] { yes, no, follow, never });
        game.onDayStarted(game.getCurrentDay() + 1);
        Check(game.getTodayEvents().Contains(yes) && game.getTodayEvents().Contains(no), "Story fixtures appear through the same live event engine");
        Check(!game.getTodayEvents().Contains(follow) && !game.getTodayEvents().Contains(never), "Unmet prerequisites and unknown event IDs do not unlock stories");
        int deliveries = 0;
        ChoiceConfirmedInfo last = null;
        game.choiceConfirmed += info => { deliveries++; last = info; };
        registry.TryGet(StoryDirector.TutorialContact, out var npc);
        int memories = npc.memories.Count;
        float startCash = player.state.cash;
        game.openEvent(yes.getId()); game.startNegotiation(); game.confirmChoice(0); game.confirmChoice(0);
        Check(deliveries == 1 && last.resultValue == "qa-success" && last.cashDelta == 7, "Satisfied branch emits success result exactly once");
        Check(player.state.cash == startCash + 7 && npc.memories.Count == memories + 1, "Success applies authored cash and NPC memory");
        Check(game.TryGetStoryResult(yes.getId(), out bool success) && success, "Success is recorded separately from selected answer");
        game.closeResult();
        game.openEvent(no.getId()); game.startNegotiation();
        float sharesBeforeFailure = company.state.getStat(CompanyStat.PLAYER_SHARES);
        company.state.setStat(CompanyStat.PLAYER_SHARES, 0);
        company.calculator.calculateAboutShares(company);
        float available = player.state.cash;
        player.state.addCash(-available);
        game.confirmChoice(0);
        Check(deliveries == 1 && !game.tryGetChoice(no.getId(), out _), "Unaffordable failure branch leaves answer uncommitted");
        player.state.addCash(available);
        game.confirmChoice(0); game.confirmChoice(0);
        Check(deliveries == 2 && last.resultValue == "qa-failure" && last.cashDelta == -3, "Unsatisfied branch emits distinct failure result exactly once");
        Check(player.state.cash == startCash + 4 && npc.memories.Count == memories + 2, "Failure applies only its own cash and NPC memory");
        Check(game.TryGetStoryResult(no.getId(), out success) && !success, "Failure remains a completed story with its own result");
        Check(last.npcResult.relationDelta.trust == -3, "Final answer rechecks changed holdings and uses authored failure NPC effect");
        game.closeResult();
        company.state.setStat(CompanyStat.PLAYER_SHARES, sharesBeforeFailure);
        company.calculator.calculateAboutShares(company);
        game.onDayStarted(game.getCurrentDay() + 1);
        Check(game.getTodayEvents().Contains(follow) && !game.getTodayEvents().Contains(never), "Day, completed answer, success and failure prerequisites unlock a follow-up together");
        Check(!game.getTodayEvents().Contains(yes) && !game.getTodayEvents().Contains(no), "Completed stories do not recur for another company");

        day = new DayStartData(game.getCurrentDay(), 1);
        string saved = game.captureJson();
        int emitted = deliveries;
        game.prepareLoad(saved, 2, day); game.applyLoad(); game.rebuildAfterLoad();
        Check(game.captureJson() == saved && deliveries == emitted, "Story results, offers and reward flag round-trip without replaying effects");
        var invalid = JObject.Parse(saved);
        ((JArray)invalid["storyResults"]).Add(((JArray)invalid["storyResults"])[0].DeepClone());
        bool rejected = false;
        try { game.prepareLoad(invalid.ToString(), 2, day); } catch (InvalidDataException) { rejected = true; }
        Check(rejected && game.captureJson() == saved, "Duplicate result records are rejected without partial state changes");
        invalid = JObject.Parse(saved); invalid.Remove("tutorialRewardGranted");
        rejected = false;
        try { game.prepareLoad(invalid.ToString(), 2, day); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "New saves require the explicit reward flag");
        invalid = JObject.Parse(saved); ((JObject)((JArray)invalid["storyResults"])[0]).Remove("succeeded");
        rejected = false;
        try { game.prepareLoad(invalid.ToString(), 2, day); } catch (InvalidDataException) { rejected = true; }
        Check(rejected && game.captureJson() == saved, "Missing outcome cannot be interpreted as a failed story");
        var legacy = JObject.Parse(saved); legacy.Remove("tutorialRewardGranted"); legacy.Remove("storyResults");
        game.prepareLoad(legacy.ToString(), 1, day); game.applyLoad(); game.rebuildAfterLoad();
        Check(!game.TutorialRewardGranted && !game.TryGetStoryResult(yes.getId(), out _) && game.tryGetChoice(yes.getId(), out _), "Legacy v1 keeps choices without inventing historical success or grants");
        game.prepareLoad(saved, 2, day); game.applyLoad(); game.rebuildAfterLoad();
        Check(game.TutorialRewardGranted && game.TryGetStoryResult(yes.getId(), out success) && success, "v2 restores its explicit history after legacy conversion");
        TakeOver.Help.HelpScreen.ShowTutorialCompletion();
        Check(GameObject.Find("Tutorial completion notice") != null && GameObject.Find("Help introduction") == null, "Reward notice precedes help notice");
        Check(Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Any(t => t.text == "튜토리얼을 완료하였습니다. 보상으로 '미래건설' 지분 5%가 지급되었습니다."), "Reward notice displays the requested exact text");
        Check(Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == "Confirm tutorial reward").GetComponentInChildren<Text>().text == "확인", "Reward action uses the shared concise confirm label");
    }
}
