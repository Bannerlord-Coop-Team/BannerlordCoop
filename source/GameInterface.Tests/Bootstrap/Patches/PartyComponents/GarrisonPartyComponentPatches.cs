using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Tests.Bootstrap.Patches.PartyComponents;

[HarmonyPatch(typeof(GarrisonPartyComponent.InitializationArgs))]
internal class GarrisonPartyComponentPatches
{
    [HarmonyPatch(nameof(GarrisonPartyComponent.InitializationArgs.InitializeGarrisonPartyProperties))]
    private static bool Prefix() => false;
}
