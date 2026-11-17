using System;
using System.Collections;
using System.IO;
using System.Linq;
using TakeOver.Documents;
using TakeOver.Events;
using TakeOver.MainMenu;
using TakeOver.NPC;
using TakeOver.SaveSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CampaignReviewVerification
{
    public const string Root = "../QA_CampaignReview/";
    public static string Result => Root + (SessionState.GetBool("Review.ExpectDocumentBroken", false) ? "before-document-fix.txt" : SessionState.GetBool("Review.ExpectBroken", false) ? "before-fix.txt" : "results.txt");
    static CampaignReviewVerification() { EditorApplication.playModeStateChanged += Changed; }
    public static void RunBeforeFix() { SessionState.SetBool("Review.ExpectDocumentBroken", false); SessionState.SetBool("Review.ExpectBroken", true); Run(); }
    public static void RunBeforeDocumentFix() { SessionState.SetBool("Review.ExpectDocumentBroken", true); SessionState.SetBool("Review.ExpectBroken", false); Run(); }
    public static void RunAfterFix() { SessionState.SetBool("Review.ExpectDocumentBroken", false); SessionState.SetBool("Review.ExpectBroken", false); Run(); }
    static void Run()
    {
        string saveRoot = Root + "save-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(saveRoot); File.WriteAllText(Result, "RUNNING\n");
        File.Copy(CampaignTestFixtures.PathFor("before-day-20.json"), saveRoot + "/progress.json", true);
        SessionState.SetString("Review.PreviousSaveRoot", Environment.GetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY") ?? "");
        Environment.SetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY", Path.GetFullPath(saveRoot));
        SessionState.SetBool("Review.Running", true);
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); EditorApplication.isPlaying = true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool("Review.Running", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        { var go = new GameObject("Campaign recovery review"); Object.DontDestroyOnLoad(go); go.AddComponent<CampaignReviewProbe>(); }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool("Review.Running", false);
            Environment.SetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY", SessionState.GetString("Review.PreviousSaveRoot", ""));
            EditorApplication.Exit(File.ReadAllText(Result).Contains("FAIL") ? 1 : 0);
        }
    }
}
public sealed class CampaignReviewProbe : MonoBehaviour
{
    sealed class WaitingDocument : ITurnBlocker
    {
        public int calls;
        public bool IsBlocking(out string reason) { calls++; reason = "review pending document"; return true; }
    }
    float deadline;
    void Check(bool value, string text) { if (!value) throw new Exception(text); File.AppendAllText(CampaignReviewVerification.Result, "PASS: " + text + "\n"); }
    void Update() { if (deadline > 0 && Time.realtimeSinceStartup > deadline) { File.AppendAllText(CampaignReviewVerification.Result, "FAIL: timeout\n"); deadline = 0; EditorApplication.isPlaying = false; } }
    IEnumerator Start()
    {
        deadline = Time.realtimeSinceStartup + 100;
        var test = Verify();
        while (true)
        {
            bool next; object value = null;
            try { next = test.MoveNext(); if (next) value = test.Current; }
            catch (Exception error) { File.AppendAllText(CampaignReviewVerification.Result, "FAIL: " + error); break; }
            if (!next) break; yield return value;
        }
        deadline = 0; EditorApplication.isPlaying = false;
    }
    IEnumerator Verify()
    {
        yield return null; yield return null;
        Object.FindFirstObjectByType<MainMenuController>().startContinue();
        while (GameSessionCoordinator.Active == null || !GameSessionCoordinator.Active.Ready) yield return null;
        yield return null;
        var clock = Object.FindFirstObjectByType<GameTurnClock>(); var game = Object.FindFirstObjectByType<EventSystemController>();
        var manager = Object.FindFirstObjectByType<GameSaveManager>(); var session = GameSessionCoordinator.Active;
        Check(clock.CurrentTurn == 19 && game.CampaignRevision == 2, "Actual title continue loads the isolated pre-deadline fixture");
        var inbox = Object.FindFirstObjectByType<DocumentSystem>().Inbox;
        foreach (var doc in inbox.Open.OfType<IApprovalDocument>().ToArray()) inbox.Resolve(doc, false);
        var approval = new DividendPolicyDocument(Object.FindFirstObjectByType<Companies>().getCompany("mirae-construction"), 30, Resources.FindObjectsOfTypeAll<DocumentSettings>().First());
        Check(inbox.Add(approval), "A real registered approval document is present in the save snapshot");
        var unread = inbox.Open.First(doc => doc is not IApprovalDocument);
        string root = GameSaveManager.SaveDirectory;
        string blockerFile = Path.GetFullPath(CampaignReviewVerification.Root + "blocked-auto-save"); File.WriteAllText(blockerFile, "fixture");
        Environment.SetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY", blockerFile);
        clock.AdvanceTurn(); for (int i = 0; i < 4; i++) yield return null;
        Check(clock.CurrentTurn == 20 && game.PendingMandatoryEvents.Count == 2 && manager.hasPendingSave && session.IsPersistenceBlocked, "Real automatic write fails while two mandatory cards are pending");
        game.openEvent(game.getTodayEvents()[0].getId()); Check(game.IsClosed, "Pending save correctly prevents opening the mandatory card");
        if (!SessionState.GetBool("Review.ExpectBroken", false))
        {
            int historyCount = inbox.History.Count;
            inbox.Resolve(approval, false); inbox.MarkRead(unread);
            if (SessionState.GetBool("Review.ExpectDocumentBroken", false))
            {
                Check(!inbox.Open.Contains(approval) && !inbox.Open.Contains(unread) && inbox.History.Count == historyCount + 2, "REPRODUCED: documents mutate while a stale full-save snapshot awaits retry");
                File.AppendAllText(CampaignReviewVerification.Result, "COMPLETE: confirmed document mutation gap\n"); yield break;
            }
            Check(inbox.Open.Contains(approval) && inbox.Open.Contains(unread) && inbox.History.Count == historyCount, "Pending-save guard preserves both approval and read-only document state");
        }
        string before = game.captureJson(); float cash = Object.FindFirstObjectByType<Player>().state.cash;
        int memories = Object.FindFirstObjectByType<NpcStateRegistry>().States.Sum(s => s.memories.Count);
        var document = new WaitingDocument(); clock.RegisterBlocker(document);
        Environment.SetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY", root);
        clock.AdvanceTurn(); yield return null;
        if (SessionState.GetBool("Review.ExpectBroken", false))
        {
            Check(manager.hasPendingSave && session.IsPersistenceBlocked && document.calls > 0, "REPRODUCED: an existing turn blocker prevents the save retry callback");
            Check(game.IsClosed && clock.CurrentTurn == 20, "Neither mandatory event entry nor turn completion can recover this state");
            File.AppendAllText(CampaignReviewVerification.Result, "COMPLETE: confirmed recovery ordering defect\n"); yield break;
        }
        Check(!manager.hasPendingSave && !session.IsPersistenceBlocked && document.calls == 0, "Save retry runs before document and mandatory-event blockers");
        Check(clock.CurrentTurn == 20 && cash == Object.FindFirstObjectByType<Player>().state.cash && memories == Object.FindFirstObjectByType<NpcStateRegistry>().States.Sum(s => s.memories.Count) && before == game.captureJson(), "Retry does not advance the date or repeat economic and NPC effects");
        Check(manager.tryReadDayStart(out var saved) && saved.day == 20, "Retry persists the exact mandatory-day snapshot");
        clock.UnregisterBlocker(document);
        game.openEvent(game.getTodayEvents()[0].getId()); Check(game.IsDetailOpen, "Mandatory card becomes accessible after save recovery"); game.closeDetail();
        clock.AdvanceTurn(); yield return null;
        Check(clock.CurrentTurn == 20 && game.PendingMandatoryEvents.Count == 2, "Normal mandatory-event lock still applies after recovery");

        var source = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(CampaignTestFixtures.PathFor("early-a2-start.json")));
        string run = (string)source["sections"].Single(s => (string)s["id"] == "events")["data"]["campaignRunId"];
        string original = CampaignTestFixtures.PathFor("early-a2-start.json");
        File.Copy(original, manager.savePath, true);
        string blockedDirectory = Path.Combine(root, "checkpoints"); File.WriteAllText(blockedDirectory, "fixture");
        Check(manager.tryLoad(out _), "Mission-start fixture restores for checkpoint-only storage failure");
        for (int i = 0; i < 4; i++) yield return null;
        Check(clock.CurrentTurn == 29 && !manager.hasPendingSave && session.IsPersistenceBlocked, "Checkpoint-only write failure blocks progress after a successful autosave");
        before = game.captureJson(); cash = Object.FindFirstObjectByType<Player>().state.cash;
        var otherDocument = new WaitingDocument(); clock.RegisterBlocker(otherDocument);
        File.Delete(blockedDirectory);
        clock.AdvanceTurn(); yield return null;
        Check(!session.IsPersistenceBlocked && otherDocument.calls == 0 && manager.HasCampaignCheckpoint(run, "early-a2"), "Checkpoint retry also runs before other turn blockers");
        Check(clock.CurrentTurn == 29 && before == game.captureJson() && cash == Object.FindFirstObjectByType<Player>().state.cash, "Checkpoint recovery preserves the original preparation state");
        clock.UnregisterBlocker(otherDocument);
        foreach (var doc in inbox.Open.OfType<IApprovalDocument>().ToArray()) inbox.Resolve(doc, false);
        clock.AdvanceTurn(); yield return null;
        Check(clock.CurrentTurn == 30, "Day progression resumes normally once storage and real blockers are resolved");
        File.AppendAllText(CampaignReviewVerification.Result, "COMPLETE\n");
    }
}
