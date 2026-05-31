using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.PartyComponents.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.Patches;

[HarmonyPatch(typeof(PartyComponent))]
internal class PartyComponentPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyComponentPatches>();

    [HarmonyPatch(nameof(PartyComponent.MobileParty), MethodType.Setter)]
    [HarmonyPrefix]
    private static void PrefixMobileParty(PartyComponent __instance, MobileParty value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
            return;
        
        if (ModInformation.IsClient)
        {
            Logger.Error("Client called managed PartyComponent.MobileParty setter");
            return;
        }

        var message = new PartyComponentMobilePartyUpdated(__instance, value);
        MessageBroker.Instance.Publish(__instance, message);
    }
}
