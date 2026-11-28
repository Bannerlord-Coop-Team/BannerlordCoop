using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Party.Patches;

/// <summary>
/// Records each party screen command by how much it actually took from its source roster, so the server
/// can replay the player's actions instead of a roster delta.
/// </summary>
[HarmonyPatch]
internal class PartyScreenCommandRecordingPatches
{
    // TransferAllTroops runs TransferTroop per troop, so it is recorded as those transfers.
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(PartyScreenLogic), nameof(PartyScreenLogic.TransferTroop)),
        AccessTools.Method(typeof(PartyScreenLogic), nameof(PartyScreenLogic.TransferTroopToLeaderSlot)),
        AccessTools.Method(typeof(PartyScreenLogic), nameof(PartyScreenLogic.UpgradeTroop)),
        AccessTools.Method(typeof(PartyScreenLogic), nameof(PartyScreenLogic.RecruitPrisoner)),
    };

    private static void Prefix(PartyScreenLogic __instance, PartyScreenLogic.PartyCommand command, out (int number, int wounded) __state)
    {
        __state = ReadSource(__instance, command);
    }

    private static void Postfix(PartyScreenLogic __instance, PartyScreenLogic.PartyCommand command, (int number, int wounded) __state)
    {
        var after = ReadSource(__instance, command);
        int taken = __state.number - after.number;
        if (taken <= 0) return;
        if (!ContainerProvider.TryResolve<IPartyScreenCommandTracker>(out var tracker)) return;

        var recorded = new PartyScreenLogic.PartyCommand();
        switch (command.Code)
        {
            case PartyScreenLogic.PartyCommandCode.UpgradeTroop:
                recorded.FillForUpgradeTroop(command.RosterSide, command.Type, command.Character, taken, command.UpgradeTarget, -1);
                break;
            case PartyScreenLogic.PartyCommandCode.RecruitTroop:
                recorded.FillForRecruitTroop(command.RosterSide, command.Type, command.Character, taken, -1);
                break;
            default:
                recorded.FillForTransferTroop(command.RosterSide, command.Type, command.Character, taken, __state.wounded - after.wounded, -1);
                break;
        }

        tracker.Record(__instance, recorded);
    }

    /// <summary>The roster a command takes its troops from, the member or prisoner roster on its side.</summary>
    internal static TroopRoster GetSourceRoster(PartyScreenLogic logic, PartyScreenLogic.PartyCommand command)
        => command.Type == PartyScreenLogic.TroopType.Member
            ? logic.MemberRosters[(int)command.RosterSide]
            : logic.PrisonerRosters[(int)command.RosterSide];

    /// <summary>How many of the command's troop its source roster holds, and how many of those are wounded.</summary>
    internal static (int number, int wounded) ReadSource(PartyScreenLogic logic, PartyScreenLogic.PartyCommand command)
    {
        var roster = GetSourceRoster(logic, command);
        int index = roster.FindIndexOfTroop(command.Character);
        if (index < 0) return (0, 0);

        return (roster.GetElementNumber(index), roster.GetElementWoundedNumber(index));
    }
}

/// <summary>
/// Keeps the recorded commands in step with the screen: a reset clears them, and the troop manager popup saves
/// on open and restores on cancel, so its commands roll back with it.
/// </summary>
[HarmonyPatch(typeof(PartyScreenLogic))]
internal class PartyScreenCommandTrackerLifecyclePatches
{
    [HarmonyPatch(nameof(PartyScreenLogic.Reset))]
    [HarmonyPostfix]
    public static void ResetPostfix(PartyScreenLogic __instance)
    {
        if (ContainerProvider.TryResolve<IPartyScreenCommandTracker>(out var tracker))
            tracker.Clear(__instance);
    }

    [HarmonyPatch(nameof(PartyScreenLogic.SavePartyScreenData))]
    [HarmonyPostfix]
    public static void SavePartyScreenDataPostfix(PartyScreenLogic __instance)
    {
        if (ContainerProvider.TryResolve<IPartyScreenCommandTracker>(out var tracker))
            tracker.Save(__instance);
    }

    [HarmonyPatch(nameof(PartyScreenLogic.ResetToLastSavedPartyScreenData))]
    [HarmonyPostfix]
    public static void ResetToLastSavedPartyScreenDataPostfix(PartyScreenLogic __instance)
    {
        if (ContainerProvider.TryResolve<IPartyScreenCommandTracker>(out var tracker))
            tracker.RestoreSaved(__instance);
    }
}
