using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.Clans.Patches.Disable;

[HarmonyPatch(typeof(ClanMemberRolesCampaignBehavior))]
internal class DisableClanMemberRolesCampaignBehavior
{
    [HarmonyPatch(nameof(ClanMemberRolesCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
