using System;           // Action<T>
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 도시맵 위의 회사 노드(동그란 버튼) 하나.
// 리스트의 CompanyRow와 같은 역할이다. 프리팹으로 만들어 두면 CompanyMapView가 회사 수만큼 복제한다.
public class CompanyMapNode : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;   // 노드에 표시할 회사 이름
    [SerializeField] private Button button;       // 노드 클릭

    // 이 노드가 표시하는 회사
    public Company company { get; private set; }

    // company : 표시할 회사
    // onClick : 클릭하면 실행할 함수 (보통 상세 패널의 show)
    public void setup(Company company, Action<Company> onClick)
    {
        this.company = company;   // this.company = 이 클래스의 변수, company = 매개변수. 이름이 같아서 this로 구분
        nameText.text = company.definition.companyName;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick(company));
    }
}
