using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class Dashboardscript : MonoBehaviour
{
    int currentday = 1;
    int remainingAP = 9;

    int leftCardAP = 0;
    int mainCardAP = 0;
    int rightCardAP = 0;

    int currentCard = 0;

    List<int> todayCards = new List<int>();
    List<bool> usedCards = new List<bool>();

    public List<Image> apBox;
    public List<Image> l_APBox;
    public List<Image> r_APBox;
    public List<Image> main_APBox;
    public List<Image> week;

    public GameObject leftCard;
    public GameObject mainCard;
    public GameObject rightCard;

    public TMP_Text leftCardName;
    public TMP_Text leftCardInformation;
    public TMP_Text mainCardName;
    public TMP_Text mainCardInformation;
    public TMP_Text rightCardName;
    public TMP_Text rightCardInformation;

    public TextAsset CardDataFile;

    CardDataList cardData;

    void Start()
    {
        loadCardData();
        selectTodayCards();

        updateDayBoxes();
        updateAPBox();
        updateCardContents();
    }

    public void updateDayBoxes()
    {
        Color inactiveColor = Color.gray;
        Color activeColor = Color.white;

        int calDay = (currentday - 1) % 7;

        for (int i = 0; i < 7; i++)
        {
            week[i].color = i == calDay ? activeColor : inactiveColor;
        }
    }

    public void nextDay()
    {
        currentday++;
        remainingAP = 9;

        updateDayBoxes();
        updateAPBox();

        selectTodayCards();
        updateCardContents();
    }

    public void updateAPBox()
    {
        Color inactiveColor = Color.white;
        Color activeColor = Color.yellow;

        for (int i = 0; i < apBox.Count; i++)
        {
            apBox[i].color = i < remainingAP ? activeColor : inactiveColor;
        }
    }

    public void CardAPBox()
    {
        Color inactiveColor = Color.clear;
        Color activeColor = Color.yellow;

        for (int i = 0; i < l_APBox.Count; i++)
        {
            l_APBox[i].color = i < leftCardAP ? activeColor : inactiveColor;
        }

        for (int i = 0; i < r_APBox.Count; i++)
        {
            r_APBox[i].color = i < rightCardAP ? activeColor : inactiveColor;
        }

        for (int i = 0; i < main_APBox.Count; i++)
        {
            main_APBox[i].color = i < mainCardAP ? activeColor : inactiveColor;
        }
    }

    public void loadCardData()
    {
        string json = CardDataFile.text;

        cardData = JsonUtility.FromJson<CardDataList>(json);
    }

    public void selectTodayCards()
    {
        todayCards.Clear();
        usedCards.Clear();

        int todayCardCount = Random.Range(2, cardData.cards.Length + 1);

        while (todayCards.Count < todayCardCount)
        {
            int randomIndex = Random.Range(0, cardData.cards.Length);

            if (!todayCards.Contains(randomIndex))
            {
                todayCards.Add(randomIndex);
                usedCards.Add(false);
            }
        }

        currentCard = 0;
    }

    public void updateCardContents()
    {
        int cardCount = todayCards.Count;

        int leftIndex = (currentCard - 1 + cardCount) % cardCount;
        int mainIndex = currentCard;
        int rightIndex = (currentCard + 1) % cardCount;

        CardData leftData = cardData.cards[todayCards[leftIndex]];
        CardData mainData = cardData.cards[todayCards[mainIndex]];
        CardData rightData = cardData.cards[todayCards[rightIndex]];

        leftCardName.text = leftData.title;
        leftCardInformation.text = leftData.description;

        mainCardName.text = mainData.title;
        mainCardInformation.text = mainData.description;

        rightCardName.text = rightData.title;
        rightCardInformation.text = rightData.description;

        leftCardAP = leftData.ap;
        mainCardAP = mainData.ap;
        rightCardAP = rightData.ap;

        CardAPBox();

        Button button = mainCard.GetComponent<Button>();

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(mainCardClick);

        button.interactable = !usedCards[currentCard];
    }

    public void mainCardClick()
    {
        if (usedCards[currentCard])
        {
            return;
        }

        CardData mainData = cardData.cards[todayCards[currentCard]];

        if (remainingAP < mainData.ap)
        {
            return;
        }

        remainingAP -= mainData.ap;
        usedCards[currentCard] = true;

        updateAPBox();

        mainCard.GetComponent<Button>().interactable = false;
    }

    public void nextCard()
    {
        currentCard++;

        if (currentCard >= todayCards.Count)
        {
            currentCard = 0;
        }

        updateCardContents();
    }

    public void previousCard()
    {
        currentCard--;

        if (currentCard < 0)
        {
            currentCard = todayCards.Count - 1;
        }

        updateCardContents();
    }
}