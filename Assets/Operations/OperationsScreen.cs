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
        [SerializeField] private Outline[] subTabOutlines;
        [SerializeField] private GameObject documentPanel;
        [SerializeField] private GameObject plainFrame;
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
            bool documentsSelected = subTabs[index] == documentsTab;
            documentPanel.SetActive(documentsSelected);
            plainFrame.SetActive(!documentsSelected);
            for (int i = 0; i < subTabs.Length; i++)
            {
                bool selected = i == index;
                subTabs[i].SetActive(selected);
                subTabLabels[i].fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
                bool embeddedLabel = selected && documentsSelected;
                subTabLabels[i].enabled = !embeddedLabel;
                subTabLabels[i].color = selected ? Color.white : new Color(.88f, .88f, .88f);
                subTabBackgrounds[i].color = selected ? new Color(.14f, .21f, .31f) : new Color(.37f, .37f, .37f);
                if (embeddedLabel) subTabBackgrounds[i].color = Color.clear;
                subTabOutlines[i].enabled = selected && !embeddedLabel;
            }
        }

        public void OpenDocumentsTab()
        {
            if (navigation == null) navigation = FindFirstObjectByType<TopBarNavigationController>();
            if (navigation.Navigate(DashboardTab.Operations)) Show(System.Array.IndexOf(subTabs, documentsTab));
        }
    }
}
