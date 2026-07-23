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
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
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
        var shouldCompleteUi = pendingUiActive && IsPendingContextActive();
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
                using (new AllowedThread())
                {
                    var faction = barter.OtherParty.MapFaction;
                    if (faction != null)
                        faction.NotAttackableByPlayerUntilTime = CampaignTime.DaysFromNow(5f);
                }
                PlayerEncounter.LeaveEncounter = true;
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

    private static bool IsPendingContextActive()
    {
        if (authorizedBarter == null) return false;
        if (pendingContext == PeaceConversationContext.Location)
        {
            var mission = CampaignMission.Current;
            return mission?.Location != null && mission.Mode == MissionMode.Barter &&
                   ContainerProvider.TryResolve<IObjectManager>(out var manager) &&
                   manager.TryGetId(mission.Location, out var locationId) && locationId == pendingContextId;
        }
        return PlayerEncounter.Current != null && authorizedBarter.OtherParty == MobileParty.ConversationParty?.Party;
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
        network.SendAll(new NetworkAuthorizeLordBarter(
            pendingRequestId,
            targetHeroId,
            context,
            contextId,
            kind));
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

    private static bool TryGetConversationContext(BarterData barterData, IObjectManager manager, out PeaceConversationContext context, out string contextId)
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
