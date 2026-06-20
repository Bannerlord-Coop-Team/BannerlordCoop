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
        => PlayerPartyTradeContext.SetBarterVM(__instance);

    [HarmonyPatch(nameof(BarterVM.OfferItemAdd))]
    [HarmonyPrefix]
    static bool OfferItemAddPrefix(BarterItemVM barterItemVM)
        => PlayerPartyTradeContext.CanOffer(barterItemVM?.Barterable);

    [HarmonyPatch(nameof(BarterVM.OfferItemRemove))]
    [HarmonyPrefix]
    static bool OfferItemRemovePrefix(BarterItemVM barterItemVM)
        => PlayerPartyTradeContext.CanOffer(barterItemVM?.Barterable);

    [HarmonyPatch("SendOffer")]
    [HarmonyPostfix]
    static void SendOfferPostfix(BarterVM __instance)
        => PlayerPartyTradeContext.PublishOfferChanged(__instance);

    [HarmonyPatch("RefreshOfferLabel")]
    [HarmonyPostfix]
    static void RefreshOfferLabelPostfix()
        => PlayerPartyTradeContext.RefreshBarterControls();

    [HarmonyPatch(nameof(BarterVM.ExecuteOffer))]
    [HarmonyPrefix]
    static bool ExecuteOfferPrefix()
    {
        if (!PlayerPartyTradeContext.IsActive) return true;
        if (!PlayerPartyTradeContext.CanAccept()) return false;

        PlayerPartyTradeContext.PublishAccept(true);
        return false;
    }

    [HarmonyPatch(nameof(BarterVM.ExecuteCancel))]
    [HarmonyPrefix]
    static bool ExecuteCancelPrefix()
    {
        if (!PlayerPartyTradeContext.IsActive) return true;
        if (!PlayerPartyTradeContext.CanCancel()) return false;

        PlayerPartyTradeContext.PublishLeave();
        return false;
    }

    [HarmonyPatch(nameof(BarterVM.ExecuteReset))]
    [HarmonyPrefix]
    static bool ExecuteResetPrefix()
        => PlayerPartyTradeContext.CanReset();

    [HarmonyPatch(nameof(BarterVM.ExecuteAutoBalance))]
    [HarmonyPrefix]
    static bool ExecuteAutoBalancePrefix()
        => PlayerPartyTradeContext.CanModifyOffer();

    [HarmonyPatch("TransferItem")]
    [HarmonyPrefix]
    static bool TransferItemPrefix(BarterItemVM item)
        => PlayerPartyTradeContext.CanOffer(item?.Barterable);
}

[HarmonyPatch(typeof(DialogButtonsParentWidget))]
internal class PlayerPartyBarterDialogButtonsPatches
{
    [HarmonyPatch("set_ResetButton")]
    [HarmonyPostfix]
    static void SetResetButtonPostfix(ButtonWidget value)
        => PlayerPartyTradeContext.SetResetButton(value);
}

[HarmonyPatch(typeof(PartyHeaderToggleWidget))]
internal class PlayerPartyBarterHeaderTogglePatches
{
    [HarmonyPatch("UpdateSize")]
    [HarmonyPostfix]
    static void UpdateSizePostfix(PartyHeaderToggleWidget __instance)
    {
        if (!PlayerPartyTradeContext.IsActive) return;
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
        => PlayerPartyTradeContext.CanOfferTransferAll(__instance, __originalMethod?.Name);
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
        => PlayerPartyTradeContext.CanOffer(__instance?.Barterable);
}

[HarmonyPatch(typeof(BarterManager))]
internal class PlayerPartyBarterManagerPatches
{
    [HarmonyPatch(nameof(BarterManager.ApplyAndFinalizePlayerBarter))]
    [HarmonyPrefix]
    static bool ApplyAndFinalizePlayerBarterPrefix()
    {
        if (!PlayerPartyTradeContext.IsActive) return true;
        if (!PlayerPartyTradeContext.CanAccept()) return false;

        PlayerPartyTradeContext.PublishAccept(true);
        return false;
    }

    [HarmonyPatch(nameof(BarterManager.CancelAndFinalizePlayerBarter))]
    [HarmonyPrefix]
    static bool CancelAndFinalizePlayerBarterPrefix()
    {
        if (!PlayerPartyTradeContext.IsActive) return true;
        if (!PlayerPartyTradeContext.CanCancel()) return false;

        PlayerPartyTradeContext.PublishLeave();
        return false;
    }
}

[HarmonyPatch(typeof(InformationManager))]
internal class PlayerPartyBarterInformationManagerPatches
{
    [HarmonyPatch(nameof(InformationManager.DisplayMessage))]
    [HarmonyPrefix]
    static bool DisplayMessagePrefix(InformationMessage message)
        => !PlayerPartyTradeContext.SuppressNativeCloseMessages;
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
        => !PlayerPartyTradeContext.SuppressNativeCloseMessages;
}
