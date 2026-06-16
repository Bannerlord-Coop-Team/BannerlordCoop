using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.Caravans.Patches;

[HarmonyPatch(typeof(CaravanPartyComponent))]
internal class DisableCreateCaravanParty
{
    [HarmonyPatch(nameof(CaravanPartyComponent.CreateCaravanParty))]
    static bool Prefix() => false;
}
