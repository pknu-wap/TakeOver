using System;
using UnityEngine;

public sealed class ActionPointService : MonoBehaviour
{
    [SerializeField] private int currentAp = 50;
    [SerializeField] private int maxAp = 50;

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
