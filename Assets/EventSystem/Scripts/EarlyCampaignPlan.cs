using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TakeOver.Events
{
    [Serializable] public sealed class CampaignStakeGoal { public string companyId; public float percent; }
    [Serializable] public sealed class CampaignStakeSnapshot { public string companyId; public float required, actual; }
    [Serializable] public sealed class CampaignMission
    {
        public string id;
        public int startDay, dueDay;
        public List<CampaignStakeGoal> goals = new List<CampaignStakeGoal>();
        public List<string> eventIds = new List<string>();
        public List<string> signals = new List<string>();
    }
    [Serializable] public sealed class CampaignEnding
    {
        public string signal, title, companyOutcome;
        public List<EventDialogueLine> lines = new List<EventDialogueLine>();
    }
    [Serializable] public sealed class EarlyCampaignPlan
    {
        public List<CampaignMission> missions = new List<CampaignMission>();
        public List<CampaignEnding> endings = new List<CampaignEnding>();
        public static EarlyCampaignPlan Load()
        {
            var asset = Resources.Load<TextAsset>("Story/EarlyCampaign");
            if (asset == null) throw new InvalidOperationException("초반 캠페인 계획이 없습니다.");
            var plan = JsonUtility.FromJson<EarlyCampaignPlan>(asset.text);
            if (plan?.missions == null || plan.missions.Count != 6 || plan.endings == null || plan.endings.Count != 14 ||
                plan.missions.Select(m => m.id).Distinct().Count() != 6 || plan.endings.Select(e => e.signal).Distinct().Count() != 14 ||
                plan.missions.Any(m => m.startDay < 1 || m.dueDay <= m.startDay || m.goals.Count < 1 || m.goals.Count > 2 ||
                    m.goals.Any(g => string.IsNullOrEmpty(g.companyId) || g.percent <= 0 || g.percent > 100) ||
                    m.signals.Count != (1 << m.goals.Count) - 1 || m.eventIds.Count < 1 || m.eventIds.Distinct().Count() != m.eventIds.Count) ||
                plan.endings.Any(e => e.lines == null || e.lines.Count == 0 || string.IsNullOrEmpty(e.companyOutcome)))
                throw new InvalidOperationException("초반 캠페인 목표·엔딩 구성을 확인하세요.");
            return plan;
        }
    }
}
