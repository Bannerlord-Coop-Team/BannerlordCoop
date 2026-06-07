using Common.Logging;
using Common.Messaging;
using HarmonyLib;
using Helpers;
using Serilog;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using GameInterface.Policies;

namespace GameInterface.Services.Party.Patches;

[HarmonyPatch(typeof(PlayerTownVisitCampaignBehavior))]
internal class SellAllPrisonersPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<SellAllPrisonersPatch>();

    [HarmonyPatch(nameof(PlayerTownVisitCampaignBehavior.SellAllTransferablePrisoners))]
    [HarmonyPrefix]
    public static bool SellAllTransferablePrisonersPrefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        TroopRoster availablePrisonersToSell = MobilePartyHelper.GetPlayerPrisonersPlayerCanSell();

        var message = new PrisonersSold(MobileParty.MainParty.Party, availablePrisonersToSell);
        MessageBroker.Instance.Publish(null, message);

        return false;
    }
}