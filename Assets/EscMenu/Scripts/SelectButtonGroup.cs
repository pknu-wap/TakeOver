using UnityEngine;

public sealed class SelectButtonGroup : MonoBehaviour
{
    [Tooltip("버튼 순서대로, 각 버튼 안의 선택 표시 오브젝트")]
    [SerializeField] private GameObject[] selectedMarks;

    [Tooltip("처음에 선택돼 있을 버튼 번호 (0부터). 설정 리셋 때도 이 번호로 돌아간다")]
    [SerializeField] private int defaultIndex = 1;

    private int currentIndex;

    public int selectedIndex => currentIndex;

    private void Awake()
    {
        resetToDefault();
    }

    public void select(int index)
    {
        currentIndex = index;
        for (int i = 0; i < selectedMarks.Length; i++)
        {
            selectedMarks[i].SetActive(i == index);
        }
    }

    public void resetToDefault()
    {
        select(defaultIndex);
    }
}
