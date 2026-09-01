#if DEBUG
using Common;
using Coop.Core.Client;
using GameInterface;
using System;
using System.Collections.Generic;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace Coop.Core.Common.Commands;

/// <summary>
/// Connection commands that must remain available while no session container exists.
/// </summary>
public static class LegacyConnectionCommands
{
    [CommandLineArgumentFunction(
        LegacyConnectionCommandExceptions.StartName,
        LegacyConnectionCommandExceptions.Prefix)]
    public static string Start(List<string> args)
    {
        if (args.Count != 0)
        {
            return "Usage: coop.debug.connection.start";
        }
        if (ModInformation.IsServer)
        {
            return "start must be run on a client.";
        }
        if (ContainerProvider.TryResolve<global::Common.LogicStates.ILogic>(out _))
        {
            return "Client co-op connection is already starting or running.";
        }

        StartClientSession();
        return "Client co-op connection started.";
    }

    [CommandLineArgumentFunction(
        LegacyConnectionCommandExceptions.ReconnectName,
        LegacyConnectionCommandExceptions.Prefix)]
    public static string Reconnect(List<string> args)
    {
        if (args.Count != 0)
        {
            return "Usage: coop.debug.connection.reconnect";
        }
        if (ModInformation.IsServer)
        {
            return "reconnect must be run on a client.";
        }
        if (ContainerProvider.TryResolve<IClientLogic>(out var clientLogic))
        {
            clientLogic.Connect();
            return "Client session is reconnecting to the configured server.";
        }

        StartClientSession();
        return "Client co-op session restarted after teardown.";
    }

    private static void StartClientSession()
    {
        if (!ProcessLifetimeClientSessionStarter.Start())
        {
            throw new InvalidOperationException("Client co-op connection start was refused.");
        }
    }
}
#endif
