using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 도시맵 화면 관리.
// 씬의 Company를 찾아서 지도(MapContent) 위에 노드를 하나씩 놓고,
// 노드를 누르면 리스트와 같은 상세 패널을 연다.
public class CompanyMapView : MonoBehaviour
{
    // 회사 이름과 지도 위 위치를 짝지어 두는 작은 데이터 상자.
    // [System.Serializable]이 있어서 인스펙터에서 목록으로 입력할 수 있다.
    [System.Serializable]
    public class NodePosition
    {
        public string companyName;   // CompanyDefinition의 회사 이름과 똑같이 입력 (예: TmpCompanyA)
        public Vector2 position;     // 지도 중앙을 (0,0)으로 한 위치
    }

    [Header("지도")]
    [SerializeField] private RectTransform nodeParent;     // 노드가 들어갈 곳 = MapContent
    [SerializeField] private CompanyMapNode nodePrefab;    // 노드 프리팹

    [Header("상세 패널")]
    [SerializeField] private CompanyDetailPanel detailPanel;

    [Header("노드 위치")]
    [Tooltip("회사별 위치. 여기 없는 회사는 지도 중앙 둘레에 자동으로 놓인다.")]
    [SerializeField] private List<NodePosition> positions = new List<NodePosition>();

    [Tooltip("자동 배치할 때 중앙에서 떨어지는 거리")]
    [SerializeField] private float autoRadius = 600f;

    void Start()
    {
        // 씬의 회사를 찾고, 이름순으로 정렬한다.
        // 찾는 순서는 실행할 때마다 달라질 수 있어서, 정렬해 두어야 자동 배치 위치가 매번 같다.
        List<Company> companies = FindObjectsByType<Company>(FindObjectsSortMode.None)
            .Where(c => c != null && c.state != null)
            .OrderBy(c => c.definition.companyName)
            .ToList();

        for (int i = 0; i < companies.Count; i++)
        {
            Company company = companies[i];

            CompanyMapNode node = Instantiate(nodePrefab, nodeParent);
            node.Setup(company, detailPanel.Show);

            // 노드 위치 지정. UI 오브젝트의 위치는 RectTransform의 anchoredPosition으로 바꾼다.
            // (RectTransform)node.transform : transform을 UI용 RectTransform으로 바꿔서 쓰는 형변환
            RectTransform rect = (RectTransform)node.transform;
            rect.anchoredPosition = GetPosition(company.definition.companyName, i, companies.Count);
        }
    }

    // 회사 이름으로 위치를 찾는다. 목록에 없으면 원 모양으로 자동 배치한다.
    private Vector2 GetPosition(string companyName, int index, int count)
    {
        foreach (NodePosition p in positions)
        {
            if (p.companyName == companyName)
            {
                return p.position;
            }
        }

        // 자동 배치: 회사들을 원 둘레에 같은 간격으로 놓는다.
        // 한 바퀴(2π 라디안)를 회사 수로 나눈 각도만큼씩 돌아가며, cos·sin으로 x·y를 구한다.
        float angle = index * Mathf.PI * 2f / Mathf.Max(count, 1);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * autoRadius;
    }
}
