#if DEBUG
using Common.Commands;
using Missions.Battles;
using System;
using System.Globalization;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Arms the receiving client's existing deck rejection path for one selected puppet.</summary>
public sealed class NavalDeckRejectCoopCommand : ICoopCommand
{
    public string Prefix => "coop.debug.naval";
    public string Name => "deck_reject";
    public string Description => "Rejects the next N received deck poses for a remote agent id (or unique prefix); clear cancels.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("agent", "Remote agent id or unique prefix from inspect agents[].agentId, or clear.", true),
        new ExpectedArgs("count", "Number of received deck poses to reject, 1 to 10000 (default 1).", false),
    };

    // This fault belongs on the receiving client: it changes only its puppet, never the host's authoritative agent.
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!TryParse(args, out string prefix, out int count))
            return new CoopCommandResult(false, "Usage: coop.debug.naval.deck_reject agent-id-or-prefix [1..10000] | clear", "command_failed");

        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (controller == null || mission.GetMissionBehavior<CoopNavalBattleBehavior>() == null)
            return new CoopCommandResult(false, "No active coop naval battle mission.", "command_failed");

        var probe = controller.MissionComponent.AgentMovementHandler.DeckMovementProbe;
        if (prefix == null)
        {
            probe.RejectNext(Guid.Empty, 0);
            return new CoopCommandResult(true, "NAVAL_DECK_REJECT cleared");
        }

        var registry = controller.MissionComponent.AgentRegistry;
        var matches = mission.Agents.Where(agent => agent.IsActive())
            .Select(agent => registry.TryGetAgentInfo(agent, out var info) ? info : null)
            .Where(info => info != null && info.AgentId.ToString().StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1)
            return new CoopCommandResult(false, "Agent id must match exactly one active registered agent; use inspect agents[].agentId.", "command_failed");

        var selected = matches[0];
        if (registry.IsLocallyControlled(selected.Agent))
            return new CoopCommandResult(false, "Choose a remote puppet on this receiver, not a locally controlled agent.", "command_failed");

        probe.RejectNext(selected.AgentId, count);
        return new CoopCommandResult(true, $"NAVAL_DECK_REJECT agentId={selected.AgentId} remaining={count}; inspect shipDecks.sendProbe.rejection");
    }

    // Validates console input without touching native mission state.
    internal static bool TryParse(ICoopCommandArgs args, out string prefix, out int count)
    {
        prefix = null;
        count = 1;
        if (args.Count < 1 || args.Count > 2) return false;
        if (args[0] == "clear") return args.Count == 1;
        string value = args[0];
        if (value.Length < 8 || value.Length > 36 || value.Any(c => !Uri.IsHexDigit(c) && c != '-')) return false;
        if (args.Count == 2 && (!int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out count) || count < 1 || count > 10000)) return false;
        prefix = value;
        return true;
    }
}
#endif
