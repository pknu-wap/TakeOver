using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TakeOver.UI
{
    [RequireComponent(typeof(Button))]
    public sealed class ButtonHoverFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly Color Accent = new Color(.5f, .85f, 1f, 1f);
        private static readonly Color SelectedAccent = new Color(.2f, .65f, 1f, 1f);
        private Button button;
        private Graphic background;
        private Color normalBackground;
        private readonly Dictionary<Graphic, Color> labelColors = new Dictionary<Graphic, Color>();
        private Outline outline;
        private bool normalOutlineEnabled, hovered, selected;
        private Color normalOutlineColor;
        private Vector2 normalOutlineDistance;

        public static void Attach(Button target)
        {
            if (target != null && target.GetComponent<ButtonHoverFeedback>() == null)
                target.gameObject.AddComponent<ButtonHoverFeedback>();
        }
        private void Awake()
        {
            button = GetComponent<Button>();
            background = button.targetGraphic;
            if (background != null)
            {
                normalBackground = background.color;
                outline = background.GetComponent<Outline>();
                if (outline == null) { outline = background.gameObject.AddComponent<Outline>(); outline.enabled = false; }
                normalOutlineEnabled = outline.enabled;
                normalOutlineColor = outline.effectColor;
                normalOutlineDistance = outline.effectDistance;
            }
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
                if (graphic is Text || graphic is TMP_Text) labelColors[graphic] = graphic.color;
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!button.IsInteractable()) return;
            hovered = true; Apply(true);
        }
        public void SetSelected(bool value) { selected = value; Apply(hovered); }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; Apply(false); }
        private void OnDisable() { hovered = false; Apply(false); }
        private void LateUpdate()
        {
            if (hovered && !button.IsInteractable()) { hovered = false; Apply(false); }
        }
        private void Apply(bool highlight)
        {
            bool activeSelection = selected && button != null && button.IsInteractable();
            var accent = activeSelection ? SelectedAccent : Accent;
            bool emphasized = highlight || activeSelection;
            if (background != null)
            {
                var color = emphasized ? Color.Lerp(normalBackground, accent, activeSelection ? .35f : .15f) : normalBackground;
                if (emphasized) color.a = Mathf.Max(color.a, activeSelection ? .35f : .18f);
                background.color = color;
            }
            foreach (var label in labelColors)
                if (label.Key != null) label.Key.color = emphasized ? accent : label.Value;
            if (outline != null)
            {
                outline.effectColor = emphasized ? new Color(accent.r, accent.g, accent.b, activeSelection ? 1f : .75f) : normalOutlineColor;
                outline.effectDistance = emphasized ? new Vector2(1.5f, -1.5f) : normalOutlineDistance;
                outline.enabled = emphasized || normalOutlineEnabled;
            }
        }
    }
}
