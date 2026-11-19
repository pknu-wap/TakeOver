using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TakeOver.Events;
using TakeOver.NPC;
using TakeOver.SaveSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TakeOver.Documents;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CardQueueVerification
{
    static CardQueueVerification() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        Directory.CreateDirectory("../QA_PairedCampaign");
        CampaignTestFixtures.InstallCompletedSave(Path.GetFullPath("../QA_PairedCampaign/save"));
        SessionState.SetString("Campaign.QueuePreviousRoot", Environment.GetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY") ?? "");
        Environment.SetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY", Path.GetFullPath("../QA_PairedCampaign/save"));
        File.WriteAllText("../QA_PairedCampaign/queue-results.txt", "RUNNING\n"); SessionState.SetBool("Campaign.Queue", true);
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        var view = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")); view.position = new Rect(40, 40, 1280, 780); view.Show();
        EditorApplication.isPlaying = true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool("Campaign.Queue", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode) new GameObject("Card queue verification").AddComponent<CardQueueProbe>();
        if (state == PlayModeStateChange.EnteredEditMode) { SessionState.SetBool("Campaign.Queue", false); Environment.SetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY", SessionState.GetString("Campaign.QueuePreviousRoot", "")); EditorApplication.Exit(File.ReadAllText("../QA_PairedCampaign/queue-results.txt").Contains("FAIL") ? 1 : 0); }
    }
}
public sealed class CardQueueProbe : MonoBehaviour
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    void Check(bool value, string text) { if (!value) throw new Exception(text); File.AppendAllText("../QA_PairedCampaign/queue-results.txt", "PASS: " + text + "\n"); }
    void OnEnable() => Application.logMessageReceived += Log;
    void OnDisable() => Application.logMessageReceived -= Log;
    void Log(string text, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception) File.AppendAllText("../QA_PairedCampaign/queue-results.txt", "FAIL: " + text + "\n" + stack); }
    IEnumerator Start()
    {
        Object.DontDestroyOnLoad(gameObject);
        var test = Verify();
        while (true)
        {
            bool next; object value = null;
            try { next = test.MoveNext(); if (next) value = test.Current; }
            catch (Exception error) { File.AppendAllText("../QA_PairedCampaign/queue-results.txt", "FAIL: " + error); break; }
            if (!next) break;
            yield return value;
        }
        EditorApplication.isPlaying = false;
    }
    IEnumerator Verify()
    {
        yield return null; yield return null; yield return null;
        var save = new GameObject("Isolated card queue save").AddComponent<CampaignIsolatedSaveManager>();
        Check(save.tryLoad(out _), "Actual completed campaign save loads: " + save.lastError);
        var game = Object.FindFirstObjectByType<EventSystemController>();
        var companies = Object.FindFirstObjectByType<Companies>();
        foreach (string id in new[] { "mirae-construction", "haneul-food", "dongbang-trading", "hansung-industry" })
        { var company = companies.getCompany(id); company.state.setStat(CompanyStat.PLAYER_SHARES, company.state.totalShares * .1f); company.calculator.calculateAboutShares(company); }
        var state = JsonUtility.FromJson<EventSystemController.EventSaveData>(game.captureJson()); state.occurrences.Clear();
        string[] ids = { "sub_mirae_payment_calendar_01", "sub_haneul_package_line_01", "sub_dongbang_address_check_01", "sub_hansung_inspection_note_01", "sub_yoon_fee_scope_01", "sub_jung_quote_check_01" };
        var database = Resources.Load<GeneralEventDatabase>("Events/GeneralEventDatabase");
        foreach (string id in ids)
        {
            var definition = database.Events.Single(item => item.getId() == id);
            state.cooldowns.RemoveAll(item => item.eventId == id);
            state.occurrences.Add(new EventSystemController.TimingData { eventId = id, companyId = definition.getCompanyIds().FirstOrDefault() ?? "", day = state.day, serial = ++state.lastOccurrenceSerial });
        }
        game.prepareLoad(JsonUtility.ToJson(state), game.saveVersion, new DayStartData(state.day, FixedDaySeed.seed)); game.applyLoad(); game.rebuildAfterLoad();
        var carousel = Object.FindFirstObjectByType<DashboardCardSlide>();
        var random = JsonUtility.ToJson(UnityEngine.Random.state); carousel.RefreshCards();
        Check(random == JsonUtility.ToJson(UnityEngine.Random.state), "Refreshing card order does not consume gameplay random state");
        var activeIds = game.getTodayEventInfos().Select(info => info.definition.getId()).ToArray();
        Check(activeIds.Length >= 6 && ids.All(activeIds.Contains), "Six fixture offers remain accessible alongside pending first contacts");
        var cards = (List<DashboardCardSlide.CardUI>)typeof(DashboardCardSlide).GetField("cardUIList", Flags).GetValue(carousel);
        Check(cards.Count(card => card.cardRect.gameObject.activeInHierarchy) == 5, "Original five-card visual layout is preserved");
        foreach (bool forward in new[] { true, false })
        {
            var reached = new HashSet<string>();
            for (int i = 0; i < activeIds.Length; i++)
            {
                string label = cards[2].speakerText.text;
                typeof(DashboardCardSlide).GetMethod("OnClickMain", Flags).Invoke(carousel, null);
                Check(game.ActiveEventId != null && game.getTodayEventInfos().Single(item => item.definition.getId() == game.ActiveEventId).speakerLabel == label, "Visible main card opens the corresponding offer");
                reached.Add(game.ActiveEventId); game.closeDetail();
                if (forward) carousel.OnClickNext(); else carousel.OnClickPrev();
                while ((bool)typeof(DashboardCardSlide).GetField("isAnimating", Flags).GetValue(carousel)) yield return null;
            }
            Check(reached.SetEquals(activeIds), forward ? "Forward rotation reaches all active offers" : "Backward rotation reaches all active offers");
        }
        yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.GetFullPath("../QA_PairedCampaign/all-offers-carousel.png")); yield return null;
        File.Copy(CampaignTestFixtures.PathFor("before-day-20.json"), save.savePath, true);
        Check(save.tryLoad(out _), "Actual pre-bundle state restores"); yield return null;
        var clock = Object.FindFirstObjectByType<GameTurnClock>();
        clock.AdvanceTurn(); yield return null; yield return null;
        Check(clock.CurrentTurn == 20 && game.getTodayEvents().Count == 2, "Day 20 shows two mandatory cards simultaneously");
        var bar = Object.FindFirstObjectByType<TakeOver.Dashboard.StoryDeadlineBar>();
        Canvas.ForceUpdateCanvases(); bar.Requirement.ForceMeshUpdate();
        Check(bar.Requirement.textInfo.lineCount == 2 && bar.Requirement.preferredHeight <= bar.Requirement.rectTransform.rect.height + 1, "Both investment requirements fit inside the existing mission bar");
        var reachable = new HashSet<string>();
        for (int i = 0; i < 2; i++)
        {
            typeof(DashboardCardSlide).GetMethod("OnClickMain", Flags).Invoke(carousel, null); reachable.Add(game.ActiveEventId); game.closeDetail();
            carousel.OnClickNext(); while ((bool)typeof(DashboardCardSlide).GetField("isAnimating", Flags).GetValue(carousel)) yield return null;
        }
        Check(reachable.SetEquals(new[] { "main_mirae_03", "main_dongbang_01" }), "Both company cards are reachable through normal carousel controls");
        yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.GetFullPath("../QA_PairedCampaign/bundle-layout-20.png")); yield return null;
        File.Copy(CampaignTestFixtures.PathFor("before-day-40.json"), save.savePath, true);
        Check(save.tryLoad(out _), "Late-stage pre-ending state restores"); yield return null;
        var trading = Object.FindFirstObjectByType<ShareTradingSystem>();
        foreach (var entry in new[] { ("haneul-food", 30), ("hansung-industry", 20) })
        {
            var company = companies.getCompany(entry.Item1);
            int leave = Mathf.CeilToInt(company.state.totalShares * entry.Item2 / 100f) - 1;
            Check(trading.sellShares(company, Mathf.RoundToInt(company.state.getBaseStat(CompanyStat.PLAYER_SHARES)) - leave), "Late failure is prepared through a real sale");
        }
        var inbox = Object.FindFirstObjectByType<DocumentSystem>().Inbox;
        foreach (var doc in inbox.Open.OfType<IApprovalDocument>().ToList()) inbox.Resolve(doc, false);
        clock.AdvanceTurn(); int timeout = 300;
        while (!game.IsMandatoryFailureReady && --timeout > 0) yield return null;
        Check(timeout > 0, "Late company ending becomes ready after the blackout");
        while (EventSceneTransition.IsBusy) yield return null;
        for (int act = 0; act < 3; act++)
        {
            game.AdvanceEnding(true);
            while (EventSceneTransition.IsBusy) yield return null;
        }
        var ending = Object.FindFirstObjectByType<CampaignEndingPresenter>();
        Check(ending.AtEnd && !ending.View.SkipButton.gameObject.activeInHierarchy, "Ending result shows only actionable ending controls");
        var choices = (List<UnityEngine.UI.Button>)typeof(EventNegotiationView).GetField("choices", Flags).GetValue(ending.View);
        Check(choices.Count == 2, "Ending offers retry and title with a valid preparation save");
        yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.GetFullPath("../QA_PairedCampaign/ending-final-controls.png")); yield return null;
        var target = choices[1];
        var point = RectTransformUtility.WorldToScreenPoint(null, target.transform.TransformPoint(((RectTransform)target.transform).rect.center));
        var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && (hits[0].gameObject == target.gameObject || hits[0].gameObject.transform.IsChildOf(target.transform)), "Ending title button is reachable by a real UI raycast");
        ExecuteEvents.Execute(target.gameObject, pointer, ExecuteEvents.pointerClickHandler); yield return null; yield return null;
        Check(SceneManager.GetActiveScene().name == "MainMenu", "Ending title button returns to the actual title scene");
        File.AppendAllText("../QA_PairedCampaign/queue-results.txt", "COMPLETE\n");
    }
}
