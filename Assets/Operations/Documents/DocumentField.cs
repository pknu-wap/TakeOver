using System.Collections.Generic;

namespace TakeOver.Documents
{
    public sealed class DocumentField
    {
        public string name { get; }
        public string label { get; }
        public IReadOnlyList<float> options { get; }
        public float min { get; }
        public float max { get; }
        public float step { get; }
        public float value { get; internal set; }

        public DocumentField(string name, string label, float value, IReadOnlyList<float> options,
            float min = 0f, float max = 0f, float step = 0f)
        {
            this.name = name;
            this.label = label;
            this.value = value;
            this.options = options;
            this.min = min;
            this.max = max;
            this.step = step;
        }
    }
}
