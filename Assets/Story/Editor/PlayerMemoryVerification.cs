using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using TakeOver.Events;
using TakeOver.NPC;
using TakeOver.PlayerMemories;
using TakeOver.SaveSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PlayerMemoryVerification
{
    public const string Output = "../QA_PlayerMemory/";
    static PlayerMemoryVerification() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "results.txt", "RUNNING\n");
        SessionState.SetBool("PlayerMemory.Verify", true); EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        EditorApplication.isPlaying = true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool("PlayerMemory.Verify", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        { var probe = new GameObject("Player memory verification"); Object.DontDestroyOnLoad(probe); probe.AddComponent<PlayerMemoryProbe>(); }
        if (state == PlayModeStateChange.EnteredEditMode)
        { SessionState.SetBool("PlayerMemory.Verify", false); EditorApplication.Exit(File.ReadAllText(Output + "results.txt").Contains("FAIL") ? 1 : 0); }
    }
}
public sealed class PlayerMemorySaveManager : GameSaveManager
{
    protected override SaveFileStorage storage => new SaveFileStorage(Path.GetFullPath(PlayerMemoryVerification.Output + "save"));
}
public sealed class PlayerMemoryProbe : MonoBehaviour
{
    const string Key = "intent.hostile-takeover";
    const string Company = "mirae-construction";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    EventSystemController game;
    PlayerMemoryService memories;
    GameTurnClock clock;
    EventDefinition main;
    float timeout;
    void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        File.AppendAllText(PlayerMemoryVerification.Output + "results.txt", "PASS: " + description + "\n");
    }
    void OnEnable() { Application.logMessageReceived += Log; }
    void OnDisable() { Application.logMessageReceived -= Log; }
    void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            File.AppendAllText(PlayerMemoryVerification.Output + "results.txt", "FAIL runtime: " + message + "\n" + stack + "\n");
    }
    void Update()
    {
        if (timeout > 0 && Time.realtimeSinceStartup > timeout)
        { File.AppendAllText(PlayerMemoryVerification.Output + "results.txt", "FAIL: timeout\n"); timeout = 0; EditorApplication.isPlaying = false; }
    }
    IEnumerator Start()
    {
        timeout = Time.realtimeSinceStartup + 180;
        var stack = new Stack<IEnumerator>(); stack.Push(Verify());
        while (stack.Count > 0)
        {
            var routine = stack.Peek(); bool next = false; object current = null;
            try { next = routine.MoveNext(); if (next) current = routine.Current; }
            catch (Exception error) { File.AppendAllText(PlayerMemoryVerification.Output + "results.txt", "FAIL: " + error + "\n"); break; }
            if (!next) { stack.Pop(); continue; }
            if (current is IEnumerator nested) stack.Push(nested); else yield return current;
        }
        timeout = 0; EditorApplication.isPlaying = false;
    }
    IEnumerator Ready(bool reload = true)
    {
        if (reload) SceneManager.LoadScene("Game");
        yield return null; yield return null; yield return null;
        game = Object.FindFirstObjectByType<EventSystemController>(); memories = Object.FindFirstObjectByType<PlayerMemoryService>();
        clock = Object.FindFirstObjectByType<GameTurnClock>(); main = AssetDatabase.LoadAssetAtPath<EventDefinition>("Assets/EventSystem/Data/InsideInfo.asset");
        var company = Object.FindFirstObjectByType<Companies>().getCompany(Company);
        company.state.setStat(CompanyStat.PLAYER_SHARES, company.state.totalShares * .1f);
        company.calculator.calculateAboutShares(company); game.refreshAvailability();
    }
    void MainChoice(int index)
    { game.openEvent(main.getId()); game.startNegotiation(); game.confirmChoice(index); game.confirmChoice(index); game.closeResult(); }
    bool Meets(EventDefinition definition) => (bool)typeof(EventSystemController).GetMethod("meetsConditions", Flags, null,
        new[] { typeof(EventDefinition), typeof(string) }, null).Invoke(game, new object[] { definition, Company });
    EventDefinition Conditional(string id, bool gate, EventConditionKind kind = EventConditionKind.PlayerHasMemory)
    {
        var asset = Object.Instantiate(main);
        var data = JObject.Parse(JsonUtility.ToJson(asset)); data["eventId"] = id; data["apCost"] = 0; data["validDays"] = 30;
        var condition = JObject.FromObject(new EventCondition { kind = kind, playerMemoryKey = Key, companyId = Company });
        data["conditions"] = gate ? new JArray(condition) : new JArray();
        data["choices"] = new JArray(new JObject
        {
            ["choiceText"] = "계획 검토", ["choiceDescription"] = "검증용", ["playerLine"] = "검토하겠습니다.",
            ["resultDialogue"] = "의도에 맞춘 응답입니다.", ["resultValue"] = "intent-present", ["cashDelta"] = 0,
            ["npcResult"] = new JObject { ["targetNpcIds"] = new JArray(), ["targetReactions"] = new JArray() },
            ["resultConditions"] = new JArray(condition.DeepClone()),
            ["failureOutcome"] = new JObject { ["resultDialogue"] = "일반적인 응답입니다.", ["resultValue"] = "intent-absent", ["cashDelta"] = 0 }
        });
        JsonUtility.FromJsonOverwrite(data.ToString(), asset);
        ((List<EventDefinition>)typeof(EventSystemController).GetField("events", Flags).GetValue(game)).Add(asset);
        return asset;
    }
    IEnumerator Verify()
    {
        yield return Ready(false);
        Check(memories != null && Object.FindFirstObjectByType<Player>().GetComponent<PlayerMemoryService>() == memories && memories.GetMemories().Count == 0, "Player owns an initially empty memory store without becoming an NPC");
        Check(main.getChoices()[1].getPlayerMemories().Count == 1 && main.getChoices()[0].getPlayerMemories().Count == 0 && main.getChoices()[2].getPlayerMemories().Count == 0, "Only the aggressive-preparation choice grants the authored permanent intent");
        MainChoice(0); Check(!memories.HasMemory(Key), "Sincere cooperation does not create hostile intent");
        yield return Ready(); MainChoice(2); Check(!memories.HasMemory(Key), "Declining cooperation does not create hostile intent");

        yield return Ready();
        IPlayerMemoryReader journal = memories;
        int added = 0, restored = 0;
        bool choiceObserverSawMemory = false, journalSawNpcEffect = false;
        journal.MemoryAdded += _ =>
        {
            added++;
            Object.FindFirstObjectByType<NpcStateRegistry>().TryGet("npc-mirae-construction-cfo-seo-jia", out var state);
            journalSawNpcEffect = state.memories.Count > 0;
        };
        journal.MemoriesRestored += _ => restored++;
        game.choiceConfirmed += info => { if (info.eventId == main.getId()) choiceObserverSawMemory = journal.HasMemory(Key, Company); };
        var gated = Conditional("qa-memory-gated", true);
        var absent = Conditional("qa-memory-absent", true, EventConditionKind.PlayerLacksMemory);
        Check(!Meets(gated) && Meets(absent), "Presence and absence conditions evaluate before the private intent exists");
        var noIntent = Conditional("qa-no-intent-response", false);
        clock.AdvanceTurn();
        ChoiceConfirmedInfo observed = null; game.choiceConfirmed += value => observed = value;
        game.openEvent(noIntent.getId()); game.startNegotiation(); game.confirmChoice(0); game.closeResult();
        Check(observed != null && observed.resultValue == "intent-absent", "A real future choice takes its fallback response when memory is absent");
        MainChoice(1);
        var record = journal.GetMemories().Single();
        Check(added == 1 && choiceObserverSawMemory && journalSawNpcEffect, "Choice observers see committed intent and journal notification follows NPC effects exactly once");
        Check(record.Key == Key && record.CompanyId == Company && record.Kind == PlayerMemoryKind.Intent && record.Permanent && record.IsPrivate && record.OwnerId == "player", "Stored record is a private permanent player intent for Mirae");
        Check(record.Title.Contains("공격적 인수") && record.Description.Contains("검토") && record.CreatedTurn == clock.CurrentTurn && record.SourceKind == PlayerMemorySourceKind.EventChoice && record.SourceId == main.getId() && record.SourceChoiceIndex == 1 && record.SourceOccurrenceId == observed.occurrenceId, "Journal metadata preserves exact text, turn, event, occurrence and original choice");
        Check(journal.HasMemory(Key) && journal.HasMemory(Key, Company) && !journal.HasMemory(Key, "haneul-food"), "Readers can query global intent or exact company scope");
        Check(Meets(gated) && !Meets(absent), "The same event prerequisites flip after intent is committed");
        Object.FindFirstObjectByType<NpcStateRegistry>().TryGet("npc-mirae-construction-cfo-seo-jia", out var seo);
        Check(seo.memories.All(memory => !memory.interpretation.Contains("공격적") && !memory.interpretation.Contains("인수")), "Seo's observational memory does not expose the private takeover plan");
        var origin = new PlayerMemoryOrigin(record.SourceKind, record.SourceId, record.SourceOccurrenceId, record.CompanyId, record.CreatedTurn, record.SourceChoiceIndex);
        memories.Record(main.getChoices()[1].getPlayerMemories(), origin);
        Check(added == 1 && journal.GetMemories().Count == 1, "Repeated event delivery cannot duplicate permanent memory or journal entries");
        bool readOnly = false;
        try { ((IList<string>)record.Tags).Add("external-mutation"); } catch (NotSupportedException) { readOnly = true; }
        Check(readOnly && !record.Tags.Contains("external-mutation"), "Journal readers cannot mutate stored tags");
        var withIntent = Conditional("qa-with-intent-response", false);
        clock.AdvanceTurn();
        game.openEvent(withIntent.getId()); game.startNegotiation(); game.confirmChoice(0); game.closeResult();
        Check(observed.resultValue == "intent-present" && game.getTodayEvents().Contains(gated) && !game.getTodayEvents().Contains(absent), "Real subsequent choice and story availability consume the memory conditions");
        while (clock.CurrentTurn < record.CreatedTurn + 4)
        {
            int before = clock.CurrentTurn; clock.AdvanceTurn();
            Check(clock.CurrentTurn == before + 1, "Actual day advances while checking memory persistence");
            yield return null;
        }
        Check(journal.HasMemory(Key, Company) && journal.GetMemories().Count == 1 && journal.GetMemories()[0].CreatedTurn == record.CreatedTurn && added == 1, "Progress beyond the NPC memory lifetime does not decay or repeat permanent intent");
        Check(seo.memories.Count == 0, "NPC temporary memory can expire independently of permanent player intent");

        var manager = new GameObject("Isolated player memory save").AddComponent<PlayerMemorySaveManager>();
        Check(manager.trySaveDayEnd(new DayStartData(clock.CurrentTurn + 1, FixedDaySeed.seed)), "Whole-game save includes private memories: " + manager.lastError);
        var storage = new SaveFileStorage(Path.GetFullPath(PlayerMemoryVerification.Output + "save"));
        string validFile = storage.read(), expectedMemory = memories.captureJson();
        var file = JsonUtility.FromJson<SaveFileData>(validFile);
        Check(file.formatVersion == 3 && file.sections.Count(section => section.id == "player-memories") == 1, "New whole save format has one explicit player-memory section");
        Check(manager.tryLoad(out _) && memories.captureJson() == expectedMemory && added == 1 && restored == 1, "Load restores memory using reset notification instead of replaying journal additions");

        var actor = Object.FindFirstObjectByType<Player>(); actor.state.addCash(333 - actor.state.cash);
        var missing = JsonUtility.FromJson<SaveFileData>(validFile); missing.sections.RemoveAll(section => section.id == "player-memories");
        storage.write(JsonUtility.ToJson(missing));
        Check(!manager.tryLoad(out _) && actor.state.cash == 333 && memories.captureJson() == expectedMemory, "Missing memory section in a new save rejects the entire load without partial state changes");
        var memorySection = file.sections.Single(section => section.id == "player-memories");
        foreach (string invalid in new[] { "permanent", "ownerId", "kind", "duplicate" })
        {
            var corrupt = JObject.Parse(expectedMemory); var first = (JObject)((JArray)corrupt["memories"])[0];
            if (invalid == "permanent") first["permanent"] = false;
            if (invalid == "ownerId") first["ownerId"] = "npc-not-player";
            if (invalid == "kind") first["kind"] = 999;
            if (invalid == "duplicate") ((JArray)corrupt["memories"]).Add(first.DeepClone());
            memorySection.json = corrupt.ToString(); storage.write(JsonUtility.ToJson(file));
            Check(!manager.tryLoad(out _) && actor.state.cash == 333 && memories.captureJson() == expectedMemory, "Malformed private memory rejects whole save atomically: " + invalid);
        }
        var legacy = JsonUtility.FromJson<SaveFileData>(validFile); legacy.formatVersion = 2; legacy.sections.RemoveAll(section => section.id == "player-memories");
        storage.write(JsonUtility.ToJson(legacy));
        Check(manager.tryReadDayStart(out _) && manager.tryLoad(out _) && memories.GetMemories().Count == 0 && game.tryGetChoice(main.getId(), out int previous) && previous == 1, "Legacy v2 save keeps known choices and initializes an empty ledger without inventing historical intent");
        Check(added == 1 && restored == 2, "Legacy initialization rebuilds the journal without new-entry notifications");
        legacy.sections.RemoveAll(section => section.id == "events"); storage.write(JsonUtility.ToJson(legacy));
        Check(!manager.tryLoad(out _), "Legacy conversion cannot fill missing event history or weaken other required sections");
        storage.write(validFile);
        Check(manager.tryLoad(out _) && memories.captureJson() == expectedMemory && journal.HasMemory(Key, Company) && added == 1, "A valid new save remains recoverable after corrupt and legacy files");

        yield return Ready();
        Check(!memories.HasMemory(Key), "Fresh game scene does not carry another playthrough's permanent memories");
        int safeObserver = 0;
        memories.MemoryAdded += _ => throw new Exception("Journal observer test");
        memories.MemoryAdded += _ => safeObserver++;
        var grant = new PlayerMemoryGrant { key = "test.system-intent", title = "시스템 기록", description = "일지 구독 검증", kind = PlayerMemoryKind.Observation };
        memories.Record(new[] { grant }, new PlayerMemoryOrigin(PlayerMemorySourceKind.System, "qa-system", "qa-system-once", "", clock.CurrentTurn));
        grant.tags.Add("changed-after-record");
        Check(safeObserver == 1 && memories.HasMemory(grant.key) && !memories.GetMemories().Single().Tags.Contains("changed-after-record"), "Observer failure and later template edits cannot corrupt memory or block other journal consumers");
        File.AppendAllText(PlayerMemoryVerification.Output + "results.txt", "COMPLETE\n");
    }
}
