using System;
using System.Collections.Generic;
using UnityEngine;

namespace TakeOver.PlayerMemories
{
    public enum PlayerMemoryKind { Intent, Observation, Decision, Consequence }
    public enum PlayerMemorySourceKind { EventChoice, Story, System }

    [Serializable]
    public sealed class PlayerMemoryGrant
    {
        public string key;
        public string title;
        [TextArea(2, 5)] public string description;
        public PlayerMemoryKind kind;
        public bool permanent = true;
        [Tooltip("비워 두면 사건의 대상 회사. NPC에게 공개하지 않는 플레이어 자신의 기억이다.")]
        public string companyId;
        public List<string> tags = new List<string>();
    }

    public sealed class PlayerMemoryOrigin
    {
        public readonly PlayerMemorySourceKind kind;
        public readonly string sourceId, occurrenceId, companyId;
        public readonly int turn, choiceIndex;
        public PlayerMemoryOrigin(PlayerMemorySourceKind kind, string sourceId, string occurrenceId, string companyId, int turn, int choiceIndex = -1)
        {
            this.kind = kind; this.sourceId = sourceId; this.occurrenceId = occurrenceId;
            this.companyId = companyId ?? ""; this.turn = turn; this.choiceIndex = choiceIndex;
        }
    }

    /// <summary>일지와 분기 소비자가 읽는 불변 기록. NPC 관계·공개 기억과 공유하지 않는다.</summary>
    [Serializable]
    public sealed class PlayerMemoryRecord
    {
        [SerializeField] private string memoryId, key, title, description, ownerId, companyId;
        [SerializeField] private PlayerMemoryKind kind;
        [SerializeField] private bool permanent;
        [SerializeField] private int createdTurn, sourceChoiceIndex;
        [SerializeField] private PlayerMemorySourceKind sourceKind;
        [SerializeField] private string sourceId, sourceOccurrenceId;
        [SerializeField] private List<string> tags;

        public string MemoryId => memoryId;
        public string Key => key;
        public string Title => title;
        public string Description => description;
        public string OwnerId => ownerId;
        public string CompanyId => companyId;
        public PlayerMemoryKind Kind => kind;
        public bool Permanent => permanent;
        public bool IsPrivate => true;
        public int CreatedTurn => createdTurn;
        public PlayerMemorySourceKind SourceKind => sourceKind;
        public string SourceId => sourceId;
        public string SourceOccurrenceId => sourceOccurrenceId;
        public int SourceChoiceIndex => sourceChoiceIndex;
        public IReadOnlyList<string> Tags => tags == null ? null : tags.AsReadOnly();

        internal PlayerMemoryRecord(PlayerMemoryGrant grant, PlayerMemoryOrigin origin)
        {
            key = grant.key; title = grant.title; description = grant.description;
            kind = grant.kind; permanent = grant.permanent; ownerId = "player";
            companyId = string.IsNullOrEmpty(grant.companyId) ? origin.companyId : grant.companyId;
            memoryId = MakeId(key, companyId); createdTurn = origin.turn;
            sourceKind = origin.kind; sourceId = origin.sourceId; sourceOccurrenceId = origin.occurrenceId;
            sourceChoiceIndex = origin.choiceIndex; tags = new List<string>(grant.tags);
        }
        internal static string MakeId(string key, string companyId) => "player:" + key + ":" + companyId;
    }

    public interface IPlayerMemoryReader
    {
        bool HasMemory(string key, string companyId = null);
        IReadOnlyList<PlayerMemoryRecord> GetMemories();
        event Action<PlayerMemoryRecord> MemoryAdded;
        event Action<IReadOnlyList<PlayerMemoryRecord>> MemoriesRestored;
    }
}
