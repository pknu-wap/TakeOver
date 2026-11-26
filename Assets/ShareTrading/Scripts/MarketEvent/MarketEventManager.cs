using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TakeOver.SaveSystem;
using UnityEngine;

public class MarketEventManager : MonoBehaviour
{
    public class ActiveMarketEvent
    {
        public int orderID;
        public MarketEventDefinition definition;
        public int startTurn;
        public string selectedCompanyID;
    }

    [Serializable]
    public sealed class SaveData
    {
        public bool recorded;
        public int lastTurn, nextOrder;
        public List<int> available;
        public List<SavedOffer> offers;
    }

    [Serializable]
    public sealed class SavedOffer
    {
        public int id, order, start;
        public bool active;
        public string companyId;
    }

    // 전체 이벤트 목록
    [SerializeField] private List<MarketEventDefinition> allEvents = new List<MarketEventDefinition>();
    // 현재 선정될 수 있는 이벤트 ID목록, 미완
    [SerializeField] private List<int> availableEventIDs = new List<int>();
    private readonly Dictionary<int, MarketEventDefinition> eventsByID = new Dictionary<int, MarketEventDefinition>();
    private readonly List<ActiveMarketEvent> activeEvents = new List<ActiveMarketEvent>();
    [SerializeField] private MarketEventEffectControl EffectControl;
    private MarketEventCompanySelector companySelector;
    private int nextEventOrderID;
    private int lastProcessedTurn;
    private List<int> initialAvailable;

    public event Action<ActiveMarketEvent, int> marketEventAdded;

    private void Awake()
    {
        companySelector = new MarketEventCompanySelector(FindFirstObjectByType<Companies>());
        foreach (MarketEventDefinition definition in allEvents)
            eventsByID.Add(definition.MarketEventID, definition);
        initialAvailable = new List<int>(availableEventIDs);
    }

    public bool tryGetDefinition(int marketEventID, out MarketEventDefinition definition)
    {
        return eventsByID.TryGetValue(marketEventID, out definition);
    }

    // 새 턴의 시장 갱신 이후, 회사 수치 재계산 이전에 호출
    public void processTurn(int currentTurn)
    {
        expireEvents(currentTurn);
        addEvent(currentTurn, "market");
        addEvent(currentTurn, "market-second");
        addEvent(currentTurn, "market-third");
        if (FixedDaySeed.StableOrder(currentTurn, "market-count") <= uint.MaxValue / 2u)
            addEvent(currentTurn, "market-fourth");
        EffectControl.applyMarketEventEffects(currentTurn);
        lastProcessedTurn = currentTurn;
    }

    internal SaveData captureState()
    {
        var activeOrders = new HashSet<int>(activeEvents.Select(entry => entry.orderID));
        return new SaveData
        {
            recorded = true, lastTurn = lastProcessedTurn, nextOrder = nextEventOrderID,
            available = new List<int>(availableEventIDs),
            offers = EffectControl.AppliedEntries.Select(entry => new SavedOffer
            {
                id = entry.definition.MarketEventID, order = entry.orderID, start = entry.startTurn,
                companyId = entry.selectedCompanyID, active = activeOrders.Contains(entry.orderID)
            }).ToList()
        };
    }

    internal void validateState(SaveData data, int day, HashSet<string> companyIds)
    {
        if (data.lastTurn < 0 || data.lastTurn > day || data.nextOrder < 0 || data.available == null || data.offers == null)
            throw new InvalidDataException("시장 사건 저장 날짜 또는 목록이 올바르지 않습니다.");
        var available = new HashSet<int>();
        foreach (int id in data.available)
            if (!eventsByID.ContainsKey(id) || !available.Add(id))
                throw new InvalidDataException("시장 사건 후보 ID가 올바르지 않습니다.");
        var active = new HashSet<int>();
        int previous = -1;
        foreach (var offer in data.offers)
        {
            if (offer == null || !eventsByID.TryGetValue(offer.id, out var definition) || offer.order <= previous ||
                offer.order >= data.nextOrder || offer.start < 1 || offer.start > data.lastTurn + definition.delayTurns ||
                offer.active != (data.lastTurn < offer.start + definition.duration) ||
                (offer.active && (!active.Add(offer.id) || available.Contains(offer.id))) ||
                (!offer.active && !definition.isPermanent) ||
                (definition.effects.Any(effect => effect.companyTargetType == MarketEventCompanyTargetType.RandomCompany) &&
                    !companyIds.Contains(offer.companyId)))
                throw new InvalidDataException("시장 사건의 대상·기한·순서가 올바르지 않습니다.");
            previous = offer.order;
        }
    }

    internal void restoreState(SaveData data)
    {
        bool recorded = data?.recorded == true;
        activeEvents.Clear();
        availableEventIDs.Clear();
        availableEventIDs.AddRange(recorded ? data.available : initialAvailable);
        nextEventOrderID = recorded ? data.nextOrder : 0;
        lastProcessedTurn = recorded ? data.lastTurn : 0;
        EffectControl.clearEffects();
        if (recorded)
            foreach (var offer in data.offers)
            {
                var entry = new ActiveMarketEvent { definition = eventsByID[offer.id], orderID = offer.order,
                    startTurn = offer.start, selectedCompanyID = offer.companyId };
                EffectControl.addEffects(entry);
                if (offer.active) activeEvents.Add(entry);
            }
        EffectControl.applyMarketEventEffects(lastProcessedTurn);
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

    private void addEvent(int currentTurn, string selectionKey)
    {
        if (availableEventIDs.Count == 0) return;

        int index = (int)(FixedDaySeed.StableOrder(currentTurn, selectionKey) % (uint)availableEventIDs.Count);
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
        foreach (MarketEventDefinition.EventEffect effect in definition.effects)
        {
            if (effect.companyTargetType != MarketEventCompanyTargetType.RandomCompany) continue;

            // 같은 이벤트의 랜덤 대상 효과들은 선정 시 확정한 회사 하나를 공유
            entry.selectedCompanyID = companySelector.selectCompanyID();
            break;
        }
        activeEvents.Add(entry);
        logSelectedEvent(entry, currentTurn);
        EffectControl.addEffects(entry);
        marketEventAdded?.Invoke(entry, currentTurn);
    }

    private void logSelectedEvent(ActiveMarketEvent entry, int currentTurn)
    {
        var companyIDs = new List<string>();
        foreach (MarketEventDefinition.EventEffect effect in entry.definition.effects)
        {
            if (effect.companyTargetType == MarketEventCompanyTargetType.AllCompanies)
                companyIDs.Add("AllCompanies");
            else if (effect.companyTargetType == MarketEventCompanyTargetType.RandomCompany)
                companyIDs.Add(entry.selectedCompanyID);
            else
                companyIDs.Add(effect.companyID);
        }

        Debug.Log($"[시장 이벤트 선정] turn={currentTurn}, orderID={entry.orderID}, "
            + $"eventID={entry.definition.MarketEventID}, Name={entry.definition.MarketEventName}, "
            + $"Type={entry.definition.applyType}, companyID=[{string.Join(", ", companyIDs)}], "
            + $"delayTurns={entry.definition.delayTurns}, duration={entry.definition.duration}", entry.definition);
    }
}
