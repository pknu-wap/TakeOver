using System;
using UnityEngine;

namespace TakeOver.NPC
{
    public enum NpcMobAppearance { Male, Female }

    /// <summary>전용 원화를 우선하고, 서브 기업의 미등록 원화만 공용 모브로 채운다.</summary>
    public static class NpcStandingCatalog
    {
        public static bool CanUseMob(NpcStateRegistry.NpcSeed seed)
        {
            var company = seed?.companyId;
            if (string.IsNullOrWhiteSpace(company) || company == "external-network") return false;
            if (company.Equals("apex", StringComparison.OrdinalIgnoreCase)
                || company.StartsWith("apex-", StringComparison.OrdinalIgnoreCase)
                || (seed.profile?.affiliation?.IndexOf("APEX", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0) return false;
            switch (company)
            {
                case "polaris-finance": case "daeseong-industry": case "sinseong-development":
                case "blueharbor-retail": case "aurora-foodtech": return false;
                default: return true;
            }
        }

        public static Sprite GetMob(NpcMobAppearance appearance, bool dimmed = false)
            => Resources.Load<Sprite>("NPC/MobStanding/Mob_"
                + (appearance == NpcMobAppearance.Female ? "Female" : "Male")
                + (dimmed ? "_Dim" : "_Standing"));

        /// <summary>런타임 사본에만 적용한다. 전용 원화와 모브 암전 이미지를 섞지 않는다.</summary>
        public static void ApplyFallback(NpcStateRegistry.NpcSeed seed)
        {
            if (seed?.profile == null || seed.profile.standingIllustration != null
                || seed.profile.dimmedStandingIllustration != null || !CanUseMob(seed)) return;
            seed.profile.standingIllustration = GetMob(seed.profile.mobAppearance);
            if (seed.profile.dimmedStandingIllustration == null)
                seed.profile.dimmedStandingIllustration = GetMob(seed.profile.mobAppearance, true);
        }
    }
}
