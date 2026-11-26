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
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TakeOver.Story.Editor
{
    public static class RemoteMergeVerification
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Fields).GetValue(target);
        private static IEnumerator Capture(string name)
        {
            Canvas.ForceUpdateCanvases(); yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(StoryPlayVerification.Output, name + ".png")); yield return null;
        }

        public static IEnumerator Verify(Action<bool, string> check)
        {
            var registry = Object.FindFirstObjectByType<NpcStateRegistry>();
            var companies = Object.FindFirstObjectByType<Companies>();
            var game = Object.FindFirstObjectByType<EventSystemController>();
            var cards = Object.FindFirstObjectByType<DashboardCardSlide>();
            var world = new EventWorldReader(companies, registry);
            foreach (var entry in new[] {
                ("neutral-jung-first-record", "Jung_Sua"), ("neutral-kim-first-review", "Kim_Taeseong") })
            {
                var definition = AssetDatabase.LoadAssetAtPath<EventDefinition>("Assets/EventSystem/Data/Repeatable/" + entry.Item1 + ".asset");
                var info = world.getEventInfo(definition, "", 3);
                check(AssetDatabase.GetAssetPath(info.cardImage).EndsWith(entry.Item2 + "_Card.png"), "Updated NPC card art is connected: " + entry.Item2);
                check(AssetDatabase.GetAssetPath(info.popupImage).EndsWith(entry.Item2 + "_Popup.png"), "Updated NPC popup art is connected: " + entry.Item2);
            }
            check(registry.TryGetProfile("npc-polaris-finance-ceo-lim-dohyeon", out var lim)
                && lim.profile.standingIllustration == AssetDatabase.LoadAssetAtPath<Sprite>("Assets/NPC/Art/Standing/Lim_Dohyeon_Standing.png")
                && lim.profile.dimmedStandingIllustration == AssetDatabase.LoadAssetAtPath<Sprite>("Assets/NPC/Art/Standing/Lim_Dohyeon_Dim.png"), "New Lim Dohyeon standing art survives NPC initialization");
            var limDefinition = AssetDatabase.LoadAssetAtPath<NpcDefinition>("Assets/NPC/Data/Profiles/Lim_Dohyeon.asset");
            var copy = Object.Instantiate(limDefinition);
            var authoring = JObject.Parse(File.ReadAllText("Assets/Story/Editor/AuthoringData.json"));
            var draft = (JObject)authoring["npcs"].Single(n => n["file"].Value<string>() == "Lim_Dohyeon").DeepClone();
            draft["seed"]["profile"]["standingIllustration"] = new JObject { ["instanceID"] = 0 };
            draft["seed"]["profile"]["dimmedStandingIllustration"] = new JObject { ["instanceID"] = 0 };
            StoryContentBuilder.ApplyNpcDraftJson(copy, draft.ToString());
            var rebuilt = copy.CreateRuntimeSeed();
            check(rebuilt.profile.standingIllustration == lim.profile.standingIllustration
                && rebuilt.profile.dimmedStandingIllustration == lim.profile.dimmedStandingIllustration,
                "Regenerating NPC story data preserves newly merged standing art");
            Object.DestroyImmediate(copy);

            var infos = Field<List<TodayEventInfo>>(game, "todayInfos"); infos.Clear();
            foreach (var id in new[] { "neutral-jung-first-record", "neutral-kim-first-review", "neutral-park-first-briefing", "sub_yoon_introduction_01", "sub_lee_headline_check_01" })
            {
                var definition = AssetDatabase.LoadAssetAtPath<EventDefinition>("Assets/EventSystem/Data/Repeatable/" + id + ".asset");
                check(definition != null, "Mixed card fixture uses an actual event: " + id);
                infos.Add(world.getEventInfo(definition, "", 3));
            }
            cards.RefreshCards(); yield return null; yield return null; Canvas.ForceUpdateCanvases();
            VerifyArt(cards, check);
            yield return Capture("remote-cards-design");
            var next = Field<List<Button>>(cards, "navigationButtons").Single(button => Enumerable.Range(0, button.onClick.GetPersistentEventCount())
                .Any(i => button.onClick.GetPersistentTarget(i) == cards && button.onClick.GetPersistentMethodName(i) == nameof(DashboardCardSlide.OnClickNext)));
            int previousIndex = Field<int>(cards, "currentIndex");
            StoryPlayProbe.Click(next);
            float until = Time.realtimeSinceStartup + 3;
            while (Field<bool>(cards, "isAnimating") && Time.realtimeSinceStartup < until) yield return null;
            check(!Field<bool>(cards, "isAnimating") && Field<int>(cards, "currentIndex") != previousIndex, "Remote card design rotates through the real navigation button");
            yield return null; VerifyArt(cards, check);
            yield return Capture("remote-cards-rotated");

            var adapter = Object.FindFirstObjectByType<TradingSaveAdapter>();
            var clock = Object.FindFirstObjectByType<GameTurnClock>();
            var day = new DayStartData(clock.CurrentTurn, 12345);
            string originalSave = adapter.captureJson();
            var company = companies.getCompany("mirae-construction");
            float originalPrice = company.state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE);
            var detached = new CompanyHistory(company.state);
            for (int i = 0; i < 18; i++)
            { company.state.setDerivedStat(CompanyDerivedStat.SHARE_PRICE, 100 + i); detached.recordHistory(i + 1); }
            company.state.setDerivedStat(CompanyDerivedStat.SHARE_PRICE, originalPrice);
            check(detached.PriceHistory.Count == 14 && detached.PriceHistory[0] == 104 && detached.PriceHistory[13] == 117, "Price chart history keeps the most recent fourteen samples");
            var prices = Field<List<float>>(company.history, "priceHistory"); prices.Clear(); prices.AddRange(detached.PriceHistory);
            string saved = adapter.captureJson();
            adapter.prepareLoad(saved, adapter.saveVersion, day); adapter.applyLoad(); adapter.rebuildAfterLoad();
            company = companies.getCompany("mirae-construction");
            check(company.history.PriceHistory.SequenceEqual(detached.PriceHistory), "Fourteen chart prices survive actual trading save/load");
            check(Object.FindFirstObjectByType<TopBarNavigationController>().Navigate(DashboardTab.Map), "Production map navigation opens the company browser");
            yield return null; yield return null;
            var detail = Object.FindFirstObjectByType<CompanyDetailPanel>(FindObjectsInactive.Include);
            detail.show(company); yield return null; yield return null; Canvas.ForceUpdateCanvases();
            var chart = Field<StockChartGraphic>(detail, "stockChart");
            check(detail.GetComponent<CanvasGroup>() == null || detail.GetComponent<CanvasGroup>().alpha >= .99f, "Redesigned company detail is visible after its background capture");
            check(chart != null && chart.IsActive() && chart.hasLine && Field<List<float>>(chart, "values").Count == 15, "Redesigned company detail renders chart history and today's price");
            check(Field<TMPro.TMP_Text[]>(chart, "axisLabels").All(label => label.IsActive() && !string.IsNullOrWhiteSpace(label.text)), "Stock chart renders its price axis labels");
            yield return Capture("remote-company-price-chart");
            detail.hide(); yield return null;
            var legacy = JObject.Parse(saved);
            foreach (var entry in legacy["companies"].Children<JObject>()) entry.Remove("priceHistory");
            adapter.prepareLoad(legacy.ToString(), adapter.saveVersion, day); adapter.applyLoad(); adapter.rebuildAfterLoad();
            check(companies.CompanyList.All(c => c.history.PriceHistory.Count == 0), "Older saves without chart prices remain loadable");
            string beforeInvalid = adapter.captureJson();
            var invalid = JObject.Parse(saved);
            ((JObject)invalid["companies"][0])["priceHistory"] = new JArray(Enumerable.Repeat(1f, 15));
            bool rejected = false;
            try { adapter.prepareLoad(invalid.ToString(), adapter.saveVersion, day); }
            catch (InvalidDataException) { rejected = true; }
            check(rejected && adapter.captureJson() == beforeInvalid, "Oversized chart history is rejected without changing live trading state");
            adapter.prepareLoad(originalSave, adapter.saveVersion, day); adapter.applyLoad(); adapter.rebuildAfterLoad();
            Object.FindFirstObjectByType<TopBarNavigationController>().ReturnHome(); yield return null; yield return null;
        }

        private static void VerifyArt(DashboardCardSlide cards, Action<bool, string> check)
        {
            foreach (var card in Field<List<DashboardCardSlide.CardUI>>(cards, "cardUIList"))
            {
                if (!card.cardRect.gameObject.activeInHierarchy) continue;
                check(card.reflectionImage != null && card.reflectionImage.sprite == card.cardImage.sprite, "Remote card reflection uses the current event art");
                float aspect = card.cardImage.sprite.rect.width / card.cardImage.sprite.rect.height;
                foreach (var image in new[] { card.cardImage, card.reflectionImage })
                {
                    var fitter = image.GetComponent<AspectRatioFitter>();
                    check(fitter != null && Mathf.Abs(fitter.aspectRatio - aspect) < .001f, "Current art and reflection preserve the source aspect ratio");
                    check(Mathf.Abs(image.rectTransform.rect.width / image.rectTransform.rect.height - aspect) < .02f, "Rendered art rectangle matches its source aspect ratio");
                }
                check(!card.speakerText.isTextOverflowing && card.speakerText.textInfo.lineCount == 1, "Remote card typography retains concise one-line labels");
            }
        }
    }
}
