using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Operations
{
    public sealed class DocumentInfoRow : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private Text value;
        public void Bind(string heading, string text)
        {
            label.text = heading;
            value.text = text;
        }
    }
}
