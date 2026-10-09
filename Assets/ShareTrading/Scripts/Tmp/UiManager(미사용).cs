using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] GameObject dashBoardUI;
    [SerializeField] GameObject companyBrowseUI;

    public void Start()
    {
        dashBoardUI.SetActive(true);
        companyBrowseUI.SetActive(false);
    }
    public void enterCompanyBrowser()
    {
        dashBoardUI.SetActive(false);
        companyBrowseUI.SetActive(true);
    }

    public void enterDashboard()
    {
        companyBrowseUI.SetActive(false);
        dashBoardUI.SetActive(true);
    }
}
