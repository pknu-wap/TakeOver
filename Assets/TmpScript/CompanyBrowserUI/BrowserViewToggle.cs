using TMPro;
using UnityEngine;

// 기업 브라우징 화면에서 "지도 보기 ↔ 리스트 보기"를 전환한다.
// CompanyBrowserUI(맨 위 부모)에 붙이고, 전환 버튼의 OnClick에 Toggle()을 연결한다.
public class BrowserViewToggle : MonoBehaviour
{
    [Tooltip("지도 모드에서만 보일 것들 (CompanyMap)")]
    [SerializeField] private GameObject[] mapObjects;

    [Tooltip("리스트 모드에서만 보일 것들 (CompanyList, SortDropdown)")]
    [SerializeField] private GameObject[] listObjects;

    [Tooltip("전환 버튼의 글자")]
    [SerializeField] private TMP_Text buttonLabel;

    [Tooltip("처음에 지도로 시작할지")]
    [SerializeField] private bool startWithMap = true;

    private bool showingMap;

    void Start()
    {
        SetMode(startWithMap);
    }

    // 전환 버튼 OnClick에 연결
    public void Toggle()
    {
        SetMode(!showingMap);   // ! : true ↔ false 뒤집기
    }

    private void SetMode(bool showMap)
    {
        showingMap = showMap;

        foreach (GameObject obj in mapObjects)
        {
            obj.SetActive(showMap);
        }
        foreach (GameObject obj in listObjects)
        {
            obj.SetActive(!showMap);
        }

        // 버튼에는 "지금 누르면 어디로 가는지"를 보여준다
        if (buttonLabel != null)
        {
            buttonLabel.text = showMap ? "리스트 보기" : "지도 보기";
        }
    }
}
