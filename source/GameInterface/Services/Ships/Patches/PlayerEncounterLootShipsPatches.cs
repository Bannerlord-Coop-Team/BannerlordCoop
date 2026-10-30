using Common;
using Common.Messaging;
using GameInterface.Services.Ships.Messages;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Ships.Patches;

[HarmonyPatch(typeof(PlayerEncounter))]
[HarmonyPatchCategory(GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class PlayerEncounterLootShipsPatches
{
    [HarmonyPatch(nameof(PlayerEncounter.OnPlayerLootShipEnd))]
    [HarmonyPrefix]
    public static bool OnPlayerLootShipEndPrefix(PlayerEncounter __instance)
    {
        if (ModInformation.IsServer) return true;

        var party = MobileParty.MainParty;
        MessageBroker.Instance.Publish(party, new LootShipsDiscardRequested(party, __instance.ReceivedLootShips.ToList()));

        return false;
    }
}
