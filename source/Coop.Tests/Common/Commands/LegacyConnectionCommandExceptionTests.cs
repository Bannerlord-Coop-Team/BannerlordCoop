#if DEBUG
using Coop.Core.Common.Commands;
using System.Reflection;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.CommandTests;

public class LegacyConnectionCommandExceptionTests
{
    [Fact]
    public void LifecycleCommands_DocumentOnlyStartAndReconnectAsLegacyExceptions()
    {
        Assert.Equal("coop.debug.connection.start",
            $"{LegacyConnectionCommandExceptions.Prefix}.{LegacyConnectionCommandExceptions.StartName}");
        Assert.Equal("coop.debug.connection.reconnect",
            $"{LegacyConnectionCommandExceptions.Prefix}.{LegacyConnectionCommandExceptions.ReconnectName}");

        MethodInfo reconnect = typeof(JoinDebugCommands).GetMethod(nameof(JoinDebugCommands.Reconnect))!;
        Assert.NotNull(reconnect.GetCustomAttribute<CommandLineFunctionality.CommandLineArgumentFunction>());

        string[] migratedMethods =
        {
            nameof(JoinDebugCommands.JoinState),
            nameof(JoinDebugCommands.ArmInactivePartyDeficit),
            nameof(JoinDebugCommands.StageInactiveParty),
            nameof(JoinDebugCommands.RestoreInactiveParty),
            nameof(JoinDebugCommands.Disconnect),
        };
        Assert.All(migratedMethods, methodName =>
        {
            MethodInfo method = typeof(JoinDebugCommands).GetMethod(methodName)!;
            Assert.Empty(method.GetCustomAttributes<CommandLineFunctionality.CommandLineArgumentFunction>());
        });
    }
}
#endif
