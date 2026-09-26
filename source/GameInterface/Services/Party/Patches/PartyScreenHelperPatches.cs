using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Party.Messages;
using HarmonyLib;
using Helpers;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Party.Patches;

[HarmonyPatch(typeof(PartyScreenHelper))]
internal class PartyScreenHelperPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyScreenHelperPatches>();
    [ThreadStatic]
    private static bool _releasedAndTakenPrisonerActionsRequested;
    [ThreadStatic]
    private static Settlement _donationSettlement;
    [ThreadStatic]
    private static FlattenedTroopRoster _donatedPrisonersRoster;

    internal static void ResetReleasedAndTakenPrisonerActionsRequest()
        => _releasedAndTakenPrisonerActionsRequested = false;

    internal static bool ConsumeReleasedAndTakenPrisonerActionsRequest()
    {
        var requested = _releasedAndTakenPrisonerActionsRequested;
        _releasedAndTakenPrisonerActionsRequested = false;
        return requested;
    }

    internal static void ResetPrisonerDonationRequest()
    {
        _donationSettlement = null;
        _donatedPrisonersRoster = null;
    }

    internal static bool ConsumePrisonerDonationRequest(
        out Settlement settlement,
        out FlattenedTroopRoster donatedPrisonersRoster)
    {
        settlement = _donationSettlement;
        donatedPrisonersRoster = _donatedPrisonersRoster;
        ResetPrisonerDonationRequest();
        return settlement != null && donatedPrisonersRoster != null;
    }

    [HarmonyPatch("ClosePartyPresentation")]
    [HarmonyPrefix]
    private static void ClosePartyPresentationPrefix(
        out (PartyState State, PartyScreenLogic Screen, TroopRoster Roster) __state)
    {
        __state = default;
        if (!ModInformation.IsClient || Game.Current?.GameStateManager?.ActiveState is not PartyState state) return;
        var screen = state.PartyScreenLogic;
        if (screen == null || screen._partyScreenMode != PartyScreenHelper.PartyScreenMode.QuestTroopManage) return;
        __state = (state, screen, screen.CurrentData.LeftMemberRoster);
    }

    [HarmonyPatch("ClosePartyPresentation")]
    [HarmonyPostfix]
    private static void ClosePartyPresentationPostfix(
        (PartyState State, PartyScreenLogic Screen, TroopRoster Roster) __state)
    {
        if (__state.State?.PartyScreenLogic != null || __state.Roster == null) return;
        MessageBroker.Instance.Publish(__state.Screen, new QuestAlternativeTroopSelectionClosed(__state.Roster));
    }

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
            _donationSettlement = Hero.MainHero.CurrentSettlement;
            _donatedPrisonersRoster = rightSideTransferredPrisonerRoster;
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
        // PartyDoneLogicAttempted carries both histories with the authoritative roster deltas. The server
        // applies those deltas first, then runs the vanilla release/take side effects. Sending a second command
        // here made the semantic action mutate the roster before the same PartyDone delta was applied.
        // Vanilla DefaultDoneHandler calls this unconditionally, so only flag actual moves;
        // otherwise every loot Done (e.g. force volunteers) looks like a prisoner action.
        if ((takenPrisonerRoster != null && !takenPrisonerRoster.IsEmpty<FlattenedTroopRosterElement>()) ||
            (releasedPrisonerRoster != null && !releasedPrisonerRoster.IsEmpty<FlattenedTroopRosterElement>()))
        {
            _releasedAndTakenPrisonerActionsRequested = true;
        }
        return false;
    }
}
