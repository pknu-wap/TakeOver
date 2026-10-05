using System;

[Serializable]
public struct NpcMarketOpinion
{
    // 어댑터에서 대상 기업에 영향을 주는 NPC를 선정하고 의견을 [-1, 1]로 정규화
    public float normalizedOpinion;
    public float influenceWeight;

    public NpcMarketOpinion(float normalizedOpinion, float influenceWeight)
    {
        this.normalizedOpinion = normalizedOpinion;
        this.influenceWeight = influenceWeight;
    }
}
