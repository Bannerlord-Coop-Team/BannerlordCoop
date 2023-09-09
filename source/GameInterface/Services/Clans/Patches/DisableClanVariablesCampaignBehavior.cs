using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(ClanVariablesCampaignBehavior))]
internal class DisableClanVariablesCampaignBehavior
{
    //[HarmonyPatch(nameof(ClanVariablesCampaignBehavior.RegisterEvents))]
    //static bool Prefix() => false;
}
