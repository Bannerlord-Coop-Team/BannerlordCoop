using Common;
using Common.Messaging;
using GameInterface.Services.UI.CoopOptions.Providers.ServerOptions;
using GameInterface.Services.UI.Messages;
using System.Collections.Generic;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.MapEvents.BattleSize.Commands;

/// <summary>Runtime observation and mutation hooks for automated battle-size live tests.</summary>
public class BattleSizeDebugCommands
{
    private const string StatusUsage = "Usage: coop.debug.battle_size.status";
    private const string SetRuntimeUsage =
        "Usage: coop.debug.battle_size.set_runtime <200|300|400|500|600|800|1000>";

    [CommandLineArgumentFunction("status", "coop.debug.battle_size")]
    public static string Status(List<string> args)
    {
        if (args.Count != 0) return StatusUsage;
        if (!ContainerProvider.TryResolve<IServerBattleSizeProvider>(out var provider))
            return "Unable to resolve server battle size provider.";

        string role = ModInformation.IsServer ? "server" : "client";
        return $"Battle size ({role}): {provider.BattleSize}.";
    }

    [CommandLineArgumentFunction("set_runtime", "coop.debug.battle_size")]
    public static string SetRuntime(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";
        if (args.Count != 1
            || !int.TryParse(args[0], out var battleSize)
            || !ServerOptionsTabProvider.IsSupportedBattleSize(battleSize))
            return SetRuntimeUsage;
        if (!ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker))
            return "Unable to resolve message broker.";
        if (!ContainerProvider.TryResolve<IServerBattleSizeProvider>(out var provider))
            return "Unable to resolve server battle size provider.";

        messageBroker.Publish(typeof(BattleSizeDebugCommands), new ServerBattleSizeSelected(battleSize));
        return $"Server battle size set to {provider.BattleSize} for this runtime.";
    }
}
