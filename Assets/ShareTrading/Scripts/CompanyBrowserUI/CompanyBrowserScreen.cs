using UnityEngine;


public class CompanyBrowserScreen : MonoBehaviour
{
    [SerializeField] private CompanyDetailPanel detailPanel;

    
    public void open()
    {
        gameObject.SetActive(true);
    }

    public void close()
    {

        if (detailPanel != null)
        {
            detailPanel.hide();
        }
        gameObject.SetActive(false);
    }
}
