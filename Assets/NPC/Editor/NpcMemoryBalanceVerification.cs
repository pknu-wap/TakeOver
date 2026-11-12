using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TakeOver.NPC.Editor
{
    /// <summary>현재 캐릭터별 Memory 전용 보너스 정책과 주요 성격 차이를 검증한다.</summary>
    internal static class NpcMemoryBalanceVerification
    {
        internal static void Verify(NpcStateRegistry registry)
        {
            var results = new List<string>();
            void Check(bool condition, string name)
            {
                if (!condition) throw new InvalidOperationException("Memory balance: " + name);
                results.Add("PASS: " + name);
            }
            NpcRelationDelta Apply(string id, NpcMemoryType type, NpcRelationDelta delta)
                => registry.EvaluatePreference(id, "ClearFundingPlan", type, delta, out _);
            void Near(float actual, float expected, string name)
                => Check(Mathf.Abs(actual - expected) < .001f, name + $" ({actual}/{expected})");

            var mixed = new NpcRelationDelta { trust = 4, respect = -4, fear = 2, hostility = 4, dependency = 3, interest = -2 };
            foreach (var profile in registry.Profiles)
            {
                var id = profile.npcId;
                Check(profile.preferenceResponse.source == NpcPreferenceSource.MemoryOnly, "Memory-only source: " + id);
                Check(!profile.preferenceResponse.enabled, "Common blanket multiplier disabled: " + id);
                var input = registry.EvaluatePreferenceDetails(id, "ClearFundingPlan", NpcMemoryType.Custom, mixed);
                Check(JsonUtility.ToJson(input.relationDelta) == JsonUtility.ToJson(mixed)
                    && !input.enabled && input.disposition == NpcActionDisposition.Unspecified,
                    "Ambiguous Custom Memory and action ID receive no bonus: " + id);
                var seen = new HashSet<NpcMemoryType>();
                foreach (var preference in profile.memoryPreferences)
                {
                    Check(seen.Add(preference.memoryType) && preference.memoryType != NpcMemoryType.Custom,
                        "Unique specific Memory type: " + id + "/" + preference.memoryType);
                    Check(!string.IsNullOrWhiteSpace(preference.rationale) && !preference.rationale.Contains("?")
                        && preference.rationale.Any(c => c >= '\uac00' && c <= '\ud7a3'),
                        "Character reason is readable Korean: " + id + "/" + preference.memoryType);
                    Check(preference.effect.mode == NpcPreferenceEffectMode.Custom
                        && JsonUtility.ToJson(preference.effect.relationBonus) == JsonUtility.ToJson(default(NpcRelationDelta)),
                        "Only Memory delta scaling, no unconditional fixed changes: " + id + "/" + preference.memoryType);
                    var withAction = registry.EvaluatePreferenceDetails(id, "ClearFundingPlan", preference.memoryType, mixed);
                    var withoutAction = registry.EvaluatePreferenceDetails(id, null, preference.memoryType, mixed);
                    Check(JsonUtility.ToJson(withAction) == JsonUtility.ToJson(withoutAction),
                        "Action cannot override Memory bonus: " + id + "/" + preference.memoryType);
                    Near(withAction.relationDelta.fear, mixed.fear, "Fear unchanged: " + id + "/" + preference.memoryType);
                    Near(withAction.relationDelta.dependency, mixed.dependency, "Dependency unchanged: " + id + "/" + preference.memoryType);
                }
            }
            Near(Apply("npc-mirae-construction-field-director-park-dosik", NpcMemoryType.MassLayoff,
                new NpcRelationDelta { hostility = 20 }).hostility, 28, "Park Dosik strongly opposes mass layoffs");
            Near(Apply("npc-hansung-industry-labor-director-choi-mijeong", NpcMemoryType.MassLayoff,
                new NpcRelationDelta { hostility = 20 }).hostility, 28, "Choi Mijeong strongly opposes mass layoffs");
            Near(Apply("npc-dongbang-trading-hr-director-park-minu", NpcMemoryType.MassLayoff,
                new NpcRelationDelta { hostility = 10 }).hostility, 12.5f, "Park Minu opposes disruptive mass layoffs");
            Near(Apply("npc-haneul-food-cfo-oh-jaeseong", NpcMemoryType.MassLayoff,
                new NpcRelationDelta { respect = 10 }).respect, 10, "Oh Jaeseong has no blanket layoff preference");
            Near(Apply("npc-haneul-food-cfo-oh-jaeseong", NpcMemoryType.GoodFaith,
                new NpcRelationDelta { respect = -10 }).respect, -10, "Oh's cost-based respect loss is not reversed");
            var competitive = Apply("npc-dongbang-trading-ceo-kang-jaehyeon", NpcMemoryType.HostileTakeover,
                new NpcRelationDelta { respect = 10, hostility = 10 });
            Near(competitive.respect, 10, "Competitive CEO's respect remains contextual");
            Near(competitive.hostility, 10, "Competitive CEO has no blanket takeover aversion");
            Near(Apply("npc-economic-reporter-lee-mingyu", NpcMemoryType.GoodFaith,
                new NpcRelationDelta { trust = 10 }).trust, 13, "Lee Mingyu values transparent disclosure");
            var journalist = Apply("npc-investigative-reporter-jung-sua", NpcMemoryType.Bribery,
                new NpcRelationDelta { trust = -10, interest = 10 });
            Near(journalist.trust, -14, "Jung Sua strongly distrusts bribery");
            Near(journalist.interest, 10, "Investigative interest is not treated as liking bribery");
            var source = Apply("npc-mirae-construction-cfo-seo-jia", NpcMemoryType.LeakedInfo,
                new NpcRelationDelta { trust = 10, interest = 10 });
            Near(source.trust, 12, "Seo values the current documented information agreement");
            Near(source.interest, 12, "Seo values useful financial information");
            var output = Path.GetFullPath("../QA_NpcMemoryBalance");
            Directory.CreateDirectory(output);
            File.WriteAllLines(Path.Combine(output, "balance-results.txt"), results);
        }
    }
}
