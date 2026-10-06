using System;

[Serializable]
public class CardData
{
    public int id;
    public int ap;
    public string title;
    public string description;
}

[Serializable]
public class CardDataList
{
    public CardData[] cards;
}