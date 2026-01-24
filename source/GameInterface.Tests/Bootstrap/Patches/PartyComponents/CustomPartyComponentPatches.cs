using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Tests.Bootstrap.Patches.PartyComponents;

[HarmonyPatch(typeof(CaravanPartyComponent.InitializationArgs))]
internal class CaravanPartyComponentPatches
{
    [HarmonyPatch(nameof(CaravanPartyComponent.InitializationArgs.InitializeCaravanOnCreation))]
    private static bool Prefix() => false;
}
