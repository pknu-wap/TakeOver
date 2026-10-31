using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using Unity.XR.GoogleVr;
using UnityEngine;
using UnityEngine.UI;
using TakeOver.Events;

public class DashboardController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI apCostText;
    [SerializeField] private TextMeshProUGUI speakerText;

    [Header("랜덤 추출 범위 설정")]
    [SerializeField] private int minDisplayCount = 1;
    [SerializeField] private int maxDisplayCount = 5;

    public void SetEvent(TodayEventInfo eventInfo)
    {
        if (eventInfo == null)
            return;

        Debug.Log($"SetEvent 호출됨: {eventInfo.speakerName}");

        speakerText.text = $"{eventInfo.speakerRole} {eventInfo.speakerName}";
        apCostText.text = eventInfo.definition.getApCost().ToString();
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(1f);

        EventSystemController eventController = FindFirstObjectByType<EventSystemController>();

        if (eventController == null)
        {
            Debug.LogError("EventSystemController 없음");
            yield break;
        }

        var infos = eventController.getTodayEventInfos();

        Debug.Log($"TodayEvent Total Count : {infos.Count}");

        if (infos != null && infos.Count > 0)
        {
            List<TodayEventInfo> shuffledInfos = ShuffleList(infos);

            int maxCount = Mathf.Min(maxDisplayCount, shuffledInfos.Count);
            int randomCount = Random.Range(minDisplayCount, maxCount + 1);

            List<TodayEventInfo> selectedEvents = shuffledInfos.GetRange(0, randomCount);

            Debug.Log($"이번에 뽑힌 랜덤 개수 : {selectedEvents.Count}개");

            SetEvent(selectedEvents[0]);
        }
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