using UnityEngine;
using System.Collections.Generic;
using TakeOver.NPC;

public class CompanyNpcRelations : MonoBehaviour
{
    private Company company;
    private string companyID;
    private readonly Dictionary<string, float> npcOpinions = new Dictionary<string, float>();
    [SerializeField] private NpcStateReader reader;
    [SerializeField] private TmpMarketData marketData;

    public void initialize(string companyID)
    {
        this.companyID = companyID;
        reader.registerCompany(companyID, this);
    }

    private void Start()
    {
        company = GetComponent<Company>();
        reEvalutate();
    }

    private void OnDestroy()
    {
        if (reader != null) reader.unregisterCompany(companyID, this);
    }

    // 신규 등록 또는 평가값 변경이 있을 때 true를 반환한다.
    public bool addNpc(NpcRuntimeState state)
    {
        float opinion = CompanyNpcCalculation.calculateOpinion(state.relation);
        float previousOpinion;
        if (npcOpinions.TryGetValue(state.npcId, out previousOpinion) && previousOpinion == opinion)
            return false;
        npcOpinions[state.npcId] = opinion;
        return true;
    }

    public bool removeNpc(string npcId)
    {
        return npcOpinions.Remove(npcId);
    }

    public void clearNpcs()
    {
        npcOpinions.Clear();
    }

    public void reEvalutate()
    {
        // Reader가 Start 이전에 분배한 값은 이 컴포넌트의 Start에서 반영한다.
        if (marketData == null) return;

        double totalOpinion = 0d;
        foreach (float opinion in npcOpinions.Values)
        {
            totalOpinion += opinion;
        }

        float sentiment = 0f;
        if (npcOpinions.Count > 0) sentiment = (float)(totalOpinion / npcOpinions.Count);

        Company targetCompany = GetComponent<Company>();
        company.marketState.setNpcSentiment(sentiment);
        company.calculator.recalculate(targetCompany, marketData);
    }
}
