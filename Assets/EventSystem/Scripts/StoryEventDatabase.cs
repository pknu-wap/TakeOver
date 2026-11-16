using System.Collections.Generic;
using UnityEngine;

namespace TakeOver.Events
{
    [CreateAssetMenu(fileName = "StoryEventDatabase", menuName = "TakeOver/Events/Story Event Database")]
    public sealed class StoryEventDatabase : ScriptableObject
    {
        [SerializeField] private List<EventDefinition> events = new List<EventDefinition>();
        public IReadOnlyList<EventDefinition> Events => events.AsReadOnly();
    }
}
