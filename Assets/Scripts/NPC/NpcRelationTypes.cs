using System;
using System.Collections.Generic;
using UnityEngine;

namespace TakeOver.NPC
{
    /// <summary>사건 기억의 공개 범위와 NPC가 기억할 사건 분류다.</summary>
    public enum NpcPublicity { Private, Company, Industry, Public }
    public enum NpcMemoryType { GoodFaith, ContractBreach, MassLayoff, HostileTakeover, SavedCompany, PublicHumiliation, LeakedInfo, Bribery, Custom }

    /// <summary>NPC가 특정 플레이어 행동을 선호하는지 나타낸다. 효과 수치는 기획 회의 전까지 두지 않는다.</summary>
    public enum NpcActionDisposition { Unspecified, Likes, Dislikes }

    /// <summary>행동 ID와 NPC의 선호 방향을 연결하는 프로필 데이터다.</summary>
    [Serializable]
    public sealed class NpcActionPreference
    {
        public string actionId;
        public NpcActionDisposition disposition;
        [TextArea] public string rationale;

        public NpcActionPreference Clone() => new NpcActionPreference
        {
            actionId = actionId,
            disposition = disposition,
            rationale = rationale
        };
    }

    /// <summary>
    /// 한 번의 사건이 NPC 관계 수치에 더할 변화량이다.
    /// 이벤트 결과나 대화 선택의 효과를 관계 시스템으로 전달할 때 사용한다.
    /// </summary>
    [Serializable]
    public struct NpcRelationDelta
    {
        // 신뢰·존중·적대감·관심은 방향이 있는 값이고, 공포·의존도는 현재 구현에서 0 이상이다.
        public float trust;
        public float respect;
        public float fear;
        public float hostility;
        public float dependency;
        public float interest;

        public static NpcRelationDelta Zero => default;
    }

    /// <summary>
    /// NPC 한 명이 플레이어를 대하는 현재 관계 상태다.
    /// 관계 그래프나 협상 조건을 만들 때 NPC별 상태 데이터로 확장할 수 있다.
    /// </summary>
    [Serializable]
    public struct NpcRelationState
    {
        // 현재 관계값. 허용 범위는 Apply에서 제한하며, 기획 수치 확정 전까지는 프로토타입 기준이다.
        public float trust;
        public float respect;
        public float fear;
        public float hostility;
        public float dependency;
        public float interest;
        public int lastUpdatedTurn;

        /// <summary>효과를 누적하고 허용 범위에 맞춘 뒤 마지막 변경 턴을 기록한다.</summary>
        public void Apply(NpcRelationDelta delta, int turn)
        {
            trust = Mathf.Clamp(trust + delta.trust, -100f, 100f);
            respect = Mathf.Clamp(respect + delta.respect, -100f, 100f);
            fear = Mathf.Clamp(fear + delta.fear, 0f, 100f);
            hostility = Mathf.Clamp(hostility + delta.hostility, -100f, 100f);
            dependency = Mathf.Clamp(dependency + delta.dependency, 0f, 100f);
            interest = Mathf.Clamp(interest + delta.interest, -100f, 100f);
            lastUpdatedTurn = turn;
        }
    }

    /// <summary>
    /// 특정 사건 하나에 대한 NPC의 기억 기록이다.
    /// 출처, 공개 범위, 지속성 및 관계 효과를 보존해 이벤트·협상 시스템에서 재사용한다.
    /// </summary>
    [Serializable]
    public sealed class NpcMemoryRecord
    {
        // 같은 사건의 중복 기록을 막기 위한 ID. 실제 게임 이벤트에서는 매 발생마다 고유해야 한다.
        public string memoryId;
        // 사건 종류와 누구에게서/누구를 대상으로 발생했는지 나타낸다.
        public NpcMemoryType memoryType;
        public string actorId;
        public string targetId;
        // 플레이어 행동 선호 프로필과 사건 기억을 연결할 선택적 규칙 ID다.
        public string actionId;
        // 기록 당시 NPC 프로필에서 조회한 선호 방향. 수치 효과나 자동 관계 변화는 아직 결정하지 않는다.
        public NpcActionDisposition actionDisposition;
        public string companyId;
        public string sourceEventId;
        // 기억의 충격도(1~5)와 발생한 게임 턴.
        public int strength = 1;
        public int turn;
        public NpcPublicity publicity = NpcPublicity.Private;
        // 영구 기억은 턴 감소 대상에서 제외하고, 일반 기억은 durationTurns 동안 영향력을 줄인다.
        public bool permanent;
        public int durationTurns = 3;
        public int remainingTurns = 3;
        // 협상·의사결정 쪽에서 참고할 수 있는 영향력 값과 증거 신뢰도.
        // 영향력 감소는 구현되어 있지만, 현재 이를 실제 판단에 소비하는 시스템은 아직 연결되지 않았다.
        [Range(0f, 1f)] public float influence = 1f;
        public float reliability = 1f;
        // NPC 관점에서 사건을 어떻게 해석했는지, 분류/검색용 태그, 기록 시 적용할 관계 변화량.
        [TextArea] public string interpretation;
        public List<string> tags = new List<string>();
        public NpcRelationDelta relationDelta;

        /// <summary>서비스가 원본 입력을 바꾸지 않도록 태그 목록까지 복사한 새 기록을 만든다.</summary>
        public NpcMemoryRecord Clone()
        {
            return new NpcMemoryRecord
            {
                memoryId = memoryId, memoryType = memoryType, actorId = actorId, targetId = targetId,
                actionId = actionId, actionDisposition = actionDisposition,
                companyId = companyId, sourceEventId = sourceEventId,
                strength = strength, turn = turn, publicity = publicity, permanent = permanent,
                durationTurns = durationTurns, remainingTurns = remainingTurns,
                influence = influence,
                reliability = reliability, interpretation = interpretation,
                tags = tags == null ? new List<string>() : new List<string>(tags), relationDelta = relationDelta
            };
        }
    }

    /// <summary>
    /// 한 NPC의 관계와 기억을 보유하는 런타임 상태 묶음이다.
    /// 향후 NPC 프로필/기업 정의와 분리된 세이브 데이터 단위로 활용할 수 있다.
    /// </summary>
    [Serializable]
    public sealed class NpcRuntimeState
    {
        // 식별용 NPC와 소속 회사 ID. 현재 테스트 브리지는 상태 하나만 다룬다.
        public string npcId;
        public string companyId;
        // 관계 누적치, 현재 목표, 과거 사건 기억 목록.
        public NpcRelationState relation;
        public string currentGoal;
        public List<NpcMemoryRecord> memories = new List<NpcMemoryRecord>();

        /// <summary>대상 NPC와 회사를 지정해 새 런타임 상태를 만든다.</summary>
        public NpcRuntimeState(string npcId, string companyId) { this.npcId = npcId; this.companyId = companyId; }

        /// <summary>저장/복원 경계에서 목록과 기억 항목을 분리한 상태 사본을 만든다.</summary>
        public NpcRuntimeState Clone()
        {
            var copy = new NpcRuntimeState(npcId, companyId)
            {
                relation = relation,
                currentGoal = currentGoal,
                memories = new List<NpcMemoryRecord>()
            };
            if (memories != null)
                foreach (var memory in memories)
                    copy.memories.Add(memory == null ? null : memory.Clone());
            return copy;
        }

        /// <summary>로드한 값을 기존 객체에 복사해 다른 시스템이 들고 있는 상태 참조를 유지한다.</summary>
        public void CopyFrom(NpcRuntimeState source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            npcId = source.npcId;
            companyId = source.companyId;
            relation = source.relation;
            currentGoal = source.currentGoal;
            if (memories == null) memories = new List<NpcMemoryRecord>();
            else memories.Clear();
            if (source.memories != null)
                foreach (var memory in source.memories)
                    memories.Add(memory == null ? null : memory.Clone());
        }
    }
}
