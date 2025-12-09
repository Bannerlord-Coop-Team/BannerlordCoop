using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.Patches;

[HarmonyPatch]
internal class MajorStatesSafePatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<MajorStatesSafePatch>();

    [HarmonyPatch(typeof(InventoryState), nameof(InventoryState.OnInitialize))]
    [HarmonyFinalizer]
    static System.Exception Inventory_OnInitialize_Finalizer(System.Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error("UI InventoryState.OnInitialize exception: {Exception}", __exception);
            InformationManager.DisplayMessage(new InformationMessage("[UI] Erreur écran inventaire (ignorée)"));
            return null;
        }
        return null;
    }

    [HarmonyPatch(typeof(InventoryState), nameof(InventoryState.OnActivate))]
    [HarmonyFinalizer]
    static System.Exception Inventory_OnActivate_Finalizer(System.Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error("UI InventoryState.OnActivate exception: {Exception}", __exception);
            InformationManager.DisplayMessage(new InformationMessage("[UI] Erreur activation inventaire (ignorée)"));
            return null;
        }
        return null;
    }

    [HarmonyPatch(typeof(PartyState), nameof(PartyState.OnInitialize))]
    [HarmonyFinalizer]
    static System.Exception Party_OnInitialize_Finalizer(System.Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error("UI PartyState.OnInitialize exception: {Exception}", __exception);
            InformationManager.DisplayMessage(new InformationMessage("[UI] Erreur écran armée (ignorée)"));
            return null;
        }
        return null;
    }

    [HarmonyPatch(typeof(PartyState), nameof(PartyState.OnActivate))]
    [HarmonyFinalizer]
    static System.Exception Party_OnActivate_Finalizer(System.Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error("UI PartyState.OnActivate exception: {Exception}", __exception);
            InformationManager.DisplayMessage(new InformationMessage("[UI] Erreur activation armée (ignorée)"));
            return null;
        }
        return null;
    }

    [HarmonyPatch(typeof(KingdomState), nameof(KingdomState.OnInitialize))]
    [HarmonyFinalizer]
    static System.Exception Kingdom_OnInitialize_Finalizer(System.Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error("UI KingdomState.OnInitialize exception: {Exception}", __exception);
            InformationManager.DisplayMessage(new InformationMessage("[UI] Erreur écran royaume (ignorée)"));
            return null;
        }
        return null;
    }

    [HarmonyPatch(typeof(KingdomState), nameof(KingdomState.OnActivate))]
    [HarmonyFinalizer]
    static System.Exception Kingdom_OnActivate_Finalizer(System.Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error("UI KingdomState.OnActivate exception: {Exception}", __exception);
            InformationManager.DisplayMessage(new InformationMessage("[UI] Erreur activation royaume (ignorée)"));
            return null;
        }
        return null;
    }

    [HarmonyPatch(typeof(QuestsState), nameof(QuestsState.OnInitialize))]
    [HarmonyFinalizer]
    static System.Exception Quests_OnInitialize_Finalizer(System.Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error("UI QuestsState.OnInitialize exception: {Exception}", __exception);
            InformationManager.DisplayMessage(new InformationMessage("[UI] Erreur écran quêtes (ignorée)"));
            return null;
        }
        return null;
    }

    [HarmonyPatch(typeof(QuestsState), nameof(QuestsState.OnActivate))]
    [HarmonyFinalizer]
    static System.Exception Quests_OnActivate_Finalizer(System.Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error("UI QuestsState.OnActivate exception: {Exception}", __exception);
            InformationManager.DisplayMessage(new InformationMessage("[UI] Erreur activation quêtes (ignorée)"));
            return null;
        }
        return null;
    }
}
