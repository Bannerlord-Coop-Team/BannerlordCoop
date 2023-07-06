using Common.Messaging;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches
{
    /// <summary>
    /// Patches for when the local player enters or leaves a settlement.
    /// </summary>
    [HarmonyPatch(typeof(PlayerEncounter))]
    public class PlayerEncounterPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(PlayerEncounter.EnterSettlement))]
        static bool EnterSettlementPrefix()
        {
            var message = new SettlementEntered(MobileParty.MainParty.TargetSettlement.StringId, MobileParty.MainParty.StringId);
            MessageBroker.Instance.Publish(MobileParty.MainParty, message);

            return true;
        }
    }
}
