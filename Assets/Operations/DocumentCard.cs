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
        [SerializeField] private Text resolvedSummary;
        [SerializeField] private Image border;

        public void Bind(string heading, string target, string status, bool overdue, Action clicked, bool resolved = false)
        {
            title.text = heading;
            company.text = target;
            deadline.text = status;
            company.gameObject.SetActive(!resolved);
            deadline.gameObject.SetActive(!resolved);
            resolvedSummary.gameObject.SetActive(resolved);
            resolvedSummary.text = $"{target} · {status}";
            deadline.color = overdue ? new Color(1f, .38f, .43f) : new Color(.68f, .79f, .92f);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => clicked());
        }

        public void SetSelected(bool selected) =>
            border.color = selected ? new Color(1f, .81f, .3f) : new Color(.14f, .26f, .43f);
    }
}
