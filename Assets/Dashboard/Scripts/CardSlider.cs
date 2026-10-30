using System.Collections;
using UnityEngine;

public class CardSlider : MonoBehaviour
{
    public RectTransform leftCard;
    public RectTransform mainCard;
    public RectTransform rightCard;

    private Vector2 leftPos;
    private Vector2 centerPos;
    private Vector2 rightPos;

    private bool isMoving = false;

    void Start()
    {
        leftPos = leftCard.anchoredPosition;
        centerPos = mainCard.anchoredPosition;
        rightPos = rightCard.anchoredPosition;
    }

    public void MoveRight()
    {
        if (isMoving) return;

        StartCoroutine(MoveRightCoroutine());
    }

    public void MoveLeft()
    {
        if (isMoving) return;

        StartCoroutine(MoveLeftCoroutine());
    }

    IEnumerator MoveRightCoroutine()
    {
        isMoving = true;

        float duration = 0.3f;
        float time = 0;

        Vector2 leftStart = leftCard.anchoredPosition;
        Vector2 centerStart = mainCard.anchoredPosition;
        Vector2 rightStart = rightCard.anchoredPosition;

        while (time < duration)
        {
            time += Time.deltaTime;

            float t = time / duration;

            leftCard.anchoredPosition =
                Vector2.Lerp(leftStart, centerPos, t);

            mainCard.anchoredPosition =
                Vector2.Lerp(centerStart, rightPos, t);

            rightCard.anchoredPosition =
                Vector2.Lerp(rightStart, leftPos, t);

            yield return null;
        }

        RectTransform temp = leftCard;

        leftCard = rightCard;
        rightCard = mainCard;
        mainCard = temp;

        leftCard.anchoredPosition = leftPos;
        mainCard.anchoredPosition = centerPos;
        rightCard.anchoredPosition = rightPos;

        isMoving = false;
    }

    IEnumerator MoveLeftCoroutine()
    {
        isMoving = true;

        float duration = 0.3f;
        float time = 0;

        Vector2 leftStart = leftCard.anchoredPosition;
        Vector2 centerStart = mainCard.anchoredPosition;
        Vector2 rightStart = rightCard.anchoredPosition;

        while (time < duration)
        {
            time += Time.deltaTime;

            float t = time / duration;

            leftCard.anchoredPosition =
                Vector2.Lerp(leftStart, rightPos, t);

            mainCard.anchoredPosition =
                Vector2.Lerp(centerStart, leftPos, t);

            rightCard.anchoredPosition =
                Vector2.Lerp(rightStart, centerPos, t);

            yield return null;
        }

        RectTransform temp = leftCard;

        leftCard = mainCard;
        mainCard = rightCard;
        rightCard = temp;

        leftCard.anchoredPosition = leftPos;
        mainCard.anchoredPosition = centerPos;
        rightCard.anchoredPosition = rightPos;

        isMoving = false;
    }
}