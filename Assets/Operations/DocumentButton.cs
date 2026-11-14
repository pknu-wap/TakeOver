using System;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Operations
{
    public sealed class DocumentButton : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private Button button;

        public void Bind(string text, Action clicked, bool interactable = true)
        {
            label.text = text;
            button.interactable = interactable;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => clicked?.Invoke());
        }

        public void SetColor(Color color) => label.color = color;
    }
}
