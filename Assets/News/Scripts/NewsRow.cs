using UnityEngine;
using UnityEngine.UI;

// [뉴스 카드의 제목, 관련 기업, 본문 표시]
public class NewsRow : MonoBehaviour
{
    [SerializeField] private Text titleText;
    [SerializeField] private Text companyText;
    [SerializeField] private Text descriptionText;

    public void setup(string title, string companyNames, string description)
    {
        titleText.text = title;
        descriptionText.text = description;

        bool hasCompany = !string.IsNullOrEmpty(companyNames);
        companyText.gameObject.SetActive(hasCompany);
        if (hasCompany)
        {
            companyText.text = companyNames;
        }
    }
}
