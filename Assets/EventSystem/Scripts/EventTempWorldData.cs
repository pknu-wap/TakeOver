using System;
using System.Collections.Generic;
using UnityEngine;

namespace TakeOver.Events
{
    public sealed class EventTempWorldData : MonoBehaviour
    {
        [Serializable]
        public sealed class SpeakerIllustration
        {
            public string speakerName;
            public Sprite illustration;
            public Sprite dimmedIllustration;
        }

        // temporary until NPC SO illustrations (PR #14)
        [SerializeField] private List<SpeakerIllustration> speakerIllustrations = new List<SpeakerIllustration>();
        public Sprite getSpeakerIllustration(string speakerName) =>
            speakerIllustrations.Find(item => item.speakerName == speakerName)?.illustration;
        public Sprite getSpeakerDimmedIllustration(string speakerName) =>
            speakerIllustrations.Find(item => item.speakerName == speakerName)?.dimmedIllustration;
    }
}
