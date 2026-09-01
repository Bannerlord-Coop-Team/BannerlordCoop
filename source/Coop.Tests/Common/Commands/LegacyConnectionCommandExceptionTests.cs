#if DEBUG
using Autofac;
using Common;
using Coop.Core.Client;
using Coop.Core.Common.Commands;
using GameInterface;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.CommandTests;

[Collection(ModInformationRoleCollection.Name)]
public class LegacyConnectionCommandExceptionTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;
    private IContainer sessionContainer;

    public void Dispose()
    {
        ContainerProvider.Clear();
        sessionContainer?.Dispose();
        ProcessLifetimeClientSessionStarter.Reset();
        ModInformation.IsServer = wasServer;
    }

    [Fact]
    public void LifecycleCommands_DocumentOnlyStartAndReconnectAsLegacyExceptions()
    {
        Assert.Equal("coop.debug.connection.start",
            $"{LegacyConnectionCommandExceptions.Prefix}.{LegacyConnectionCommandExceptions.StartName}");
        Assert.Equal("coop.debug.connection.reconnect",
            $"{LegacyConnectionCommandExceptions.Prefix}.{LegacyConnectionCommandExceptions.ReconnectName}");

        AssertLegacyAttribute(nameof(LegacyConnectionCommands.Start));
        AssertLegacyAttribute(nameof(LegacyConnectionCommands.Reconnect));

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

    [Fact]
    public void Reconnect_WhileSessionExists_ReconnectsExistingClientLogic()
    {
        ModInformation.IsServer = false;
        var clientLogic = new Mock<IClientLogic>();
        int processStarts = 0;
        ProcessLifetimeClientSessionStarter.Configure(() =>
        {
            processStarts++;
            return true;
        });
        var builder = new ContainerBuilder();
        builder.RegisterInstance(clientLogic.Object).As<IClientLogic>();
        sessionContainer = builder.Build();
        ContainerProvider.SetContainer(sessionContainer);

        string result = LegacyConnectionCommands.Reconnect(new List<string>());

        Assert.Equal("Client session is reconnecting to the configured server.", result);
        clientLogic.Verify(logic => logic.Connect(), Times.Once);
        Assert.Equal(0, processStarts);
    }

    [Fact]
    public void Reconnect_AfterSessionTeardown_StartsNewClientSession()
    {
        ModInformation.IsServer = false;
        int processStarts = 0;
        ProcessLifetimeClientSessionStarter.Configure(() =>
        {
            processStarts++;
            return true;
        });
        ContainerProvider.Clear();

        string result = LegacyConnectionCommands.Reconnect(new List<string>());

        Assert.Equal("Client co-op session restarted after teardown.", result);
        Assert.Equal(1, processStarts);
    }

    [Fact]
    public void Start_WithoutSession_StartsNewClientSession()
    {
        ModInformation.IsServer = false;
        int processStarts = 0;
        ProcessLifetimeClientSessionStarter.Configure(() =>
        {
            processStarts++;
            return true;
        });
        ContainerProvider.Clear();

        string result = LegacyConnectionCommands.Start(new List<string>());

        Assert.Equal("Client co-op connection started.", result);
        Assert.Equal(1, processStarts);
    }

    private static void AssertLegacyAttribute(string methodName)
    {
        MethodInfo method = typeof(LegacyConnectionCommands).GetMethod(methodName)!;
        Assert.NotNull(method.GetCustomAttribute<CommandLineFunctionality.CommandLineArgumentFunction>());
    }
}
#endif
