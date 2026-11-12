using System;
using UnityEngine;

namespace TakeOver.NPC
{
    public enum NpcStoryStance
    {
        [InspectorName("미설정")] Unspecified,
        [InspectorName("끝까지 대립하는 인물")] EnduringAdversary,
        [InspectorName("계약에 따른 조건부 거래자")] ConditionalPartner,
        [InspectorName("직업적 경계를 지키는 실무자")] ProfessionalBoundary,
        [InspectorName("소속과 원칙 사이에서 갈등하는 내부자")] AmbivalentInsider
    }

    /// <summary>제작용 서사 설계. 관계 수치에 따른 자동 전향 규칙이나 플레이어에게 공개할 프로필이 아니다.</summary>
    [Serializable]
    public sealed class NpcStoryDesign
    {
        public NpcStoryStance stance;
        [TextArea(2, 5)] public string personalGoal;
        [TextArea(2, 5)] public string loyaltyReason;
        [TextArea(2, 5)] public string innerConflict;
        [TextArea(2, 5)] public string pressureTactics;
        [TextArea(2, 5)] public string cooperationTrigger;
        [TextArea(2, 5)] public string requestedTerms;
        [TextArea(2, 5)] public string cooperationLimit;
        [TextArea(2, 5)] public string informationScope;
        [TextArea(2, 5)] public string refusalConditions;
        [TextArea(2, 5)] public string renewedConflict;
        [TextArea(3, 8)] public string storyArc;
    }
}
