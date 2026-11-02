using System;
using System.Collections;   // IEnumerator (코루틴)
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 회사를 누르면 화면 가운데에 뜨는 "기업 상세 정보" 팝업.
// 뒤 배경은 ScreenBlurBackground로 어둡고 흐리게 만든다.
// 에디터에서는 이 오브젝트(DetailPanel)를 꺼 둔 상태로 둘 것.
[RequireComponent(typeof(CanvasGroup))]
public class CompanyDetailPanel : MonoBehaviour
{
    [Header("배경")]
    [SerializeField] private ScreenBlurBackground blurBackground;   // 비워 두면 흐림 없이 바로 뜬다

    [Header("대표 사진")]
    [SerializeField] private Image coverImage;
    [SerializeField] private Image coverLogoImage;
    [SerializeField] private TMP_Text coverNameText;
    [SerializeField] private Color coverEmptyColor = new Color(0.12f, 0.17f, 0.28f);   // 사진이 없을 때 칸 색

    [Header("회사 소개")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text industryText;
    [SerializeField] private TMP_Text coreValueText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("핵심 지표 (왼쪽부터 6칸)")]
    [SerializeField] private MetricBox[] metricBoxes;

    [Header("내 보유")]
    [SerializeField] private TMP_Text holdingText;

    [Header("거래")]
    [SerializeField] private TMP_InputField amountInput;   // 수량 입력칸
    [SerializeField] private TMP_Text playerCashText;      // 내 잔액
    [SerializeField] private TMP_Text messageText;         // "매수 완료", "잔액 부족" 같은 안내

    public event Action OnTraded;

    public Company current { get; private set; }

    private ShareTradingSystem tradingSystem;
    private Player player;
    private CanvasGroup canvasGroup;
    private bool isOpen;

    // 지도 노드·목록 줄을 누르면 불린다.
    public void show(Company company)
    {
        current = company;
        findReferences();
        refresh();

        if (messageText != null)
        {
            messageText.text = "";
        }

        // 이미 떠 있으면 내용만 바꾼다. (다시 찍으면 팝업까지 배경에 찍혀 버린다)
        if (isOpen)
        {
            return;
        }

        isOpen = true;
        gameObject.SetActive(true);

        if (blurBackground != null)
        {
            StartCoroutine(openWithBlur());
        }
        else
        {
            setVisible(true);
        }
    }

    public void hide()
    {
        isOpen = false;
        StopAllCoroutines();
        gameObject.SetActive(false);
    }

    // 팝업을 한 프레임 동안 투명하게 둔 채 화면을 찍고, 그다음 보이게 한다.
    // 이렇게 해야 배경 사진에 팝업 자신이 찍히지 않는다.
    private IEnumerator openWithBlur()
    {
        setVisible(false);
        yield return new WaitForEndOfFrame();   // 이번 프레임 그리기가 끝날 때까지 기다림
        blurBackground.captureAndBlur();
        setVisible(true);
    }

    // CanvasGroup : 자식 UI 전체의 투명도와 클릭 여부를 한 번에 정하는 컴포넌트
    private void setVisible(bool visible)
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
        if (canvasGroup == null)
        {
            // 프리팹에 CanvasGroup을 깜빡하고 안 붙였어도 동작하게 여기서 붙여 준다.
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;
    }

    // 지금 선택된 회사의 정보로 모든 칸을 다시 채운다.
    private void refresh()
    {
        if (current == null)
        {
            return;
        }

        CompanyDefinition def = current.definition;

        // 대표 사진: 있으면 원래 색(흰색), 없으면 빈 칸 색으로 칠한다.
        if (coverImage != null)
        {
            coverImage.sprite = def.coverImage;
            coverImage.color = def.coverImage != null ? Color.white : coverEmptyColor;
        }
        if (coverLogoImage != null)
        {
            coverLogoImage.sprite = def.logo;
            coverLogoImage.enabled = def.logo != null;
        }
        setText(coverNameText, def.companyName);

        setText(nameText, def.companyName);
        setText(industryText, def.industry);
        setText(coreValueText, def.coreValue);
        setText(descriptionText, def.description);

        float price = CompanyUIUtil.getDerived(current, CompanyDerivedStat.SHARE_PRICE);
        float stake = CompanyUIUtil.getDerived(current, CompanyDerivedStat.STAKE);
        float myShares = CompanyUIUtil.getStat(current, CompanyStat.PLAYER_SHARES);

        // 제목과 값을 한 곳에서 정해 두면, 순서를 바꾸거나 지표를 교체할 때 여기만 고치면 된다.
        setMetric(0, "주가", $"{price:N2}");
        setMetric(1, "매출", $"{CompanyUIUtil.getStat(current, CompanyStat.REVENUE):N0}");
        setMetric(2, "순이익", $"{CompanyUIUtil.getDerived(current, CompanyDerivedStat.NET_PROFIT):N0}");
        setMetric(3, "현금", $"{CompanyUIUtil.getStat(current, CompanyStat.CASH):N0}");
        setMetric(4, "부채비율", $"{CompanyUIUtil.getDerived(current, CompanyDerivedStat.DEBT_RATIO):P1}");
        setMetric(5, "보유 지분", $"{stake:0.#}%");

        setText(holdingText, $"보유 주식  {myShares:N0}주\n평가 금액  {myShares * price:N2}");

        // 내 잔액. Unity 오브젝트에는 ?.가 제대로 동작하지 않는 경우가 있어서 != null로 직접 확인한다.
        float myCash = 0f;
        if (player != null && player.state != null)
        {
            myCash = player.state.cash;
        }
        setText(playerCashText, $"내 잔액  {myCash:N0}");
    }

    private void setMetric(int index, string label, string value)
    {
        if (metricBoxes != null && index < metricBoxes.Length && metricBoxes[index] != null)
        {
            metricBoxes[index].set(label, value);
        }
    }

    private static void setText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    public void buy()
    {
        trade(true);
    }

    public void sell()
    {
        trade(false);
    }

    private void trade(bool isBuy)
    {
        if (current == null)
        {
            return;   // 선택된 회사가 없으면 아무것도 안 함
        }

        findReferences();
        if (tradingSystem == null)
        {
            setText(messageText, "거래 시스템을 찾을 수 없어요");
            return;
        }

        // 입력칸 글자를 정수로 바꾼다. 숫자가 아니면 TryParse가 false를 돌려준다.
        if (amountInput == null || !int.TryParse(amountInput.text, out int amount) || amount <= 0)
        {
            setText(messageText, "수량을 1 이상의 숫자로 입력하세요");
            return;
        }

        bool success = isBuy
            ? tradingSystem.buyShares(current, amount)
            : tradingSystem.sellShares(current, amount);

        string action = isBuy ? "매수" : "매도";
        if (success)
        {
            refresh();   // 팝업은 그대로 두고 숫자만 새로 고침
            setText(messageText, $"{amount}주 {action} 완료");
            OnTraded?.Invoke();
        }
        else
        {
            setText(messageText, isBuy
                ? $"{action} 실패 (잔액이나 남은 주식을 확인하세요)"
                : $"{action} 실패 (보유 주식을 확인하세요)");
        }
    }

    private void findReferences()
    {
        if (tradingSystem == null)
        {
            tradingSystem = FindFirstObjectByType<ShareTradingSystem>();
        }
        if (player == null)
        {
            player = FindFirstObjectByType<Player>();
        }
    }
}
