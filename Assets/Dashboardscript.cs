using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class Dashboardscript : MonoBehaviour
{
    int currentday = 1;
    int remainingAP = 5; //임의로 첫 AP 5 설정
    int totalAP = 9;
    int firstCardAP = 1; // 첫 번째 카드 소모 AP / 추후 수정 예정
    int secondCardAP = 1; // 두 번째 카드 소모 AP / 추후 수정 예정
    int thirdCardAP = 1; // 세 번째 카드 소모 AP / 추후 수정 예정

    bool usedFirstCard = false;
    bool usedSecondCard = false;
    bool usedThirdCard = false;

    public List<Image> apBox;

    public List<Image> f_APBox;

    public List<Image> s_APBox;

    public List<Image> t_APBox;

    public List<Image> week;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() // 기본실행
    {
        updateDayBoxes();
        updateAPBox();
        CardAPBox();
    }


    public void updateDayBoxes() // 요일 따라 박스 색깔 바꿈
    {
        Color inactiveColor = Color.gray;
        Color activeColor = Color.white;

        int calDay = (currentday - 1) % 7;

        for (int i = 0; i < 7; i++)
        {
            
            if (i == calDay)
            {
                week[i].color = activeColor;
            } else
            {
                week[i].color = inactiveColor;
            }
        }
        
    }

    public void nextDay() // 버튼 누르면 다음 날로 넘어감
    {
        currentday++;
        remainingAP = 5; // 초기값 5로 되돌림, 이후 remainingAP 로직 완성 시 수정하기
        usedFirstCard = usedSecondCard = usedThirdCard = false;
        updateDayBoxes();
        updateAPBox();
    }

    public void updateAPBox() // TopBar AP Box 남은 AP 따라 색깔 바꿈
    {
        Color inactiveColor = Color.white;
        Color activeColor = Color.yellow;

        for (int j = 0; j < totalAP; j++)
        {
            if (j < remainingAP)
            {
                apBox[j].color = activeColor;
            } else
            {
                apBox[j].color = inactiveColor;
            }
        }
    }

    public void CardAPBox() // 카드에 지정된 AP 따라 APcountBox 색깔 바꿈 
    {
        Color inactiveColor = Color.clear;
        Color activeColor = Color.yellow;

        for(int k = 0; k < 5; k++)
        {
            if (k < firstCardAP)
            {
                f_APBox[k].color = activeColor;
            } else
            {
                f_APBox[k].color = inactiveColor;
            }
            if (k < secondCardAP)
            {
                s_APBox[k].color = activeColor;
            }
            else
            {
                s_APBox[k].color = inactiveColor;
            }
            if (k < thirdCardAP)
            {
                t_APBox[k].color = activeColor;
            }
            else
            {
                t_APBox[k].color = inactiveColor;
            }
        }
    }

    public void FirstCardClick() // 첫 번째 카드 클릭 시 실행
    {
        if (!usedFirstCard && remainingAP >= firstCardAP)
        {
            remainingAP -= firstCardAP;
            usedFirstCard = true;
            updateAPBox();
        }
    }

    public void SecondCardClick() // 두 번째 카드 클릭 시 실행
    {
        if (!usedSecondCard && remainingAP >= secondCardAP)
        {
            remainingAP -= secondCardAP;
            usedSecondCard = true;
            updateAPBox();
        }
    }

    public void ThirdCardClick() // 세 번째 카드 클릭 시 실행
    {
        if (!usedThirdCard && remainingAP >= thirdCardAP)
        {
            remainingAP -= thirdCardAP;
            usedThirdCard = true;
            updateAPBox();
        }
    }
}
