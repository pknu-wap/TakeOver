using UnityEngine;

public class ShareAmountSelect : MonoBehaviour
{
    [SerializeField] private TMPro.TMP_InputField inputField;

    public bool tryGetAmount(out int amount)
    {
        return int.TryParse(inputField.text, out amount) && amount > 0;
    }
}
