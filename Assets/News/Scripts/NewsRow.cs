using UnityEngine;
using UnityEngine.UI;

// 뉴스 목록의 한 줄 (제목 + 관련 기업)
public class NewsRow : MonoBehaviour
{
    [SerializeField] private Text titleText;
    [SerializeField] private Text companyText;

    public void setup(string title, string companyNames)
    {
        titleText.text = title;

        bool hasCompany = !string.IsNullOrEmpty(companyNames);
        companyText.gameObject.SetActive(hasCompany);
        if (hasCompany)
        {
            companyText.text = companyNames;
        }
    }
}
