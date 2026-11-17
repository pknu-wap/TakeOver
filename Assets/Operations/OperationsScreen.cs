using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Operations
{
    public sealed class OperationsScreen : MonoBehaviour
    {
        [SerializeField] private Button[] subTabButtons;
        [SerializeField] private GameObject[] subTabs;
        [SerializeField] private GameObject documentsTab;
        [SerializeField] private Text[] subTabLabels;
        [SerializeField] private Image[] subTabBackgrounds;
        [SerializeField] private Image panel;
        [SerializeField] private Sprite[] panelSprites;
        [SerializeField] private Vector2 selectedLabelOffset;
        private TopBarNavigationController navigation;

        private void Awake()
        {
            navigation = FindFirstObjectByType<TopBarNavigationController>();
            for (int i = 0; i < subTabButtons.Length; i++)
            {
                int index = i;
                subTabButtons[i].onClick.AddListener(() => Show(index));
            }
        }

        public void open()
        {
            gameObject.SetActive(true);
            Show(System.Array.IndexOf(subTabs, documentsTab));
        }

        public void close() => gameObject.SetActive(false);

        public void Show(int index)
        {
            panel.sprite = panelSprites[index];
            for (int i = 0; i < subTabs.Length; i++)
            {
                bool selected = i == index;
                subTabs[i].SetActive(selected);
                subTabLabels[i].fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
                subTabLabels[i].rectTransform.anchoredPosition = selected ? selectedLabelOffset : Vector2.zero;
                subTabLabels[i].color = selected ? Color.white : new Color(.88f, .88f, .88f);
                subTabBackgrounds[i].enabled = !selected;
            }
        }

        public void OpenDocumentsTab()
        {
            if (navigation == null) navigation = FindFirstObjectByType<TopBarNavigationController>();
            if (navigation.Navigate(DashboardTab.Operations)) Show(System.Array.IndexOf(subTabs, documentsTab));
        }
    }
}
