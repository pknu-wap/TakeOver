using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TakeOver.SaveSystem;
using UnityEngine;

public partial class NewsFeed : ISaveParticipant, ILegacySaveSectionInitializer
{
    [Serializable] private sealed class SavedNews { public List<NewsEntry> entries = new List<NewsEntry>(); }
    private SavedNews preparedNews;
    public string saveId => "news";
    public int saveVersion => 1;
    public string captureJson() => JsonUtility.ToJson(new SavedNews { entries = entries });
    public void prepareLoad(string json, int version, DayStartData day)
    {
        preparedNews = null;
        var data = JsonUtility.FromJson<SavedNews>(json);
        if (version != 1 || Newtonsoft.Json.Linq.JObject.Parse(json)["entries"]?.Type != Newtonsoft.Json.Linq.JTokenType.Array || data?.entries == null || data.entries.Any(item => item == null || item.turn < 1 || item.turn > day.day || string.IsNullOrWhiteSpace(item.title) || item.companyIDs == null || item.companyIDs.Any(string.IsNullOrEmpty)))
            throw new InvalidDataException("뉴스 저장 기록이 올바르지 않습니다.");
        var all = FindFirstObjectByType<Companies>();
        if (all != null && data.entries.SelectMany(item => item.companyIDs).Any(id => all.getCompany(id) == null)) throw new InvalidDataException("뉴스 회사 참조가 올바르지 않습니다.");
        preparedNews = data;
    }
    public void applyLoad()
    {
        entries.Clear();
        foreach (var entry in preparedNews.entries)
        {
            entry.description = describeNews(entry.title, entry.description);
            if (entry.title == FormerMarketHeadline) entry.title = MarketBriefingHeadline;
            entries.Add(entry);
        }
        preparedNews = null;
    }
    public void rebuildAfterLoad() => NewsChanged?.Invoke();
    public bool TryCreateLegacySection(int version, out SaveSectionData section)
    {
        section = new SaveSectionData { id = saveId, version = saveVersion, json = JsonUtility.ToJson(new SavedNews()) };
        return version == 2 || version == 3;
    }
}
