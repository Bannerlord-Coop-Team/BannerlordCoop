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

    private static (int number, int wounded) ReadSource(PartyScreenLogic logic, PartyScreenLogic.PartyCommand command)
    {
        TroopRoster roster = command.Type == PartyScreenLogic.TroopType.Member
            ? logic.MemberRosters[(int)command.RosterSide]
            : logic.PrisonerRosters[(int)command.RosterSide];
        int index = roster.FindIndexOfTroop(command.Character);
        if (index < 0) return (0, 0);

        return (roster.GetElementNumber(index), roster.GetElementWoundedNumber(index));
    }
}
