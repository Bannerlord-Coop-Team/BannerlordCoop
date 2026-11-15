using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Barter;
using TaleWorlds.Core;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.ExtraWidgets;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Party;

namespace GameInterface.Services.Barters.Patches;

[HarmonyPatch(typeof(BarterVM))]
internal class PlayerPartyBarterVMPatches
{
    [HarmonyPatch(MethodType.Constructor, typeof(BarterData))]
    [HarmonyPostfix]
    static void ConstructorPostfix(BarterVM __instance)
    {
        if (!ContainerProvider.TryResolve<IPlayerPartyTradeContext>(out var trade)) return;

        trade.SetBarterVM(__instance);
    }

    [HarmonyPatch(nameof(BarterVM.InitBarterList))]
    [HarmonyPrefix]
    static void InitBarterListPrefix(BarterVM __instance, BarterData args)
    {
        if (!ContainerProvider.TryResolve<IPlayerPartyTradeContext>(out var trade)) return;

        trade.AddMarriageGroupLists(__instance, args);
    }

    [HarmonyPatch(nameof(BarterVM.OfferItemAdd))]
    [HarmonyPrefix]
    static bool OfferItemAddPrefix(BarterItemVM barterItemVM)
    {
        if (!ContainerProvider.TryResolve<IPlayerPartyTradeContext>(out var trade)) return true;

        return trade.CanOffer(barterItemVM?.Barterable);
    }

    [HarmonyPatch(nameof(BarterVM.OfferItemRemove))]
    [HarmonyPrefix]
    static bool OfferItemRemovePrefix(BarterItemVM barterItemVM)
    {
        if (!ContainerProvider.TryResolve<IPlayerPartyTradeContext>(out var trade)) return true;

        return trade.CanOffer(barterItemVM?.Barterable);
    }

    // Vanilla calls SendOffer after every single item change, so this only marks the offer for the next flush.
    [HarmonyPatch("SendOffer")]
    [HarmonyPostfix]
    static void SendOfferPostfix()
    {
        if (!ContainerProvider.TryResolve<IPlayerPartyTradeSender>(out var tradeSender)) return;

        tradeSender.MarkChanged();
    }

    [HarmonyPatch("RefreshOfferLabel")]
    [HarmonyPostfix]
    static void RefreshOfferLabelPostfix()
    {
        if (!ContainerProvider.TryResolve<IPlayerPartyTradeContext>(out var trade)) return;

        trade.RefreshBarterControls();
    }

    [HarmonyPatch(nameof(BarterVM.ExecuteReset))]
    [HarmonyPrefix]
    static bool ExecuteResetPrefix()
    {
        if (!ContainerProvider.TryResolve<IPlayerPartyTradeContext>(out var trade)) return true;

        return trade.CanReset();
    }

    [HarmonyPatch("TransferItem")]
    [HarmonyPrefix]
    static bool TransferItemPrefix(BarterItemVM item)
    {
        if (!ContainerProvider.TryResolve<IPlayerPartyTradeContext>(out var trade)) return true;

        return trade.CanOffer(item?.Barterable);
    }
}

[HarmonyPatch(typeof(DialogButtonsParentWidget))]
internal class PlayerPartyBarterDialogButtonsPatches
{
    [HarmonyPatch("set_ResetButton")]
    [HarmonyPostfix]
    static void SetResetButtonPostfix(ButtonWidget value)
    {
        if (!ContainerProvider.TryResolve<IPlayerPartyTradeContext>(out var trade)) return;

        trade.SetResetButton(value);
    }
}

[HarmonyPatch(typeof(PartyHeaderToggleWidget))]
internal class PlayerPartyBarterHeaderTogglePatches
{
    [HarmonyPatch("UpdateSize")]
    [HarmonyPostfix]
    static void UpdateSizePostfix(PartyHeaderToggleWidget __instance)
    {
        if (!ContainerProvider.TryResolve<IPlayerPartyTradeContext>(out var trade)) return;
        if (!trade.IsActive) return;
        if (__instance?.ListPanel == null) return;
        if (__instance.ListPanel.ChildCount != 0) return;
        if (__instance.ListPanel.Id?.Contains("Diplomatic") != true) return;

        __instance.IsVisible = false;
        __instance.ListPanel.IsVisible = false;
    }
}

[HarmonyPatch]
internal class PlayerPartyBarterTransferAllPatches
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(BarterVM), nameof(BarterVM.ExecuteTransferAllLeftFief));
        yield return AccessTools.Method(typeof(BarterVM), nameof(BarterVM.ExecuteTransferAllLeftItem));
        yield return AccessTools.Method(typeof(BarterVM), nameof(BarterVM.ExecuteTransferAllLeftPrisoner));
        yield return AccessTools.Method(typeof(BarterVM), nameof(BarterVM.ExecuteTransferAllLeftOther));
        yield return AccessTools.Method(typeof(BarterVM), nameof(BarterVM.ExecuteTransferAllRightFief));
        yield return AccessTools.Method(typeof(BarterVM), nameof(BarterVM.ExecuteTransferAllRightItem));
        yield return AccessTools.Method(typeof(BarterVM), nameof(BarterVM.ExecuteTransferAllRightPrisoner));
        yield return AccessTools.Method(typeof(BarterVM), nameof(BarterVM.ExecuteTransferAllRightOther));
        yield return AccessTools.Method(typeof(BarterVM), nameof(BarterVM.ExecuteTransferAllGoldLeft));
        yield return AccessTools.Method(typeof(BarterVM), nameof(BarterVM.ExecuteTransferAllGoldRight));
    }

    [HarmonyPrefix]
    static bool ExecuteTransferAllPrefix(BarterVM __instance, MethodBase __originalMethod)
    {
        if (!ContainerProvider.TryResolve<IPlayerPartyTradeContext>(out var trade)) return true;

        return trade.CanOfferTransferAll(__instance, __originalMethod?.Name);
    }
}

[HarmonyPatch]
internal class PlayerPartyBarterItemVMPatches
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(BarterItemVM), nameof(BarterItemVM.ExecuteAction));
        yield return AccessTools.Method(typeof(BarterItemVM), nameof(BarterItemVM.ExecuteAddOffered));
        yield return AccessTools.Method(typeof(BarterItemVM), nameof(BarterItemVM.ExecuteRemoveOffered));
    }

    [HarmonyPrefix]
    static bool ExecuteItemActionPrefix(BarterItemVM __instance)
    {
        if (!ContainerProvider.TryResolve<IPlayerPartyTradeContext>(out var trade)) return true;

        return trade.CanOffer(__instance?.Barterable);
    }
}

[HarmonyPatch(typeof(BarterItemVM), nameof(BarterItemVM.CurrentOfferedAmount), MethodType.Setter)]
internal class PlayerPartyBarterItemAmountPatches
{
    [HarmonyPrefix]
    static bool SetCurrentOfferedAmountPrefix(BarterItemVM __instance, int value)
    {
        if (!ContainerProvider.TryResolve<IPlayerPartyTradeContext>(out var trade)) return true;

        return trade.CanSetOfferedAmount(
            __instance?.Barterable,
            __instance?.CurrentOfferedAmount ?? 0,
            value);
    }
}

// Turns accepting a player-party trade, from the barter screen or the barter manager, into a coop accept.
[HarmonyPatch]
internal class PlayerPartyBarterAcceptPatches
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(BarterVM), nameof(BarterVM.ExecuteOffer));
        yield return AccessTools.Method(typeof(BarterManager), nameof(BarterManager.ApplyAndFinalizePlayerBarter));
    }

    [HarmonyPrefix]
    static bool AcceptPrefix()
    {
        if (!ContainerProvider.TryResolve<IPlayerPartyTradeSender>(out var tradeSender)) return true;

        // A player trade sends the accept to the server, an NPC barter runs vanilla.
        return !tradeSender.Accept();
    }
}

// Turns cancelling a player-party trade, from the barter screen or the barter manager, into leaving the coop trade.
[HarmonyPatch]
internal class PlayerPartyBarterCancelPatches
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(BarterVM), nameof(BarterVM.ExecuteCancel));
        yield return AccessTools.Method(typeof(BarterManager), nameof(BarterManager.CancelAndFinalizePlayerBarter));
    }

    [HarmonyPrefix]
    static bool CancelPrefix()
    {
        if (!ContainerProvider.TryResolve<IPlayerPartyTradeSender>(out var tradeSender)) return true;

        // A player trade leaves through the server, an NPC barter runs vanilla.
        return !tradeSender.Leave();
    }
}

[HarmonyPatch(typeof(InformationManager))]
internal class PlayerPartyBarterInformationManagerPatches
{
    [HarmonyPatch(nameof(InformationManager.DisplayMessage))]
    [HarmonyPrefix]
    static bool DisplayMessagePrefix(InformationMessage message)
    {
        if (!ContainerProvider.TryResolve<IPlayerPartyTradeContext>(out var trade)) return true;

        return !trade.SuppressNativeCloseMessages;
    }
}

[HarmonyPatch(typeof(MBInformationManager))]
internal class PlayerPartyBarterMBInformationManagerPatches
{
    [HarmonyPatch(
        nameof(MBInformationManager.AddQuickInformation),
        typeof(TextObject),
        typeof(int),
        typeof(BasicCharacterObject),
        typeof(Equipment),
        typeof(string))]
    [HarmonyPrefix]
    static bool AddQuickInformationPrefix(TextObject message)
    {
        if (!ContainerProvider.TryResolve<IPlayerPartyTradeContext>(out var trade)) return true;

        return !trade.SuppressNativeCloseMessages;
    }
}
