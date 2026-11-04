using System;
using TakeOver.NPC;
using UnityEngine;

public sealed class ActionPointService : MonoBehaviour
{
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
