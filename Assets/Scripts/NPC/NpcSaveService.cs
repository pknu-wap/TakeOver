using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TakeOver.NPC
{
    /// <summary>NPC 상태와 공통 턴을 저장·복원하는 경계다. 저장 파일은 Unity persistentDataPath에 둔다.</summary>
    public sealed class NpcSaveService : MonoBehaviour
    {
        [Serializable]
        private sealed class SaveData
        {
            public int currentTurn = 1;
            public List<NpcRuntimeState> npcs = new List<NpcRuntimeState>();
        }

        [SerializeField] private NpcStateRegistry registry;
        [SerializeField] private GameTurnClock turnClock;
        [SerializeField] private string fileName = "takeover-npc-state.json";

        /// <summary>현재 파일 이름을 Unity의 사용자별 영구 데이터 폴더에 결합한 경로다.</summary>
        public string SavePath => Path.Combine(Application.persistentDataPath, fileName);

        /// <summary>저장 대상 NPC 레지스트리와 함께 복원할 공용 턴 시계를 연결한다.</summary>
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

        /// <summary>현재 턴과 NPC 상태 사본을 JSON으로 저장한다. 레지스트리가 없으면 예외를 던진다.</summary>
        public void Save()
        {
            if (registry == null) throw new InvalidOperationException("NpcStateRegistry가 연결되지 않았습니다.");
            var data = new SaveData
            {
                currentTurn = turnClock == null ? 1 : turnClock.CurrentTurn,
                npcs = registry.CreateSnapshot()
            };
            File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
        }

        /// <summary>저장 파일을 읽어 상태와 턴을 복원한다. 파일이 없거나 JSON 결과가 비면 false를 반환한다.</summary>
        public bool Load()
        {
            if (registry == null) throw new InvalidOperationException("NpcStateRegistry가 연결되지 않았습니다.");
            if (!File.Exists(SavePath)) return false;
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            if (data == null) return false;
            registry.ReplaceAll(data.npcs ?? new List<NpcRuntimeState>());
            if (turnClock != null) turnClock.RestoreTurn(data.currentTurn);
            return true;
        }
    }
}
