#if DEBUG
using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Battles.Messages;
using System;
using System.Collections.Generic;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Battles.Commands;

public static class BattleGuardFixtureDebugCommand
{
    [CommandLineArgumentFunction("guard_fixture_start", "coop.debug.battle")]
    public static string Start(List<string> args)
    {
        const string usage =
            "Usage: coop.debug.battle.guard_fixture_start battle-instance-id guard-agent-id guard-authority striker-agent-id striker-authority foot|mounted calibration|guard|attack";
        if (ModInformation.IsClient)
            return "This function can only be used by the server";
        if (args.Count != 7 ||
            string.IsNullOrEmpty(args[0]) ||
            !Guid.TryParse(args[1], out Guid guardAgentId) ||
            !Guid.TryParse(args[3], out Guid strikerAgentId) ||
            guardAgentId == Guid.Empty ||
            strikerAgentId == Guid.Empty ||
            guardAgentId == strikerAgentId ||
            string.IsNullOrEmpty(args[2]) ||
            string.IsNullOrEmpty(args[4]))
            return usage;
        if (!TryParseMode(args[5], out BattleGuardFixtureMode mode) ||
            !TryParsePhase(args[6], out BattleGuardFixturePhase phase))
        {
            return usage;
        }
        var command = new NetworkBattleGuardFixtureCommand(
            args[0],
            guardAgentId,
            args[2],
            strikerAgentId,
            args[4],
            mode,
            phase);
        if (!TryDispatch(command, out string error))
            return error;
        return $"BATTLE_GUARD_FIXTURE_SENT instance={args[0]} guard={guardAgentId} " +
            $"guardAuthority={args[2]} striker={strikerAgentId} " +
            $"strikerAuthority={args[4]} mode={mode} phase={phase}";
    }

    [CommandLineArgumentFunction("guard_fixture_reset", "coop.debug.battle")]
    public static string Reset(List<string> args)
    {
        if (ModInformation.IsClient)
            return "This function can only be used by the server";
        if (args.Count != 1 || string.IsNullOrEmpty(args[0]))
            return "Usage: coop.debug.battle.guard_fixture_reset battle-instance-id";
        var command = new NetworkBattleGuardFixtureCommand(
            args[0],
            Guid.Empty,
            null,
            Guid.Empty,
            null,
            BattleGuardFixtureMode.Foot,
            BattleGuardFixturePhase.Calibration,
            reset: true);
        if (!TryDispatch(command, out string error))
            return error;
        return $"BATTLE_GUARD_FIXTURE_RESET_SENT instance={args[0]}";
    }

    private static bool TryDispatch(
        NetworkBattleGuardFixtureCommand command,
        out string error)
    {
        if (!ContainerProvider.TryResolve<INetwork>(out INetwork network))
        {
            error = "Unable to resolve the campaign network";
            return false;
        }
        if (!ContainerProvider.TryResolve<IMessageBroker>(out IMessageBroker messageBroker))
        {
            error = "Unable to resolve the campaign message broker";
            return false;
        }

        network.SendAll(command);
        messageBroker.Publish(typeof(BattleGuardFixtureDebugCommand), command);
        error = null;
        return true;
    }

    private static bool TryParseMode(string value, out BattleGuardFixtureMode mode)
    {
        if (string.Equals(value, "foot", StringComparison.OrdinalIgnoreCase))
        {
            mode = BattleGuardFixtureMode.Foot;
            return true;
        }
        if (string.Equals(value, "mounted", StringComparison.OrdinalIgnoreCase))
        {
            mode = BattleGuardFixtureMode.Mounted;
            return true;
        }

        mode = default;
        return false;
    }

    private static bool TryParsePhase(string value, out BattleGuardFixturePhase phase)
    {
        if (string.Equals(value, "calibration", StringComparison.OrdinalIgnoreCase))
        {
            phase = BattleGuardFixturePhase.Calibration;
            return true;
        }
        if (string.Equals(value, "guard", StringComparison.OrdinalIgnoreCase))
        {
            phase = BattleGuardFixturePhase.Guard;
            return true;
        }
        if (string.Equals(value, "attack", StringComparison.OrdinalIgnoreCase))
        {
            phase = BattleGuardFixturePhase.Attack;
            return true;
        }

        phase = default;
        return false;
    }
}
#endif
