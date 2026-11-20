using System;
using System.Collections.Generic;
using UnityEngine;

namespace TakeOver.Startups
{
    public static class StartupDiscovery
    {
        public enum Outlook { Low, Medium, High }

        public static int getCandidateCount(int legwork, StartupDiscoverySettings settings)
        {
            float t = Mathf.Clamp01(legwork / (float)settings.statMax);
            float count = Mathf.Lerp(settings.candidatesAtZero, settings.candidatesAtMax, t);
            return (int)Math.Round(count, MidpointRounding.AwayFromZero);
        }

        public static List<int> pickIndices(IReadOnlyList<float> growthRates, int count, int eye,
            StartupDiscoverySettings settings, System.Random random)
        {
            int n = growthRates.Count;
            List<int> picked = new List<int>();
            if (n == 0 || count <= 0) return picked;

            List<int> order = new List<int>();
            for (int i = 0; i < n; i++) order.Add(i);
            order.Sort((a, b) => growthRates[a].CompareTo(growthRates[b]));
            float[] score = new float[n];
            for (int rank = 0; rank < n; rank++)
                score[order[rank]] = n > 1 ? rank / (float)(n - 1) : 0.5f;

            float strength = settings.eyeBias * Mathf.Clamp01(eye / (float)settings.statMax);
            double[] weight = new double[n];
            for (int i = 0; i < n; i++) weight[i] = Math.Exp(strength * score[i]);

            List<int> left = new List<int>(order);
            while (picked.Count < count && left.Count > 0)
            {
                double total = 0;
                foreach (int i in left) total += weight[i];
                double roll = random.NextDouble() * total;

                int chosen = left[left.Count - 1];
                foreach (int i in left)
                {
                    roll -= weight[i];
                    if (roll <= 0) { chosen = i; break; }
                }
                picked.Add(chosen);
                left.Remove(chosen);
            }
            return picked;
        }

        public static Outlook getTrueOutlook(float growthRate, StartupDiscoverySettings settings)
        {
            if (growthRate >= settings.highOutlookGrowth) return Outlook.High;
            if (growthRate < settings.lowOutlookGrowth) return Outlook.Low;
            return Outlook.Medium;
        }

        public static float getAccuracy(int analysis, StartupDiscoverySettings settings)
        {
            float t = Mathf.Clamp01(analysis / (float)settings.statMax);
            return Mathf.Lerp(settings.accuracyAtZero, settings.accuracyAtMax, t);
        }

        public static Outlook readOutlook(Outlook truth, int analysis, StartupDiscoverySettings settings,
            System.Random random)
        {
            if (random.NextDouble() < getAccuracy(analysis, settings)) return truth;
            if (truth == Outlook.Medium) return random.Next(2) == 0 ? Outlook.High : Outlook.Low;
            return Outlook.Medium;
        }

        public static string describe(Outlook outlook) =>
            outlook == Outlook.High ? "상" : outlook == Outlook.Medium ? "중" : "하";
    }
}
