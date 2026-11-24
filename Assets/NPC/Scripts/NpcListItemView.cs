using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace TakeOver.NPC
{
    public class NpcListItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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
        private bool selected, hovered;
        private CanvasGroup availabilityGroup;

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
            selected = isSelected;
            ApplyColors();
            if (nameText != null)
                nameText.fontStyle = isSelected ? FontStyles.Bold : FontStyles.Normal;
        }

        public void SetListVisualState(bool isSelected, Color selectedBackground, Color normalBackground,
            Color selectedName, Color normalName)
        {
            selected = isSelected;
            selectedBgColor = selectedBackground;
            unselectedBgColor = normalBackground;
            selectedTextColor = selectedName;
            unselectedTextColor = normalName;
            ApplyColors();
        }
        public void SetAvailable(bool available)
        {
            if (button != null) button.interactable = available;
            if (availabilityGroup == null) availabilityGroup = GetComponent<CanvasGroup>();
            if (availabilityGroup == null) availabilityGroup = gameObject.AddComponent<CanvasGroup>();
            availabilityGroup.alpha = available ? 1f : .45f;
            if (!available) hovered = false;
            ApplyColors();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button != null && !button.IsInteractable()) return;
            hovered = true; ApplyColors();
        }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; ApplyColors(); }
        private void OnDisable() { hovered = false; ApplyColors(); }
        private void LateUpdate()
        {
            if (hovered && button != null && !button.IsInteractable()) { hovered = false; ApplyColors(); }
        }
        private void ApplyColors()
        {
            bool available = button == null || button.IsInteractable();
            if (backgroundImage != null)
                backgroundImage.color = !available ? unselectedBgColor : hovered ? Color.Lerp(selectedBgColor, Color.white, .24f) : selected ? selectedBgColor : unselectedBgColor;
            if (nameText != null) nameText.color = !available ? Color.gray : hovered || selected ? selectedTextColor : unselectedTextColor;
        }

        private void HandleClicked()
        {
            onClicked?.Invoke(boundNpcId);
        }
    }
}
