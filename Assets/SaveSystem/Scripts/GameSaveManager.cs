using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TakeOver.SaveSystem
{
    /// <summary>등록된 파트의 저장·복원 조정. 날짜 진행과 게임 규칙은 호출자 담당</summary>
    public class GameSaveManager : MonoBehaviour
    {
        [Tooltip("ISaveParticipant 구현 컴포넌트. 복원 후 재구성 순서대로 등록")]
        [SerializeField] private MonoBehaviour[] participants = new MonoBehaviour[0];
        private SaveFileData pendingSave;

        private void Awake()
        {
            FixedDaySeed.GenerateSeed();
        }

        private SaveFileStorage Storage
        {
            get { return new SaveFileStorage(Application.persistentDataPath); }
        }

        public bool IsBusy { get; private set; }
        public bool HasPendingSave
        {
            get { return pendingSave != null; }
        }
        public string LastError { get; private set; } = "";
        public string SavePath
        {
            get { return Storage.SavePath; }
        }

        public bool HasSave()
        {
            return Storage.Exists();
        }

        /// <summary>게임 상태 복원 없이 저장된 날짜와 시드 읽기</summary>
        public bool TryReadDayStart(out DayStartData dayStart)
        {
            dayStart = default;
            LastError = "";
            try
            {
                SaveFileData data = JsonUtility.FromJson<SaveFileData>(Storage.Read());
                if (data == null || data.formatVersion != SaveFileData.CurrentFormatVersion
                    || data.dayStart.day < 1)
                    throw new InvalidDataException("저장 파일의 형식 버전·날짜가 올바르지 않습니다.");
                dayStart = data.dayStart;
                return true;
            }
            catch (Exception error)
            {
                return Fail(error.Message);
            }
        }

        /// <summary>하루 마감 완료 후, 다음 날짜 시작 전에 호출. 게임 종료 시 호출 제외</summary>
        public bool TrySaveDayEnd(DayStartData nextDay)
        {
            if (IsBusy || HasPendingSave)
            {
                return Fail("처리 중이거나 재시도할 저장이 남아 있습니다.");
            }
            IsBusy = true;
            LastError = "";
            try
            {
                ISaveParticipant[] modules = GetParticipants();
                SaveFileData data = new SaveFileData
                {
                    formatVersion = SaveFileData.CurrentFormatVersion,
                    dayStart = nextDay,
                    sections = new List<SaveSectionData>()
                };
                foreach (ISaveParticipant module in modules)
                {
                    SaveSectionData section = new SaveSectionData
                    {
                        id = module.SaveId,
                        version = module.SaveVersion,
                        json = module.CaptureJson()
                    };
                    data.sections.Add(section);
                }
                ValidateFile(data, modules);
                pendingSave = data;
                WritePending();
                return true;
            }
            catch (Exception error)
            {
                return Fail(error.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>실패 당시 저장 사본만 재기록. 하루 마감 재실행 제외</summary>
        public bool TryRetrySave(out DayStartData dayStart)
        {
            dayStart = new DayStartData();
            if (IsBusy || !HasPendingSave)
            {
                return Fail("재시도할 저장이 없거나 처리 중입니다.");
            }
            IsBusy = true;
            LastError = "";
            try
            {
                DayStartData context = pendingSave.dayStart;
                WritePending();
                dayStart = context;
                return true;
            }
            catch (Exception error)
            {
                return Fail(error.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>각 파트와 저장 시드 복원 후 날짜 정보 반환</summary>
        public bool TryLoad(out DayStartData dayStart)
        {
            dayStart = new DayStartData();
            if (IsBusy || HasPendingSave)
            {
                return Fail("처리 중이거나 아직 기록하지 못한 마감 상태가 있습니다.");
            }
            IsBusy = true;
            LastError = "";
            try
            {
                ISaveParticipant[] modules = GetParticipants();
                string json = Storage.Read();
                SaveFileData data = JsonUtility.FromJson<SaveFileData>(json);
                ValidateFile(data, modules);

                // 모든 파트의 검증 완료 후 실제 상태 적용
                foreach (ISaveParticipant module in modules)
                {
                    foreach (SaveSectionData section in data.sections)
                    {
                        if (section.id == module.SaveId)
                        {
                            module.PrepareLoad(section.json, section.version, data.dayStart);
                            break;
                        }
                    }
                }
                foreach (ISaveParticipant module in modules)
                {
                    module.ApplyLoad();
                }
                foreach (ISaveParticipant module in modules)
                {
                    module.RebuildAfterLoad();
                }
                FixedDaySeed.ChangeSeed(data.dayStart.seed);
                dayStart = data.dayStart;
                return true;
            }
            catch (Exception error)
            {
                return Fail(error.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private ISaveParticipant[] GetParticipants()
        {
            if (participants.Length == 0)
            {
                throw new InvalidOperationException("저장할 파트를 등록해야 합니다.");
            }
            ISaveParticipant[] modules = new ISaveParticipant[participants.Length];
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < participants.Length; i++)
            {
                ISaveParticipant module = participants[i] as ISaveParticipant;
                if (module == null)
                {
                    throw new InvalidOperationException("저장 파트의 계약·ID·버전을 확인하세요.");
                }
                if (string.IsNullOrWhiteSpace(module.SaveId) || module.SaveVersion < 1)
                {
                    throw new InvalidOperationException("저장 파트의 계약·ID·버전을 확인하세요.");
                }
                if (ids.Contains(module.SaveId))
                {
                    throw new InvalidOperationException("저장 파트의 계약·ID·버전을 확인하세요.");
                }
                ids.Add(module.SaveId);
                modules[i] = module;
            }
            return modules;
        }

        private void ValidateFile(SaveFileData data, ISaveParticipant[] modules)
        {
            if (data == null)
            {
                throw new InvalidDataException("저장 파일의 형식 버전·날짜·등록 파트가 일치하지 않습니다.");
            }
            if (data.formatVersion != SaveFileData.CurrentFormatVersion)
            {
                throw new InvalidDataException("저장 파일의 형식 버전·날짜·등록 파트가 일치하지 않습니다.");
            }
            if (data.dayStart.day < 1 || data.sections == null || data.sections.Count != modules.Length)
            {
                throw new InvalidDataException("저장 파일의 형식 버전·날짜·등록 파트가 일치하지 않습니다.");
            }

            HashSet<string> registeredIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ISaveParticipant module in modules)
            {
                registeredIds.Add(module.SaveId);
            }

            HashSet<string> savedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (SaveSectionData section in data.sections)
            {
                if (section == null)
                {
                    throw new InvalidDataException("저장 구역이 누락되거나 중복됐습니다.");
                }
                if (string.IsNullOrWhiteSpace(section.id) || section.version < 1 || string.IsNullOrWhiteSpace(section.json))
                {
                    throw new InvalidDataException("저장 구역이 누락되거나 중복됐습니다.");
                }
                if (savedIds.Contains(section.id) || !registeredIds.Contains(section.id))
                {
                    throw new InvalidDataException("저장 구역이 누락되거나 중복됐습니다.");
                }
                savedIds.Add(section.id);
            }
        }

        private void WritePending()
        {
            Storage.Write(JsonUtility.ToJson(pendingSave));
            FixedDaySeed.ChangeSeed(pendingSave.dayStart.seed);
            pendingSave = null;
        }

        private bool Fail(string message)
        {
            LastError = message;
            return false;
        }

        [ContextMenu("테스트 저장")]
        private void TestSave()
        {
            TakeOver.NPC.GameTurnClock clock =
                FindFirstObjectByType<TakeOver.NPC.GameTurnClock>();

            DayStartData data =
                new DayStartData(clock.CurrentTurn, FixedDaySeed.Seed);

            if (TrySaveDayEnd(data))
            {
                Debug.Log("저장 완료: " + SavePath);
            }
            else
            {
                Debug.LogError("저장 실패: " + LastError);
            }
        }
    }
    
}
