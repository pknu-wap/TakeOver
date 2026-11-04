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
            FixedDaySeed.generateSeed();
        }

        private SaveFileStorage storage
        {
            get { return new SaveFileStorage(Application.persistentDataPath); }
        }

        public bool isBusy { get; private set; }
        public bool hasPendingSave
        {
            get { return pendingSave != null; }
        }
        public string lastError { get; private set; } = "";
        public string savePath
        {
            get { return storage.savePath; }
        }

        public bool hasSave()
        {
            return storage.exists();
        }

        /// <summary>게임 상태 복원 없이 저장된 날짜와 시드 읽기</summary>
        public bool tryReadDayStart(out DayStartData dayStart)
        {
            dayStart = default;
            lastError = "";
            try
            {
                SaveFileData data = JsonUtility.FromJson<SaveFileData>(storage.read());
                if (data == null || data.formatVersion != SaveFileData.currentFormatVersion
                    || data.dayStart.day < 1)
                    throw new InvalidDataException("저장 파일의 형식 버전·날짜가 올바르지 않습니다.");
                dayStart = data.dayStart;
                return true;
            }
            catch (Exception error)
            {
                return fail(error.Message);
            }
        }

        /// <summary>하루 마감 완료 후, 다음 날짜 시작 전에 호출. 게임 종료 시 호출 제외</summary>
        public bool trySaveDayEnd(DayStartData nextDay)
        {
            if (isBusy || hasPendingSave)
            {
                return fail("처리 중이거나 재시도할 저장이 남아 있습니다.");
            }
            isBusy = true;
            lastError = "";
            try
            {
                ISaveParticipant[] modules = getParticipants();
                SaveFileData data = new SaveFileData
                {
                    formatVersion = SaveFileData.currentFormatVersion,
                    dayStart = nextDay,
                    sections = new List<SaveSectionData>()
                };
                foreach (ISaveParticipant module in modules)
                {
                    SaveSectionData section = new SaveSectionData
                    {
                        id = module.saveId,
                        version = module.saveVersion,
                        json = module.captureJson()
                    };
                    data.sections.Add(section);
                }
                validateFile(data, modules);
                pendingSave = data;
                writePending();
                return true;
            }
            catch (Exception error)
            {
                return fail(error.Message);
            }
            finally
            {
                isBusy = false;
            }
        }

        /// <summary>실패 당시 저장 사본만 재기록. 하루 마감 재실행 제외</summary>
        public bool tryRetrySave(out DayStartData dayStart)
        {
            dayStart = new DayStartData();
            if (isBusy || !hasPendingSave)
            {
                return fail("재시도할 저장이 없거나 처리 중입니다.");
            }
            isBusy = true;
            lastError = "";
            try
            {
                DayStartData context = pendingSave.dayStart;
                writePending();
                dayStart = context;
                return true;
            }
            catch (Exception error)
            {
                return fail(error.Message);
            }
            finally
            {
                isBusy = false;
            }
        }

        /// <summary>각 파트와 저장 시드 복원 후 날짜 정보 반환</summary>
        public bool tryLoad(out DayStartData dayStart)
        {
            dayStart = new DayStartData();
            if (isBusy || hasPendingSave)
            {
                return fail("처리 중이거나 아직 기록하지 못한 마감 상태가 있습니다.");
            }
            isBusy = true;
            lastError = "";
            try
            {
                ISaveParticipant[] modules = getParticipants();
                string json = storage.read();
                SaveFileData data = JsonUtility.FromJson<SaveFileData>(json);
                validateFile(data, modules);

                // 모든 파트의 검증 완료 후 실제 상태 적용
                foreach (ISaveParticipant module in modules)
                {
                    foreach (SaveSectionData section in data.sections)
                    {
                        if (section.id == module.saveId)
                        {
                            module.prepareLoad(section.json, section.version, data.dayStart);
                            break;
                        }
                    }
                }
                foreach (ISaveParticipant module in modules)
                {
                    module.applyLoad();
                }
                foreach (ISaveParticipant module in modules)
                {
                    module.rebuildAfterLoad();
                }
                FixedDaySeed.changeSeed(data.dayStart.seed);
                dayStart = data.dayStart;
                return true;
            }
            catch (Exception error)
            {
                return fail(error.Message);
            }
            finally
            {
                isBusy = false;
            }
        }

        private ISaveParticipant[] getParticipants()
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
                if (string.IsNullOrWhiteSpace(module.saveId) || module.saveVersion < 1)
                {
                    throw new InvalidOperationException("저장 파트의 계약·ID·버전을 확인하세요.");
                }
                if (ids.Contains(module.saveId))
                {
                    throw new InvalidOperationException("저장 파트의 계약·ID·버전을 확인하세요.");
                }
                ids.Add(module.saveId);
                modules[i] = module;
            }
            return modules;
        }

        private void validateFile(SaveFileData data, ISaveParticipant[] modules)
        {
            if (data == null)
            {
                throw new InvalidDataException("저장 파일의 형식 버전·날짜·등록 파트가 일치하지 않습니다.");
            }
            if (data.formatVersion != SaveFileData.currentFormatVersion)
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
                registeredIds.Add(module.saveId);
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

        private void writePending()
        {
            storage.write(JsonUtility.ToJson(pendingSave));
            FixedDaySeed.changeSeed(pendingSave.dayStart.seed);
            pendingSave = null;
        }

        private bool fail(string message)
        {
            lastError = message;
            return false;
        }

        [ContextMenu("테스트 저장")]
        private void testSave()
        {
            TakeOver.NPC.GameTurnClock clock =
                FindFirstObjectByType<TakeOver.NPC.GameTurnClock>();

            DayStartData data =
                new DayStartData(clock.CurrentTurn, FixedDaySeed.seed);

            if (trySaveDayEnd(data))
            {
                Debug.Log("저장 완료: " + savePath);
            }
            else
            {
                Debug.LogError("저장 실패: " + lastError);
            }
        }
    }
    
}
