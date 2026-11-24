using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TakeOver.Events;
using TakeOver.NPC;

namespace TakeOver.Journal
{
   
    public sealed class JournalScreen : MonoBehaviour
    {
        [Header("카드")]
        [Tooltip("가운데 종이 카드 전체. 기록이 없으면 숨긴다")]
        [SerializeField] private GameObject cardRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text dateText;
        [SerializeField] private TMP_Text summaryText;
        [Tooltip("'주요 내용' 아래 점 목록을 한 텍스트에 줄바꿈으로 넣는다")]
        [SerializeField] private TMP_Text keyPointsText;

        [Header("사진 (클립 달린 폴라로이드)")]
        [Tooltip("폴라로이드 틀 전체. 사진이 없는 사건이면 통째로 숨긴다")]
        [SerializeField] private GameObject photoRoot;
        [SerializeField] private Image photoImage;

        [Header("관련 인물")]
        [Tooltip("'관련 인물' 제목 + 칩 줄. 인물이 없으면 숨긴다")]
        [SerializeField] private GameObject peopleSection;
        [Tooltip("Horizontal Layout Group이 붙은 칩 부모")]
        [SerializeField] private RectTransform peopleContainer;
        [SerializeField] private JournalPersonChip personChipPrefab;

        [Header("넘기기")]
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [Tooltip("왼쪽에 살짝 보이는 이전 종이. 이전 기록이 있을 때만 보인다 (선택)")]
        [SerializeField] private GameObject previousPeek;
        [Tooltip("오른쪽에 살짝 보이는 다음 종이. 다음 기록이 있을 때만 보인다 (선택)")]
        [SerializeField] private GameObject nextPeek;
        [SerializeField] private UnityEngine.UI.Image entryIndicator;

        [Tooltip("기록이 하나도 없을 때 보여줄 안내")]
        [SerializeField] private GameObject emptyMessage;

        private static readonly Color selectedIndicatorColor = new Color32(247, 232, 203, 255);
        private static readonly Color inactiveIndicatorColor = new Color32(143, 157, 175, 255);
        private readonly UnityEngine.UI.Image[] entryIndicators = new UnityEngine.UI.Image[7];
        private EventSystemController events;
        private NpcStateRegistry npcs;
        private List<JournalEntry> entries = new List<JournalEntry>();
        private int currentIndex;

        
        private readonly List<JournalPersonChip> chipPool = new List<JournalPersonChip>();
        private readonly StringBuilder textBuilder = new StringBuilder();

        void Awake()
        {
            previousButton.onClick.AddListener(showPrevious);
            nextButton.onClick.AddListener(showNext);

            entryIndicators[0] = entryIndicator;
            for (int i = 1; i < entryIndicators.Length; i++)
                entryIndicators[i] = Instantiate(entryIndicator, entryIndicator.transform.parent);
        }

        
        void OnEnable()
        {
            findDependencies();
            rebuild();
        }

        public void open()
        {
            gameObject.SetActive(true);
        }

        public void close()
        {
            gameObject.SetActive(false);
        }

        public void showPrevious()
        {
            if (currentIndex > 0) showEntry(currentIndex - 1);
        }

        public void showNext()
        {
            if (currentIndex < entries.Count - 1) showEntry(currentIndex + 1);
        }

        private void findDependencies()
        {
            
            if (events == null) events = FindFirstObjectByType<EventSystemController>();
            if (npcs == null) npcs = FindFirstObjectByType<NpcStateRegistry>();
        }

        private void rebuild()
        {
            entries = JournalEntryBuilder.buildStoryEntries(events, npcs);

            bool hasEntries = entries.Count > 0;
            cardRoot.SetActive(hasEntries);
            emptyMessage.SetActive(!hasEntries);
            if (!hasEntries)
            {
                setArrows(false, false);
                refreshIndicators();
                return;
            }

            
            showEntry(entries.Count - 1);
        }

        private void showEntry(int index)
        {
            currentIndex = index;
            JournalEntry entry = entries[index];

            titleText.text = entry.title;
            dateText.text = entry.resolvedDay > 0
                ? GameCalendar.DateOf(entry.resolvedDay).ToString("yyyy.MM.dd", CultureInfo.InvariantCulture)
                : string.Empty;
            summaryText.text = entry.summary;

            textBuilder.Clear();
            foreach (string point in entry.keyPoints)
            {
                if (textBuilder.Length > 0) textBuilder.Append('\n');
                textBuilder.Append("•  ").Append(point);
            }
            keyPointsText.text = textBuilder.ToString();

            bool hasPhoto = entry.photo != null;
            photoRoot.SetActive(hasPhoto);
            if (hasPhoto) photoImage.sprite = entry.photo;

            showPeople(entry.people);
            setArrows(index > 0, index < entries.Count - 1);
            refreshIndicators();
        }

        private void refreshIndicators()
        {
            int visibleCount = Mathf.Min(entries.Count, entryIndicators.Length);
            int firstIndex = Mathf.Clamp(currentIndex - entryIndicators.Length / 2, 0, entries.Count - visibleCount);

            for (int i = 0; i < entryIndicators.Length; i++)
            {
                var indicator = entryIndicators[i];
                bool visible = i < visibleCount;
                indicator.gameObject.SetActive(visible);
                if (visible)
                    indicator.color = firstIndex + i == currentIndex ? selectedIndicatorColor : inactiveIndicatorColor;
            }
        }

        private void showPeople(List<JournalPerson> people)
        {
            peopleSection.SetActive(people.Count > 0);

            
            while (chipPool.Count < people.Count)
                chipPool.Add(Instantiate(personChipPrefab, peopleContainer));

            for (int i = 0; i < chipPool.Count; i++)
            {
                bool used = i < people.Count;
                chipPool[i].gameObject.SetActive(used);
                if (used) chipPool[i].setup(people[i]);
            }
        }

        private void setArrows(bool hasPrevious, bool hasNext)
        {
            previousButton.gameObject.SetActive(hasPrevious);
            nextButton.gameObject.SetActive(hasNext);
            if (previousPeek != null) previousPeek.SetActive(hasPrevious);
            if (nextPeek != null) nextPeek.SetActive(hasNext);
        }

        
        [ContextMenu("디자인 확인용 예시 카드 보기")]
        private void showSampleEntries()
        {
            if (!Application.isPlaying)
            {
                Debug.Log("플레이 모드에서 실행해 주세요.", this);
                return;
            }

            var first = new JournalEntry
            {
                title = "주가 조작 제안 사건",
                resolvedDay = 1,
                summary = "비공개 거래를 통해 주가를 인위적으로 조작하자는 제안을 받았다.\n제안의 발신자는 아직 특정되지 않았으나, 내부 인물일 가능성이 높다.",
                photo = photoImage.sprite
            };
            first.keyPoints.Add("나의 선택: 제안을 거절한다");
            first.keyPoints.Add("윤하진  신뢰 10 증가 · 친밀 5 증가");
            first.keyPoints.Add("서지아  신뢰 3 감소");
            first.people.Add(new JournalPerson { npcId = "sample-a", displayName = "윤하진" });
            first.people.Add(new JournalPerson { npcId = "sample-b", displayName = "서지아" });

            var second = new JournalEntry { title = "두 번째 예시 사건", resolvedDay = 2, summary = "화살표 넘김 확인용 카드입니다." };
            second.keyPoints.Add("나의 선택: 협력 방안을 함께 검토한다");
            second.keyPoints.Add("관계 변화 없음");

            entries = new List<JournalEntry> { first, second };
            cardRoot.SetActive(true);
            emptyMessage.SetActive(false);
            showEntry(0);
        }
    }
}
