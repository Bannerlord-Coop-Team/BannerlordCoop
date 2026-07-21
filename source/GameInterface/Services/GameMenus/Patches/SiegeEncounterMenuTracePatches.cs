#if DEBUG
using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.GameMenus.Patches;

internal static class SiegeEncounterMenuTrace
{
    internal const string MenuId = "join_siege_event";

    private static readonly ILogger Logger = LogManager.GetLogger(typeof(SiegeEncounterMenuTrace));

    internal static bool IsCurrentMenu => CurrentMenuId == MenuId;

    internal static void Log(string operation, bool includeStackTrace = false)
    {
        LogState(operation, MobileParty.MainParty, includeStackTrace);
    }

    internal static void LogLeaveRoute(MobileParty party, bool requestLeave)
    {
        LogState($"leave consequence route={(requestLeave ? "synchronized" : "vanilla")}", party, false);
    }

    private static void LogState(string operation, MobileParty party, bool includeStackTrace)
    {
        Logger.Information(
            "[Issue2093] {Operation}; menu={MenuId}; encounter={Encounter}; encounterSettlement={EncounterSettlement}; " +
            "settlementCurrent={SettlementCurrent}; partyCurrent={PartyCurrent}; target={TargetSettlement}; " +
            "shortTarget={ShortTermTargetSettlement}; defaultBehavior={DefaultBehavior}; " +
            "shortTermBehavior={ShortTermBehavior}; siegeEvent={SiegeEvent}; armyLeader={ArmyLeader}; " +
            "isArmyLeader={IsArmyLeader}; mapEventSide={MapEventSide}{StackTrace}",
            operation,
            CurrentMenuId,
            PlayerEncounter.Current != null ? "present" : "null",
            Describe(PlayerEncounter.EncounterSettlement),
            Describe(Settlement.CurrentSettlement),
            Describe(party?.CurrentSettlement),
            Describe(party?.TargetSettlement),
            Describe(party?.ShortTermTargetSettlement),
            party?.DefaultBehavior.ToString() ?? "null",
            party?.ShortTermBehavior.ToString() ?? "null",
            party?.SiegeEvent != null ? "present" : "null",
            party?.Army?.LeaderParty?.StringId ?? "null",
            party?.Army != null && party.Army.LeaderParty == party,
            party?.Party?.MapEventSide != null ? "present" : "null",
            includeStackTrace ? Environment.NewLine + Environment.StackTrace : string.Empty);
    }

    private static string CurrentMenuId =>
        Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "null";

    private static string Describe(Settlement settlement) =>
        settlement?.StringId ?? "null";
}

[HarmonyPatch(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu), typeof(string))]
internal static class SiegeEncounterMenuActivateTracePatch
{
    [HarmonyPrefix]
    private static void Prefix(string menuId)
    {
        if (menuId == SiegeEncounterMenuTrace.MenuId)
            SiegeEncounterMenuTrace.Log($"ActivateGameMenu({menuId})", true);
    }
}

[HarmonyPatch(typeof(GameMenu), nameof(GameMenu.SwitchToMenu), typeof(string))]
internal static class SiegeEncounterMenuSwitchTracePatch
{
    [HarmonyPrefix]
    private static void Prefix(string menuId)
    {
        if (menuId == SiegeEncounterMenuTrace.MenuId || SiegeEncounterMenuTrace.IsCurrentMenu)
            SiegeEncounterMenuTrace.Log($"SwitchToMenu({menuId})", true);
    }
}

[HarmonyPatch(typeof(GameMenu), nameof(GameMenu.ExitToLast))]
internal static class SiegeEncounterMenuExitTracePatch
{
    [HarmonyPrefix]
    private static void Prefix(out bool __state)
    {
        __state = SiegeEncounterMenuTrace.IsCurrentMenu;
        if (__state)
            SiegeEncounterMenuTrace.Log("ExitToLast prefix", true);
    }

    [HarmonyPostfix]
    private static void Postfix(bool __state)
    {
        if (__state)
            SiegeEncounterMenuTrace.Log("ExitToLast postfix");
    }
}

[HarmonyPatch(typeof(PlayerEncounter), nameof(PlayerEncounter.Finish))]
internal static class SiegeEncounterFinishTracePatch
{
    [HarmonyPrefix]
    private static void Prefix(out bool __state)
    {
        __state = SiegeEncounterMenuTrace.IsCurrentMenu;
        if (__state)
            SiegeEncounterMenuTrace.Log("PlayerEncounter.Finish prefix", true);
    }

    [HarmonyPostfix]
    private static void Postfix(bool __state)
    {
        if (__state)
            SiegeEncounterMenuTrace.Log("PlayerEncounter.Finish postfix");
    }
}
#endif
