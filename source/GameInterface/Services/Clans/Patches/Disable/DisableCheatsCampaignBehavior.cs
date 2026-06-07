using HarmonyLib;
using SandBox.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Clans.Patches.Disable;

[HarmonyPatch(typeof(ClanMemberRolesCampaignBehavior))]
internal class DisableClanMemberRolesCampaignBehavior
{
    [HarmonyPatch(nameof(ClanMemberRolesCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
