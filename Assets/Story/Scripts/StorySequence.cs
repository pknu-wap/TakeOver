using System;
using System.Collections.Generic;
using TakeOver.NPC;
using UnityEngine;

namespace TakeOver.Story
{
    public enum StorySpeaker { Narration, Player, Npc, UnregisteredVoice }
    public enum StoryGoal { None, SelectCompany, BuyShares, ViewRepresentative, ViewMemory, EndDay }

    [Serializable]
    public sealed class StoryChoice
    {
        public string title;
        [TextArea(2, 5)] public string playerLine;
        [TextArea(2, 5)] public string response;
        public string targetNpcId;
        public NpcMemoryType memoryType = NpcMemoryType.Custom;
        [TextArea] public string interpretation;
        public NpcRelationDelta relationDelta;
    }

    [Serializable]
    public sealed class StoryBeat
    {
        public string id;
        public string sceneTitle;
        public Sprite background;
        public Color backgroundTint = Color.white;
        public StorySpeaker speaker;
        public string npcId;
        public string voiceLabel;
        [TextArea(2, 8)] public string text;
        public bool showPlayer = true;
        public StoryGoal goal;
        public List<StoryChoice> choices = new List<StoryChoice>();
        [Tooltip("타이틀에서 도입부 전체를 생략할 때 적용할 선택지. 대화창 스킵에는 적용하지 않습니다.")]
        [Min(0)] public int skipDefaultChoice;
    }

    [CreateAssetMenu(menuName = "TakeOver/Story/Sequence")]
    public sealed class StorySequence : ScriptableObject
    {
        [Min(.1f)] public float fadeSeconds = .45f;
        public Font font;
        public Sprite playerStanding;
        public Sprite playerDimmed;
        public Sprite dialoguePanel;
        public List<StoryBeat> beats = new List<StoryBeat>();
    }
}
