using UnityEngine;
using UnityEngine.UI;
namespace TakeOver.Employees
{
    public class EmployeeCompanySelectionView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private GameObject dot;
        [SerializeField] private Sprite normalSprite, selectedSprite;
        public void SetSelected(bool selected)
        {
            background.sprite=selected?selectedSprite:normalSprite;
            dot.SetActive(selected);
        }
    }
}
