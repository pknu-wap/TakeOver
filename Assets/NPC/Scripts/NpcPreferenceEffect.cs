using System;

namespace TakeOver.NPC
{
    public enum NpcPreferenceEffectMode { NpcDefault, Custom, Disabled }
    public enum NpcPreferenceSource { ActionThenMemory, MemoryOnly }

    [Flags]
    public enum NpcPreferenceAxes
    {
        None = 0, Trust = 1, Respect = 2, Fear = 4, Hostility = 8, Dependency = 16, Interest = 32,
        Default = Trust | Respect | Hostility | Interest,
        All = Trust | Respect | Fear | Hostility | Dependency | Interest
    }

    /// <summary>한 NPC의 행동/기억 항목에만 적용하는 선택적 보너스다. 기본 모드는 기존 NPC 설정을 따른다.</summary>
    [Serializable]
    public sealed class NpcPreferenceEffect
    {
        public NpcPreferenceEffectMode mode;
        public float multiplier = 1.5f;
        public NpcPreferenceAxes affectedAxes = NpcPreferenceAxes.Default;
        public NpcRelationDelta relationBonus;

        public NpcPreferenceEffect Clone() => new NpcPreferenceEffect
        {
            mode = mode, multiplier = multiplier, affectedAxes = affectedAxes, relationBonus = relationBonus
        };
    }

    /// <summary>이벤트 기록과 Inspector 미리보기가 함께 사용하는 계산 결과다.</summary>
    public struct NpcPreferenceEvaluation
    {
        public NpcRelationDelta relationDelta;
        public NpcActionDisposition disposition;
        public bool enabled;
        public bool usedOverride;
        public float multiplier;
        public NpcRelationDelta configuredBonus;

        public NpcMemoryEffectLog CreateLog(NpcRelationDelta before) => new NpcMemoryEffectLog
        {
            preferenceEnabled = enabled, multiplier = multiplier, usedOverride = usedOverride,
            configuredBonus = configuredBonus, beforePreference = before,
            preferenceBonus = NpcRelationDelta.Subtract(relationDelta, before)
        };
    }
}
