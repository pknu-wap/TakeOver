using System;
using System.Collections.Generic;
using UnityEngine;
using TakeOver.Events;

namespace TakeOver.NPC
{
    /// <summary>
    /// 실제 선택 확정 알림의 NPC 결과를 공용 채널로 전달한다. 수동 반응 SO 입력도 별도로 지원한다.
    /// </summary>
    public sealed class NpcEventResultReceiver : MonoBehaviour
    {
        [SerializeField] private NpcEventChannel eventChannel;
        [SerializeField] private NpcStateRegistry registry;
        [SerializeField] private List<NpcChoiceReactionDefinition> reactions = new List<NpcChoiceReactionDefinition>();
        [Header("NPC 소유 UnityEvent에서 사용할 수동 입력")]
        [SerializeField] private string configuredEventId;
        [SerializeField, Min(0)] private int configuredChoiceIndex;
        [SerializeField] private string configuredResultValue;
        public string LastError { get; private set; } = "";
        private EventSystemController eventSystem;
        public IReadOnlyList<NpcChoiceReactionDefinition> RegisteredReactions => reactions == null
            ? Array.Empty<NpcChoiceReactionDefinition>() : reactions.AsReadOnly();

        private void Awake()
        {
            if (eventChannel == null) eventChannel = GetComponent<NpcEventChannel>();
            if (registry == null) registry = GetComponent<NpcStateRegistry>();
        }

        private void OnEnable() => TrySubscribe();
        // 다른 프리팹의 Awake 순서와 무관하게 Start에서도 연결을 확인한다.
        private void Start() => TrySubscribe();
        private void Update()
        {
            // 늦게 생성되거나 교체된 이벤트 시스템만 다시 찾는다. 연결 중에는 검색하지 않는다.
            if (eventSystem == null) TrySubscribe();
        }
        private void TrySubscribe()
        {
            if (eventSystem != null) return;
            eventSystem = FindFirstObjectByType<EventSystemController>();
            if (eventSystem != null) eventSystem.choiceConfirmed += OnChoiceConfirmed;
        }
        private void OnDisable()
        {
            if (eventSystem != null) eventSystem.choiceConfirmed -= OnChoiceConfirmed;
            eventSystem = null;
        }
        private void OnChoiceConfirmed(ChoiceConfirmedInfo info) => TryPublishEventResult(info);

        /// <summary>실제 알림과 NPC 미리보기가 공유하는 입력 경계다. 미리보기만 별도 발생 ID를 공급한다.</summary>
        public bool TryPublishEventResult(ChoiceConfirmedInfo info, string previewOccurrenceId = null)
        {
            LastError = "";
            // 거절처럼 NPC 효과가 없는 정상 선택은 조용히 건너뛴다.
            if (info == null) return Fail("선택 결과가 없습니다.");
            if (info.npcResult == null) return true;
            try
            {
                if (eventChannel == null || registry == null)
                    throw new InvalidOperationException("NPC 채널 또는 레지스트리가 연결되지 않았습니다.");
                // 이벤트 SO와 다른 알림 구독자의 데이터를 변경하지 않는다. 수치는 이벤트 설정을 그대로 사용한다.
                var payload = JsonUtility.FromJson<NpcEventPayload>(JsonUtility.ToJson(info.npcResult));
                payload.eventId = previewOccurrenceId ?? info.occurrenceId;
                payload.companyId = info.companyId;
                registry.EnsureInitialNpcsInitialized();
                if (payload.targetNpcIds != null)
                    foreach (var id in payload.targetNpcIds)
                        if (!registry.TryGet(id, out _)) throw new ArgumentException($"등록되지 않은 대상 NPC: {id}");
                eventChannel.Publish(payload);
                return true;
            }
            catch (ArgumentException error) { return Fail(error.Message); }
            catch (InvalidOperationException error) { return Fail(error.Message); }
        }

        // 현재 실제 이벤트는 eventId마다 1회 선택이므로 기본 발생 ID로 eventId를 사용한다.
        // 반복 이벤트 도입 시 공급자가 고유 occurrenceId를 넘기는 오버로드를 사용한다.
        /// <summary>true는 채널 발행 완료다. 새 Memory 추가나 중복 여부를 뜻하지 않는다.</summary>
        public bool ReceiveChoice(string eventId, int choiceIndex, string resultValue) =>
            ReceiveChoice(eventId, choiceIndex, resultValue, eventId);

        /// <summary>
        /// 기존 호출과 반환값을 유지하는 호환 진입점이다. 실제 처리 결과가 아닌 발행 여부를 반환한다.
        /// 새 연결 코드에서는 의미가 명확한 TryPublishChoice를 사용한다.
        /// </summary>
        public bool ReceiveChoice(string eventId, int choiceIndex, string resultValue, string occurrenceId)
            => TryPublishChoice(eventId, choiceIndex, resultValue, occurrenceId);

        /// <summary>기본 발생 ID로 발행한다. true는 발행 완료이며 실제 반영 여부를 보장하지 않는다.</summary>
        public bool TryPublishChoice(string eventId, int choiceIndex, string resultValue) =>
            TryPublishChoice(eventId, choiceIndex, resultValue, eventId);

        /// <summary>반응을 검증해 채널에 발행한다. 구독자가 없거나 중복 Memory인 경우도 발행 자체는 성공할 수 있다.</summary>
        public bool TryPublishChoice(string eventId, int choiceIndex, string resultValue, string occurrenceId)
        {
            LastError = "";
            if (eventChannel == null || registry == null)
                return Fail("NPC 채널 또는 레지스트리가 연결되지 않았습니다.");
            if (string.IsNullOrWhiteSpace(eventId) || choiceIndex < 0 || string.IsNullOrWhiteSpace(occurrenceId))
                return Fail("이벤트 ID, 선택 인덱스 또는 발생 ID가 올바르지 않습니다.");

            NpcChoiceReactionDefinition match = null;
            if (reactions != null)
                foreach (var reaction in reactions)
                {
                    if (reaction == null || !reaction.Matches(eventId, choiceIndex, resultValue)) continue;
                    if (match != null) return Fail("동일한 이벤트 결과의 NPC 반응 설정이 중복돼 있습니다.");
                    match = reaction;
                }
            if (match == null) return Fail("해당 이벤트 결과의 NPC 반응 설정이 없습니다.");

            try
            {
                var payload = match.CreatePayload(occurrenceId);
                registry.EnsureInitialNpcsInitialized();
                if (payload.targetNpcIds == null || payload.targetNpcIds.Count == 0)
                    return Fail("대상 NPC를 하나 이상 지정해야 합니다.");
                foreach (var id in payload.targetNpcIds)
                    if (!registry.TryGet(id, out _)) return Fail($"등록되지 않은 대상 NPC: {id}");
                eventChannel.Publish(payload);
                return true;
            }
            catch (ArgumentException error) { return Fail(error.Message); }
            catch (InvalidOperationException error) { return Fail(error.Message); }
        }

        /// <summary>Inspector의 매개변수 없는 NPC UnityEvent 연결점. 자동 발행은 하지 않는다.</summary>
        public void ReceiveConfiguredChoice() => ReceiveChoice(configuredEventId, configuredChoiceIndex, configuredResultValue);
        /// <summary>문자열 동적 UnityEvent를 사용할 때 결과값만 입력받는다.</summary>
        public void ReceiveResultValue(string value) => ReceiveChoice(configuredEventId, configuredChoiceIndex, value);
        private bool Fail(string message)
        {
            LastError = message;
            Debug.LogWarning($"NPC 이벤트 수신: {message}", this);
            return false;
        }
    }
}
