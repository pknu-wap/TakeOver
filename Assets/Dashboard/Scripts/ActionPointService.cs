using System;
using TakeOver.NPC;
using UnityEngine;

public sealed class ActionPointService : MonoBehaviour, TakeOver.SaveSystem.ISaveParticipant
{
    [Serializable] private sealed class SaveData { public int current, maximum; }
    private SaveData prepared;
    public string saveId => "action-points";
    public int saveVersion => 1;
    public string captureJson() => JsonUtility.ToJson(new SaveData { current = currentAp, maximum = maxAp });
    public void prepareLoad(string json, int savedVersion, TakeOver.SaveSystem.DayStartData dayStart)
    {
        prepared = null;
        var data = JsonUtility.FromJson<SaveData>(json);
        if (savedVersion != saveVersion || data == null || data.maximum <= 0 || data.current < 0 || data.current > data.maximum)
            throw new System.IO.InvalidDataException("행동 포인트 저장 데이터가 올바르지 않습니다.");
        prepared = data;
    }
    public void applyLoad()
    {
        if (prepared == null) throw new InvalidOperationException("prepareLoad를 먼저 완료해야 합니다.");
        currentAp = prepared.current; maxAp = prepared.maximum; prepared = null;
    }
    public void rebuildAfterLoad() => Changed?.Invoke();
    [SerializeField] private int currentAp = 50;
    [SerializeField] private int maxAp = 50;

    private GameTurnClock turnClock;

    private void Awake()
    {
        turnClock = FindFirstObjectByType<GameTurnClock>();
    }

    private void OnEnable()
    {
        if (turnClock != null) turnClock.TurnAdvanced += HandleTurnAdvanced;
    }

    private void OnDisable()
    {
        if (turnClock != null) turnClock.TurnAdvanced -= HandleTurnAdvanced;
    }

    private void HandleTurnAdvanced(int newTurn)
    {
        Restore();
    }

    public int Current => currentAp;
    public int Max => maxAp;
    public event Action Changed;

    public bool TryConsume(int cost)
    {
        if (cost <= 0) return true;
        if (cost > currentAp) return false;
        currentAp -= cost;
        Changed?.Invoke();
        return true;
    }

    public void Restore()
    {
        if (currentAp == maxAp) return;
        currentAp = maxAp;
        Changed?.Invoke();
    }
}
