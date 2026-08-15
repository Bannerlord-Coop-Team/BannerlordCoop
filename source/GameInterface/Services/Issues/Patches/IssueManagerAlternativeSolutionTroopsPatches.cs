using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Interfaces;
using Helpers;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch]
internal class IssueManagerAlternativeSolutionTroopsPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<IssueManagerAlternativeSolutionTroopsPatches>();

    private static bool _inquiryInFlight;

    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.TryToMakeTroopsReturn))]
    [HarmonyPrefix]
    private static bool TryToMakeTroopsReturnPrefix(IssueBase issue)
    {
        var troops = issue?.AlternativeSolutionSentTroops;
        if (troops == null || troops.Count == 0) return false;

        bool modelGatePasses = IsLocalMainHeroSafelyAvailable() && MobileParty.MainParty != null
            && Campaign.Current.Models.IssueModel.CanTroopsReturnFromAlternativeSolution();

        if (!ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownershipRegistry)) return false;

        if (ownershipRegistry.IsLocalPeerOwner(issue.IssueOwner) && modelGatePasses)
        {
            MakeAlternativeTroopsReturn(troops);
            return false;
        }

        if (!ownershipRegistry.TryGetOwnerControllerId(issue.IssueOwner, out var ownerControllerId))
        {
            Logger.Error(
                "TryToMakeTroopsReturn: no recorded owner ControllerId for issue owner {IssueOwner} - these " +
                "troops cannot be tracked for a later return and are lost. Expected only for an issue type " +
                "that never went through the generic alternative-solution accept/ownership routing.",
                issue.IssueOwner);
            return false;
        }

        if (!ContainerProvider.TryResolve<IAwaitingAlternativeSolutionTroopsRegistry>(out var troopsRegistry)) return false;

        troopsRegistry.Deposit(ownerControllerId, troops);
        MessageBroker.Instance.Publish(issue, new AwaitingAlternativeSolutionTroopsDepositedLocally(issue.IssueOwner, ownerControllerId, troops));

        NotifyTrueOwnerOfConfirmedDeposit(issue.IssueOwner, ownerControllerId, troops);

        return false;
    }

    private static void NotifyTrueOwnerOfConfirmedDeposit(Hero issueOwner, string ownerControllerId, TroopRoster troops)
    {
        if (!ModInformation.IsServer) return;

        if (ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider)
            && controllerIdProvider.ControllerId == ownerControllerId)
        {
            return;
        }

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager)) return;
        if (!playerManager.TryGetPeer(ownerControllerId, out var peer)) return;

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return;
        if (!objectManager.TryGetIdWithLogging(issueOwner, out var ownerId)) return;

        if (!ContainerProvider.TryResolve<ITroopRosterInterface>(out var troopRosterInterface)) return;
        if (!ContainerProvider.TryResolve<INetwork>(out var network)) return;

        var packed = troopRosterInterface.PackTroopRosterData(troops);
        network.Send(peer, new NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed(ownerId, packed));
    }

    [HarmonyPatch(typeof(IssueManager), "CheckIfTroopsCanReturnToMainParty")]
    [HarmonyPrefix]
    private static bool CheckIfTroopsCanReturnToMainPartyPrefix()
    {
        TryCheckIfTroopsCanReturnToMainParty();
        return false;
    }

    internal static void TryCheckIfTroopsCanReturnToMainParty()
    {
        if (!IsLocalMainHeroSafelyAvailable() || MobileParty.MainParty == null) return;
        if (_inquiryInFlight) return;

        if (!ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider)) return;
        var localControllerId = controllerIdProvider.ControllerId;
        if (string.IsNullOrEmpty(localControllerId)) return;

        if (!ContainerProvider.TryResolve<IAwaitingAlternativeSolutionTroopsRegistry>(out var troopsRegistry)) return;
        if (!troopsRegistry.TryGet(localControllerId, out var troops)) return;
        if (!Campaign.Current.Models.IssueModel.CanTroopsReturnFromAlternativeSolution()) return;

        TextObject textObject = BuildReturnedTroopsInquiryText(troops);

        _inquiryInFlight = true;
        InformationManager.ShowInquiry(new InquiryData(string.Empty, textObject.ToString(), isAffirmativeOptionShown: true,
            isNegativeOptionShown: false, GameTexts.FindText("str_ok").ToString(), null, delegate
            {
                MakeAlternativeTroopsReturn(troops);
                if (ContainerProvider.TryResolve<IAwaitingAlternativeSolutionTroopsRegistry>(out var registryAtDrainTime))
                {
                    registryAtDrainTime.Clear(localControllerId);
                }
                _inquiryInFlight = false;
                MessageBroker.Instance.Publish(null, new AwaitingAlternativeSolutionTroopsDrainedLocally(localControllerId));
            }, null), pauseGameActiveState: true);
    }

    private static bool IsLocalMainHeroSafelyAvailable() => Game.Current?.PlayerTroop != null;

    private static TextObject BuildReturnedTroopsInquiryText(TroopRoster troops)
    {
        TextObject textObject = new TextObject("{=xPhEQgcI}As you travel, you spot your companions are waiting ahead. They greet you and report that they have returned from their mission with {NUMBER} {?(NUMBER > 1)}troops{?}troop{\\?} and they are all ready to rejoin your party.");

        if (troops.TotalHeroes == 1)
        {
            Hero companionHero = null;
            foreach (TroopRosterElement item in troops.GetTroopRoster())
            {
                if (item.Character.IsHero)
                {
                    companionHero = item.Character.HeroObject;
                    break;
                }
            }

            if (companionHero != null)
            {
                textObject = new TextObject("{=Z5mUfcdS}As you travel, you spot {COMPANION.NAME} waiting ahead. {?COMPANION.GENDER}She{?}He{\\?} greets you and reports that {?COMPANION.GENDER}she{?}he{\\?} has returned from {?COMPANION.GENDER}her{?}his{\\?} mission with {NUMBER} {?(NUMBER > 1)}troops{?}troop{\\?} and they are all ready to rejoin your party.");
                StringHelpers.SetCharacterProperties("COMPANION", companionHero.CharacterObject, textObject);
            }
        }

        textObject.SetTextVariable("NUMBER", troops.TotalManCount);
        return textObject;
    }

    private static void MakeAlternativeTroopsReturn(TroopRoster roster)
    {
        foreach (TroopRosterElement item in roster.GetTroopRoster())
        {
            if (item.Character.IsHero)
            {
                item.Character.HeroObject.ChangeState(Hero.CharacterStates.Active);
            }
        }

        MobileParty.MainParty.MemberRoster.Add(roster);
    }
}
