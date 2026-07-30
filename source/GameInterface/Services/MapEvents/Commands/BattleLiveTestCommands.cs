#if DEBUG
using Common;
using Common.Network;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.Players;
using System.Collections.Generic;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.MapEvents.Commands;

internal static class BattleLiveTestCommands
{
    [CommandLineArgumentFunction("battle_live_action", "coop.debug.mapevent")]
    public static string SendBattleLiveTestAction(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 2 || !TryParseAction(args[1], out var action))
            return "Usage: coop.debug.mapevent.battle_live_action <controllerId> <start|deploy|wound|win|leave|finish>";

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !playerManager.TryGetPlayer(args[0], out var player) ||
            !playerManager.IsConnected(player) ||
            !playerManager.TryGetPeer(args[0], out var peer))
        {
            return $"Player {args[0]} is not connected.";
        }

        if (!ContainerProvider.TryResolve<INetwork>(out var network))
            return "Unable to resolve Network.";

        network.Send(peer, new NetworkBattleLiveTestAction(action));
        return $"Sent battle live-test action {action} to {args[0]}.";
    }

    private static bool TryParseAction(string value, out BattleLiveTestAction action)
    {
        switch (value.ToLowerInvariant())
        {
            case "start":
                action = BattleLiveTestAction.StartAttackMission;
                return true;
            case "deploy":
                action = BattleLiveTestAction.FinishDeployment;
                return true;
            case "wound":
                action = BattleLiveTestAction.WoundPlayer;
                return true;
            case "win":
                action = BattleLiveTestAction.KillEnemyTeam;
                return true;
            case "leave":
                action = BattleLiveTestAction.LeaveBattle;
                return true;
            case "finish":
                action = BattleLiveTestAction.FinishEncounter;
                return true;
            default:
                action = default;
                return false;
        }
    }
}
#endif
