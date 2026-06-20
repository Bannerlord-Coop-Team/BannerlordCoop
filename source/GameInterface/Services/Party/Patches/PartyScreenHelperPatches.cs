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

namespace GameInterface.Services.Party.Patches;

[HarmonyPatch(typeof(PartyScreenHelper))]
internal class PartyScreenHelperPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyScreenHelperPatches>();

    [HarmonyPatch(nameof(PartyScreenHelper.OpenScreenAsCreateClanPartyForHeroPartyScreenClosed))]
    [HarmonyPrefix]
    public static bool OpenScreenAsCreateClanPartyForHeroPartyScreenClosedPrefix(PartyBase leftOwnerParty, TroopRoster leftMemberRoster, TroopRoster leftPrisonRoster, PartyBase rightOwnerParty, TroopRoster rightMemberRoster, TroopRoster rightPrisonRoster, bool fromCancel)
    {
        if (!fromCancel)
        {
            Hero newLeaderHero = null;
            for (int i = 0; i < leftMemberRoster.data.Length; i++)
            {
                CharacterObject character = leftMemberRoster.data[i].Character;
                if (character != null && character.IsHero)
                {
                    newLeaderHero = leftMemberRoster.data[i].Character.HeroObject;
                }
            }
            var message = new NewClanPartyScreenClosed(
                Hero.MainHero,
                newLeaderHero,
                leftMemberRoster,
                leftPrisonRoster
            );
            
            MessageBroker.Instance.Publish(null, message);
        }

        return false;
    }

    [HarmonyPatch(nameof(PartyScreenHelper.SellPrisonersDoneHandler))]
    [HarmonyPrefix]
    public static bool SellPrisonersDoneHandlerPrefix(ref bool __result, TroopRoster leftPrisonRoster)
    {
        var message = new PrisonersSold(MobileParty.MainParty.Party, leftPrisonRoster);
        MessageBroker.Instance.Publish(null, message);

        __result = true;
        return false;
    }

    [HarmonyPatch(nameof(PartyScreenHelper.DonateGarrisonDoneHandler))]
    [HarmonyPrefix]
    public static bool DonateGarrisonDoneHandlerPrefix(ref bool __result, TroopRoster leftMemberRoster)
    {
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
        var message = new PrisonersReleasedAndTaken(releasedPrisonerRoster, takenPrisonerRoster);
        MessageBroker.Instance.Publish(null, message);

        return false;
    }
}