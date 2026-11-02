using TMPro;
using UnityEngine;

// 상세 패널 "핵심 지표"의 작은 칸 하나 (제목 + 값).
// 칸마다 이 스크립트를 붙여 두면, CompanyDetailPanel이 순서대로 제목과 값을 넣어 준다.
public class MetricBox : MonoBehaviour
{
    [SerializeField] private TMP_Text labelText;   // 위쪽 작은 글자 (예: 주가)
    [SerializeField] private TMP_Text valueText;   // 아래쪽 큰 글자 (예: 12,500)

    public void set(string label, string value)
    {
        if (labelText != null)
        {
            labelText.text = label;
        }
        if (valueText != null)
        {
            valueText.text = value;
        }
    }
}
