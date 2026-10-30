using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Dashboardscript : MonoBehaviour
{
    public Image leftCard;
    public Image mainCard;
    public Image rightCard;

    private Vector2 leftPos;
    private Vector2 centerPos;
    private Vector2 rightPos;

    private bool isMoving = false;

    void Start()
    {
        leftPos =
            leftCard.rectTransform.anchoredPosition;

        centerPos =
            mainCard.rectTransform.anchoredPosition;

        rightPos =
            rightCard.rectTransform.anchoredPosition;
    }

    public void NextCard()
    {
        if (!isMoving)
        {
            StartCoroutine(MoveRight());
        }
    }

    public void PreviousCard()
    {
        if (!isMoving)
        {
            StartCoroutine(MoveLeft());
        }
    }

    IEnumerator MoveRight()
    {
        isMoving = true;

        float duration = 1.0f;
        float time = 0;

        Vector2 leftStart =
            leftCard.rectTransform.anchoredPosition;

        Vector2 centerStart =
            mainCard.rectTransform.anchoredPosition;

        Vector2 rightStart =
            rightCard.rectTransform.anchoredPosition;

        while (time < duration)
        {
            time += Time.deltaTime;

            float t = Mathf.SmoothStep(0f, 1f, time / duration);

            leftCard.rectTransform.localScale =
                Vector3.Lerp(
                    Vector3.one * 0.8f,
                    Vector3.one * 1.2f,
                    t);

            mainCard.rectTransform.localScale =
                Vector3.Lerp(
                    Vector3.one * 1.2f,
                    Vector3.one * 0.8f,
                    t);

            rightCard.rectTransform.localScale =
                Vector3.one * 0.8f;

            yield return null;
        }

        Image temp = leftCard;

        leftCard = rightCard;
        rightCard = mainCard;
        mainCard = temp;

        isMoving = false;
    }

    IEnumerator MoveLeft()
    {
        isMoving = true;

        float duration = 0.3f;
        float time = 0;

        Vector2 leftStart =
            leftCard.rectTransform.anchoredPosition;

        Vector2 centerStart =
            mainCard.rectTransform.anchoredPosition;

        Vector2 rightStart =
            rightCard.rectTransform.anchoredPosition;

        while (time < duration)
        {
            time += Time.deltaTime;

            float t = time / duration;

            leftCard.rectTransform.anchoredPosition =
                Vector2.Lerp(leftStart, rightPos, t);

            mainCard.rectTransform.anchoredPosition =
                Vector2.Lerp(centerStart, leftPos, t);

            rightCard.rectTransform.anchoredPosition =
                Vector2.Lerp(rightStart, centerPos, t);

            leftCard.rectTransform.localScale = Vector3.one * 0.8f;

            mainCard.rectTransform.localScale =
                Vector3.Lerp(
                    Vector3.one * 1.2f,
                    Vector3.one * 0.8f,
                    t);

            rightCard.rectTransform.localScale =
                Vector3.Lerp(
                    Vector3.one * 0.8f,
                    Vector3.one * 1.2f,
                    t);

            yield return null;
        }

        Image temp = leftCard;

        leftCard = mainCard;
        mainCard = rightCard;
        rightCard = temp;

        isMoving = false;
    }
}