using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TakeOver.SaveSystem;
using UnityEngine;

namespace TakeOver.PlayerMemories
{
    [DisallowMultipleComponent]
    public sealed class PlayerMemoryService : MonoBehaviour, IPlayerMemoryReader, ISaveParticipant, ISaveRebuildOrder, ILegacySaveSectionInitializer
    {
        [Serializable] private sealed class SaveData { public List<PlayerMemoryRecord> memories = new List<PlayerMemoryRecord>(); }
        private readonly List<PlayerMemoryRecord> records = new List<PlayerMemoryRecord>();
        private List<PlayerMemoryRecord> prepared;
        public string saveId => "player-memories";
        public int saveVersion => 1;
        public int rebuildOrder => 90;
        public event Action<PlayerMemoryRecord> MemoryAdded;
        public event Action<IReadOnlyList<PlayerMemoryRecord>> MemoriesRestored;

        public bool HasMemory(string key, string companyId = null) => !string.IsNullOrWhiteSpace(key) &&
            records.Any(record => record.Key == key && (companyId == null || record.CompanyId == companyId));
        public IReadOnlyList<PlayerMemoryRecord> GetMemories() => Array.AsReadOnly(records.ToArray());

        public void ValidateGrants(IReadOnlyList<PlayerMemoryGrant> grants, PlayerMemoryOrigin origin)
        {
            if (grants == null || origin == null) throw new ArgumentException("플레이어 기억의 출처가 없습니다.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var grant in grants)
            {
                if (grant == null || grant.tags == null) throw new ArgumentException("플레이어 기억 설정이 누락됐습니다.");
                var record = new PlayerMemoryRecord(grant, origin);
                ValidateRecord(record, int.MaxValue);
                if (!ids.Add(record.MemoryId)) throw new ArgumentException("선택 결과의 플레이어 기억이 중복됐습니다.");
            }
        }
        public void Record(IReadOnlyList<PlayerMemoryGrant> grants, PlayerMemoryOrigin origin)
        {
            NotifyAdded(Commit(grants, origin));
        }
        internal IReadOnlyList<PlayerMemoryRecord> Commit(IReadOnlyList<PlayerMemoryGrant> grants, PlayerMemoryOrigin origin)
        {
            ValidateGrants(grants, origin);
            var added = new List<PlayerMemoryRecord>();
            foreach (var grant in grants)
            {
                var record = new PlayerMemoryRecord(grant, origin);
                if (records.Any(existing => existing.MemoryId == record.MemoryId)) continue;
                records.Add(record);
                added.Add(record);
            }
            return added.AsReadOnly();
        }
        internal void NotifyAdded(IReadOnlyList<PlayerMemoryRecord> added)
        { foreach (var record in added) Notify(MemoryAdded, record); }
        private static void ValidateRecord(PlayerMemoryRecord record, int latestTurn)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.Key) || record.Key.Contains(":") || record.Key.Any(char.IsWhiteSpace) ||
                record.OwnerId != "player" || record.CompanyId == null || record.CompanyId.Contains(":") || record.CompanyId.Any(char.IsWhiteSpace) ||
                record.MemoryId != PlayerMemoryRecord.MakeId(record.Key, record.CompanyId) ||
                string.IsNullOrWhiteSpace(record.Title) || string.IsNullOrWhiteSpace(record.Description) ||
                !record.Permanent || !Enum.IsDefined(typeof(PlayerMemoryKind), record.Kind) ||
                !Enum.IsDefined(typeof(PlayerMemorySourceKind), record.SourceKind) ||
                record.CreatedTurn < 1 || record.CreatedTurn > latestTurn || record.SourceChoiceIndex < -1 ||
                record.SourceKind == PlayerMemorySourceKind.EventChoice && record.SourceChoiceIndex < 0 ||
                string.IsNullOrWhiteSpace(record.SourceId) || string.IsNullOrWhiteSpace(record.SourceOccurrenceId) ||
                record.Tags == null || record.Tags.Any(string.IsNullOrWhiteSpace) || record.Tags.Distinct(StringComparer.Ordinal).Count() != record.Tags.Count)
                throw new InvalidDataException("플레이어 기억의 영구 여부·출처·내용이 올바르지 않습니다.");
        }
        // 일지는 구독자다. 구독 실패 때문에 확정된 게임 선택이나 다른 구독자의 처리를 중단하지 않는다.
        private static void Notify<T>(Action<T> notification, T value)
        {
            if (notification == null) return;
            foreach (Action<T> listener in notification.GetInvocationList())
                try { listener(value); }
                catch (Exception error) { Debug.LogWarning("플레이어 기억 구독 처리 실패: " + error.Message); }
        }
        public string captureJson() => JsonUtility.ToJson(new SaveData { memories = new List<PlayerMemoryRecord>(records) });
        public void prepareLoad(string json, int savedVersion, DayStartData dayStart)
        {
            prepared = null;
            if (savedVersion != saveVersion || string.IsNullOrWhiteSpace(json) || dayStart.day < 1) throw new InvalidDataException("지원하지 않는 플레이어 기억 저장 형식입니다.");
            var raw = JObject.Parse(json);
            if (raw["memories"]?.Type != JTokenType.Array) throw new InvalidDataException("플레이어 기억 목록이 누락됐습니다.");
            foreach (var item in raw["memories"])
            {
                if (item.Type != JTokenType.Object) throw new InvalidDataException("플레이어 기억 기록이 올바르지 않습니다.");
                foreach (string name in new[] { "memoryId", "key", "title", "description", "ownerId", "companyId", "sourceId", "sourceOccurrenceId" })
                    if (item[name]?.Type != JTokenType.String) throw new InvalidDataException("플레이어 기억의 문자열 항목이 누락됐습니다.");
                foreach (string name in new[] { "kind", "createdTurn", "sourceChoiceIndex", "sourceKind" })
                    if (item[name]?.Type != JTokenType.Integer) throw new InvalidDataException("플레이어 기억의 출처 수치가 누락됐습니다.");
                if (item["permanent"]?.Type != JTokenType.Boolean || item["tags"]?.Type != JTokenType.Array || item["tags"].Any(tag => tag.Type != JTokenType.String))
                    throw new InvalidDataException("플레이어 기억의 영구 여부 또는 태그가 누락됐습니다.");
            }
            var data = JsonUtility.FromJson<SaveData>(json);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in data.memories)
            {
                ValidateRecord(record, dayStart.day);
                if (!ids.Add(record.MemoryId)) throw new InvalidDataException("플레이어 기억 저장에 중복 ID가 있습니다.");
            }
            prepared = data.memories;
        }
        public void applyLoad()
        {
            if (prepared == null) throw new InvalidOperationException("플레이어 기억 prepareLoad를 먼저 완료해 주십시오.");
            records.Clear(); records.AddRange(prepared); prepared = null;
        }
        public void rebuildAfterLoad() => Notify(MemoriesRestored, GetMemories());
        public bool TryCreateLegacySection(int sourceFormatVersion, out SaveSectionData section)
        {
            section = sourceFormatVersion == 2 ? new SaveSectionData { id = saveId, version = saveVersion, json = "{\"memories\":[]}" } : null;
            return section != null;
        }
    }
}
