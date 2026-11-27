using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TakeOver.Events
{
    public sealed partial class EventSystemController
    {
        private List<CampaignMission> missions;
        private CampaignMission displayedMission;
        private float[] displayedStakes = Array.Empty<float>();
        private string missionDisplay = "";
        private string lastMissionDisplay;

        public bool HasActiveMission => ActiveMission != null;
        public string ActiveMissionId => ActiveMission?.id ?? "";
        public int MandatoryDaysRemaining
        {
            get
            {
                var mission = ActiveMission;
                return mission == null ? 0 : Mathf.Max(0, mission.dueDay + campaignDayOffset - currentDay);
            }
        }
        public string MissionRequirement
        {
            get
            {
                var mission = ActiveMission;
                if (mission == null)
                {
                    displayedMission = null;
                    missionDisplay = "";
                    return missionDisplay;
                }

                bool changed = mission != displayedMission;
                if (displayedStakes.Length != mission.goals.Count)
                    displayedStakes = new float[mission.goals.Count];
                for (int i = 0; i < mission.goals.Count; i++)
                {
                    float stake = Stake(mission.goals[i].companyId);
                    changed |= !stake.Equals(displayedStakes[i]);
                    displayedStakes[i] = stake;
                }
                if (!changed) return missionDisplay;

                displayedMission = mission;
                var display = new StringBuilder();
                for (int i = 0; i < mission.goals.Count; i++)
                {
                    var goal = mission.goals[i];
                    float stake = displayedStakes[i];
                    if (i > 0) display.Append('\n');
                    display.Append(worldReader?.getCompany(goal.companyId)?.companyName ?? goal.companyId)
                        .Append(' ').Append(StakeText.Format(goal.percent)).Append(" 이상  ")
                        .Append(stake + .0001f >= goal.percent ? "달성" : "미달")
                        .Append(" (현재 ").Append(StakeText.Format(stake)).Append(')');
                }
                missionDisplay = display.ToString();
                return missionDisplay;
            }
        }

        private void InitializeMissionList()
        {
            IEnumerable<CampaignMission> definitions = campaignRevision == 2 ? campaignPlan.missions : events
                .Where(e => e.getId() == MandatoryEventId || e.IsStory && e.MissionStartDay > 0)
                .Select(e => new CampaignMission
                {
                    id = e.getId(),
                    startDay = e.getId() == MandatoryEventId ? 1 : e.MissionStartDay,
                    dueDay = e.getId() == MandatoryEventId ? MandatoryFailureDay : e.CampaignDay,
                    goals = new List<CampaignStakeGoal> { new CampaignStakeGoal { companyId = MissionCompany(e), percent = 10 } },
                    eventIds = new List<string> { e.getId() },
                    signals = new List<string> { e.getId() == MandatoryEventId ? MandatoryEndingSignal : e.FailureSignal }
                });
            missions = definitions.OrderBy(m => m.dueDay).ToList();
            displayedMission = null;
        }

        private CampaignMission ActiveMission
        {
            get
            {
                foreach (var mission in missions)
                {
                    if (currentDay < mission.startDay + campaignDayOffset) continue;
                    foreach (string id in mission.eventIds)
                        if (!selectedChoices.ContainsKey(id)) return mission;
                }
                return null;
            }
        }

        private CampaignMission MissionFor(EventDefinition item) =>
            item == null ? null : missions.FirstOrDefault(m => m.eventIds.Contains(item.getId()));
        private bool IsMission(EventDefinition item) => MissionFor(item) != null;
        private float Stake(string id) => worldReader?.getLiveStake(id) ?? 0;

        private void Update()
        {
            if (mandatoryState == MandatoryStoryState.Failed || currentDay < 1) return;
            string display = MissionRequirement;
            if (display == lastMissionDisplay) return;
            lastMissionDisplay = display;
            refreshAvailability();
            MandatoryDeadlineChanged?.Invoke();
        }
    }
}
