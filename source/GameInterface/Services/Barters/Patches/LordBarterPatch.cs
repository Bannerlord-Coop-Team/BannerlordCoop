using Common;
using Common.Network;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Barters.Messages;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.Barters.Patches;

[HarmonyPatch(typeof(BarterManager))]
internal static class LordBarterPatch
{
    private static BarterData authorizedBarter;
    private static bool requestPending;
    private static bool pendingUiActive;
    private static string pendingRequestId;
    private static string pendingTargetHeroId;
    private static PeaceConversationContext pendingContext;
    private static string pendingContextId;
    private static LordBarterKind pendingKind;

    [HarmonyPatch(nameof(BarterManager.BeginPlayerBarter))]
    [HarmonyPostfix]
    private static void BeginPlayerBarterPostfix(BarterData args)
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed() || args == null) return;
        if (requestPending)
        {
            if (args != authorizedBarter)
                pendingUiActive = false;
            return;
        }

        if (authorizedBarter != null)
            CancelAuthorization();

        if (args.OffererHero == null ||
            !args.OffererHero.IsControlledByThisInstance() ||
            !TryGetKind(args, out var kind))
        {
            return;
        }

        TryAuthorize(args, kind);
    }

    [HarmonyPatch(nameof(BarterManager.ApplyAndFinalizePlayerBarter))]
    [HarmonyPrefix]
    private static bool ApplyAndFinalizePlayerBarterPrefix(Hero offererHero, BarterData barterData)
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed() ||
            offererHero == null || !offererHero.IsControlledByThisInstance() || !TryGetKind(barterData, out _))
            return true;

        if (requestPending) return false;

        if (authorizedBarter != barterData ||
            string.IsNullOrEmpty(pendingRequestId) ||
            !ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<INetwork>(out var network) ||
            !TryCreateTerms(barterData.GetOfferedBarterables(), objectManager, out var terms))
        {
            ShowMessage("Unable to send the lord barter to the server.");
            return false;
        }

        requestPending = true;
        pendingUiActive = true;
        network.SendAll(new NetworkRequestLordBarter(
            pendingTargetHeroId,
            pendingContext,
            pendingContextId,
            pendingKind,
            terms.ToArray(),
            pendingRequestId,
            CollectDefectionPersuasionOutcomes(pendingKind, barterData?.OtherHero)));
        return false;
    }

    // A client that wins the recruitment persuasion gains no Charm XP: the XP writes are blocked
    // client-side (GainRawXpPatch/SetSkillXpPatch/ChangeSkillLevelPatch) and the server never runs
    // the dialogue, so it was simply lost. Ship the per-attempt outcomes so the server can award it.
    //
    // Vanilla re-awards every surviving successful attempt against this lord (the list is only pruned
    // after an in-game year), so we reproduce that rather than sending just this conversation's - but
    // cap it, because the list is client-owned. 8 = one clean conversation's 4 reservation types,
    // doubled.
    internal const int MaxDefectionPersuasionOutcomes = 8;

    private static DefectionPersuasionOutcome[] CollectDefectionPersuasionOutcomes(
        LordBarterKind kind, Hero conversationHero)
    {
        if (kind != LordBarterKind.JoinKingdomAsClan || conversationHero == null)
            return Array.Empty<DefectionPersuasionOutcome>();

        var behavior = Campaign.Current?.GetCampaignBehavior<LordDefectionCampaignBehavior>();
        var attempts = behavior?._previousDefectionPersuasionAttempts;
        if (attempts == null) return Array.Empty<DefectionPersuasionOutcome>();

        var outcomes = new List<DefectionPersuasionOutcome>();
        foreach (var attempt in attempts)
        {
            if (attempt.PersuadedHero != conversationHero) continue;
            if (attempt.Result != PersuasionOptionResult.Success &&
                attempt.Result != PersuasionOptionResult.CriticalSuccess) continue;
            if (attempt.Args == null) continue;
            if (outcomes.Count == MaxDefectionPersuasionOutcomes) break;

            outcomes.Add(new DefectionPersuasionOutcome(
                (int)attempt.Result,
                (int)attempt.Args.ArgumentStrength));
        }

        return outcomes.ToArray();
    }

    [HarmonyPatch(nameof(BarterManager.CancelAndFinalizePlayerBarter))]
    [HarmonyPrefix]
    private static bool CancelAndFinalizePlayerBarterPrefix(BarterData barterData)
    {
        if (barterData != authorizedBarter) return true;
        if (requestPending) return false;
        CancelAuthorization();
        return true;
    }

    internal static void CompleteRequest(NetworkLordBarterResult result, IBarterClientPresentation presentation)
    {
        if (!requestPending ||
            authorizedBarter == null ||
            result.RequestId != pendingRequestId ||
            result.ContextId != pendingContextId)
            return;

        var barter = authorizedBarter;
        var context = pendingContext;
        var kind = pendingKind;
        var shouldCompleteUi = pendingUiActive;
        if (!result.Accepted)
        {
            requestPending = false;
            pendingUiActive = false;
            ShowMessage(string.IsNullOrWhiteSpace(result.Reason) ? "The server rejected the lord barter." : result.Reason);
            return;
        }

        ClearPendingRequest();
        if (shouldCompleteUi && BarterManager.Instance != null)
        {
            BarterManager.Instance.LastBarterIsAccepted = true;
            BarterManager.Instance.Close();
        }

        try
        {
            presentation.SynchronizeMainHeroGold(result.PlayerGold);
            if (shouldCompleteUi && BarterManager.Instance != null)
                BarterManager.Instance.HandleHeroCooldown(barter.OtherHero);
            if (shouldCompleteUi && kind == LordBarterKind.SafePassage &&
                context == PeaceConversationContext.MapParty &&
                PlayerEncounter.Current != null &&
                barter.OtherParty == MobileParty.ConversationParty?.Party)
            {
                var siegeEvent = barter.OtherParty?.SiegeEvent;
                var mainParty = MobileParty.MainParty;
                using (new AllowedThread())
                {
                    var faction = barter.OtherParty.MapFaction;
                    if (faction != null)
                        faction.NotAttackableByPlayerUntilTime = CampaignTime.DaysFromNow(5f);
                }
                if (siegeEvent != null &&
                    siegeEvent.BesiegerCamp.HasInvolvedPartyForEventType(barter.OtherParty) &&
                    siegeEvent.BesiegedSettlement.HasInvolvedPartyForEventType(PartyBase.MainParty))
                {
                    using (new AllowedThread())
                    {
                        Campaign.Current.GameMenuManager.SetNextMenu("menu_siege_safe_passage_accepted");
                        PlayerSiege.FinalizePlayerSiege();
                    }
                }
                else
                {
                    PlayerEncounter.LeaveEncounter = true;
                    if (mainParty.SiegeEvent != null &&
                        mainParty.SiegeEvent.BesiegerCamp
                            .HasInvolvedPartyForEventType(PartyBase.MainParty))
                    {
                        mainParty.BesiegerCamp = null;
                    }
                }
            }
        }
        catch
        {
            // The authoritative result has already closed the barter UI.
        }

        if (shouldCompleteUi)
            TrySetConclusionLine(kind);

        if (shouldCompleteUi && Campaign.Current?.ConversationManager?.IsConversationInProgress == true)
        {
            try
            {
                Campaign.Current.ConversationManager.ContinueConversation();
            }
            catch
            {
                // The authoritative result has already closed the barter UI.
            }
        }
        MBInformationManager.AddQuickInformation(GameTexts.FindText("str_offer_accepted"));
    }

    internal static void ClearPendingRequest()
    {
        authorizedBarter = null;
        requestPending = false;
        pendingUiActive = false;
        pendingRequestId = null;
        pendingTargetHeroId = null;
        pendingContext = default;
        pendingContextId = null;
        pendingKind = default;
    }

    private static bool TryGetKind(BarterData barterData, out LordBarterKind kind)
    {
        kind = default;
        if (barterData?.OtherHero == null || barterData.OtherHero.IsPlayerHero() ||
            barterData.GetOfferedBarterables().OfType<PeaceBarterable>().Any() ||
            barterData.GetOfferedBarterables().OfType<MarriageBarterable>().Any())
            return false;

        var offered = barterData.GetOfferedBarterables();
        if (offered.OfType<JoinKingdomAsClanBarterable>().Any())
            kind = LordBarterKind.JoinKingdomAsClan;
        else if (offered.OfType<SafePassageBarterable>().Any())
        {
            if (barterData.OtherParty?.MobileParty?.IsBandit == true) return false;
            kind = LordBarterKind.SafePassage;
        }
        else
            kind = LordBarterKind.Generic;
        return true;
    }

    private static bool TryAuthorize(BarterData barterData, LordBarterKind kind)
    {
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<INetwork>(out var network) ||
            !objectManager.TryGetId(barterData.OtherHero, out var targetHeroId) ||
            !TryGetConversationContext(barterData, objectManager, out var context, out var contextId))
        {
            return false;
        }

        authorizedBarter = barterData;
        pendingRequestId = Guid.NewGuid().ToString("N");
        pendingTargetHeroId = targetHeroId;
        pendingContext = context;
        pendingContextId = contextId;
        pendingKind = kind;
        string targetKingdomId = null;
        if (kind == LordBarterKind.JoinKingdomAsClan)
        {
            var joinKingdom = barterData.GetOfferedBarterables()
                .OfType<JoinKingdomAsClanBarterable>()
                .FirstOrDefault();
            if (joinKingdom?.TargetKingdom == null ||
                !objectManager.TryGetId(joinKingdom.TargetKingdom, out targetKingdomId))
            {
                ClearPendingRequest();
                return false;
            }
        }
        network.SendAll(new NetworkAuthorizeLordBarter(
            pendingRequestId,
            targetHeroId,
            context,
            contextId,
            kind,
            targetKingdomId));
        return true;
    }

    private static void CancelAuthorization()
    {
        var requestId = pendingRequestId;
        ClearPendingRequest();
        if (!string.IsNullOrEmpty(requestId) && ContainerProvider.TryResolve<INetwork>(out var network))
            network.SendAll(new NetworkCancelLordBarterAuthorization(requestId));
    }

    private static void TrySetConclusionLine(LordBarterKind kind)
    {
        try
        {
            var textId = kind == LordBarterKind.JoinKingdomAsClan
                ? "str_defect_barter_agreed"
                : "str_barter_agreed";
            var conclusion = Campaign.Current?.ConversationManager?
                .FindMatchingTextOrNull(textId, CharacterObject.OneToOneConversationCharacter);
            if (conclusion != null)
                MBTextManager.SetTextVariable("BARTER_CONCLUSION_LINE", conclusion);
        }
        catch
        {
            // The authoritative result has already closed the barter UI.
        }
    }

    internal static bool TryGetConversationContext(BarterData barterData, IObjectManager manager, out PeaceConversationContext context, out string contextId)
    {
        var location = CampaignMission.Current?.Location;
        if (location != null && manager.TryGetId(location, out contextId))
        {
            context = PeaceConversationContext.Location;
            return true;
        }

        if (barterData.OtherParty?.MobileParty?.IsActive == true && manager.TryGetId(barterData.OtherParty, out contextId))
        {
            context = PeaceConversationContext.MapParty;
            return true;
        }

        // Last: a settlement-menu conversation. There is no location mission and no map party to
        // point at, so identify the conversation by the settlement both sides are standing in.
        // Checked after the two above so an ordinary map or location conversation keeps its stronger
        // context - this only catches what would otherwise have no context at all.
        var settlement = barterData.OffererParty?.MobileParty?.CurrentSettlement;
        if (settlement != null &&
            barterData.OtherHero?.CurrentSettlement == settlement &&
            manager.TryGetId(settlement, out contextId))
        {
            context = PeaceConversationContext.Settlement;
            return true;
        }

        context = default;
        contextId = null;
        return false;
    }

    private static bool TryCreateTerms(IEnumerable<Barterable> barterables, IObjectManager manager, out List<PeaceBarterTerm> terms)
    {
        terms = new List<PeaceBarterTerm>();
        foreach (var barterable in barterables)
        {
            if (barterable is SafePassageBarterable || barterable is NoAttackBarterable || barterable is JoinKingdomAsClanBarterable)
                continue;
            if (barterable == null || barterable.CurrentAmount <= 0 || !manager.TryGetId(barterable.OriginalOwner, out var ownerId))
                return false;

            PeaceBarterTerm term;
            switch (barterable)
            {
                case GoldBarterable:
                    term = new PeaceBarterTerm(PeaceBarterTermType.Gold, ownerId, null, null, true, barterable.CurrentAmount);
                    break;
                case ItemBarterable item:
                    var equipment = item.ItemRosterElement.EquipmentElement;
                    if (equipment.Item == null || !manager.TryGetId(equipment.Item, out var itemId)) return false;
                    string modifierId = null;
                    if (equipment.ItemModifier != null && !manager.TryGetId(equipment.ItemModifier, out modifierId)) return false;
                    term = new PeaceBarterTerm(PeaceBarterTermType.Item, ownerId, itemId, modifierId, equipment.ItemModifier == null, barterable.CurrentAmount);
                    break;
                case FiefBarterable fief when manager.TryGetId(fief.TargetSettlement, out var settlementId):
                    term = new PeaceBarterTerm(PeaceBarterTermType.Fief, ownerId, settlementId, null, true, barterable.CurrentAmount);
                    break;
                case TransferPrisonerBarterable transfer when transfer._prisonerCharacter?.CharacterObject != null && manager.TryGetId(transfer._prisonerCharacter.CharacterObject, out var transferPrisonerId):
                    term = new PeaceBarterTerm(PeaceBarterTermType.TransferPrisoner, ownerId, transferPrisonerId, null, true, barterable.CurrentAmount);
                    break;
                case SetPrisonerFreeBarterable release when release._prisonerCharacter?.CharacterObject != null && manager.TryGetId(release._prisonerCharacter.CharacterObject, out var releasePrisonerId):
                    term = new PeaceBarterTerm(PeaceBarterTermType.ReleasePrisoner, ownerId, releasePrisonerId, null, true, barterable.CurrentAmount);
                    break;
                default:
                    return false;
            }
            terms.Add(term);
        }
        return true;
    }

    private static void ShowMessage(string message) => InformationManager.DisplayMessage(new InformationMessage(message));
}
