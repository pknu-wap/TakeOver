using System;
using System.Collections.Generic;

namespace TakeOver.Documents
{
    [Serializable]
    public struct NameValue
    {
        public string name;
        public float value;
    }

    [Serializable]
    public struct NameText
    {
        public string label;
        public string value;
    }

    [Serializable]
    public sealed class DocumentSaveRecord
    {
        public string type;
        public string companyId;
        public int deadlineTurn;
        public List<NameValue> fields;
        public bool resolved;
        public bool approved;
        public int resolvedTurn;
        public int arrival;
        public int arrivalTurn;
        public bool snapshot;
        public string title;
        public string author;
        public List<NameText> infoRows;
    }
}
