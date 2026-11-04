using UnityEngine;

// 기업 브라우징 화면에서 "지도 보기 ↔ 리스트 보기"를 전환한다.
// CompanyBrowserUI(맨 위 부모)에 붙이고, 상단 메뉴에서 모드를 지정한다.
public class BrowserViewToggle : MonoBehaviour
{
    [Tooltip("지도 모드에서만 보일 것들 (CompanyMap)")]
    [SerializeField] private GameObject[] mapObjects;

    [Tooltip("리스트 모드에서만 보일 것들 (CompanyList, SortDropdown)")]
    [SerializeField] private GameObject[] listObjects;

    [Tooltip("처음에 지도로 시작할지")]
    [SerializeField] private bool startWithMap = true;

    private bool modeSpecified;

    void Start()
    {
        if (!modeSpecified) setMode(startWithMap);
    }

    public void showMap() => setMode(true);
    public void showList() => setMode(false);

    private void setMode(bool showMap)
    {
        modeSpecified = true;

        setActiveAll(mapObjects, showMap);
        setActiveAll(listObjects, !showMap);

    }

    // 목록 안의 오브젝트를 한꺼번에 켜고 끈다.
    // 프리팹에서 오브젝트를 지워 칸이 비어(Missing) 있어도 에러 없이 건너뛴다.
    private static void setActiveAll(GameObject[] targets, bool active)
    {
        if (targets == null)
        {
            return;
        }
        foreach (GameObject obj in targets)
        {
            if (obj != null)
            {
                obj.SetActive(active);
            }
        }
    }
}
