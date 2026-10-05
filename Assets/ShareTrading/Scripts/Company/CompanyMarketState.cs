using System.Collections.Generic;

public class CompanyMarketState
{
    private sealed class ActiveEventImpact
    {
        public MarketEventImpact impact;
        public int remainingTurns;
    }

    private readonly List<ActiveEventImpact> activeEvents = new List<ActiveEventImpact>();

    public float npcSentiment { get; private set; }
    public float eventEvaluationScore { get; private set; }
    public float eventLiquidityScore { get; private set; }
    public float marketNoise { get; private set; }
    public int activeEventCount => activeEvents.Count;

    internal void setNpcSentiment(float value)
    {
        npcSentiment = CompanyCalculationMath.clamp(value, -1f, 1f);
    }

    internal void setNpcOpinions(IReadOnlyList<NpcMarketOpinion> opinions)
    {
        double weightedOpinion = 0d;
        double totalWeight = 0d;
        foreach (NpcMarketOpinion opinion in opinions)
        {
            float weight = CompanyCalculationMath.finite(opinion.influenceWeight);
            if (weight <= 0f) continue;
            weightedOpinion += CompanyCalculationMath.clamp(opinion.normalizedOpinion, -1f, 1f) * (double)weight;
            totalWeight += weight;
        }
        setNpcSentiment(totalWeight == 0d ? 0f : CompanyCalculationMath.finite(weightedOpinion / totalWeight));
    }

    internal bool addEventImpact(MarketEventImpact impact)
    {
        if (impact.durationTurns <= 0) return false;
        impact.evaluationImpact = CompanyCalculationMath.clamp(impact.evaluationImpact, -1f, 1f);
        impact.liquidityImpact = CompanyCalculationMath.clamp(impact.liquidityImpact, -1f, 1f);
        activeEvents.Add(new ActiveEventImpact { impact = impact, remainingTurns = impact.durationTurns });
        updateEventScores();
        return true;
    }

    internal void updateTurn(float noise)
    {
        for (int i = activeEvents.Count - 1; i >= 0; i--)
        {
            if (--activeEvents[i].remainingTurns <= 0) activeEvents.RemoveAt(i);
        }
        updateEventScores();
        marketNoise = CompanyCalculationMath.clamp(noise, -0.02f, 0.02f);
    }

    private void updateEventScores()
    {
        double evaluation = 0d;
        double liquidity = 0d;
        foreach (ActiveEventImpact entry in activeEvents)
        {
            double decay = (double)entry.remainingTurns / entry.impact.durationTurns;
            evaluation += entry.impact.evaluationImpact * decay;
            liquidity += entry.impact.liquidityImpact * decay;
        }
        eventEvaluationScore = CompanyCalculationMath.clamp(evaluation, -1f, 1f);
        eventLiquidityScore = CompanyCalculationMath.clamp(liquidity, -1f, 1f);
    }

    private static float getReputationScore(CompanyState state)
    {
        return CompanyCalculationMath.normalizeScore(state.getStat(CompanyStat.REPUTATION_B2C)) * 0.6f
            + CompanyCalculationMath.normalizeScore(state.getStat(CompanyStat.REPUTATION_B2B)) * 0.4f;
    }

    public float getMarketEvaluation(CompanyState state, float globalMarketScore)
    {
        float reputationFactor = 1f + getReputationScore(state) * 0.15f;
        float positionFactor = 1f + CompanyCalculationMath.normalizeScore(state.getStat(CompanyStat.MARKET_POSITION)) * 0.1f;
        float npcFactor = 1f + npcSentiment * 0.1f;
        float eventFactor = 1f + eventEvaluationScore * 0.25f;
        float globalFactor = 1f + globalMarketScore * 0.08f;
        float noiseFactor = 1f + marketNoise;
        return CompanyCalculationMath.clampFactor(
            reputationFactor * positionFactor * npcFactor * eventFactor * globalFactor * noiseFactor, 0.6f, 1.6f);
    }

    public float getMarketLiquidity(CompanyState state, float globalMarketScore)
    {
        return CompanyCalculationMath.clampFactor(1f
            + CompanyCalculationMath.normalizeScore(state.getStat(CompanyStat.MARKET_POSITION)) * 0.15f
            + getReputationScore(state) * 0.1f
            + eventLiquidityScore * 0.2f
            + globalMarketScore * 0.1f, 0.5f, 1.5f);
    }
}
