using UnityEngine;

public class Company : MonoBehaviour
{
    [SerializeField] private CompanyDefinition companyDefinition;

    public CompanyState state { get; private set; }
    public CompanyHistory history { get; private set; }
    public CompanyStatCalculator calculator;

    private void Awake()
    {
        if (companyDefinition == null)
        {
            Debug.LogError("CompanyDefinition 연결 안됨");
            return;
        }

        state = new CompanyState(companyDefinition);
        history = new CompanyHistory(state);
        calculator = new CompanyStatCalculator();

        printLog();
    }
    
    public void printLog()
    {
        if (companyDefinition == null)
        {
            return;
        }
        else
        {
            Debug.Log($"{companyDefinition.companyName} 로드됨.");
        }
    }
}
