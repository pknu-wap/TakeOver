using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Operations
{
    public sealed class OperationsScreen : MonoBehaviour
    {
        [SerializeField] private Button[] subTabButtons;
        [SerializeField] private GameObject[] subTabs;
        private int current;
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
            Show(current);
        }

        public void close() => gameObject.SetActive(false);

        public void Show(int index)
        {
            current = index;
            for (int i = 0; i < subTabs.Length; i++) subTabs[i].SetActive(i == index);
        }

        public void OpenDocumentsTab()
        {
            if (navigation == null) navigation = FindFirstObjectByType<TopBarNavigationController>();
            if (navigation.Navigate(DashboardTab.Operations)) Show(0);
        }
    }
}
