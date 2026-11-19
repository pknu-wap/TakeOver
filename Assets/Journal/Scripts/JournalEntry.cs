using System.Collections.Generic;
using UnityEngine;

namespace TakeOver.Journal
{
    
    public sealed class JournalEntry
    {
        public string eventId;
        public int resolvedDay;
        public string title;
        public string summary;
        public Sprite photo;

        
        public readonly List<string> keyPoints = new List<string>();

        
        public readonly List<JournalPerson> people = new List<JournalPerson>();
    }

    
    public sealed class JournalPerson
    {
        public string npcId;
        public string displayName;
        public Sprite portrait;
    }
}
