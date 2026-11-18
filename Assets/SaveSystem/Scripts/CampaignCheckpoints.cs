using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using TakeOver.Events;
using TakeOver.NPC;
using UnityEngine;

namespace TakeOver.SaveSystem
{
    public partial class GameSaveManager
    {
        private string checkpointLoadJson;
        private SaveFileStorage CheckpointStorage(string run, string mission)
        {
            if (run == null || !Regex.IsMatch(run, "^[a-f0-9]{32}$") || mission == null || !Regex.IsMatch(mission, "^[a-z0-9_-]{1,80}$")) throw new InvalidDataException("체크포인트 식별자가 올바르지 않습니다.");
            return new SaveFileStorage(Path.Combine(SaveDirectory, "checkpoints", run, mission));
        }
        private string ReadCheckpoint(string run, string mission)
        {
            string json = CheckpointStorage(run, mission).read();
            var data = JsonUtility.FromJson<SaveFileData>(json);
            if (data == null || data.formatVersion != SaveFileData.currentFormatVersion || data.sections == null) throw new InvalidDataException("준비 저장 형식이 다릅니다.");
            var events = data.sections.Single(s => s.id == "events");
            var payload = JsonUtility.FromJson<EventSystemController.EventSaveData>(events.json);
            var plan = EarlyCampaignPlan.Load().missions.Single(m => m.id == mission);
            if (events.version != 6 || payload.campaignRevision != 2 || payload.campaignRunId != run || payload.mandatoryState == MandatoryStoryState.Failed || payload.mandatoryState == MandatoryStoryState.Forced ||
                payload.day != (plan.startDay == 1 ? 2 : plan.startDay) || data.dayStart.day != payload.day || payload.campaignDayOffset != 0 || plan.eventIds.Any(id => payload.choices.Any(c => c.eventId == id)))
                throw new InvalidDataException("현재 미션의 준비 시작 저장이 아닙니다.");
            return json;
        }
        public bool HasCampaignCheckpoint(string run, string mission)
        {
            try
            {
                var data = JsonUtility.FromJson<SaveFileData>(ReadCheckpoint(run, mission));
                var modules = getParticipants(); validateFile(data, modules);
                foreach (var module in modules)
                {
                    var section = data.sections.Single(s => s.id == module.saveId);
                    module.prepareLoad(section.json, section.version, data.dayStart);
                }
                return true;
            }
            catch { return false; }
        }
        public bool TryWriteCampaignCheckpoint(EventSystemController events)
        {
            if (events == null || events.CampaignRevision != 2 || events.MandatoryState == MandatoryStoryState.Failed || events.MandatoryState == MandatoryStoryState.Forced || !events.IsClosed ||
                TakeOver.Story.StoryDirector.IsRunning || !events.TutorialRewardGranted || string.IsNullOrEmpty(events.ActiveMissionId)) return true;
            var plan = EarlyCampaignPlan.Load().missions.Single(m => m.id == events.ActiveMissionId);
            int expectedDay = plan.startDay == 1 ? 2 : plan.startDay;
            // The first checkpoint follows the day-1 tutorial and is intentionally on day 2.
            if (events.getCurrentDay() != expectedDay || HasCampaignCheckpoint(events.CampaignRunId, events.ActiveMissionId)) return true;
            try
            {
                var modules = getParticipants();
                var data = new SaveFileData { formatVersion = SaveFileData.currentFormatVersion, dayStart = new DayStartData(events.getCurrentDay(), FixedDaySeed.seed),
                    sections = modules.Select(m => new SaveSectionData { id = m.saveId, version = m.saveVersion, json = m.captureJson() }).ToList() };
                validateFile(data, modules);
                CheckpointStorage(events.CampaignRunId, plan.id).write(JsonUtility.ToJson(data)); return true;
            }
            catch (Exception error) { lastError = error.Message; return false; }
        }
        public bool TryLoadCampaignCheckpoint(string run, string mission, out DayStartData day)
        {
            day = default;
            try { checkpointLoadJson = ReadCheckpoint(run, mission); return tryLoad(out day); }
            catch (Exception error) { lastError = error.Message; return false; }
            finally { checkpointLoadJson = null; }
        }
    }
}
