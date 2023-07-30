using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Control;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches
{
    /// <summary>
    /// Publishes an event when a new main party is set.
    /// Used to request control of the party.
    /// </summary>
    [HarmonyPatch(typeof(Campaign), nameof(Campaign.MainParty), MethodType.Setter)]
    internal class SetMainPartyPatch
    {
        private static bool Prefix(ref Campaign __instance, ref MobileParty value)
        {
            if (value?.StringId == null) return true;
            if (Campaign.Current?.MainParty == null) return true;

            MessageBroker.Instance.Publish(__instance, new MainPartyChanged(value.StringId));
            return true;
        }
    }
}
