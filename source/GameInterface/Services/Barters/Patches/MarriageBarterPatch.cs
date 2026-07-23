using Common;
using Common.Network;
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
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Barters.Patches;

[HarmonyPatch(typeof(BarterManager))]
internal static class MarriageBarterPatch
{
    private static BarterData authorizedBarter;
    private static bool requestPending;
    private static bool pendingUiActive;
    private static string pendingRequestId;
    private static string pendingCounterpartyHeroId;
    private static string pendingHeroBeingProposedToId;
    private static string pendingProposingHeroId;
    private static MarriageConversationContext pendingContext;
    private static string pendingContextId;

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

        var marriageBarterable = args.GetBarterables().OfType<MarriageBarterable>().FirstOrDefault();
        if (marriageBarterable == null ||
            args.OffererHero == null ||
            !args.OffererHero.IsControlledByThisInstance() ||
            !TryAuthorize(args, marriageBarterable))
        {
            return;
        }
    }

    [HarmonyPatch(nameof(BarterManager.ApplyAndFinalizePlayerBarter))]
    [HarmonyPrefix]
    private static bool ApplyAndFinalizePlayerBarterPrefix(Hero offererHero, BarterData barterData)
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (offererHero == null || !offererHero.IsControlledByThisInstance() || barterData == null) return true;

        var marriageBarterable = barterData.GetOfferedBarterables().OfType<MarriageBarterable>().FirstOrDefault();
        if (marriageBarterable == null) return true;
        if (requestPending) return false;

        if (authorizedBarter != barterData ||
            string.IsNullOrEmpty(pendingRequestId) ||
            !ContainerProvider.TryResolve<INetwork>(out var network) ||
            !ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            ShowMessage("Unable to send the marriage offer to the server.");
            return false;
        }

        if (!TryCreateTerms(barterData.GetOfferedBarterables(), objectManager, out var terms))
        {
            ShowMessage("This marriage offer contains a barter term that co-op cannot validate.");
            return false;
        }

        requestPending = true;
        pendingUiActive = true;
        network.SendAll(new NetworkRequestMarriageBarter(
            pendingCounterpartyHeroId,
            pendingContext,
            pendingContextId,
            pendingHeroBeingProposedToId,
            pendingProposingHeroId,
            terms.ToArray(),
            pendingRequestId));
        return false;
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

    internal static bool CompleteRequest(
        NetworkMarriageBarterResult result,
        IBarterClientPresentation barterClientPresentation)
    {
        if (!requestPending ||
            authorizedBarter == null ||
            pendingRequestId != result.RequestId ||
            pendingCounterpartyHeroId != result.CounterpartyHeroId ||
            pendingHeroBeingProposedToId != result.HeroBeingProposedToId ||
            pendingProposingHeroId != result.ProposingHeroId)
        {
            return false;
        }

        var completedBarter = authorizedBarter;
        var shouldCompleteUi = pendingUiActive;
        ClearPendingRequest();
        if (!result.Accepted)
        {
            if (shouldCompleteUi)
                TryReauthorize(completedBarter);

            ShowMessage(string.IsNullOrWhiteSpace(result.Reason)
                ? "The server rejected the marriage barter."
                : result.Reason);
            return true;
        }

        barterClientPresentation.SynchronizeMainHeroGold(result.PlayerGold);
        if (shouldCompleteUi && BarterManager.Instance != null)
        {
            BarterManager.Instance.HandleHeroCooldown(completedBarter.OtherHero);
            BarterManager.Instance.LastBarterIsAccepted = true;
            BarterManager.Instance.Close();
        }

        if (shouldCompleteUi && Campaign.Current?.ConversationManager?.IsConversationInProgress == true)
            Campaign.Current.ConversationManager.ContinueConversation();

        MBInformationManager.AddQuickInformation(GameTexts.FindText("str_offer_accepted"));
        return true;
    }

    internal static void ClearPendingRequest()
    {
        authorizedBarter = null;
        requestPending = false;
        pendingUiActive = false;
        pendingRequestId = null;
        pendingCounterpartyHeroId = null;
        pendingHeroBeingProposedToId = null;
        pendingProposingHeroId = null;
        pendingContext = default;
        pendingContextId = null;
    }

    private static bool TryAuthorize(BarterData barterData, MarriageBarterable marriageBarterable)
    {
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<INetwork>(out var network) ||
            !objectManager.TryGetId(barterData.OtherHero, out var counterpartyHeroId) ||
            !objectManager.TryGetId(marriageBarterable.HeroBeingProposedTo, out var heroBeingProposedToId) ||
            !objectManager.TryGetId(marriageBarterable.ProposingHero, out var proposingHeroId) ||
            !TryGetConversationContext(barterData, objectManager, out var context, out var contextId))
        {
            return false;
        }

        var requestId = Guid.NewGuid().ToString("N");
        authorizedBarter = barterData;
        pendingRequestId = requestId;
        pendingCounterpartyHeroId = counterpartyHeroId;
        pendingHeroBeingProposedToId = heroBeingProposedToId;
        pendingProposingHeroId = proposingHeroId;
        pendingContext = context;
        pendingContextId = contextId;
        network.SendAll(new NetworkAuthorizeMarriageBarter(
            requestId,
            counterpartyHeroId,
            context,
            contextId,
            heroBeingProposedToId,
            proposingHeroId));
        return true;
    }

    private static void TryReauthorize(BarterData barterData)
    {
        var marriageBarterable = barterData.GetBarterables().OfType<MarriageBarterable>().FirstOrDefault();
        if (marriageBarterable != null)
            TryAuthorize(barterData, marriageBarterable);
    }

    private static void CancelAuthorization()
    {
        var requestId = pendingRequestId;
        ClearPendingRequest();
        if (!string.IsNullOrEmpty(requestId) && ContainerProvider.TryResolve<INetwork>(out var network))
            network.SendAll(new NetworkCancelMarriageBarterAuthorization(requestId));
    }

    private static bool TryGetConversationContext(
        BarterData barterData,
        IObjectManager objectManager,
        out MarriageConversationContext context,
        out string contextId)
    {
        var location = CampaignMission.Current?.Location;
        if (location != null && objectManager.TryGetId(location, out contextId))
        {
            context = MarriageConversationContext.Location;
            return true;
        }

        if (barterData.OtherParty != null && objectManager.TryGetId(barterData.OtherParty, out contextId))
        {
            context = MarriageConversationContext.MapParty;
            return true;
        }

        context = default;
        contextId = null;
        return false;
    }

    private static bool TryCreateTerms(
        IEnumerable<Barterable> barterables,
        IObjectManager objectManager,
        out List<MarriageBarterTerm> terms)
    {
        terms = new List<MarriageBarterTerm>();
        foreach (var barterable in barterables)
        {
            if (barterable is MarriageBarterable) continue;
            if (!TryCreateTerm(barterable, objectManager, out var term)) return false;

            terms.Add(term);
        }

        return true;
    }

    private static bool TryCreateTerm(
        Barterable barterable,
        IObjectManager objectManager,
        out MarriageBarterTerm term)
    {
        term = default;
        if (barterable == null || barterable.CurrentAmount <= 0 ||
            !objectManager.TryGetId(barterable.OriginalOwner, out var ownerHeroId))
        {
            return false;
        }

        switch (barterable)
        {
            case GoldBarterable:
                term = new MarriageBarterTerm(
                    MarriageBarterTermType.Gold,
                    ownerHeroId,
                    null,
                    null,
                    true,
                    barterable.CurrentAmount);
                return true;
            case ItemBarterable itemBarterable:
                return TryCreateItemTerm(itemBarterable, ownerHeroId, objectManager, out term);
            case FiefBarterable fiefBarterable:
                if (!objectManager.TryGetId(fiefBarterable.TargetSettlement, out var settlementId)) return false;

                term = new MarriageBarterTerm(
                    MarriageBarterTermType.Fief,
                    ownerHeroId,
                    settlementId,
                    null,
                    true,
                    barterable.CurrentAmount);
                return true;
            case TransferPrisonerBarterable prisonerBarterable:
                return TryCreatePrisonerTerm(prisonerBarterable, ownerHeroId, objectManager, out term);
            default:
                return false;
        }
    }

    private static bool TryCreateItemTerm(
        ItemBarterable barterable,
        string ownerHeroId,
        IObjectManager objectManager,
        out MarriageBarterTerm term)
    {
        term = default;
        var equipmentElement = barterable.ItemRosterElement.EquipmentElement;
        if (equipmentElement.Item == null || !objectManager.TryGetId(equipmentElement.Item, out var itemId))
            return false;

        var modifier = equipmentElement.ItemModifier;
        string modifierId = null;
        if (modifier != null && !objectManager.TryGetId(modifier, out modifierId))
            return false;

        term = new MarriageBarterTerm(
            MarriageBarterTermType.Item,
            ownerHeroId,
            itemId,
            modifierId,
            modifier == null,
            barterable.CurrentAmount);
        return true;
    }

    private static bool TryCreatePrisonerTerm(
        TransferPrisonerBarterable barterable,
        string ownerHeroId,
        IObjectManager objectManager,
        out MarriageBarterTerm term)
    {
        term = default;
        var prisoner = barterable._prisonerCharacter;
        if (prisoner?.CharacterObject == null ||
            !objectManager.TryGetId(prisoner.CharacterObject, out var characterId))
        {
            return false;
        }

        term = new MarriageBarterTerm(
            MarriageBarterTermType.Prisoner,
            ownerHeroId,
            characterId,
            null,
            true,
            barterable.CurrentAmount);
        return true;
    }

    private static void ShowMessage(string message)
        => InformationManager.DisplayMessage(new InformationMessage(message));
}
