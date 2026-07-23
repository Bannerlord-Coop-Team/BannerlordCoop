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
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Barters.Patches;

[HarmonyPatch(typeof(BarterManager))]
internal static class PeaceBarterPatch
{
    private static readonly FieldInfo ReleasedPrisonerField =
        AccessTools.Field(typeof(SetPrisonerFreeBarterable), "_prisonerCharacter");
    private static readonly MethodInfo HandleHeroCooldownMethod =
        AccessTools.Method(typeof(BarterManager), "HandleHeroCooldown");

    private static BarterData pendingBarter;
    private static bool pendingUiActive;
    private static string pendingRequestId;
    private static PeaceConversationContext pendingContext;
    private static string pendingContextId;

    [HarmonyPatch(nameof(BarterManager.BeginPlayerBarter))]
    [HarmonyPostfix]
    private static void BeginPlayerBarterPostfix(BarterData args)
    {
        if (ModInformation.IsServer || args == null || pendingBarter == null) return;
        if (args != pendingBarter)
            pendingUiActive = false;
    }

    [HarmonyPatch(nameof(BarterManager.ApplyAndFinalizePlayerBarter))]
    [HarmonyPrefix]
    private static bool ApplyAndFinalizePlayerBarterPrefix(Hero offererHero, BarterData barterData)
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (offererHero == null || !offererHero.IsControlledByThisInstance() || barterData == null) return true;

        var offeredBarterables = barterData.GetOfferedBarterables();
        if (!offeredBarterables.OfType<PeaceBarterable>().Any()) return true;

        if (pendingBarter != null)
        {
            if (IsPendingContextActive())
                return false;

            ClearPendingRequest();
        }

        var targetHero = barterData.OtherHero;
        if (targetHero == null)
        {
            ShowMessage("The peace counterparty is no longer available.");
            return false;
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<INetwork>(out var network) ||
            !objectManager.TryGetId(targetHero, out var targetHeroId) ||
            !TryGetConversationContext(barterData, objectManager, out var context, out var contextId))
        {
            ShowMessage("Unable to send the peace barter to the server.");
            return false;
        }

        if (!TryCreateTerms(offeredBarterables, objectManager, out var terms))
        {
            ShowMessage("This peace offer contains a barter term that co-op cannot validate.");
            return false;
        }

        pendingBarter = barterData;
        pendingUiActive = true;
        pendingRequestId = Guid.NewGuid().ToString("N");
        pendingContext = context;
        pendingContextId = contextId;
        network.SendAll(new NetworkRequestPeaceBarter(
            targetHeroId,
            context,
            contextId,
            terms.ToArray(),
            pendingRequestId));
        return false;
    }

    [HarmonyPatch(nameof(BarterManager.CancelAndFinalizePlayerBarter))]
    [HarmonyPrefix]
    private static bool CancelAndFinalizePlayerBarterPrefix(BarterData barterData)
    {
        if (pendingBarter == null || barterData != pendingBarter) return true;
        if (IsPendingContextActive()) return false;

        ClearPendingRequest();
        return true;
    }

    internal static void CompleteRequest(NetworkPeaceBarterResult result)
    {
        if (pendingBarter == null ||
            pendingRequestId != result.RequestId ||
            pendingContextId != result.ContextId)
            return;

        var completedBarter = pendingBarter;
        var completedContext = pendingContext;
        var shouldCompleteUi = pendingUiActive && IsPendingContextActive();
        ClearPendingRequest();
        if (!result.Accepted)
        {
            ShowMessage(string.IsNullOrWhiteSpace(result.Reason)
                ? "The server rejected the peace barter."
                : result.Reason);
            return;
        }

        BarterClientPresentation.SynchronizeMainHeroGold(result.PlayerGold);
        var encounterIsActive = shouldCompleteUi && completedContext == PeaceConversationContext.MapParty &&
            PlayerEncounter.Current != null &&
            completedBarter.OtherParty == MobileParty.ConversationParty?.Party;
        if (encounterIsActive)
            PlayerEncounter.LeaveEncounter = true;

        if (shouldCompleteUi && BarterManager.Instance != null)
        {
            HandleHeroCooldownMethod?.Invoke(BarterManager.Instance, new object[] { completedBarter.OtherHero });
            BarterManager.Instance.LastBarterIsAccepted = true;
            BarterManager.Instance.Close();
        }

        if (shouldCompleteUi && Campaign.Current?.ConversationManager?.IsConversationInProgress == true)
            Campaign.Current.ConversationManager.ContinueConversation();

        MBInformationManager.AddQuickInformation(GameTexts.FindText("str_offer_accepted"));
    }

    internal static void ClearPendingRequest()
    {
        pendingBarter = null;
        pendingUiActive = false;
        pendingRequestId = null;
        pendingContext = default;
        pendingContextId = null;
    }

    private static bool IsPendingContextActive()
    {
        if (pendingBarter == null) return false;

        if (pendingContext == PeaceConversationContext.Location)
        {
            var mission = CampaignMission.Current;
            return mission?.Location != null &&
                   mission.Mode == MissionMode.Barter &&
                   ContainerProvider.TryResolve<IObjectManager>(out var objectManager) &&
                   objectManager.TryGetId(mission.Location, out var locationId) &&
                   locationId == pendingContextId;
        }

        return PlayerEncounter.Current != null &&
               pendingBarter.OtherParty == MobileParty.ConversationParty?.Party;
    }

    private static bool TryGetConversationContext(
        BarterData barterData,
        IObjectManager objectManager,
        out PeaceConversationContext context,
        out string contextId)
    {
        var location = CampaignMission.Current?.Location;
        if (location != null && objectManager.TryGetId(location, out contextId))
        {
            context = PeaceConversationContext.Location;
            return true;
        }

        if (barterData.OtherParty?.MobileParty?.IsActive == true &&
            objectManager.TryGetId(barterData.OtherParty, out contextId))
        {
            context = PeaceConversationContext.MapParty;
            return true;
        }

        context = default;
        contextId = null;
        return false;
    }

    private static bool TryCreateTerms(
        IEnumerable<Barterable> barterables,
        IObjectManager objectManager,
        out List<PeaceBarterTerm> terms)
    {
        terms = new List<PeaceBarterTerm>();
        foreach (var barterable in barterables)
        {
            if (barterable is PeaceBarterable) continue;
            if (!TryCreateTerm(barterable, objectManager, out var term)) return false;

            terms.Add(term);
        }

        return true;
    }

    private static bool TryCreateTerm(
        Barterable barterable,
        IObjectManager objectManager,
        out PeaceBarterTerm term)
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
                term = new PeaceBarterTerm(
                    PeaceBarterTermType.Gold,
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

                term = new PeaceBarterTerm(
                    PeaceBarterTermType.Fief,
                    ownerHeroId,
                    settlementId,
                    null,
                    true,
                    barterable.CurrentAmount);
                return true;
            case TransferPrisonerBarterable prisonerBarterable:
                return TryCreatePrisonerTerm(
                    PeaceBarterTermType.TransferPrisoner,
                    prisonerBarterable._prisonerCharacter?.CharacterObject,
                    ownerHeroId,
                    barterable.CurrentAmount,
                    objectManager,
                    out term);
            case SetPrisonerFreeBarterable releasedPrisoner:
                var releasedHero = ReleasedPrisonerField?.GetValue(releasedPrisoner) as Hero;
                return TryCreatePrisonerTerm(
                    PeaceBarterTermType.ReleasePrisoner,
                    releasedHero?.CharacterObject,
                    ownerHeroId,
                    barterable.CurrentAmount,
                    objectManager,
                    out term);
            default:
                return false;
        }
    }

    private static bool TryCreateItemTerm(
        ItemBarterable barterable,
        string ownerHeroId,
        IObjectManager objectManager,
        out PeaceBarterTerm term)
    {
        term = default;
        var equipmentElement = barterable.ItemRosterElement.EquipmentElement;
        if (equipmentElement.Item == null || !objectManager.TryGetId(equipmentElement.Item, out var itemId))
            return false;

        var modifier = equipmentElement.ItemModifier;
        string modifierId = null;
        if (modifier != null && !objectManager.TryGetId(modifier, out modifierId))
            return false;

        term = new PeaceBarterTerm(
            PeaceBarterTermType.Item,
            ownerHeroId,
            itemId,
            modifierId,
            modifier == null,
            barterable.CurrentAmount);
        return true;
    }

    private static bool TryCreatePrisonerTerm(
        PeaceBarterTermType type,
        CharacterObject prisoner,
        string ownerHeroId,
        int amount,
        IObjectManager objectManager,
        out PeaceBarterTerm term)
    {
        term = default;
        if (prisoner == null || !objectManager.TryGetId(prisoner, out var prisonerId)) return false;

        term = new PeaceBarterTerm(type, ownerHeroId, prisonerId, null, true, amount);
        return true;
    }

    private static void ShowMessage(string message)
        => InformationManager.DisplayMessage(new InformationMessage(message));
}
