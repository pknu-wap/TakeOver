using System.Collections.Generic;
using TakeOver.NPC;
using UnityEngine;

namespace TakeOver.Story
{
    public enum RivalAffiliationKind
    {
        [InspectorName("미설정")] Unspecified,
        [InspectorName("독립 금융 협력사")] IndependentPartner,
        [InspectorName("그룹 계열사")] GroupSubsidiary,
        [InspectorName("공동사업 파트너 · 지분구조 미정")] ProjectPartner,
        [InspectorName("전략적 투자 자회사")] StrategicInvestmentSubsidiary
    }
    [CreateAssetMenu(menuName = "TakeOver/Story/Rival company")]
    public sealed class RivalCompanyDefinition : ScriptableObject
    {
        public CompanyDefinition company;
        public List<NpcDefinition> members = new List<NpcDefinition>();
        [Header("그룹 관계 · 서사 제작용")]
        public RivalAffiliationKind affiliationKind;
        [TextArea(2, 5)] public string groupRelationship;
        [TextArea(2, 5)] public string pressureStrategy;
        [TextArea(2, 5)] public string internalConflict;
        [TextArea(2, 5)] public string negotiationOpening;
        [TextArea(2, 5)] public string loyaltyStructure;
        [TextArea(2, 5)] public string cooperationBoundaries;
        [Tooltip("확정 수치가 없는 신규 기업의 초기 재무값은 플레이 가능한 임시 밸런스다.")]
        public bool provisionalFinancials = true;
    }
}
