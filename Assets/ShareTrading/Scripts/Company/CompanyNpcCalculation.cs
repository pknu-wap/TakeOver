using TakeOver.NPC;

public static class CompanyNpcCalculation
{
    public static float calculateOpinion(NpcRelationState relation)
    {
        // 임시: 관계값만 평가하며 기억은 중복 반영하지 않는다.
        // 신뢰 축에 적대감이 포함되어 있다. 기존 시장 반영 강도를 유지한다.
        return CompanyCalculationMath.clamp(
            ((double)relation.trust + relation.respect) / 300d, -1f, 1f);
    }
}
