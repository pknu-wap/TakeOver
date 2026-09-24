using UnityEngine;

public class Player : MonoBehaviour
{
    public PlayerState state { get; private set; }

    private void Awake()
    {
        state = new PlayerState(
            initialCash: 10000f,
            initialManPower: 10f,
            initialInformation: 5f,
            initialReputation: 0f
        );

        printLog();  
    }

    public void printLog()
    {
        if (state == null)
        {
            Debug.LogError("PlayerState 연결 안됨");
        }
        else
        {
            Debug.Log($"PlayerState 로드됨. " +
                      $"Cash: {state.cash}, " +
                      $"ManPower: {state.manPower}, " +
                      $"Information: {state.information}, " +
                      $"Reputation: {state.reputation}");
        } 
    }
}
