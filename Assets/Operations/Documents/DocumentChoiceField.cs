using System;
using System.Collections.Generic;

namespace TakeOver.Documents
{
    public sealed class DocumentChoice
    {
        public int key;
        public string label;
        public bool enabled = true;
    }

    public sealed class DocumentChoiceField
    {
        private readonly Func<IReadOnlyList<DocumentChoice>> choices;
        public string name { get; }
        public string label { get; }
        public int? value { get; internal set; }

        public DocumentChoiceField(string name, string label, Func<IReadOnlyList<DocumentChoice>> choices, int? value = null)
        {
            this.name = name;
            this.label = label;
            this.choices = choices;
            this.value = value;
        }

        public IReadOnlyList<DocumentChoice> GetChoices() => choices();
    }
}
