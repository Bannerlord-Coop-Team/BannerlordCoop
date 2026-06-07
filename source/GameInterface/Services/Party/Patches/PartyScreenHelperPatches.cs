using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Party.Messages;
using HarmonyLib;
using Helpers;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using GameInterface.Policies;

namespace GameInterface.Services.Party.Patches;

[HarmonyPatch(typeof(PartyScreenHelper))]
internal class PartyScreenHelperPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyScreenHelperPatches>();

    [HarmonyPatch(nameof(PartyScreenHelper.SellPrisonersDoneHandler))]
    [HarmonyPrefix]
    public static bool SellPrisonersDoneHandlerPrefix(ref bool __result, TroopRoster leftPrisonRoster)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        var message = new PrisonersSold(MobileParty.MainParty.Party, leftPrisonRoster);
        MessageBroker.Instance.Publish(null, message);

        __result = true;
        return false;
    }

    [HarmonyPatch(nameof(PartyScreenHelper.DonateGarrisonDoneHandler))]
    [HarmonyPrefix]
    public static bool DonateGarrisonDoneHandlerPrefix(ref bool __result, TroopRoster leftMemberRoster)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        Settlement currentSettlement = Hero.MainHero.CurrentSettlement;

        var message = new GarrisonDonated(currentSettlement, leftMemberRoster);
        MessageBroker.Instance.Publish(null, message);

        __result = true;
        return false;
    }

    [HarmonyPatch(nameof(PartyScreenHelper.DonatePrisonersDoneHandler))]
    [HarmonyPrefix]
    public static bool DonatePrisonersDoneHandlerPrefix(ref bool __result, FlattenedTroopRoster rightSideTransferredPrisonerRoster, PartyBase rightParty = null)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (!rightSideTransferredPrisonerRoster.IsEmpty<FlattenedTroopRosterElement>())
        {
            Settlement currentSettlement = Hero.MainHero.CurrentSettlement;

            var message = new PrisonersDonated(rightSideTransferredPrisonerRoster, currentSettlement, rightParty);
            MessageBroker.Instance.Publish(null, message);
        }

        __result = true;
        return false;
    }

    [HarmonyPatch(nameof(PartyScreenHelper.ManageGarrisonDoneHandler))]
    [HarmonyPrefix]
    public static bool ManageGarrisonDoneHandlerPrefix(ref bool __result, TroopRoster leftMemberRoster, TroopRoster leftPrisonRoster)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        Settlement currentSettlement = Hero.MainHero.CurrentSettlement;

        var message = new GarrisonManaged(currentSettlement, leftMemberRoster, leftPrisonRoster);
        MessageBroker.Instance.Publish(null, message);

        __result = true;
        return false;
    }

    [HarmonyPatch(nameof(PartyScreenHelper.HandleReleasedAndTakenPrisoners))]
    [HarmonyPrefix]
    public static bool HandleReleasedAndTakenPrisonersPrefix(FlattenedTroopRoster takenPrisonerRoster, FlattenedTroopRoster releasedPrisonerRoster)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        var message = new PrisonersReleasedAndTaken(releasedPrisonerRoster, takenPrisonerRoster);
        MessageBroker.Instance.Publish(null, message);

        return false;
    }
}