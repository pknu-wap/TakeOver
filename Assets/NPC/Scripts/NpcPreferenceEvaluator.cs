using System;
using System.Collections.Generic;

namespace TakeOver.NPC
{
    /// <summary>행동 선호의 독립 스냅샷과 프로필 관계 보정 규칙을 담당한다. 런타임 상태는 변경하지 않는다.</summary>
    internal sealed class NpcPreferenceEvaluator
    {
        private readonly Dictionary<string, Dictionary<string, NpcActionPreference>> actionsByNpc
            = new Dictionary<string, Dictionary<string, NpcActionPreference>>(StringComparer.Ordinal);

        internal void Configure(string npcId, IList<NpcActionPreference> preferences)
        {
            var actions = new Dictionary<string, NpcActionPreference>(StringComparer.Ordinal);
            if (preferences != null)
                foreach (var preference in preferences)
                {
                    if (preference == null || string.IsNullOrWhiteSpace(preference.actionId)
                        || actions.ContainsKey(preference.actionId)) continue;
                    // 원본 변경과 분리한다. 미지정 선호도 보존해 중복 ID의 첫 항목 우선 규칙을 유지한다.
                    actions.Add(preference.actionId, preference.Clone());
                }
            actionsByNpc[npcId] = actions;
        }

        internal bool TryGet(string npcId, string actionId, out NpcActionDisposition disposition)
        {
            disposition = NpcActionDisposition.Unspecified;
            if (string.IsNullOrWhiteSpace(npcId) || string.IsNullOrWhiteSpace(actionId)) return false;
            if (!TryGetEntry(npcId, actionId, out var entry)) return false;
            disposition = entry.disposition;
            return true;
        }

        private bool TryGetEntry(string npcId, string actionId, out NpcActionPreference entry)
        {
            entry = null;
            return !string.IsNullOrWhiteSpace(npcId) && !string.IsNullOrWhiteSpace(actionId)
                && actionsByNpc.TryGetValue(npcId, out var actions) && actions.TryGetValue(actionId, out entry)
                && entry.disposition != NpcActionDisposition.Unspecified;
        }

        internal NpcPreferenceEvaluation Evaluate(NpcStateRegistry.NpcSeed seed, string npcId, string actionId,
            NpcMemoryType memoryType, NpcRelationDelta delta)
        {
            var evaluation = new NpcPreferenceEvaluation
            { relationDelta = delta, multiplier = 1f, enabled = seed.preferenceResponse != null && seed.preferenceResponse.enabled };
            var source = seed.preferenceResponse == null ? NpcPreferenceSource.ActionThenMemory : seed.preferenceResponse.source;
            if (!Enum.IsDefined(typeof(NpcPreferenceSource), source))
                throw new ArgumentException("호불호 보너스 기준이 올바르지 않습니다.");
            NpcPreferenceEffect effect = null;
            if (source == NpcPreferenceSource.ActionThenMemory && TryGetEntry(npcId, actionId, out var entry))
            {
                evaluation.disposition = entry.disposition;
                effect = entry.effect;
            }
            else if (seed.memoryPreferences != null)
                foreach (var preference in seed.memoryPreferences)
                    if (preference != null && preference.memoryType == memoryType)
                    { evaluation.disposition = preference.disposition; effect = preference.effect; break; }

            if (!Enum.IsDefined(typeof(NpcActionDisposition), evaluation.disposition))
                throw new ArgumentException("호불호 분류가 올바르지 않습니다.");
            if (evaluation.disposition != NpcActionDisposition.Unspecified)
            {
                var mode = effect == null ? NpcPreferenceEffectMode.NpcDefault : effect.mode;
                if (!Enum.IsDefined(typeof(NpcPreferenceEffectMode), mode))
                    throw new ArgumentException("개별 호불호 효과 방식이 올바르지 않습니다.");
                evaluation.usedOverride = mode != NpcPreferenceEffectMode.NpcDefault;
                if (mode == NpcPreferenceEffectMode.Disabled) evaluation.enabled = false;
                if (mode == NpcPreferenceEffectMode.Custom)
                {
                    NpcInputValidation.ValidateDelta(effect.relationBonus);
                    evaluation.enabled = true;
                    evaluation.multiplier = effect.multiplier;
                    evaluation.configuredBonus = effect.relationBonus;
                    evaluation.relationDelta = Add(NpcPreferenceResponse.ApplyMultiplier(delta,
                        evaluation.disposition, effect.multiplier, effect.affectedAxes), effect.relationBonus);
                }
                else if (mode == NpcPreferenceEffectMode.NpcDefault && seed.preferenceResponse != null)
                {
                    var response = seed.preferenceResponse;
                    evaluation.enabled = response.enabled;
                    evaluation.multiplier = !response.enabled ? 1f : evaluation.disposition == NpcActionDisposition.Likes
                        ? response.likedMultiplier : response.dislikedMultiplier;
                    evaluation.relationDelta = response.Apply(delta, evaluation.disposition);
                }
            }
            NpcInputValidation.ValidateDelta(evaluation.relationDelta);
            return evaluation;
        }

        internal static NpcRelationDelta ApplyProfileBias(NpcStateRegistry.NpcSeed seed, NpcMemoryType memoryType, NpcRelationDelta relationDelta)
        {
            if (seed.profile == null) return relationDelta;

            relationDelta = Add(relationDelta, seed.profile.anyEventRelationDelta);
            if (seed.profile.eventRelationBiases == null) return relationDelta;
            foreach (var bias in seed.profile.eventRelationBiases)
                if (bias != null && bias.memoryType == memoryType)
                    relationDelta = Add(relationDelta, bias.relationDelta);
            return relationDelta;
        }

        private static NpcRelationDelta Add(NpcRelationDelta left, NpcRelationDelta right)
        {
            left.trust += right.trust;
            left.respect += right.respect;
            left.fear += right.fear;
            left.hostility += right.hostility;
            left.dependency += right.dependency;
            left.interest += right.interest;
            return left;
        }

    }
}
