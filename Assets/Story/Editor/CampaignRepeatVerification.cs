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
using Object = UnityEngine.Object;

public sealed partial class CampaignIntegrationProbe
{
    private IEnumerator VerifyRepeatChoices()
    {
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
    }

    private IEnumerator VerifyRepeatTail()
    {
        yield return Ready(99, false);
        CampaignTestFixtures.InstallCompletedSave(GameSaveManager.SaveDirectory);
        Check(saves.tryLoad(out _), "Completed 192-day repeat fixture loads"); yield return null;
        Check(clock.CurrentTurn == 234 && game.RepeatHistory.Count == 213, "Existing long-run history is present");
        yield return VerifyDailyRepeats(28, 1);
        yield return VerifyRepeatSave();
        File.AppendAllText(CampaignIntegrationVerification.Output + "results.txt", $"COMPLETE {assertions} PASS\n");
    }
    private IEnumerator VerifyDailyRepeats(int days, int minimumVariants)
    {
        var seen = new HashSet<string>();
        int offers = 0;
        for (int step = 0; step < days; step++)
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
            if (step == Mathf.Min(40, days / 2))
            {
                Check(saves.trySaveDayEnd(new DayStartData(clock.CurrentTurn, FixedDaySeed.seed)) && saves.tryLoad(out _), "Live repeat history saves and restores: " + saves.lastError);
                yield return null;
                Check(stable == game.captureJson(), "Load keeps exact live history and cooldowns");
            }
        }
        Check(offers > (days == 192 ? 30 : 0) && seen.Count >= minimumVariants && game.RepeatHistory.Select(item => item.serial).Distinct().Count() == game.RepeatHistory.Count, "Daily probability produces repeated, unique occurrences across the eligible event pool");
        File.WriteAllText(CampaignIntegrationVerification.Output + "repeat-observation.txt", $"{days} stress days / {offers} offers / {seen.Count} of 27 eligible scene variants observed\n");
    }
    private IEnumerator VerifyRepeatSave()
    {
        // Explicitly reject duplicated occurrence serials before mutating the running scene.
        var corrupt = JObject.Parse(game.captureJson());
        corrupt["repeatHistory"].Last["serial"] = 0;
        string unchanged = game.captureJson(); bool rejected = false;
        try { game.prepareLoad(corrupt.ToString(), game.saveVersion, new DayStartData(clock.CurrentTurn, FixedDaySeed.seed)); } catch (InvalidDataException) { rejected = true; }
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
    }
}
