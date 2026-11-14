using UnityEngine;

namespace TakeOver.Documents
{
    [CreateAssetMenu(fileName = "DocumentSettings", menuName = "Operations/Document Settings")]
    public sealed class DocumentSettings : ScriptableObject
    {
        [Min(1)] public int quarterTurns = 21;
        [Min(1)] public int dividendDeadlineTurns = 7;
        public float[] dividendRatioOptions = { 0f, 0.3f, 0.5f, 0.7f };
        public float takeoverRatio = 0.51f;
        [Min(1)] public int historyLimit = 10;
    }
}
