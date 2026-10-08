using System;
using UnityEngine;

namespace TakeOver.NPC
{
    /// <summary>실제 이벤트의 ID·선택 인덱스·결과값을 NPC 사건 입력으로 해석하는 NPC 소유 데이터다.</summary>
    [CreateAssetMenu(fileName = "NewChoiceReaction", menuName = "TakeOver/NPC/Choice Reaction")]
    public sealed class NpcChoiceReactionDefinition : ScriptableObject
    {
        [SerializeField] private string sourceEventId;
        [SerializeField, Min(0)] private int choiceIndex;
        [SerializeField] private string resultValue;
        // 대상과 Memory 효과는 기존 사건 양식을 그대로 사용한다. 프로필 성향 보정도 기존 처리기를 거친다.
        [SerializeField] private NpcEventPayload npcEvent = new NpcEventPayload();

        // 테스트 화면은 조건을 읽기만 하고 SO 원본을 수정하지 않는다.
        public string SourceEventId => sourceEventId;
        public int ChoiceIndex => choiceIndex;
        public string ResultValue => resultValue;

        public bool Matches(string eventId, int index, string value) =>
            !string.IsNullOrWhiteSpace(sourceEventId)
            && string.Equals(sourceEventId, eventId, StringComparison.Ordinal)
            && choiceIndex == index && string.Equals(resultValue, value, StringComparison.Ordinal);

        /// <summary>동일 발생 ID의 재전달은 기존 Memory 중복 검사로 처리한다. 매번 새 ID를 만들지 않는다.</summary>
        public NpcEventPayload CreatePayload(string occurrenceId)
        {
            if (npcEvent == null) throw new InvalidOperationException("NPC 사건 입력이 없습니다.");
            var copy = JsonUtility.FromJson<NpcEventPayload>(JsonUtility.ToJson(npcEvent));
            copy.eventId = occurrenceId;
            return copy;
        }
    }
}
