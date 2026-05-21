using Common.Messaging;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(DefaultEncounterModel))]
internal class DefaultEncounterModelPatches
{
    [HarmonyPatch(nameof(DefaultEncounterModel.CreateMapEventComponentForEncounter))]
    [HarmonyPostfix]
    private static void Postfix(PartyBase attackerParty, PartyBase defenderParty, MapEventComponent __result)
    {
        if (__result == null) return;

        if (attackerParty.IsMobile && attackerParty.MobileParty.IsPlayerParty())
        {
            var message = new PlayerEncounterStarted(attackerParty.MobileParty, __result.MapEvent);
            MessageBroker.Instance.Publish(attackerParty.MobileParty, message);
        }

        if (defenderParty.IsMobile && defenderParty.MobileParty.IsPlayerParty())
        {
            var message = new PlayerEncounterStarted(defenderParty.MobileParty, __result.MapEvent);
            MessageBroker.Instance.Publish(defenderParty.MobileParty, message);
        }
    }
}
