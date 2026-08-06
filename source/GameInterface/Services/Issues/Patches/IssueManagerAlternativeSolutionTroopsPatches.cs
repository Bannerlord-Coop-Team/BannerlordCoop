using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
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

/// <summary>
/// Replaces <see cref="IssueManager.TryToMakeTroopsReturn"/>/<see cref="IssueManager.CheckIfTroopsCanReturnToMainParty"/>
/// entirely - vanilla's own <c>_awaitingAlternativeSolutionTroops</c> is a single flat, non-per-owner
/// <see cref="TroopRoster"/> field, which permanently strands a disconnected owner's troops (nothing routes
/// them back on reconnect) and can duplicate them across every connected client if that field is ever non-empty.
/// Fixed via <see cref="AwaitingAlternativeSolutionTroopsRegistry"/>, keyed by the owning peer's own
/// <c>ControllerId</c> (resolved via <see cref="VillageNeedsToolsIssueOwnership"/>) instead of Hero - by the
/// time troops reach this point, <c>IssueFinalized()</c> has already cleared the issue's own state, so the
/// connection identity is the only durable key left. Persisted alongside
/// <see cref="VillageNeedsToolsIssueOwnership"/>'s own save record - see
/// <see cref="AwaitingAlternativeSolutionTroopsPersistencePatches"/>.
///
/// Also fixes a separate dedicated-host NRE reachable through the same entry point: vanilla's
/// <c>DefaultIssueModel.CanTroopsReturnFromAlternativeSolution</c> dereferences <c>Hero.MainHero.IsPrisoner</c>
/// with no null guard, and <c>Hero.MainHero</c> is null on a dedicated server.
/// <see cref="TryToMakeTroopsReturnPrefix"/> guards via <see cref="IsLocalMainHeroSafelyAvailable"/> before
/// ever calling the model gate.
/// </summary>
[HarmonyPatch]
internal class IssueManagerAlternativeSolutionTroopsPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<IssueManagerAlternativeSolutionTroopsPatches>();

    // Prevents a re-entrant HourlyTick (the inquiry callback is async) from stacking a second inquiry.
    private static bool _inquiryInFlight;

    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.TryToMakeTroopsReturn))]
    [HarmonyPrefix]
    private static bool TryToMakeTroopsReturnPrefix(IssueBase issue)
    {
        var troops = issue?.AlternativeSolutionSentTroops;
        if (troops == null || troops.Count == 0) return false;

        // Never call the model gate with a null Hero.MainHero/MobileParty.MainParty.
        bool modelGatePasses = IsLocalMainHeroSafelyAvailable() && MobileParty.MainParty != null
            && Campaign.Current.Models.IssueModel.CanTroopsReturnFromAlternativeSolution();

        if (VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(issue.IssueOwner) && modelGatePasses)
        {
            MakeAlternativeTroopsReturn(troops);
            return false;
        }

        if (!VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(issue.IssueOwner, out var ownerControllerId))
        {
            Logger.Error(
                "TryToMakeTroopsReturn: no recorded owner ControllerId for issue owner {IssueOwner} - these " +
                "troops cannot be tracked for a later return and are lost. Expected only for an issue type " +
                "that never went through the generic alternative-solution accept/ownership routing.",
                issue.IssueOwner);
            return false;
        }

        AwaitingAlternativeSolutionTroopsRegistry.Deposit(ownerControllerId, troops);
        MessageBroker.Instance.Publish(issue, new AwaitingAlternativeSolutionTroopsDepositedLocally(ownerControllerId, troops));

        return false;
    }

    [HarmonyPatch(typeof(IssueManager), "CheckIfTroopsCanReturnToMainParty")]
    [HarmonyPrefix]
    private static bool CheckIfTroopsCanReturnToMainPartyPrefix()
    {
        if (!IsLocalMainHeroSafelyAvailable() || MobileParty.MainParty == null) return false;
        if (_inquiryInFlight) return false;

        if (!ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider)) return false;
        var localControllerId = controllerIdProvider.ControllerId;
        if (string.IsNullOrEmpty(localControllerId)) return false;

        if (!AwaitingAlternativeSolutionTroopsRegistry.TryGet(localControllerId, out var troops)) return false;
        if (!Campaign.Current.Models.IssueModel.CanTroopsReturnFromAlternativeSolution()) return false;

        TextObject textObject = BuildReturnedTroopsInquiryText(troops);

        _inquiryInFlight = true;
        InformationManager.ShowInquiry(new InquiryData(string.Empty, textObject.ToString(), isAffirmativeOptionShown: true,
            isNegativeOptionShown: false, GameTexts.FindText("str_ok").ToString(), null, delegate
            {
                MakeAlternativeTroopsReturn(troops);
                AwaitingAlternativeSolutionTroopsRegistry.Clear(localControllerId);
                _inquiryInFlight = false;
                MessageBroker.Instance.Publish(null, new AwaitingAlternativeSolutionTroopsDrainedLocally(localControllerId));
            }, null), pauseGameActiveState: true);

        return false;
    }

    /// <summary>
    /// Checks <c>Game.Current?.PlayerTroop</c> rather than bare <c>Hero.MainHero</c>: <c>Hero.MainHero</c> is
    /// <c>CharacterObject.PlayerCharacter.HeroObject</c>, and evaluating it at all throws the instant
    /// <c>Game.Current.PlayerTroop</c> is null - exactly the dedicated-host condition this exists to detect.
    /// </summary>
    private static bool IsLocalMainHeroSafelyAvailable() => Game.Current?.PlayerTroop != null;

    /// <summary>Reimplementation of vanilla's inquiry-text construction, with a null-companion-Hero guard vanilla itself lacks.</summary>
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

    /// <summary>Reproduction of vanilla's private <c>IssueManager.MakeAlternativeTroopsReturn</c>.</summary>
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
