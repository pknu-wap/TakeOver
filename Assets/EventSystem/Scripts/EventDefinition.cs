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
    public enum EventChoiceGeneration { Fixed, OwnedCompanies }

    [Serializable]
    public sealed class EventRestructuringSpeaker
    {
        public string companyId;
        public string npcId;
    }

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
        [SerializeField] private string choiceDescription;
        [SerializeField, TextArea(2, 4)] private string playerLine;
        [SerializeField, TextArea(2, 5)] private string resultDialogue;
        [SerializeField] private string resultValue;
        [SerializeField] private NpcEventPayload npcResult = new NpcEventPayload();
        [SerializeField] private float cashDelta;
        [SerializeField] private float stockSurgeMinRatio;
        [SerializeField] private float stockSurgeMaxRatio;
        [SerializeField] private float buybackPremiumRatio;

        public string getText() => choiceText;
        public string getDescription() => choiceDescription;
        public string getPlayerLine() => playerLine;
        public string getResultDialogue() => resultDialogue;
        public string getResultValue() => resultValue;
        public NpcEventPayload getNpcResult() => npcResult;
        public float getCashDelta() => cashDelta;
        public float getStockSurgeMinRatio() => stockSurgeMinRatio;
        public float getStockSurgeMaxRatio() => stockSurgeMaxRatio;
        public float getBuybackPremiumRatio() => buybackPremiumRatio;
        public EventChoice createForCompany(string companyName)
        {
            EventChoice choice = JsonUtility.FromJson<EventChoice>(JsonUtility.ToJson(this));
            choice.choiceText = (choiceText ?? "").Replace("{company}", companyName);
            choice.choiceDescription = (choiceDescription ?? "").Replace("{company}", companyName);
            choice.playerLine = (playerLine ?? "").Replace("{company}", companyName);
            choice.resultDialogue = (resultDialogue ?? "").Replace("{company}", companyName);
            return choice;
        }
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
        [SerializeField, TextArea(2, 4)] private string companyQuestion;
        [SerializeField, TextArea(2, 4)] private string companyPlayerLine;
        [SerializeField] private List<EventChoice> choices = new List<EventChoice>();
        [SerializeField] private List<EventCondition> conditions = new List<EventCondition>();
        [SerializeField, Min(1)] private int validDays = 1;
        [SerializeField] private EventSpeakerSource speakerSource;
        [SerializeField] private EventChoiceGeneration choiceGeneration;
        [SerializeField] private List<EventRestructuringSpeaker> restructuringSpeakers = new List<EventRestructuringSpeaker>();

        public IReadOnlyList<EventCondition> getConditions() => conditions.AsReadOnly();
        public int getValidDays() => Mathf.Max(1, validDays);
        public EventSpeakerSource getSpeakerSource() => speakerSource;
        public EventChoiceGeneration getChoiceGeneration() => choiceGeneration;
        public bool tryGetRestructuringNpcId(string companyId, out string npcId)
        {
            EventRestructuringSpeaker speaker = restructuringSpeakers.Find(item => item.companyId == companyId);
            npcId = speaker?.npcId ?? "";
            return speaker != null;
        }
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
        public string getCompanyQuestion() => companyQuestion;
        public string getCompanyPlayerLine() => companyPlayerLine;
        public IReadOnlyList<EventChoice> getChoices() => choices.AsReadOnly();
    }
}
