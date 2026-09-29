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

        public string SavePath => Path.Combine(Application.persistentDataPath, fileName);

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
