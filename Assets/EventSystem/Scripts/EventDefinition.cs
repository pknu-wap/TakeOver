using System;
using System.Collections.Generic;
using UnityEngine;
using TakeOver.NPC;

namespace TakeOver.Events
{
    public enum EventConditionKind { NpcRelation, CompanyConsumerReaction, CompanyStake }
    public enum EventComparison { AtLeast, AtMost }
    public enum EventRelationAxis { trust, respect, fear, hostility, dependency, interest }
    public enum EventSpeakerSource { Fixed, CompanyCeo, CompanyRestructuring }

    [Serializable]
    public sealed class EventCondition
    {
        public EventConditionKind kind;
        public string npcId;
        public EventRelationAxis relationAxis;
        public EventComparison comparison;
        public float value;
        public string relaxedByEventId;
        public int relaxedByChoiceIndex;
        public float relaxedValue;
    }

    [Serializable]
    public sealed class EventChoice
    {
        [SerializeField] private string choiceText;
        [SerializeField, TextArea(2, 5)] private string resultDialogue;
        [SerializeField] private string resultValue;
        [SerializeField] private NpcEventPayload npcResult = new NpcEventPayload();
        [SerializeField] private float cashDelta;

        public string getText() => choiceText;
        public string getResultDialogue() => resultDialogue;
        public string getResultValue() => resultValue;
        public NpcEventPayload getNpcResult() => npcResult;
        public float getCashDelta() => cashDelta;
    }

    [CreateAssetMenu(fileName = "Event", menuName = "TakeOver/Events/Event Definition")]
    public sealed class EventDefinition : ScriptableObject
    {
        [SerializeField] private string eventId;
        [SerializeField] private string title;
        [SerializeField] private Sprite image;
        [SerializeField, TextArea(3, 8)] private string description;
        [SerializeField, Min(0)] private int apCost;
        [SerializeField] private string startButtonText;
        [SerializeField] private List<string> companyIds = new List<string>();
        [SerializeField] private string speakerName;
        [SerializeField] private string speakerRole;
        [SerializeField, TextArea(3, 8)] private string dialogue;
        [SerializeField] private List<EventChoice> choices = new List<EventChoice>();
        [SerializeField] private List<EventCondition> conditions = new List<EventCondition>();
        [SerializeField, Min(1)] private int validDays = 1;
        [SerializeField] private EventSpeakerSource speakerSource;

        public IReadOnlyList<EventCondition> getConditions() => conditions.AsReadOnly();
        public int getValidDays() => Mathf.Max(1, validDays);
        public EventSpeakerSource getSpeakerSource() => speakerSource;
        public bool hasCompanyConditions() => conditions.Exists(condition => condition.kind != EventConditionKind.NpcRelation);

        public string getId() => eventId;
        public string getTitle() => title;
        public Sprite getImage() => image;
        public string getDescription() => description;
        public int getApCost() => apCost;
        public string getStartButtonText() => startButtonText;
        public IReadOnlyList<string> getCompanyIds() => companyIds.AsReadOnly();
        public string getSpeakerName() => speakerName;
        public string getSpeakerRole() => speakerRole;
        public string getDialogue() => dialogue;
        public IReadOnlyList<EventChoice> getChoices() => choices.AsReadOnly();
    }
}
