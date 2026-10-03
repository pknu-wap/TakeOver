using UnityEngine;

public class A : MonoBehaviour
{
    [SerializeField] private UIManager UIManager;
    [SerializeField] private GameObject dashboardUI;

    public void OnToggleCompanyBrowser()
    {
        if (dashboardUI.activeSelf)
        {
            UIManager.enterCompanyBrowser();
        }
        else
        {
            UIManager.enterDashboard();
        }
    }
}
