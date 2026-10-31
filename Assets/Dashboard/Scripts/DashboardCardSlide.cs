using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TakeOver.Events;

public class DashboardCardSlide : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private TextMeshProUGUI speakerText;
    [SerializeField] private TextMeshProUGUI apCostText;

    [Header("Random Display Settings")]
    [SerializeField] private int minDisplayCount = 1;
    [SerializeField] private int maxDisplayCount = 5;

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(1f);

        EventSystemController eventController = FindFirstObjectByType<EventSystemController>();

        if (eventController == null)
        {
            Debug.LogError("EventSystemController를 찾을 수 없습니다.");
            yield break;
        }

        var infos = eventController.getTodayEventInfos();

        if (infos != null && infos.Count > 0)
        {
            List<TodayEventInfo> shuffledInfos = ShuffleList(infos);

            int maxCount = Mathf.Min(maxDisplayCount, shuffledInfos.Count);
            int randomCount = Random.Range(minDisplayCount, maxCount + 1);

            List<TodayEventInfo> selectedEvents = shuffledInfos.GetRange(0, randomCount);

            foreach (var eventInfo in selectedEvents)
            {
                Debug.Log($"[TodayEventInfo] Speaker: {eventInfo.speakerRole} {eventInfo.speakerName} | AP Cost: {eventInfo.definition?.getApCost()}");
            }

            SetEvent(selectedEvents[0]);
        }
    }

    public void SetEvent(TodayEventInfo eventInfo)
    {
        if (eventInfo == null)
            return;

        if (speakerText != null)
            speakerText.text = $"{eventInfo.speakerRole} {eventInfo.speakerName}";

        if (apCostText != null)
            apCostText.text = eventInfo.definition.getApCost().ToString();
    }

    private List<T> ShuffleList<T>(IReadOnlyList<T> list)
    {
        List<T> result = new List<T>(list);

        for (int i = result.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);

            T temp = result[i];
            result[i] = result[randomIndex];
            result[randomIndex] = temp;
        }

        return result;
    }
}