using System;
using System.Collections.Generic;
using UnityEngine;

namespace TakeOver.Events
{
    [Serializable]
    public sealed class EventCompanyData
    {
        public string companyId;
        public string companyName;
        [Range(0, 100)] public float stake;
        [Range(0, 100)] public float consumerReaction = 50;
        public string ceoNpcId;
        public string ceoName;
        public string restructuringNpcId;
        public string restructuringName;
        public string restructuringRole;
    }

    public sealed class EventTempWorldData : MonoBehaviour
    {
        [Serializable]
        public sealed class SpeakerIllustration
        {
            public string speakerName;
            public Sprite illustration;
        }

        [SerializeField] private List<EventCompanyData> companies = new List<EventCompanyData>();
        // temporary until NPC SO illustrations (PR #14)
        [SerializeField] private List<SpeakerIllustration> speakerIllustrations = new List<SpeakerIllustration>();
        public IReadOnlyList<EventCompanyData> getCompanies() => companies.AsReadOnly();
        public Sprite getSpeakerIllustration(string speakerName) =>
            speakerIllustrations.Find(item => item.speakerName == speakerName)?.illustration;
    }
}
