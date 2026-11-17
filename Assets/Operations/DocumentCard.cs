using System;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Operations
{
    public sealed class DocumentCard : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Text title;
        [SerializeField] private Text company;
        [SerializeField] private Text deadline;
        [SerializeField] private Image border;
        [SerializeField] private Image icon;
        [SerializeField] private Sprite pendingIcon;
        [SerializeField] private Sprite doneIcon;
        [SerializeField] private Outline highlight;

        public void Bind(string heading, string target, string status, bool overdue, Action clicked, bool resolved = false)
        {
            title.text = heading;
            company.text = target;
            deadline.text = status;
            title.color = resolved ? new Color(.72f, .73f, .76f) : Color.white;
            deadline.color = overdue ? new Color(1f, .38f, .43f) : title.color;
            icon.sprite = resolved ? doneIcon : pendingIcon;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => clicked());
        }

        public void SetSelected(bool selected)
        {
            border.color = selected ? Color.white : new Color(.16f, .32f, .44f);
            highlight.enabled = selected;
        }
    }
}
