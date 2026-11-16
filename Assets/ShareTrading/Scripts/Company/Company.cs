using UnityEngine;

public class Company : MonoBehaviour
{
    [SerializeField] private CompanyDefinition companyDefinition;

    public CompanyDefinition definition => companyDefinition;

    public CompanyState state { get; private set; }
    public CompanyMarketState marketState { get; private set; }
    public CompanyHistory history { get; private set; }
    [field: SerializeField] public CompanyNpcRelations npcRelations { get; private set; }
    public CompanyStatCalculator calculator;
    public bool createdFromCode { get; private set; }

    private void Awake()
    {
        if (state != null) return;

        if (companyDefinition == null)
        {
            Debug.LogError("CompanyDefinition 연결 안됨");
            return;
        }

        createRuntimeState();

        npcRelations.initialize(companyDefinition.companyID);

        printLog();
    }

    public void initialize(CompanyDefinition definition)
    {
        if (state != null)
        {
            Debug.LogError("이미 초기화된 기업입니다.", this);
            return;
        }
        if (definition == null)
        {
            Debug.LogError("CompanyDefinition 연결 안됨");
            return;
        }

        companyDefinition = definition;
        createdFromCode = true;
        createRuntimeState();

        if (npcRelations == null) npcRelations = gameObject.AddComponent<CompanyNpcRelations>();

        printLog();
    }

    private void createRuntimeState()
    {
        state = new CompanyState(companyDefinition);
        marketState = new CompanyMarketState();
        history = new CompanyHistory(state);
        calculator = new CompanyStatCalculator();
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
