using System;
using System.Collections.Generic;
using UnityEngine;
using TakeOver.NPC;
using TakeOver.PlayerMemories;

namespace TakeOver.Events
{
    public enum EventConditionKind { NpcRelation, CompanyConsumerReaction, CompanyStake, CompanyProfitDeteriorating, Day, EventCompleted, EventChoice, StorySucceeded, StoryFailed, CompanyGreenmailStake, PlayerHasMemory, PlayerLacksMemory }
    public enum EventCategory { General, Story }
    public enum EventComparison { AtLeast, AtMost }
    public enum EventRelationAxis { trust, respect, fear, hostility, dependency, interest }
    public enum EventSpeakerSource { Fixed, CompanyCeo, CompanyRestructuring }
    public enum EventChoiceGeneration { Fixed, OwnedCompanies }

    [Serializable]
    public sealed class EventDialogueLine
    {
        public string npcId;
        public string text;
        public bool player;
        public bool narration;
        public int scene;
    }

    [Serializable]
    public sealed class EventCompanyVariant
    {
        public string companyId, title, description;
        public List<EventDialogueLine> openingLines = new List<EventDialogueLine>();
        public List<EventChoice> choices = new List<EventChoice>();
    }

    [Serializable]
    public sealed class EventNpcArt
    {
        public string npcId;
        public Sprite cardImage;
        public Sprite popupImage;
    }

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
        [Tooltip("비워 두면 현재 이벤트의 대상 회사")]
        public string companyId;
        public string requiredEventId;
        public int requiredChoiceIndex;
        public string playerMemoryKey;
    }

    [Serializable]
    public class EventOutcome
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
        [SerializeField] private List<PlayerMemoryGrant> playerMemories = new List<PlayerMemoryGrant>();
        [SerializeField] private List<EventDialogueLine> resultLines = new List<EventDialogueLine>();
        public IReadOnlyList<EventDialogueLine> ResultLines => resultLines;

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
        public IReadOnlyList<PlayerMemoryGrant> getPlayerMemories() => playerMemories.AsReadOnly();
        protected void ResolveCompanyName(string companyName)
        {
            choiceText = (choiceText ?? "").Replace("{company}", companyName);
            choiceDescription = (choiceDescription ?? "").Replace("{company}", companyName);
            playerLine = (playerLine ?? "").Replace("{company}", companyName);
            resultDialogue = (resultDialogue ?? "").Replace("{company}", companyName);
        }
    }

    [Serializable]
    public sealed class EventChoice : EventOutcome
    {
        [Tooltip("스토리 이벤트에서만 사용. 모두 만족하면 기본 결과, 하나라도 불만족하면 실패 결과")]
        [SerializeField] private List<EventCondition> resultConditions = new List<EventCondition>();
        [SerializeField] private EventOutcome failureOutcome = new EventOutcome();
        public IReadOnlyList<EventCondition> getResultConditions() => resultConditions.AsReadOnly();
        public EventOutcome getFailureOutcome() => failureOutcome;
        public EventChoice createForCompany(string companyName)
        {
            EventChoice choice = JsonUtility.FromJson<EventChoice>(JsonUtility.ToJson(this));
            choice.ResolveCompanyName(companyName);
            return choice;
        }
    }

    [CreateAssetMenu(fileName = "Event", menuName = "TakeOver/Events/Event Definition")]
    public sealed class EventDefinition : ScriptableObject
    {
        [SerializeField] private string eventId;
        [SerializeField] private EventCategory category;
        public bool IsStory => category == EventCategory.Story;
        [SerializeField] private bool linearDialogue;
        public bool IsLinearDialogue => linearDialogue;
        [SerializeField] private bool tutorialOnly;
        public bool IsTutorialOnly => tutorialOnly;
        [SerializeField] private string title;
        [SerializeField] private Sprite image;
        [SerializeField] private Sprite cardImage;
        [SerializeField] private List<EventNpcArt> npcArts = new List<EventNpcArt>();
        [SerializeField] private Sprite highlightImage;
        [SerializeField] private Sprite negotiationBackground;
        [SerializeField, TextArea(3, 8)] private string description;
        [SerializeField, Min(0)] private int apCost;
        [SerializeField] private string startButtonText;
        [SerializeField] private List<string> companyIds = new List<string>();
        [SerializeField] private string speakerNpcId;
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
        [SerializeField] private bool repeatable;
        [SerializeField] private int repeatCooldown = 3;
        [SerializeField] private string repeatGroup;
        [SerializeField] private bool firstContact;
        [SerializeField] private int campaignDay;
        [SerializeField] private int missionStartDay;
        [SerializeField] private string prerequisiteEventId;
        [SerializeField] private List<string> prerequisiteEventIds = new List<string>();
        [SerializeField] private string failureSignal;
        [SerializeField] private List<EventDialogueLine> openingLines = new List<EventDialogueLine>();
        [SerializeField] private List<EventCompanyVariant> companyVariants = new List<EventCompanyVariant>();
        public bool Repeatable => repeatable;
        public int RepeatCooldown => Mathf.Max(1, repeatCooldown);
        public string RepeatGroup => repeatGroup;
        public bool FirstContact => firstContact;
        public int CampaignDay => campaignDay;
        public int MissionStartDay => missionStartDay;
        public string PrerequisiteEventId => prerequisiteEventId;
        public IReadOnlyList<string> PrerequisiteEventIds => prerequisiteEventIds;
        public string FailureSignal => failureSignal;
        public IReadOnlyList<EventDialogueLine> OpeningLines => openingLines;
        public EventCompanyVariant Variant(string companyId) => companyVariants.Find(item => item.companyId == companyId);

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
        public bool hasCompanyConditions() => conditions.Exists(condition => string.IsNullOrEmpty(condition.companyId) &&
            (condition.kind == EventConditionKind.CompanyStake || condition.kind == EventConditionKind.CompanyGreenmailStake || condition.kind == EventConditionKind.CompanyConsumerReaction ||
             condition.kind == EventConditionKind.CompanyProfitDeteriorating || condition.kind == EventConditionKind.NpcRelation && string.IsNullOrEmpty(condition.npcId)));

        public string getId() => eventId;
        public string getTitle() => title;
        public Sprite getImage() => image;
        public Sprite getCardImage() => cardImage;
        public Sprite getCardImageFor(string npcId)
        {
            EventNpcArt art = npcArts.Find(item => item.npcId == npcId);
            return art != null && art.cardImage != null ? art.cardImage : getCardImage();
        }
        public Sprite getImageFor(string npcId)
        {
            EventNpcArt art = npcArts.Find(item => item.npcId == npcId);
            return art != null && art.popupImage != null ? art.popupImage : getImage();
        }
        public Sprite getHighlightImage() => highlightImage;
        public Sprite getNegotiationBackground() => negotiationBackground;
        public string getDescription() => description;
        public int getApCost() => apCost;
        public string getStartButtonText() => startButtonText;
        public IReadOnlyList<string> getCompanyIds() => companyIds.AsReadOnly();
        public string getSpeakerNpcId() => speakerNpcId;
        public string getSpeakerName() => speakerName;
        public string getSpeakerRole() => speakerRole;
        public string getDialogue() => dialogue;
        public string getCompanyQuestion() => companyQuestion;
        public string getCompanyPlayerLine() => companyPlayerLine;
        public IReadOnlyList<EventChoice> getChoices() => choices.AsReadOnly();
    }
}
