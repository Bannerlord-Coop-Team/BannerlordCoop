#if DEBUG
using Common.Commands;
using System;
using System.Text;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Commands;

/// <summary>Reports who commands each battle formation, for the reinforcement Delegate Command checks in #3462.</summary>
internal class FormationControlCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    // coop.debug.battle.formation_control_state [hold|charge]
    /// <summary>Lists every non-empty formation with its AI control, player owner and movement order. With an order,
    /// the local player first issues it to all selectable formations through its order controller, like the order menu.</summary>
    public sealed class FormationControlStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.battle";

        public string Name => "formation_control_state";

        public string Description => "Reports formation AI control and orders, optionally after a player hold or charge order.";

        public CoopCommandSide Side => CoopCommandSide.Client;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("order", "hold or charge, issued by the local player to all of its formations first.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var mission = Mission.Current;
            if (mission == null)
                return Failed("No active mission.");

            string issued = "none";
            if (args.Count == 1)
            {
                if (!TryParseOrder(args[0], out var orderType))
                    return Failed("The optional order must be 'hold' or 'charge'.");

                var orderController = mission.PlayerTeam?.PlayerOrderController;
                if (orderController == null)
                    return Failed("The player team has no order controller.");

                orderController.SelectAllFormations();
                orderController.SetOrder(orderType);
                orderController.ClearSelectedFormations();
                issued = orderType.ToString();
            }

            var playerTeam = mission.PlayerTeam;
            var output = new StringBuilder();
            output.Append($"FORMATION_CONTROL_STATE issued={issued} mainAgentActive={Agent.Main != null && Agent.Main.IsActive()} " +
                $"playerGeneral={playerTeam?.IsPlayerGeneral ?? false} playerSergeant={playerTeam?.IsPlayerSergeant ?? false}");

            foreach (var team in mission.Teams)
            {
                string role = TeamRole(mission, team);
                foreach (var formation in team.FormationsIncludingEmpty)
                {
                    if (formation.CountOfUnits == 0) continue;

                    output.Append($"\nteam={role} side={team.Side} formation={formation.FormationIndex} " +
                        $"units={formation.CountOfUnits} ai={formation.IsAIControlled} " +
                        $"playerOwner={formation.PlayerOwner != null} " +
                        $"order={formation.GetReadonlyMovementOrderReference().OrderType}");
                }
            }

            return Succeeded(output.ToString());
        }

        private static bool TryParseOrder(string value, out OrderType orderType)
        {
            if (string.Equals(value, "hold", StringComparison.OrdinalIgnoreCase))
            {
                orderType = OrderType.StandYourGround;
                return true;
            }

            if (string.Equals(value, "charge", StringComparison.OrdinalIgnoreCase))
            {
                orderType = OrderType.Charge;
                return true;
            }

            orderType = OrderType.None;
            return false;
        }

        private static string TeamRole(Mission mission, Team team)
        {
            if (team == mission.PlayerTeam) return "player";
            if (team == mission.PlayerAllyTeam) return "ally";
            if (mission.PlayerTeam != null && team.IsEnemyOf(mission.PlayerTeam)) return "enemy";
            return "other";
        }
    }
}
#endif
