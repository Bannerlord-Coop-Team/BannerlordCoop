using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Control;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches
{
    [HarmonyPatch(typeof(Campaign), nameof(Campaign.MainParty), MethodType.Setter)]
    internal class SetMainPartyPatch
    {
        private static bool Prefix(ref Campaign __instance, ref MobileParty value)
        {
            MessageBroker.Instance.Publish(__instance, new MainPartyChanged(value.StringId));
            return true;
        }
    }
}
