using System;
using System.Collections.Generic;
using UnityEngine;

namespace TakeOver.NPC
{
    /// <summary>한 NPC가 사건을 기억하고 관계 효과를 받는 방식이다. 미지정 대상은 공통 기본 결과를 사용한다.</summary>
    [Serializable]
    public sealed class NpcEventReaction
    {
        // targetNpcIds에 포함된 한 NPC의 기억 결과를 공통 payload 값보다 우선 적용한다.
        public string targetNpcId;
        public NpcMemoryType memoryType;
        public int strength = 1;
        public NpcPublicity publicity;
        public bool permanent;
        public int durationTurns = 3;
        public float reliability = 1f;
        [TextArea] public string interpretation;
        public List<string> tags = new List<string>();
        public NpcRelationDelta relationDelta;
    }

    /// <summary>
    /// 이벤트/포트폴리오 결과의 공통 사실과 NPC별 반응을 담는다.
    /// eventId는 한 번 발생한 사건에 고유해야 한다. 같은 ID 재전송은 해당 NPC의 Memory가 활성 상태일 동안만 중복 처리되지 않는다.
    /// </summary>
    [Serializable]
    public sealed class NpcEventPayload
    {
        // 사건 발생 단위의 고유 ID. 처리기는 각 대상의 Memory ID를 eventId:targetNpcId로 만든다.
        public string eventId;
        // 사건이 일어난 회사와 행위자. NPC 대상은 targetNpcIds에서 별도로 지정한다.
        public string companyId;
        public string actorId;
        // 선호 행동 프로필과 결과를 연결한다. 미입력 시 선호 데이터를 조회하지 않는다.
        public string actionId;
        // 사건 결과를 받을 NPC ID 목록. 채널이 비어 있음/중복을 검사하고 processor가 등록 여부를 확인한다.
        public List<string> targetNpcIds = new List<string>();
        // 대상별 결과가 있으면 해당 NPC에 우선 적용한다. 미지정 대상은 아래 공통 결과를 사용한다.
        public List<NpcEventReaction> targetReactions = new List<NpcEventReaction>();
        // 개별 반응이 없는 대상에 적용할 기본 Memory 결과와 관계 변화량.
        public NpcMemoryType memoryType;
        public int strength = 1;
        public NpcPublicity publicity;
        public bool permanent;
        public int durationTurns = 3;
        public float reliability = 1f;
        public string interpretation;
        public List<string> tags = new List<string>();
        public NpcRelationDelta relationDelta;
    }

    /// <summary>생산자와 NPC 처리기를 느슨하게 연결하는 공용 채널이다.</summary>
    public sealed class NpcEventChannel : MonoBehaviour
    {
        public event Action<NpcEventPayload> Published;

        /// <summary>
        /// 최소 계약: 사건 발생마다 재사용하지 않는 eventId, 레지스트리에 등록된 targetNpcIds를 제공한다.
        /// 채널은 ID 형식/중복/반응 연결을 검사하고, 실제 NPC 등록 여부는 processor가 확인해 미등록 대상을 경고 후 건너뛴다.
        /// publisher는 수신자가 payload를 보관할 수 있으므로 발행 후 payload와 내부 목록을 수정하지 않는다.
        /// ID 비교는 대소문자를 구분한다. 대상 중복은 허용하지 않으며, 대상별 결과는 targetReactions에 한 번씩만 지정한다.
        /// </summary>
        public void Publish(NpcEventPayload payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (string.IsNullOrWhiteSpace(payload.eventId)) throw new ArgumentException("사건마다 고유 eventId가 필요합니다.", nameof(payload));
            if (payload.targetNpcIds == null || payload.targetNpcIds.Count == 0)
                throw new ArgumentException("사건의 영향을 받는 NPC ID를 하나 이상 지정해야 합니다.", nameof(payload));
            var targets = new HashSet<string>(StringComparer.Ordinal);
            // 어느 대상도 처리하기 전에 공통 결과와 모든 개별 결과를 검사한다.
            NpcInputValidation.ValidateEvent(payload.memoryType, payload.publicity, payload.reliability, payload.relationDelta);
            foreach (var targetId in payload.targetNpcIds)
            {
                if (string.IsNullOrWhiteSpace(targetId)) throw new ArgumentException("대상 NPC ID는 비어 있을 수 없습니다.", nameof(payload));
                if (!targets.Add(targetId)) throw new ArgumentException($"대상 NPC '{targetId}'가 중복 지정되었습니다.", nameof(payload));
            }
            var reactionTargets = new HashSet<string>(StringComparer.Ordinal);
            if (payload.targetReactions != null)
            {
                foreach (var reaction in payload.targetReactions)
                {
                    if (reaction == null || string.IsNullOrWhiteSpace(reaction.targetNpcId))
                        throw new ArgumentException("NPC별 반응에는 대상 NPC ID가 필요합니다.", nameof(payload));
                    if (!targets.Contains(reaction.targetNpcId))
                        throw new ArgumentException($"NPC별 반응 대상 '{reaction.targetNpcId}'가 targetNpcIds에 없습니다.", nameof(payload));
                    if (!reactionTargets.Add(reaction.targetNpcId))
                        throw new ArgumentException($"NPC별 반응 대상 '{reaction.targetNpcId}'가 중복 지정되었습니다.", nameof(payload));
                    NpcInputValidation.ValidateEvent(reaction.memoryType, reaction.publicity, reaction.reliability, reaction.relationDelta);
                }
            }
            Published?.Invoke(payload);
        }
    }
}
