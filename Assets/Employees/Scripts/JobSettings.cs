using System;
using UnityEngine;

namespace TakeOver.Employees
{
    [Serializable]
    public struct JobStatGain
    {
        public JobType job;
        public AbilityStatType stat;
        // true면 보조 스텟(minorStatGain), false면 크게 영향을 주는 스텟(majorStatGain)
        public bool minor;
    }

    // 직무 업무 틀의 임시 값 모음. 전부 확정 전 값이다.
    [CreateAssetMenu(fileName = "JobSettings", menuName = "Employees/Job Settings")]
    public sealed class JobSettings : ScriptableObject
    {
        [Min(1)] public int baseDurationDays = 7;
        [Min(1)] public int durationAtMaxDiligence = 4;
        public float majorStatGain = 1f;
        public float minorStatGain = 0.5f;
        public JobStatGain[] statGains =
        {
            new JobStatGain { job = JobType.Appraisal, stat = AbilityStatType.Analysis },
            new JobStatGain { job = JobType.Appraisal, stat = AbilityStatType.Legwork },
            new JobStatGain { job = JobType.Discovery, stat = AbilityStatType.Eye },
            new JobStatGain { job = JobType.Discovery, stat = AbilityStatType.Legwork },
            new JobStatGain { job = JobType.Discovery, stat = AbilityStatType.Analysis, minor = true }
        };

        // 성실도 0이면 baseDurationDays, 성실도 20이면 durationAtMaxDiligence, 사이는 직선(0.5는 올림).
        public int GetDuration(int diligence)
        {
            float days = Mathf.Lerp(baseDurationDays, durationAtMaxDiligence, Mathf.Clamp01(diligence / 20f));
            return Mathf.Max(1, (int)Math.Round(days, MidpointRounding.AwayFromZero));
        }

        public float GetGainAmount(JobStatGain gain) => gain.minor ? minorStatGain : majorStatGain;
    }
}
