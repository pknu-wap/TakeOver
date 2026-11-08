using System.Collections.Generic;
using UnityEngine;

public class MarketEventManager : MonoBehaviour
{
    public class ActiveMarketEvent
    {
        public int orderID;
        public MarketEventDefinition definition;
        public int startTurn;
    }

    // 전체 이벤트 목록
    [SerializeField] private List<MarketEventDefinition> allEvents = new List<MarketEventDefinition>();
    // 현재 선정될 수 있는 이벤트 ID목록, 미완
    [SerializeField] private List<int> availableEventIDs = new List<int>();
    private readonly Dictionary<int, MarketEventDefinition> eventsByID = new Dictionary<int, MarketEventDefinition>();
    private readonly List<ActiveMarketEvent> activeEvents = new List<ActiveMarketEvent>();
    [SerializeField] private MarketEventEffectControl EffectControl;
    private int nextEventOrderID;

    private void Awake()
    {
        foreach (MarketEventDefinition definition in allEvents)
            eventsByID.Add(definition.MarketEventID, definition);
    }

    // 새 턴의 시장 갱신 이후, 회사 수치 재계산 이전에 호출
    public void processTurn(int currentTurn)
    {
        expireEvents(currentTurn);
        addEvent(currentTurn);
        EffectControl.applyMarketEventEffects(currentTurn);
    }

    private void expireEvents(int currentTurn)
    {
        for (int i = activeEvents.Count - 1; i >= 0; i--)
        {
            ActiveMarketEvent entry = activeEvents[i];
            if (currentTurn < entry.startTurn + entry.definition.duration) continue;

            if (!entry.definition.isPermanent)
                EffectControl.delEffects(entry.orderID);

            activeEvents.RemoveAt(i);
            availableEventIDs.Add(entry.definition.MarketEventID);
        }
    }

    private void addEvent(int currentTurn)
    {
        if (availableEventIDs.Count == 0) return;

        int index = UnityEngine.Random.Range(0, availableEventIDs.Count);
        int eventID = availableEventIDs[index];
        if (!eventsByID.TryGetValue(eventID, out MarketEventDefinition definition))
        {
            Debug.LogWarning($"시장 이벤트 ID {eventID}에 해당하는 SO가 없습니다.", this);
            return;
        }

        availableEventIDs.RemoveAt(index);
        var entry = new ActiveMarketEvent
        {
            orderID = nextEventOrderID++,
            definition = definition,
            startTurn = currentTurn + definition.delayTurns
        };
        activeEvents.Add(entry);
        EffectControl.addEffects(entry);
    }
}
