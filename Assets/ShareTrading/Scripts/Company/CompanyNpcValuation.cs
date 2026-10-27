using TakeOver.NPC;

internal static class CompanyNpcValuation
{
    public static float calculateOpinion(NpcRelationState relation)
    {
        // 임시: 관계값만 평가하며 기억은 중복 반영하지 않는다.
        // 신뢰 + 존중 - 적대
        return CompanyCalculationMath.clamp(
            ((double)relation.trust + relation.respect - relation.hostility) / 300d, -1f, 1f);
    }
}
