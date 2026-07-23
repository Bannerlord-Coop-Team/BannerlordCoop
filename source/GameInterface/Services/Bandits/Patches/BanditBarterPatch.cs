using Common;
using Common.Network;
using GameInterface.Policies;
using GameInterface.Services.Barters;
using GameInterface.Services.Bandits.Messages;
using GameInterface.Services.Inventory.Data;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
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

namespace GameInterface.Services.Bandits.Patches;

[HarmonyPatch(typeof(BarterManager))]
internal static class BanditBarterPatch
{
    private static BarterData pendingBarter;
    private static string pendingBanditPartyId;
    private static string pendingRequestId;
    private static bool pendingUiActive;

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
        if (offererHero != Hero.MainHero || barterData?.OffererParty != PartyBase.MainParty) return true;

        var offeredBarterables = barterData.GetOfferedBarterables();
        var safePassage = offeredBarterables.OfType<SafePassageBarterable>().FirstOrDefault();
        var banditParty = safePassage?.OriginalParty?.MobileParty;
        if (banditParty?.IsActive != true || !banditParty.IsBandit || barterData.OtherParty != banditParty.Party)
            return true;

        if (pendingBarter != null)
        {
            if (IsPendingEncounterActive())
                return false;

            ClearPendingRequest();
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<INetwork>(out var network) ||
            !objectManager.TryGetId(banditParty, out var banditPartyId))
        {
            ShowMessage("Unable to send the bandit barter to the server.");
            return false;
        }

        var requestId = Guid.NewGuid().ToString("N");
        if (!TryCreateRequest(
                offeredBarterables,
                barterData.OffererParty,
                banditPartyId,
                requestId,
                objectManager,
                out var request))
        {
            ShowMessage("Safe-passage offers in co-op can only transfer gold, items, or prisoners to the bandits.");
            return false;
        }

        pendingBarter = barterData;
        pendingBanditPartyId = banditPartyId;
        pendingRequestId = requestId;
        pendingUiActive = true;
        network.SendAll(request);
        return false;
    }

    [HarmonyPatch(nameof(BarterManager.CancelAndFinalizePlayerBarter))]
    [HarmonyPrefix]
    private static bool CancelAndFinalizePlayerBarterPrefix(BarterData barterData)
    {
        if (pendingBarter == null || barterData != pendingBarter) return true;
        if (IsPendingEncounterActive()) return false;

        ClearPendingRequest();
        return true;
    }

    internal static void CompleteRequest(
        NetworkBanditBarterResult result,
        IBarterClientPresentation barterClientPresentation)
    {
        if (pendingBarter == null ||
            pendingBanditPartyId != result.BanditPartyId ||
            pendingRequestId != result.RequestId)
            return;

        var completedBarter = pendingBarter;
        var shouldCompleteUi = pendingUiActive;
        ClearPendingRequest();

        if (!result.Accepted)
        {
            ShowMessage(string.IsNullOrWhiteSpace(result.Reason)
                ? "The server rejected the bandit barter."
                : result.Reason);
            return;
        }

        barterClientPresentation.SynchronizeMainHeroGold(result.PlayerGold);

        var encounterIsActive = shouldCompleteUi && PlayerEncounter.Current != null &&
            completedBarter.OtherParty == MobileParty.ConversationParty?.Party;

        if (encounterIsActive && BarterManager.Instance != null)
        {
            BarterManager.Instance.LastBarterIsAccepted = true;
            BarterManager.Instance.Close();
        }

        if (encounterIsActive)
        {
            PlayerEncounter.LeaveEncounter = true;
            if (Campaign.Current?.ConversationManager?.IsConversationInProgress == true)
                Campaign.Current.ConversationManager.ContinueConversation();
        }

        MBInformationManager.AddQuickInformation(GameTexts.FindText("str_offer_accepted"));
    }

    internal static void ClearPendingRequest()
    {
        pendingBarter = null;
        pendingBanditPartyId = null;
        pendingRequestId = null;
        pendingUiActive = false;
    }

    private static bool IsPendingEncounterActive()
        => pendingBarter != null &&
           PlayerEncounter.Current != null &&
           pendingBarter.OtherParty == MobileParty.ConversationParty?.Party;

    private static bool TryCreateRequest(
        IEnumerable<Barterable> barterables,
        PartyBase playerParty,
        string banditPartyId,
        string requestId,
        IObjectManager objectManager,
        out NetworkRequestBanditBarter request)
    {
        var playerItems = new List<ItemRosterElementData>();
        var playerPrisoners = new List<TroopRosterElementData>();
        var playerGold = 0;

        foreach (var barterable in barterables)
        {
            if (barterable is SafePassageBarterable) continue;
            if (barterable == null || barterable.CurrentAmount <= 0)
            {
                request = default;
                return false;
            }

            if (barterable.OriginalParty != playerParty)
            {
                request = default;
                return false;
            }

            switch (barterable)
            {
                case GoldBarterable:
                    if (!TryAddGold(ref playerGold, barterable.CurrentAmount))
                    {
                        request = default;
                        return false;
                    }
                    break;
                case ItemBarterable itemBarterable:
                    if (!TryAddItem(itemBarterable, playerItems, objectManager))
                    {
                        request = default;
                        return false;
                    }
                    break;
                case TransferPrisonerBarterable prisonerBarterable:
                    if (!TryAddPrisoner(prisonerBarterable, playerPrisoners, objectManager))
                    {
                        request = default;
                        return false;
                    }
                    break;
                default:
                    request = default;
                    return false;
            }
        }

        request = new NetworkRequestBanditBarter(
            banditPartyId,
            playerGold,
            playerItems.ToArray(),
            playerPrisoners.ToArray(),
            requestId);
        return true;
    }

    private static bool TryAddGold(ref int playerGold, int amount)
    {
        var total = (long)playerGold + amount;
        if (total > int.MaxValue) return false;

        playerGold = (int)total;
        return true;
    }

    private static bool TryAddItem(
        ItemBarterable barterable,
        ICollection<ItemRosterElementData> items,
        IObjectManager objectManager)
    {
        var equipmentElement = barterable.ItemRosterElement.EquipmentElement;
        if (equipmentElement.Item == null || !objectManager.TryGetId(equipmentElement.Item, out var itemId))
            return false;

        string modifierId = null;
        var modifierIsNull = equipmentElement.ItemModifier == null;
        if (!modifierIsNull && !objectManager.TryGetId(equipmentElement.ItemModifier, out modifierId))
            return false;

        items.Add(new ItemRosterElementData(
            new ItemObjectData(itemId, modifierId, modifierIsNull),
            barterable.CurrentAmount));
        return true;
    }

    private static bool TryAddPrisoner(
        TransferPrisonerBarterable barterable,
        ICollection<TroopRosterElementData> prisoners,
        IObjectManager objectManager)
    {
        var prisoner = barterable._prisonerCharacter?.CharacterObject;
        if (prisoner == null || !objectManager.TryGetId(prisoner, out var prisonerId))
            return false;

        prisoners.Add(new TroopRosterElementData(prisonerId, barterable.CurrentAmount, 0, 0));
        return true;
    }

    private static void ShowMessage(string message)
        => InformationManager.DisplayMessage(new InformationMessage(message));
}
