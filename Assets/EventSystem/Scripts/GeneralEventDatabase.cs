using System.Collections.Generic;
using UnityEngine;

namespace TakeOver.Events
{
    [CreateAssetMenu(menuName = "TakeOver/Events/General Event Database")]
    public sealed class GeneralEventDatabase : ScriptableObject
    {
        [SerializeField] private List<EventDefinition> events = new List<EventDefinition>();
        public IReadOnlyList<EventDefinition> Events => events.AsReadOnly();
    }
}
