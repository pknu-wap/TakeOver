using System;
using System.IO;
using UnityEngine;

namespace TakeOver.SaveSystem
{
    public partial class GameSaveManager
    {
        public const int ManualSlotCount = 3;
        private static SaveFileStorage ManualStorage(int slot)
        {
            if (slot < 1 || slot > ManualSlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            return new SaveFileStorage(Path.Combine(SaveDirectory, "manual", slot.ToString()));
        }
        public static bool HasManualSlot(int slot) => ManualStorage(slot).exists();
        public static bool HasAutomaticSlot() => new SaveFileStorage(SaveDirectory).exists();
        public static string DescribeAutomaticSlot() => DescribeSlot(new SaveFileStorage(SaveDirectory), "자동저장");
        public static string DescribeManualSlot(int slot)
            => DescribeSlot(ManualStorage(slot), "수동저장 " + slot);
        private static string DescribeSlot(SaveFileStorage file, string label)
        {
            if (!file.exists()) return label + " · 빈 저장";
            try
            {
                var data = JsonUtility.FromJson<SaveFileData>(file.read());
                if (data == null || !IsReadableFormat(data.formatVersion) || data.dayStart.day < 1)
                    throw new InvalidDataException();
                return $"{label} · {data.dayStart.day}일차\n{File.GetLastWriteTime(file.savePath):yyyy.MM.dd HH:mm}";
            }
            catch { return label + " · 읽을 수 없는 저장"; }
        }
        public bool TrySaveManualSlot(int slot, DayStartData day)
        {
            if (isBusy || hasPendingSave) return fail("진행 중인 저장을 먼저 마쳐 주세요.");
            isBusy = true; lastError = "";
            try { ManualStorage(slot).write(JsonUtility.ToJson(CaptureSnapshot(day))); return true; }
            catch (Exception error) { return fail(error.Message); }
            finally { isBusy = false; }
        }
        public bool TryLoadManualSlot(int slot, out DayStartData day)
        {
            day = default;
            try { checkpointLoadJson = ManualStorage(slot).read(); return tryLoad(out day); }
            catch (Exception error) { return fail(error.Message); }
            finally { checkpointLoadJson = null; }
        }
    }
}
