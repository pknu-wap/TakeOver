using System.Collections.Generic;

public class CompanyMarketState
{
    public float eventPriceMultiplier { get; set; } = 1f;
    public float marketNoise { get; set; }
    private float marketEventLiquidityMultiplier = 1f;
    private float marketEventLiquidityNormalValue;

    public void setMarketLiquidityEffect(float multiplier, float normalValue)
    {
        marketEventLiquidityMultiplier = multiplier;
        marketEventLiquidityNormalValue = normalValue;
    }

    public void resetMarketEventEffects()
    {
        marketEventLiquidityMultiplier = 1f;
        marketEventLiquidityNormalValue = 0f;
    }

    public void applyPriceRise(float rate)
    {
        eventPriceMultiplier *= 1f + rate;
    }

    public void updateTurn(float noise)
    {
        marketNoise = CompanyCalculationMath.clamp(noise, -0.02f, 0.02f);
    }

    private static float getReputationScore(CompanyState state)
    {
        return CompanyCalculationMath.normalizeScore(state.getStat(CompanyStat.REPUTATION_B2C)) * 0.6f
            + CompanyCalculationMath.normalizeScore(state.getStat(CompanyStat.REPUTATION_B2B)) * 0.4f;
    }

    public float getMarketEvaluation(CompanyState state, float globalMarketScore, float npcSentiment)
    {
        float reputationFactor = 1f + getReputationScore(state) * 0.15f;
        float positionFactor = 1f + CompanyCalculationMath.normalizeScore(state.getStat(CompanyStat.MARKET_POSITION)) * 0.1f;
        float npcFactor = 1f + npcSentiment * 0.1f;
        float globalFactor = 1f + globalMarketScore * 0.08f;
        float noiseFactor = 1f + marketNoise;
        return CompanyCalculationMath.clampFactor(
            reputationFactor * positionFactor * npcFactor * globalFactor * noiseFactor, 0.6f, 1.6f);
    }

    public float getMarketLiquidity(CompanyState state, float globalMarketScore)
    {
        float liquidity = CompanyCalculationMath.clampFactor(1f
            + CompanyCalculationMath.normalizeScore(state.getStat(CompanyStat.MARKET_POSITION)) * 0.15f
            + getReputationScore(state) * 0.1f
            + globalMarketScore * 0.1f, 0.5f, 1.5f);
        return liquidity * marketEventLiquidityMultiplier + marketEventLiquidityNormalValue;
    }
}
