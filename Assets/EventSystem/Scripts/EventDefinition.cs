using System;
using System.Collections.Generic;
using UnityEngine;
using TakeOver.NPC;

namespace TakeOver.Events
{
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
