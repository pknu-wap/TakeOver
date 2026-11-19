using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TakeOver.Documents;
using TakeOver.Events;
using TakeOver.NPC;
using TakeOver.SaveSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed partial class CampaignIntegrationProbe
{
    private IEnumerator VerifyPairedFailures()
    {
        var plan = EarlyCampaignPlan.Load();
        var registry = Object.FindFirstObjectByType<NpcStateRegistry>();
        foreach (var ending in plan.endings)
        {
            Check(ending.lines.Count >= 13 && ending.lines.All(line => line.narration && !line.player && string.IsNullOrEmpty(line.npcId)) && ending.lines.Select(line => line.scene).Distinct().Count() == 3 && !string.IsNullOrEmpty(ending.companyOutcome), "Ending has three background-only acts and a full company outcome: " + ending.signal);
            foreach (var line in ending.lines)
                Check(string.IsNullOrEmpty(line.npcId) || registry.TryGetProfile(line.npcId, out _), "Ending speaker registered: " + line.npcId);
        }
        int branches = 0;
        foreach (var mission in plan.missions)
        for (int mask = 1; mask < 1 << mission.goals.Count; mask++)
        {
            File.Copy(CampaignIntegrationVerification.Output + "before-" + mission.dueDay + ".json", saves.savePath, true);
            Check(saves.tryLoad(out _), "Pre-deadline actual game state restores: " + saves.lastError); yield return null;
            int signalCount = 0; StoryDeadlineFailureInfo failure = null;
            Action<StoryDeadlineFailureInfo> receive = value => { signalCount++; failure = value; }; game.BadEndingRequested += receive;
            for (int i = 0; i < mission.goals.Count; i++)
            {
                if ((mask & 1 << i) == 0) continue;
                var company = companies.getCompany(mission.goals[i].companyId);
                int leave = Mathf.CeilToInt(company.state.totalShares * mission.goals[i].percent / 100f) - 1;
                int sell = Mathf.RoundToInt(company.state.getBaseStat(CompanyStat.PLAYER_SHARES)) - leave;
                Check(sell > 0 && trading.sellShares(company, sell), "Real sale produces one-share-below-threshold failure: " + mission.goals[i].companyId);
            }
            var inbox = Object.FindFirstObjectByType<DocumentSystem>().Inbox;
            foreach (var doc in inbox.Open.OfType<IApprovalDocument>().ToList()) inbox.Resolve(doc, false);
            clock.AdvanceTurn(); yield return null;
            while (!game.IsMandatoryFailureReady) yield return null;
            Check(signalCount == 1 && failure.endingSignalId == mission.signals[mask - 1] && failure.failedDay == mission.dueDay && failure.goals.Count == mission.goals.Count,
                "All failed goals map to one distinct ending: " + mission.id + "/" + mask);
            Check(game.getTodayEvents().Count == 0 && !game.CanAdvanceTurn(clock.CurrentTurn), "Failure hides normal offers and locks the turn");
            Check(!trading.buyShares(companies.getCompany(companyIds[0]), 1) && !trading.sellShares(companies.getCompany(companyIds[0]), 1), "Failure blocks actual buy and sell APIs");
            int day = clock.CurrentTurn; float cash = player.state.cash; int memoryCount = Object.FindFirstObjectByType<NpcStateRegistry>().States.Sum(s => s.memories.Count);
            game.AdvanceEnding(false); yield return null;
            int index = game.EndingLineIndex;
            string original = game.captureJson();
            var corrupt = JObject.Parse(original); corrupt["failedEndingSignal"] = "wrong-ending";
            bool rejected = false;
            try { game.prepareLoad(corrupt.ToString(), game.saveVersion, new DayStartData(day, FixedDaySeed.seed)); } catch (InvalidDataException) { rejected = true; }
            Check(rejected && original == game.captureJson(), "Mismatched ending signal is rejected before mutation");
            Check(saves.trySaveDayEnd(new DayStartData(day, FixedDaySeed.seed)) && saves.tryLoad(out _), "Failed mission and ending reading position save and restore: " + saves.lastError);
            while (!game.IsMandatoryFailureReady) yield return null;
            Check(signalCount == 2 && game.EndingLineIndex == index && game.LastDeadlineFailure.endingSignalId == failure.endingSignalId, "Failure load resumes same ending and redelivers once");
            var presenter = Object.FindFirstObjectByType<CampaignEndingPresenter>();
            int guard = 20000;
            bool capturedCompany = false;
            while (!presenter.AtEnd && --guard > 0)
            {
                if (EventSceneTransition.IsBusy) { yield return null; continue; }
                Check(!presenter.View.NpcImage.gameObject.activeInHierarchy && !presenter.View.PlayerImage.gameObject.activeInHierarchy && !presenter.View.CurrentSpeaker.gameObject.activeInHierarchy,
                    "Ending page shows only background and narration: " + mission.signals[mask - 1] + "/" + game.EndingLineIndex);
                if (!Application.isBatchMode && !capturedCompany && branches == 13 && game.CurrentEnding.lines[game.EndingLineIndex].scene == 1)
                { capturedCompany = true; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.GetFullPath(CampaignIntegrationVerification.Output + "ending-company-scene.png")); yield return null; }
                presenter.View.ContinueButton.onClick.Invoke(); yield return null;
            }
            while (EventSceneTransition.IsBusy) yield return null;
            Check(guard > 0 && presenter.AtEnd, "Every authored ending reaches final company outcome: " + mission.signals[mask - 1]);
            Check(presenter.View.CurrentDialogue.text == $"엔딩 {branches + 1}: {game.CurrentEnding.title}",
                "Final ending card displays the planned number and title: " + mission.signals[mask - 1]);
            Check(player.state.cash == cash && Object.FindFirstObjectByType<NpcStateRegistry>().States.Sum(s => s.memories.Count) == memoryCount && clock.CurrentTurn == day,
                "Reading an ending does not duplicate money, memory or settlement");
            Check(saves.HasCampaignCheckpoint(game.CampaignRunId, mission.id), "A separate mission preparation snapshot survives the failure save");
            var buttons = (List<Button>)typeof(EventNegotiationView).GetField("choices", Private).GetValue(presenter.View);
            Check(buttons.Count == 2 && buttons[0].GetComponentInChildren<Text>().text == "재도전", "Shared ending UI exposes retry and title");
            if (!Application.isBatchMode && (branches == 0 || branches == 13))
            {
                yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.GetFullPath(CampaignIntegrationVerification.Output + "ending-" + mission.id + ".png")); yield return null;
            }
            game.BadEndingRequested -= receive;
            if (branches == 13)
            {
                string checkpoint = Path.Combine(GameSaveManager.SaveDirectory, "checkpoints", game.CampaignRunId, mission.id, "progress.json");
                string intact = File.ReadAllText(checkpoint), live = game.captureJson();
                var damaged = JObject.Parse(intact);
                ((JArray)damaged["sections"]).Single(s => (string)s["id"] == "events")["data"]["campaignRunId"] = Guid.NewGuid().ToString("N");
                File.WriteAllText(checkpoint, damaged.ToString());
                Check(!saves.HasCampaignCheckpoint(game.CampaignRunId, mission.id) && !saves.TryLoadCampaignCheckpoint(game.CampaignRunId, mission.id, out _) && live == game.captureJson(), "Mismatched preparation save cannot restore or alter live state");
                presenter.Present(); yield return null;
                Check(((List<Button>)typeof(EventNegotiationView).GetField("choices", Private).GetValue(presenter.View)).Count == 1, "Invalid checkpoint hides retry and retains title escape");
                File.WriteAllText(checkpoint, intact); presenter.Present(); yield return null;
            }
            if (branches == 0)
            {
                buttons[0].onClick.Invoke();
                while (GameSessionCoordinator.Active == null || !GameSessionCoordinator.Active.Ready) yield return null;
                yield return null;
                game = Object.FindFirstObjectByType<EventSystemController>(); clock = Object.FindFirstObjectByType<GameTurnClock>();
                companies = Object.FindFirstObjectByType<Companies>(); trading = Object.FindFirstObjectByType<ShareTradingSystem>(); player = Object.FindFirstObjectByType<Player>();
                saves = new GameObject("Retry verification save").AddComponent<CampaignIsolatedSaveManager>();
                Check(clock.CurrentTurn == 2 && game.MandatoryState != MandatoryStoryState.Failed && !TakeOver.Story.StoryDirector.IsRunning, "Actual retry button reloads mission start without tutorial");
                Check(saves.tryReadDayStart(out var restored) && restored.day == 2, "Retry session replaces failure autosave with restored preparation state");
            }
            else
            {
                string run = game.CampaignRunId;
                Check(!saves.TryLoadCampaignCheckpoint(Guid.NewGuid().ToString("N"), mission.id, out _) && game.EndingLineIndex == game.CurrentEnding.lines.Count, "Unrelated campaign checkpoint cannot change live state");
                Check(saves.TryLoadCampaignCheckpoint(run, mission.id, out _), "Retry restores entire preparation snapshot: " + saves.lastError); yield return null;
                Check(clock.CurrentTurn == (mission.startDay == 1 ? 2 : mission.startDay) && game.MandatoryState != MandatoryStoryState.Failed && game.CampaignRunId == run,
                    "Retry resets all failed mission state at its preparation day");
            }
            branches++;
        }
        Check(branches == 14, "All fourteen failure combinations and company endings are playable");
        yield return Ready(99);
        File.Copy(CampaignTestFixtures.PathFor("legacy-v5-progress.json"), saves.savePath, true);
        Check(saves.tryLoad(out _), "Previously completed v5 event save loads using legacy campaign: " + saves.lastError); yield return null;
        Check(game.CampaignRevision == 1 && game.StoryEvents.Count == 11 && companyIds.All(game.CompanyStoryCompleted) && !game.HasActiveMission,
            "Old save retains 29-day progression and original greenmail completion gates");
        string legacy = game.captureJson();
        Check(saves.trySaveDayEnd(new DayStartData(clock.CurrentTurn, FixedDaySeed.seed)) && saves.tryLoad(out _) && legacy == game.captureJson(), "Resaving a legacy campaign preserves its mode and exact choices");
    }
}
