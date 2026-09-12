#if DEBUG
using Common;
using Common.Commands;
using Coop.Core.Common.Commands;
using GameInterface;
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Commands;

[Collection(ModInformationRoleCollection.Name)]
public class ConnectionDirectCommandTests
{
    [Fact]
    public void SessionCommands_AreDirectCommandsInTheOwningType()
    {
        Type[] commandTypes = typeof(JoinDebugCommands).GetNestedTypes(BindingFlags.Public)
            .Where(type => typeof(ICoopCommand).IsAssignableFrom(type))
            .ToArray();

        Assert.Equal(5, commandTypes.Length);
        Assert.All(commandTypes, type =>
        {
            Assert.Equal(typeof(object), type.BaseType);
            Assert.Equal(new[] { typeof(ICoopCommand) }, type.GetInterfaces());
            Assert.EndsWith("CoopCommand", type.Name);
        });
    }

    [Fact]
    public void Reconnect_RemainsAProcessLifetimeAttributedCommand()
    {
        MethodInfo method = typeof(JoinDebugCommands).GetMethod(
            nameof(JoinDebugCommands.Reconnect),
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(method);
        Assert.True(method.IsDefined(
            typeof(CommandLineFunctionality.CommandLineArgumentFunction),
            inherit: false));
    }

    [Fact]
    public void ReconnectWithoutSession_RestartsThroughProcessLifetimeStarter()
    {
        bool wasServer = ModInformation.IsServer;
        bool started = false;
        try
        {
            ModInformation.IsServer = false;
            ContainerProvider.Clear();
            JoinDebugCommands.ConfigureClientSessionStarter(() =>
            {
                started = true;
                return true;
            });

            string output = JoinDebugCommands.Reconnect(new System.Collections.Generic.List<string>());

            Assert.True(started);
            Assert.Equal("Client co-op session restarted after teardown.", output);
        }
        finally
        {
            JoinDebugCommands.ResetClientSessionStarter();
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void DisconnectServerRejection_IsExplicitFailure()
    {
        bool wasServer = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = true;
            var command = new JoinDebugCommands.DisconnectCoopCommand();

            CoopCommandResult result = command.ProcessCommand(
                new CoopCommandArgsFactory().FromValues(Array.Empty<string>()));

            Assert.False(result.Succeeded);
            Assert.Equal("command_failed", result.ErrorCode);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }
}
#endif
