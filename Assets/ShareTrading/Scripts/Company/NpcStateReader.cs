using UnityEngine;
using System.Collections.Generic;
using TakeOver.NPC;

public class NpcStateReader : MonoBehaviour
{
    private readonly Dictionary<string, CompanyNpcRelations> activateCompanies
        = new Dictionary<string, CompanyNpcRelations>();
    private NpcStateRegistry registry;

    private void Start()
    {
        registry = FindFirstObjectByType<NpcStateRegistry>();
        if (registry == null)
        {
            Debug.LogError("NPC 저장소를 찾을 수 없습니다.", this);
            return;
        }

        registry.EnsureInitialNpcsInitialized();
        registry.StateChanged += handleNpcStateChanged;
        readNpcs();
    }

    private void OnDestroy()
    {
        if (registry != null) registry.StateChanged -= handleNpcStateChanged;
    }

    public void registerCompany(string companyID, CompanyNpcRelations relations)
    {
        activateCompanies[companyID] = relations;
        // 초기 등록은 Start에서, 게임 중 등록은 해당 회사만 분배한다.
        if (registry == null) return;

        relations.clearNpcs();
        foreach (NpcRuntimeState state in registry.States)
        {
            if (state.companyId == companyID) relations.addNpc(state);
        }
        relations.reEvalutate();
    }

    public void unregisterCompany(string companyID, CompanyNpcRelations relations)
    {
        CompanyNpcRelations registered;
        if (activateCompanies.TryGetValue(companyID, out registered) && registered == relations)
            activateCompanies.Remove(companyID);
    }

    public void readNpcs()
    {
        if (registry == null) return;

        foreach (CompanyNpcRelations relations in activateCompanies.Values)
        {
            relations.clearNpcs();
        }

        foreach (NpcRuntimeState state in registry.States)
        {
            if (state.companyId == null) continue;
            CompanyNpcRelations relations;
            if (activateCompanies.TryGetValue(state.companyId, out relations))
                relations.addNpc(state);
        }

        // 분배를 마친 뒤 회사마다 한 번씩 재계산한다.
        foreach (CompanyNpcRelations relations in activateCompanies.Values)
        {
            relations.reEvalutate();
        }
    }

    private void handleNpcStateChanged(NpcRuntimeState state)
    {
        if (state.companyId == null) return;
        CompanyNpcRelations relations;
        if (!activateCompanies.TryGetValue(state.companyId, out relations)) return;
        if (relations.addNpc(state)) relations.reEvalutate();
    }
}
