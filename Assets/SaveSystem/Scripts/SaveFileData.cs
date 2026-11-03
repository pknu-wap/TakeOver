using System;
using System.Collections.Generic;

namespace TakeOver.SaveSystem
{
    /// <summary>하루 마감 완료 후, 다음 날짜 시작 처리 직전의 상태</summary>
    [Serializable]
    public struct DayStartData
    {
        public int day;
        public int seed;

        public DayStartData(int day, int seed)
        {
            this.day = day;
            this.seed = seed;
        }
    }

    [Serializable]
    public sealed class SaveFileData
    {
        public const int CurrentFormatVersion = 2;
        public int formatVersion;
        public DayStartData dayStart;
        public List<SaveSectionData> sections;
    }

    [Serializable]
    public sealed class SaveSectionData
    {
        public string id;
        public int version;
        public string json;
    }
}
