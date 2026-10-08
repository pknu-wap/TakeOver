using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TakeOver.NPC
{
    /// <summary>NPC 상태와 턴을 검증한 뒤 저장·복원하는 경계다.</summary>
    public sealed class NpcSaveService : MonoBehaviour
    {
        [Serializable]
        private sealed class SaveData
        {
            // 필수 항목 누락을 정상 초기 상태로 오인하지 않도록 기본값을 두지 않는다.
            public int currentTurn;
            public List<NpcRuntimeState> npcs;
        }

        [SerializeField] private NpcStateRegistry registry;
        [SerializeField] private GameTurnClock turnClock;
        [SerializeField] private string fileName = "takeover-npc-state.json";
        public string SavePath => Path.Combine(Application.persistentDataPath, fileName);
        public string LastError { get; private set; } = "";

        public void Configure(NpcStateRegistry stateRegistry, GameTurnClock clock)
        {
            registry = stateRegistry;
            turnClock = clock;
        }

        private void Awake()
        {
            if (registry == null) registry = GetComponent<NpcStateRegistry>();
            if (turnClock == null) turnClock = GetComponent<GameTurnClock>();
        }

        /// <summary>기존 호출 계약은 실패 시 예외를 유지하고 UI에서는 TrySave를 사용한다.</summary>
        public void Save()
        {
            if (!TrySave()) throw new InvalidOperationException(LastError);
        }

        /// <summary>임시 파일을 완성한 뒤 교체해 쓰기 실패 시 기존 저장 파일을 보존한다.</summary>
        public bool TrySave()
        {
            LastError = "";
            try
            {
                if (registry == null) throw new InvalidOperationException("NPC 레지스트리가 연결되지 않았습니다.");
                var data = new SaveData
                {
                    currentTurn = turnClock == null ? 1 : turnClock.CurrentTurn,
                    npcs = registry.CreateSnapshot()
                };
                Validate(data);
                var path = SavePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var temporaryPath = path + ".tmp";
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Replace(temporaryPath, path, null);
                else File.Move(temporaryPath, path);
                return true;
            }
            catch (Exception error) when (IsStorageError(error))
            {
                LastError = $"NPC 저장 실패: {error.Message}";
                return false;
            }
        }

        /// <summary>파싱과 전체 검증이 끝난 후에만 현재 NPC 상태와 턴을 교체한다.</summary>
        public bool Load()
        {
            LastError = "";
            SaveData data;
            try
            {
                if (registry == null) throw new InvalidOperationException("NPC 레지스트리가 연결되지 않았습니다.");
                if (!File.Exists(SavePath))
                {
                    LastError = "저장 파일이 없습니다.";
                    return false;
                }
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                Validate(data);
            }
            catch (Exception error) when (IsStorageError(error))
            {
                LastError = $"NPC 불러오기 실패: {error.Message}";
                return false;
            }
            // RestoreTurn은 알림을 보내지 않는다. NPC 교체 알림에서 복원된 턴을 읽도록 먼저 맞춘다.
            if (turnClock != null) turnClock.RestoreTurn(data.currentTurn);
            registry.ReplaceAll(data.npcs);
            return true;
        }

        private static bool IsStorageError(Exception error) => error is IOException
            || error is UnauthorizedAccessException || error is ArgumentException
            || error is InvalidOperationException || error is NotSupportedException
            || error is System.Security.SecurityException;

        /// <summary>기획 수치를 새로 제한하지 않고 식별자·목록·수치의 기본 무결성을 검사한다.</summary>
        private static void Validate(SaveData data)
        {
            if (data == null || data.currentTurn < 1 || data.npcs == null)
                throw new InvalidDataException("저장 파일의 턴 또는 NPC 목록이 올바르지 않습니다.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var state in data.npcs)
            {
                if (state == null || string.IsNullOrWhiteSpace(state.npcId) || !ids.Add(state.npcId)
                    || state.memories == null || state.relation.lastUpdatedTurn < 0
                    || state.relation.lastUpdatedTurn > data.currentTurn || !IsFinite(state.relation))
                    throw new InvalidDataException("NPC 식별자 또는 상태가 올바르지 않습니다.");
                var memoryIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var memory in state.memories)
                {
                    if (memory == null || string.IsNullOrWhiteSpace(memory.memoryId) || !memoryIds.Add(memory.memoryId)
                        || memory.targetId != state.npcId || memory.turn < 1 || memory.turn > data.currentTurn
                        || !Enum.IsDefined(typeof(NpcMemoryType), memory.memoryType)
                        || !Enum.IsDefined(typeof(NpcPublicity), memory.publicity)
                        || !Enum.IsDefined(typeof(NpcActionDisposition), memory.actionDisposition)
                        || memory.strength < 1 || memory.strength > 5
                        || memory.durationTurns < 1 || memory.durationTurns > 20
                        || memory.remainingTurns < 0 || memory.remainingTurns > memory.durationTurns
                        || !IsUnitValue(memory.influence) || !IsUnitValue(memory.reliability)
                        || !IsFinite(memory.relationDelta))
                        throw new InvalidDataException("NPC 기억 데이터가 올바르지 않습니다.");
                }
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsUnitValue(float value) => IsFinite(value) && value >= 0f && value <= 1f;
        private static bool IsFinite(NpcRelationState value) => IsFinite(value.trust) && IsFinite(value.respect)
            && IsFinite(value.fear) && IsFinite(value.hostility) && IsFinite(value.dependency) && IsFinite(value.interest);
        private static bool IsFinite(NpcRelationDelta value) => IsFinite(value.trust) && IsFinite(value.respect)
            && IsFinite(value.fear) && IsFinite(value.hostility) && IsFinite(value.dependency) && IsFinite(value.interest);
    }
}
