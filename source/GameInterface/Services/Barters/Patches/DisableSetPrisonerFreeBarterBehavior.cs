using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;

namespace GameInterface.Services.Barters.Patches;

[HarmonyPatch(typeof(SetPrisonerFreeBarterBehavior))]
internal class DisableSetPrisonerFreeBarterBehavior
{
    [HarmonyPatch(nameof(SetPrisonerFreeBarterBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
