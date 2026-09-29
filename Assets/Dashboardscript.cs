using UnityEngine;
using UnityEngine.UI;

public class NewMonoBehaviourScript : MonoBehaviour
{
    int currentday = 1;
    int remainingAP = 5; //임의로 총 AP 5 설정
    int FirstCardAP = 1; // 첫 번째 카드 소모 AP / 추후 수정 예정
    int SecondCardAP = 1; // 두 번째 카드 소모 AP / 추후 수정 예정
    int ThirdCardAP = 1; // 세 번째 카드 소모 AP / 추후 수정 예정

    bool UsedFirstCard = false;
    bool UsedSecondCard = false;
    bool UsedThirdCard = false;

    public Image APBox1;
    public Image APBox2;
    public Image APBox3;
    public Image APBox4;
    public Image APBox5;
    public Image APBox6;
    public Image APBox7;
    public Image APBox8;
    public Image APBox9;

    public Image F_APBox1;
    public Image F_APBox2;
    public Image F_APBox3;
    public Image F_APBox4;
    public Image F_APBox5;

    public Image S_APBox1;
    public Image S_APBox2;
    public Image S_APBox3;
    public Image S_APBox4;
    public Image S_APBox5;

    public Image T_APBox1;
    public Image T_APBox2;
    public Image T_APBox3;
    public Image T_APBox4;
    public Image T_APBox5;

    public Image Monday;
    public Image Tuesday;
    public Image Wednesday;
    public Image Thursday;
    public Image Friday;
    public Image Saturday;
    public Image Sunday;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() // 기본실행
    {
        UpdateDayBoxes();
        UpdateAPBox();
        CardAPBox();
    }

    public void UpdateDayBoxes() // 요일 따라 박스 색깔 바꿈
    {
        Color inactiveColor = Color.gray;
        Color activeColor = Color.white;

        Monday.color = inactiveColor;
        Tuesday.color = inactiveColor;
        Wednesday.color = inactiveColor;
        Thursday.color = inactiveColor;
        Friday.color = inactiveColor;
        Saturday.color = inactiveColor;
        Sunday.color = inactiveColor;

        if (currentday % 7 == 1)
            Monday.color = activeColor;
        else if (currentday % 7 == 2)
            Tuesday.color = activeColor;
        else if (currentday % 7 == 3)
            Wednesday.color = activeColor;
        else if (currentday % 7 == 4)
            Thursday.color = activeColor;
        else if (currentday % 7 == 5)
            Friday.color = activeColor;
        else if (currentday % 7 == 6)
            Saturday.color = activeColor;
        else if (currentday % 7 == 0)
            Sunday.color = activeColor;
    }

    public void NextDay() // 버튼 누르면 다음 날로 넘어감
    {
        currentday++;
        remainingAP = 5; // 초기값 5로 되돌림, 이후 remainingAP 로직 완성 시 수정하기
        UsedFirstCard = UsedSecondCard = UsedThirdCard = false;
        UpdateDayBoxes();
        UpdateAPBox();
    }

    public void UpdateAPBox() // TopBar AP Box 남은 AP 따라 색깔 바꿈
    {
        Color inactiveColor = Color.white;
        Color activeColor = Color.yellow;

        APBox1.color = remainingAP >= 1 ? activeColor : inactiveColor;
        APBox2.color = remainingAP >= 2 ? activeColor : inactiveColor;
        APBox3.color = remainingAP >= 3 ? activeColor : inactiveColor;
        APBox4.color = remainingAP >= 4 ? activeColor : inactiveColor;
        APBox5.color = remainingAP >= 5 ? activeColor : inactiveColor;
        APBox6.color = remainingAP >= 6 ? activeColor : inactiveColor;
        APBox7.color = remainingAP >= 7 ? activeColor : inactiveColor;
        APBox8.color = remainingAP >= 8 ? activeColor : inactiveColor;
        APBox9.color = remainingAP >= 9 ? activeColor : inactiveColor;
    }

    public void CardAPBox() // 카드에 지정된 AP 따라 APcountBox 색깔 바꿈 
    {
        Color inactiveColor = Color.clear;
        Color activeColor = Color.yellow;

        F_APBox1.color = FirstCardAP >= 1 ? activeColor : inactiveColor;
        F_APBox2.color = FirstCardAP >= 2 ? activeColor : inactiveColor;
        F_APBox3.color = FirstCardAP >= 3 ? activeColor : inactiveColor;
        F_APBox4.color = FirstCardAP >= 4 ? activeColor : inactiveColor;
        F_APBox5.color = FirstCardAP >= 5 ? activeColor : inactiveColor;

        S_APBox1.color = SecondCardAP >= 1 ? activeColor : inactiveColor;
        S_APBox2.color = SecondCardAP >= 2 ? activeColor : inactiveColor;
        S_APBox3.color = SecondCardAP >= 3 ? activeColor : inactiveColor;
        S_APBox4.color = SecondCardAP >= 4 ? activeColor : inactiveColor;
        S_APBox5.color = SecondCardAP >= 5 ? activeColor : inactiveColor;

        T_APBox1.color = ThirdCardAP >= 1 ? activeColor : inactiveColor;
        T_APBox2.color = ThirdCardAP >= 2 ? activeColor : inactiveColor;
        T_APBox3.color = ThirdCardAP >= 3 ? activeColor : inactiveColor;
        T_APBox4.color = ThirdCardAP >= 4 ? activeColor : inactiveColor;
        T_APBox5.color = ThirdCardAP >= 5 ? activeColor : inactiveColor;
    }

    public void FirstCardClick() // 첫 번째 카드 클릭 시 실행
    {
        if (!UsedFirstCard && remainingAP >= FirstCardAP)
        {
            remainingAP -= FirstCardAP;
            UsedFirstCard = true;
            UpdateAPBox();
        }
    }

    public void SecondCardClick() // 두 번째 카드 클릭 시 실행
    {
        if (!UsedSecondCard && remainingAP >= SecondCardAP)
        {
            remainingAP -= SecondCardAP;
            UsedSecondCard = true;
            UpdateAPBox();
        }
    }

    public void ThirdCardClick() // 세 번째 카드 클릭 시 실행
    {
        if (!UsedThirdCard && remainingAP >= ThirdCardAP)
        {
            remainingAP -= ThirdCardAP;
            UsedThirdCard = true;
            UpdateAPBox();
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
