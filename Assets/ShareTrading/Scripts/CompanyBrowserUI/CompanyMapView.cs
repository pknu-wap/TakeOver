using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 도시맵 화면 관리.
// 씬의 Company를 찾아서 지도(MapContent) 위에 노드를 하나씩 놓고,
// 노드를 누르면 리스트와 같은 상세 패널을 연다.
public class CompanyMapView : MonoBehaviour
{
    // 회사 ID 하나에 대한 노드 위치를 묶어 두는 작은 데이터 상자.
    // 로고는 지도와 목록이 같이 쓰도록 CompanyDefinition.logo로 옮겼다.
    [System.Serializable]
    public class NodeSetting
    {
        public string companyID;     // CompanyDefinition의 companyID와 똑같이 입력 (예: mirae-construction)
        public Vector2 position;     // 지도 중앙을 (0,0)으로 한 위치
    }

    [Header("지도")]
    [SerializeField] private RectTransform nodeParent;     // 노드가 들어갈 곳 = MapContent
    [SerializeField] private CompanyMapNode nodePrefab;    // 노드 프리팹

    [Header("상세 패널")]
    [SerializeField] private CompanyDetailPanel detailPanel;

    [Header("노드 설정")]
    [Tooltip("회사 ID별 위치. 여기 없는 회사는 지도 중앙 둘레에 자동으로 놓인다.")]
    [SerializeField] private List<NodeSetting> nodeSettings = new List<NodeSetting>();

    [Tooltip("자동 배치할 때 중앙에서 떨어지는 거리")]
    [SerializeField] private float autoRadius = 600f;

    void Start()
    {
        // 목록을 "ID → 설정" 사전으로 한 번만 바꿔 둔다.
        Dictionary<string, NodeSetting> settingByID = buildSettingTable();

        // 씬의 회사를 찾고, ID순으로 정렬한다.
        // 찾는 순서는 실행할 때마다 달라질 수 있어서, 정렬해 두어야 자동 배치 위치가 매번 같다.
        List<Company> companies = FindObjectsByType<Company>(FindObjectsSortMode.None)
            .Where(c => c != null && c.state != null)
            .OrderBy(c => c.definition.companyID)
            .ToList();

        for (int i = 0; i < companies.Count; i++)
        {
            Company company = companies[i];
            string companyID = company.definition.companyID;

            // TryGetValue : 사전에 그 ID가 있으면 true + setting에 값을 넣어 주고, 없으면 false + setting은 null
            settingByID.TryGetValue(companyID, out NodeSetting setting);

            CompanyMapNode node = Instantiate(nodePrefab, nodeParent);
            node.setup(company, company.definition.logo, detailPanel.show);

            // 노드 위치 지정. UI 오브젝트의 위치는 RectTransform의 anchoredPosition으로 바꾼다.
            RectTransform rect = (RectTransform)node.transform;

            if (setting != null)
            {
                rect.anchoredPosition = setting.position;
            }
            else
            {
                // ID 오타나 설정 누락을 바로 알 수 있게 Console에 노란 경고를 남긴다.
                Debug.LogWarning($"[CompanyMapView] '{companyID}' 노드 설정이 없어 자동 배치합니다.");
                rect.anchoredPosition = getAutoPosition(i, companies.Count);
            }
        }
    }

    // 인스펙터 목록을 ID로 바로 찾을 수 있는 사전(Dictionary)으로 바꾼다.
    // 빈 칸과 중복 ID는 건너뛰고 경고를 남긴다.
    private Dictionary<string, NodeSetting> buildSettingTable()
    {
        Dictionary<string, NodeSetting> table = new Dictionary<string, NodeSetting>();

        foreach (NodeSetting setting in nodeSettings)
        {
            if (setting == null || string.IsNullOrWhiteSpace(setting.companyID))
            {
                continue;
            }

            string key = setting.companyID.Trim();   // 앞뒤 실수로 들어간 공백 제거

            if (table.ContainsKey(key))
            {
                Debug.LogWarning($"[CompanyMapView] '{key}' 노드 설정이 중복돼서 첫 번째 것만 씁니다.");
                continue;
            }

            table.Add(key, setting);
        }

        return table;
    }

    // 자동 배치: 회사들을 원 둘레에 같은 간격으로 놓는다.
    // 한 바퀴(2π 라디안)를 회사 수로 나눈 각도만큼씩 돌아가며, cos·sin으로 x·y를 구한다.
    private Vector2 getAutoPosition(int index, int count)
    {
        float angle = index * Mathf.PI * 2f / Mathf.Max(count, 1);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * autoRadius;
    }
}
