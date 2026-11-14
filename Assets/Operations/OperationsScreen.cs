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
        private int current = -1;
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
            Show(current < 0 ? System.Array.IndexOf(subTabs, documentsTab) : current);
        }

        public void close() => gameObject.SetActive(false);

        public void Show(int index)
        {
            current = index;
            for (int i = 0; i < subTabs.Length; i++)
            {
                bool selected = i == index;
                subTabs[i].SetActive(selected);
                subTabLabels[i].fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
                subTabLabels[i].color = selected ? new Color(1f, .81f, .3f) : new Color(.65f, .75f, .88f);
                subTabBackgrounds[i].color = selected ? new Color(.025f, .065f, .13f) : new Color(.055f, .15f, .29f);
            }
        }

        public void OpenDocumentsTab()
        {
            if (navigation == null) navigation = FindFirstObjectByType<TopBarNavigationController>();
            if (navigation.Navigate(DashboardTab.Operations)) Show(System.Array.IndexOf(subTabs, documentsTab));
        }
    }
}
