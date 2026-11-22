using UnityEngine;


public class CompanyBrowserScreen : MonoBehaviour
{
    [SerializeField] private CompanyDetailPanel detailPanel;

    public void CoverDashboardBackground()
    {
        // Map controls are authored in this canvas; cover the dashboard beneath them.
        var background = transform.Find("ListBackground");
        if (background == null) return;
        background.gameObject.SetActive(true);
        background.SetAsFirstSibling();
        var image = background.GetComponent<UnityEngine.UI.Image>();
        if (image != null)
        {
            var color = image.color; color.a = 1; image.color = color;
            var mapFrame = transform.Find("CompanyMap")?.GetComponent<UnityEngine.UI.Image>();
            if (mapFrame != null) mapFrame.color = color;
        }
    }

    
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
