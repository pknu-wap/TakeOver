using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.NPC
{
    public class NpcListItemView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image portraitImage;
        [SerializeField] private TextMeshProUGUI nameText;

        [Header("Selection Colors")]
        [SerializeField] private Color selectedBgColor = new Color(0.039f, 0.141f, 0.298f, 1f); // #0A244C
        [SerializeField] private Color unselectedBgColor = new Color(0.847f, 0.835f, 0.808f, 0.95f); // #D8D5CE
        [SerializeField] private Color selectedTextColor = Color.white;
        [SerializeField] private Color unselectedTextColor = new Color(0.133f, 0.133f, 0.133f, 1f); // #222222

        private string boundNpcId;
        private Action<string> onClicked;

        public string BoundNpcId => boundNpcId;

        public void Bind(string npcId, string displayName, Sprite portrait, bool isSelected, Action<string> clickCallback)
        {
            boundNpcId = npcId;
            onClicked = clickCallback;

            if (nameText != null)
            {
                nameText.text = string.IsNullOrEmpty(displayName) ? npcId : displayName;
            }

            if (portraitImage != null)
            {
                portraitImage.sprite = portrait != null ? portrait : NpcInfoPanel.FallbackStanding;
                portraitImage.preserveAspect = true;
            }

            SetSelected(isSelected);

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(HandleClicked);
            }
        }

        public void SetSelected(bool isSelected)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = isSelected ? selectedBgColor : unselectedBgColor;
            }

            if (nameText != null)
            {
                nameText.color = isSelected ? selectedTextColor : unselectedTextColor;
                nameText.fontStyle = isSelected ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        private void HandleClicked()
        {
            onClicked?.Invoke(boundNpcId);
        }
    }
}