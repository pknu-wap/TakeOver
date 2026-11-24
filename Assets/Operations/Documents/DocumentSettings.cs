using UnityEngine;

namespace TakeOver.Documents
{
    [CreateAssetMenu(fileName = "DocumentSettings", menuName = "Operations/Document Settings")]
    public sealed class DocumentSettings : ScriptableObject
    {
        [Min(1)] public int quarterTurns = 21;
        [Min(1)] public int dividendDeadlineTurns = 7;
        public float[] dividendRatioOptions = { 0f, 0.3f, 0.5f, 0.7f };
        public float takeoverRatio = 0.51f;
        [Min(1)] public int historyLimit = 20;

        [Header("인원 요청 (임시 값)")]
        [Min(1)] public int supportRequestDeadlineTurns = 7;
        [Min(1)] public int supportRequestRetryTurns = 7;

        [Header("인수 후 안건 (임시 값)")]
        [Min(1)] public int agendaDeadlineTurns = 7;
        [Min(0)] public float facilityLiquidityThreshold = 2f;
        public float[] facilityInvestmentRatios = { .2f, .4f, .6f };
        [Min(0)] public int facilityDelayTurns = 14;
        [Min(1)] public int facilityDurationTurns = 21;
        [Min(0)] public float facilityGrowthCoefficient = .025f;
        public float[] facilityEffectThresholds = { .02f, .04f };
        [Min(1)] public int restructuringLossWeeks = 3;
        public float[] restructuringEmployeeRatios = { .1f, .2f, .3f };
        public float[] restructuringReputationLosses = { 2f, 5f, 10f };
        public float[] restructuringSavingThresholds = { .02f, .04f };
        [Min(0)] public float restructuringReputationThreshold = 7f;
    }
}
