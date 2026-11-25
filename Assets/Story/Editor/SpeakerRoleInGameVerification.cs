using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TakeOver.Events;
using TakeOver.NPC;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TakeOver.Story.Editor
{
    // Actual MainMenu -> tutorial -> Game UI. Only occurrence availability and company state
    // are seeded; opening, dialogue, choices and closing use production controllers/buttons.
    public static class SpeakerRoleInGameVerification
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Fields).GetValue(target);
        private static bool CardLabelStaysInside(DashboardCardSlide.CardUI card)
        {
            var corners = new Vector3[4]; card.speakerText.rectTransform.GetWorldCorners(corners);
            return corners.All(p => card.cardRect.rect.Contains((Vector2)card.cardRect.InverseTransformPoint(p)));
        }
        private static IEnumerator Capture(string name)
        {
            Canvas.ForceUpdateCanvases();
            yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(StoryPlayVerification.Output, name + ".png"));
            yield return null;
        }

        public static IEnumerator Verify(Action<bool, string> check)
        {
            check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Game", "Focused checks use production Game scene");
            foreach (var debug in Object.FindObjectsByType<NpcFeatureTestScene>(FindObjectsSortMode.None))
            { debug.ShowTestUI = false; debug.ShowNpcInfo = false; }
            var game = Object.FindFirstObjectByType<EventSystemController>();
            var registry = Object.FindFirstObjectByType<NpcStateRegistry>();
            var companies = Object.FindFirstObjectByType<Companies>();
            var insideInfo = AssetDatabase.LoadAssetAtPath<EventDefinition>("Assets/EventSystem/Data/InsideInfo.asset");
            var insideSpeaker = new EventWorldReader(companies, registry).getEventInfo(insideInfo, "mirae-construction", 3);
            check(insideSpeaker.speakerLabel == "미래건설 · CFO 서지아" && insideSpeaker.cardSpeakerLabel == "미래건설 서지아",
                "Inside information event has no duplicated company prefix");
            foreach (var seed in registry.Profiles)
            {
                string expectedLabel = $"{seed.profile.affiliation} · {seed.profile.DialogueRole} {seed.profile.displayName}";
                check(seed.GetSpeakerLabel() == expectedLabel, "All assigned positions share company/role/name formatting: " + seed.npcId);
                string expectedCard = seed.companyId == "external-network"
                    ? $"{seed.profile.DialogueRole} {seed.profile.displayName}" : $"{seed.profile.affiliation} {seed.profile.displayName}";
                check(seed.GetCardLabel() == expectedCard, "Card format is independent of dialogue format: " + seed.npcId);
            }
            var roles = new Dictionary<string, string>
            {
                { "npc-dongbang-trading-hr-director-park-minu", "인사책임자" },
                { "npc-daeseong-industry-hr-director-choi-eunsu", "인사책임자" },
                { "npc-sinseong-development-community-director-kang-minji", "지역관계 책임자" },
                { "npc-blueharbor-retail-operations-director-kim-dongseok", "매장운영 책임자" },
                { "npc-political-lobbyist-park-sanghyeon", "로비스트" }
            };
            var scenarios = new List<(EventDefinition definition, string company, string npc)>();
            var restructuring = AssetDatabase.LoadAssetAtPath<EventDefinition>("Assets/EventSystem/Data/Restructuring.asset");
            foreach (var npc in roles.Keys.Where(id => !id.Contains("lobbyist")))
            {
                registry.TryGetProfile(npc, out var seed);
                scenarios.Add((restructuring, seed.companyId, npc));
            }
            foreach (var guid in AssetDatabase.FindAssets("t:EventDefinition", new[] { "Assets/EventSystem/Data", "Assets/Story/Data/Campaign" }))
            {
                var definition = AssetDatabase.LoadAssetAtPath<EventDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (definition.getSpeakerSource() == EventSpeakerSource.Fixed && roles.ContainsKey(definition.getSpeakerNpcId()))
                    scenarios.Add((definition, definition.getCompanyIds().FirstOrDefault() ?? "", definition.getSpeakerNpcId()));
            }
            check(scenarios.Count == 10, "Only ten current event/company scenarios for the five NPCs");
            foreach (var neutral in registry.Profiles.Where(s => s.companyId == "external-network"))
            {
                var definition = AssetDatabase.FindAssets("t:EventDefinition", new[] { "Assets/EventSystem/Data" })
                    .Select(guid => AssetDatabase.LoadAssetAtPath<EventDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                    .First(d => d.getSpeakerSource() == EventSpeakerSource.Fixed && d.getSpeakerNpcId() == neutral.npcId);
                var info = new EventWorldReader(companies, registry).getEventInfo(definition, "", 3);
                var infos = Field<List<TodayEventInfo>>(game, "todayInfos"); infos.Clear(); infos.Add(info);
                var cards = Object.FindFirstObjectByType<DashboardCardSlide>(); cards.RefreshCards();
                yield return null; Canvas.ForceUpdateCanvases();
                var card = Field<List<DashboardCardSlide.CardUI>>(cards, "cardUIList")[2];
                check(card.speakerText.text == neutral.GetCardLabel() && !card.speakerText.text.Contains("외부 인물"),
                    "Neutral home card has role and name without affiliation: " + neutral.npcId);
                check(!card.speakerText.isTextOverflowing, "Neutral card label fits: " + neutral.npcId);
                check(card.speakerText.textInfo.lineCount == 1, "Neutral card label stays on one line: " + neutral.npcId);
                check(CardLabelStaysInside(card), "Neutral label stays within its card: " + neutral.npcId);
                yield return Capture("ingame-neutral-card-" + neutral.npcId);
            }
            if (Environment.GetEnvironmentVariable("TAKEOVER_REMOTE_INTEGRATION_QA") == "1")
                yield return RemoteMergeVerification.Verify(check);
            Field<IDictionary>(game, "selectedChoices").Clear();
            var gate = typeof(EventSystemController).GetMethod("PresentationGate", Fields);
            check(!(bool)gate.Invoke(game, new object[] { restructuring, "dongbang-trading" }), "Original company still requires its company story entry");
            var eligible = typeof(EventSystemController).GetMethod("meetsConditions", Fields, null,
                new[] { typeof(EventDefinition), typeof(string) }, null);
            foreach (var id in new[] { "daeseong-industry", "sinseong-development", "blueharbor-retail" })
            {
                check((bool)gate.Invoke(game, new object[] { restructuring, id }), "Company without main campaign can reach its own event conditions: " + id);
                var company = companies.getCompany(id);
                company.state.setStat(CompanyStat.PLAYER_SHARES, 0); company.calculator.calculateAboutShares(company);
                check(!(bool)eligible.Invoke(game, new object[] { restructuring, id }), "Additional company still requires enough shares: " + id);
            }
            check(!(bool)gate.Invoke(game, new object[] { restructuring, "missing-company" }), "Unknown company is not enabled by independent company gate");
            foreach (var scenario in scenarios)
            {
                float readyDeadline = Time.realtimeSinceStartup + 15;
                while (EventSceneTransition.IsBusy || TakeOver.SaveSystem.GameSessionCoordinator.Active?.IsPersistenceBlocked == true)
                {
                    if (Time.realtimeSinceStartup > readyDeadline) throw new Exception("Game did not become ready for focused events");
                    yield return null;
                }
                var definition = scenario.definition;
                var key = "ingame-" + definition.getId() + "-" + scenario.company;
                Object.FindFirstObjectByType<TopBarNavigationController>().ReturnHome();
                yield return null; yield return null;
                Object.FindFirstObjectByType<ActionPointService>().Restore();
                foreach (var company in companies.CompanyList)
                {
                    company.state.setStat(CompanyStat.PLAYER_SHARES, company.state.totalShares * .6f);
                    company.state.setStat(CompanyStat.COST, company.state.getStat(CompanyStat.REVENUE) + 100);
                    company.state.beforeNetProfit = 1000000000;
                    company.calculator.calculateAboutShares(company);
                }
                var selectedChoices = Field<IDictionary>(game, "selectedChoices"); selectedChoices.Clear();
                foreach (var entry in new[] { EventSystemController.MandatoryEventId, "main_haneul_01", "main_dongbang_01", "main_hansung_01" })
                    selectedChoices[entry] = 0;
                typeof(EventSystemController).GetField("tutorialRewardGranted", Fields).SetValue(game, true);
                Field<IDictionary>(game, "companyChoices").Clear();
                // Let the real mission bar react to seeded holdings before selecting the fixture card.
                yield return null; yield return null;
                var occurrences = Field<IList>(game, "occurrences"); occurrences.Clear();
                var occurrence = Activator.CreateInstance(occurrences.GetType().GetGenericArguments()[0], true);
                var type = occurrence.GetType();
                type.GetField("definition").SetValue(occurrence, definition);
                type.GetField("companyId").SetValue(occurrence, scenario.company);
                type.GetField("firstDay").SetValue(occurrence, Field<int>(game, "currentDay"));
                type.GetField("serial").SetValue(occurrence, 1000 + scenarios.IndexOf(scenario));
                typeof(EventSystemController).GetField("lastOccurrenceSerial", Fields).SetValue(game, 1000 + scenarios.IndexOf(scenario));
                occurrences.Add(occurrence);
                var info = new EventWorldReader(companies, registry).getEventInfo(definition, scenario.company, 3);
                var infos = Field<List<TodayEventInfo>>(game, "todayInfos"); infos.Clear(); infos.Add(info);
                var definitions = Field<List<EventDefinition>>(game, "todayEvents"); definitions.Clear(); definitions.Add(definition);
                var cards = Object.FindFirstObjectByType<DashboardCardSlide>();
                cards.RefreshCards();
                yield return null;
                Canvas.ForceUpdateCanvases();
                var mainCard = Field<List<DashboardCardSlide.CardUI>>(cards, "cardUIList")[2];
                check(mainCard.speakerText.text == info.cardSpeakerLabel, "Home card uses concise company/name or neutral role/name: " + key
                    + " / actual=" + mainCard.speakerText.text + " / expected=" + info.cardSpeakerLabel);
                check(!mainCard.speakerText.isTextOverflowing, "Home card speaker label fits: " + key);
                check(mainCard.speakerText.textInfo.lineCount == 1, "Company card label stays on one line: " + key);
                check(CardLabelStaysInside(mainCard), "Company/role/name text stays inside its card: " + key);
                yield return Capture(key + "-card");
                StoryPlayProbe.Click(mainCard.cardRect.GetComponent<Button>());
                yield return null;
                check(game.IsDetailOpen, "Production detail opens: " + key + " / state=" + Field<object>(game, "screenState")
                    + " / mandatory=" + Field<object>(game, "mandatoryState") + " / stake="
                    + (string.IsNullOrEmpty(scenario.company) ? 0 : companies.getCompany(scenario.company).state.getDerivedStat(CompanyDerivedStat.STAKE)));
                yield return Capture(key + "-detail");
                var detail = Field<EventDetailView>(game, "detailView");
                StoryPlayProbe.Click(Field<Button>(detail, "startButton"));
                while (EventSceneTransition.IsBusy) yield return null;
                var view = Field<EventNegotiationView>(game, "negotiationView");
                bool sawRole = false, selected = false;
                int clicks = 0;
                float deadline = Time.realtimeSinceStartup + 40;
                while (!game.IsClosed)
                {
                    yield return null;
                    if (EventSceneTransition.IsBusy) continue;
                    if (Time.realtimeSinceStartup > deadline || clicks > 160)
                        throw new Exception("In-game event did not finish: " + key);
                    Canvas.ForceUpdateCanvases();
                    registry.TryGetProfile(scenario.npc, out var seed);
                    var speaker = view.CurrentSpeaker;
                    if (!sawRole && speaker != null && speaker.IsActive() && speaker.text.Contains(seed.profile.displayName))
                    {
                        yield return Capture(key + "-speaker");
                        check(speaker.text.Contains(roles[scenario.npc]), "Visible in-game speaker uses requested role: " + key + " = " + speaker.text);
                        check(speaker.text == seed.GetSpeakerLabel(roles[scenario.npc]), "In-game dialogue keeps all affiliations including neutral NPCs: " + key);
                        check(speaker.preferredWidth <= speaker.rectTransform.rect.width + 1, "In-game speaker label fits: " + key);
                        sawRole = true;
                    }
                    var choices = Field<List<Button>>(view, "choices");
                    if (choices.Any(button => button != null && button.gameObject.activeInHierarchy && button.IsInteractable()))
                    {
                        yield return Capture(key + "-choices");
                        StoryPlayProbe.Click(choices.First(button => button.gameObject.activeInHierarchy && button.IsInteractable()));
                        selected = true; clicks++;
                    }
                    else if (view.ContinueButton.gameObject.activeInHierarchy && view.ContinueButton.IsInteractable())
                    { StoryPlayProbe.Click(view.ContinueButton); clicks++; }
                    else continue;
                    yield return new WaitForSecondsRealtime(.22f);
                }
                while (EventSceneTransition.IsBusy) yield return null;
                check(sawRole, "Requested NPC was visible: " + key);
                check(definition.IsLinearDialogue || selected, "In-game choices were selected: " + key);
                check(game.tryGetChoice(definition.getId(), out _) || game.tryGetChoice(definition.getId(), scenario.company, out _), "In-game event result recorded: " + key);
                yield return Capture(key + "-closed");
            }
        }
    }
}
