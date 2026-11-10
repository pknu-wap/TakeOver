using System;
using System.IO;
using TakeOver.SaveSystem;
using UnityEngine;

namespace TakeOver.NPC
{
    public sealed partial class NpcSaveService
    {
        private SaveData preparedGameSave;
        public string saveId => "npc";
        public int saveVersion => 1;

        public string captureJson()
        {
            var data = new SaveData { currentTurn = turnClock.CurrentTurn, npcs = registry.CreateSnapshot() };
            Validate(data);
            return JsonUtility.ToJson(data);
        }

        public void prepareLoad(string json, int savedVersion, DayStartData dayStart)
        {
            preparedGameSave = null;
            if (savedVersion != saveVersion) throw new InvalidDataException("지원하지 않는 NPC 저장 버전입니다.");
            var data = JsonUtility.FromJson<SaveData>(json);
            Validate(data);
            if (data.currentTurn > dayStart.day || dayStart.day - data.currentTurn > 1)
                throw new InvalidDataException("NPC 저장 날짜가 다른 파트와 일치하지 않습니다.");
            foreach (var state in data.npcs)
                if (!registry.TryGetProfile(state.npcId, out var seed) || seed.companyId != state.companyId)
                    throw new InvalidDataException("NPC 저장 ID 또는 소속이 현재 정의와 일치하지 않습니다.");
            preparedGameSave = data;
        }

        public void applyLoad()
        {
            if (preparedGameSave == null) throw new InvalidOperationException("prepareLoad를 먼저 완료해야 합니다.");
            turnClock.RestoreTurn(preparedGameSave.currentTurn);
            registry.ReplaceAll(preparedGameSave.npcs, false);
            preparedGameSave = null;
        }

        public void rebuildAfterLoad() => registry.NotifyAllStatesChanged();
    }
}
