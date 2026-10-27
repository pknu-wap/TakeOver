using System;
using System.Collections.Generic;
using UnityEngine;

namespace TakeOver.NPC
{
    /// <summary>이벤트 입력을 상태에 반영하기 전에 검사하는 공통 경계다. 기존 유한값 보정 규칙은 유지한다.</summary>
    internal static class NpcInputValidation
    {
        internal static void ValidateEvent(NpcMemoryType type, NpcPublicity publicity,
            float reliability, NpcRelationDelta delta)
        {
            if (!Enum.IsDefined(typeof(NpcMemoryType), type) || !Enum.IsDefined(typeof(NpcPublicity), publicity))
                throw new ArgumentException("기억 유형 또는 공개 범위가 올바르지 않습니다.");
            if (!Finite(reliability)) throw new ArgumentException("기억 신뢰도에 NaN 또는 무한대가 포함돼 있습니다.");
            ValidateDelta(delta);
        }

        internal static void ValidateDelta(NpcRelationDelta delta)
        {
            if (!Finite(delta.trust) || !Finite(delta.respect) || !Finite(delta.fear)
                || !Finite(delta.hostility) || !Finite(delta.dependency) || !Finite(delta.interest))
                throw new ArgumentException("관계 변화량에 NaN 또는 무한대가 포함돼 있습니다.");
        }

        internal static void ValidateEffectLog(NpcMemoryEffectLog log)
        {
            if (log == null) return;
            if (!Finite(log.multiplier) || log.multiplier < 1)
                throw new ArgumentException("사건 로그의 선호 배율이 올바르지 않습니다.");
            ValidateDelta(log.beforePreference);
            ValidateDelta(log.preferenceBonus);
            ValidateDelta(log.actuallyApplied);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>사건 기억의 공개 범위와 NPC가 기억할 사건 분류다.</summary>
    public enum NpcPublicity { Private, Company, Industry, Public }
    public enum NpcMemoryType { GoodFaith, ContractBreach, MassLayoff, HostileTakeover, SavedCompany, PublicHumiliation, LeakedInfo, Bribery, Custom }

    /// <summary>이벤트가 회사와 직책으로 화자를 찾는 검색용 값이다. 표시용 role과 분리한다.</summary>
    public enum NpcPosition { None, CEO }

    /// <summary>NPC가 특정 플레이어 행동을 선호하는지 나타낸다.</summary>
    public enum NpcActionDisposition { Unspecified, Likes, Dislikes }

    /// <summary>결과 Memory의 선호. 구체 actionId가 지정되면 행동 선호를 우선한다.</summary>
    [Serializable]
    public sealed class NpcMemoryPreference
    {
        public NpcMemoryType memoryType;
        public NpcActionDisposition disposition;
        [TextArea] public string rationale;
    }

    /// <summary>기획 미확정인 선택적 배율. 기본은 꺼 두어 기존 인물의 효과를 유지한다.</summary>
    [Serializable]
    public sealed class NpcPreferenceResponse
    {
        public bool enabled;
        [Min(1f)] public float likedMultiplier = 1.5f;
        [Min(1f)] public float dislikedMultiplier = 1.5f;

        public NpcRelationDelta Apply(NpcRelationDelta delta, NpcActionDisposition disposition)
        {
            if (!enabled || disposition == NpcActionDisposition.Unspecified) return delta;
            var multiplier = disposition == NpcActionDisposition.Likes ? likedMultiplier : dislikedMultiplier;
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 1f)
                throw new ArgumentException("선호 반응 배율은 1 이상의 유한한 값이어야 합니다.");
            // 신뢰·존중·관심 증가와 적대감 감소를 호의로 본다. 공포·의존도는 호불호와 동치가 아니므로 유지한다.
            var likes = disposition == NpcActionDisposition.Likes;
            delta.trust = Scale(delta.trust, likes, multiplier);
            delta.respect = Scale(delta.respect, likes, multiplier);
            delta.interest = Scale(delta.interest, likes, multiplier);
            delta.hostility = -Scale(-delta.hostility, likes, multiplier);
            return delta;
        }

        private static float Scale(float value, bool likes, float multiplier) =>
            (likes && value > 0f) || (!likes && value < 0f) ? value * multiplier : value;
    }

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
        // 변화량은 모든 축에서 음수일 수 있다. 현재 상태의 하한/상한은 NpcRelationState.Apply가 적용한다.
        public float trust;
        public float respect;
        public float fear;
        public float hostility;
        public float dependency;
        public float interest;

        public static NpcRelationDelta Zero => default;

        /// <summary>선호 보정의 추가분과 실제 적용량을 기록하기 위한 축별 차이다.</summary>
        public static NpcRelationDelta Subtract(NpcRelationDelta after, NpcRelationDelta before) => new NpcRelationDelta
        {
            trust = after.trust - before.trust, respect = after.respect - before.respect,
            fear = after.fear - before.fear, hostility = after.hostility - before.hostility,
            dependency = after.dependency - before.dependency, interest = after.interest - before.interest
        };

        public static NpcRelationDelta Between(NpcRelationState after, NpcRelationState before) => new NpcRelationDelta
        {
            trust = after.trust - before.trust, respect = after.respect - before.respect,
            fear = after.fear - before.fear, hostility = after.hostility - before.hostility,
            dependency = after.dependency - before.dependency, interest = after.interest - before.interest
        };
    }

    /// <summary>
    /// NPC 한 명이 플레이어를 대하는 현재 관계 상태다.
    /// 관계 그래프나 협상 조건을 만들 때 NPC별 상태 데이터로 확장할 수 있다.
    /// </summary>
    [Serializable]
    public struct NpcRelationState
    {
        // 신뢰·존중은 -100~100, 두려움·적대감·의존도·관심은 0~100이다.
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
            hostility = Mathf.Clamp(hostility + delta.hostility, 0f, 100f);
            dependency = Mathf.Clamp(dependency + delta.dependency, 0f, 100f);
            interest = Mathf.Clamp(interest + delta.interest, 0f, 100f);
            lastUpdatedTurn = turn;
        }
    }

    /// <summary>사건 발생 당시의 선호 계산 근거다. 나중에 SO가 바뀌어도 당시 결과를 그대로 보여준다.</summary>
    [Serializable]
    public sealed class NpcMemoryEffectLog
    {
        public bool preferenceEnabled;
        public float multiplier = 1f;
        public NpcRelationDelta beforePreference;
        public NpcRelationDelta preferenceBonus;
        public NpcRelationDelta actuallyApplied;

        public NpcMemoryEffectLog Clone() => new NpcMemoryEffectLog
        {
            preferenceEnabled = preferenceEnabled, multiplier = multiplier,
            beforePreference = beforePreference, preferenceBonus = preferenceBonus, actuallyApplied = actuallyApplied
        };
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
        // 기록 당시 조회한 행동 또는 결과 Memory의 선호 방향. 선택적 임시 배율의 적용 근거를 보존한다.
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
        public NpcMemoryEffectLog effectLog;

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
                tags = tags == null ? new List<string>() : new List<string>(tags), relationDelta = relationDelta,
                effectLog = effectLog?.Clone()
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
