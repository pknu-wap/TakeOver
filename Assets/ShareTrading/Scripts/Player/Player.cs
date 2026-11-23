using UnityEngine;

public class Player : MonoBehaviour
{
    public PlayerState state { get; private set; }
    public const string DefaultName = "주인공";
    public string ProtagonistName { get; private set; } = DefaultName;

    public void SetName(string name)
    {
        ProtagonistName = string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim();
    }

    private void Awake()
    {
        if (GetComponent<TakeOver.PlayerMemories.PlayerMemoryService>() == null)
            gameObject.AddComponent<TakeOver.PlayerMemories.PlayerMemoryService>();
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
