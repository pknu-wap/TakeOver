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
    public sealed class DocumentSaveRecord
    {
        public string type;
        public string companyId;
        public int deadlineTurn;
        public List<NameValue> fields;
        public bool resolved;
        public bool approved;
        public int resolvedTurn;
    }
}
