using System;

[Serializable]
public struct MarketEventImpact
{
    // 시장 영향은 [-1, 1]을 사용하며, 지속 기간에는 임시로 적용한 턴을 포함하도록 계산
    public float evaluationImpact;
    public float liquidityImpact;
    public int durationTurns;

    public MarketEventImpact(float evaluationImpact, float liquidityImpact, int durationTurns)
    {
        this.evaluationImpact = evaluationImpact;
        this.liquidityImpact = liquidityImpact;
        this.durationTurns = durationTurns;
    }
}
