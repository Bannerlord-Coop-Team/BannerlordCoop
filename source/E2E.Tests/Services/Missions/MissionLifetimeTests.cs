using Autofac;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.ObjectManager;
using Missions;
using Missions.Agents;
using Missions.Battles;
using Missions.Services.Network;
using Missions.Taverns;
using Missions.Tournaments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>Exercises actual controller graphs while the client session stays alive.</summary>
public class MissionLifetimeTests : MissionTestEnvironment
{
    public MissionLifetimeTests(ITestOutputHelper output) : base(output, numClients: 1) { }

    [Theory]
    [InlineData(typeof(CoopBattleController), 0)]
    [InlineData(typeof(CoopBattleController), 1)]
    [InlineData(typeof(CoopBattleController), 2)]
    [InlineData(typeof(CoopLocationsController), 0)]
    [InlineData(typeof(CoopLocationsController), 1)]
    [InlineData(typeof(CoopLocationsController), 2)]
    [InlineData(typeof(CoopTournamentController), 0)]
    [InlineData(typeof(CoopTournamentController), 1)]
    [InlineData(typeof(CoopTournamentController), 2)]
    public void EndedMissionGraphsAreCollectibleBeforeSessionShutdown(Type controllerType, int exitPath)
    {
        var client = Clients.Single();
        var references = new List<WeakReference>();
        client.Call(() =>
        {
            for (int i = 0; i < 3; i++)
                references.AddRange(CreateAndEnd(client, controllerType, exitPath));
        });

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.All(references, reference => Assert.False(reference.IsAlive));
        GC.KeepAlive(client.Container);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] CreateAndEnd(EnvironmentInstance client, Type controllerType, int exitPath)
    {
        var controller = (CoopMissionController)client.Container.Resolve(controllerType);
        Assert.Same(client.Resolve<IMissionContext>(), controller.ResolveMissionBehavior<IMissionContext>());
        Assert.Same(client.Resolve<IBattleNetwork>(), controller.ResolveMissionBehavior<IBattleNetwork>());
        Assert.Same(client.Resolve<INetwork>(), controller.ResolveMissionBehavior<INetwork>());
        Assert.Same(client.Resolve<IMessageBroker>(), controller.ResolveMissionBehavior<IMessageBroker>());
        Assert.Same(client.Resolve<IPacketManager>(), controller.ResolveMissionBehavior<IPacketManager>());
        Assert.Same(client.Resolve<IObjectManager>(), controller.ResolveMissionBehavior<IObjectManager>());
        Assert.Same(client.Resolve<INetworkAgentRegistry>(), controller.ResolveMissionBehavior<INetworkAgentRegistry>());
        Assert.Same(client.Resolve<INetworkWorldItemRegistry>(), controller.ResolveMissionBehavior<INetworkWorldItemRegistry>());

        var component = (ICoopMissionComponent)typeof(CoopMissionController)
            .GetField("coopMissionComponent", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(controller)!;
        var references = new[]
        {
            new WeakReference(controller), new WeakReference(component),
            new WeakReference(component.AgentMovementHandler), new WeakReference(component.AgentActionHandler),
            new WeakReference(component.MissileHandler), new WeakReference(component.WeaponDropHandler),
            new WeakReference(component.WeaponPickupHandler), new WeakReference(component.AgentDeathHandler)
        };
        if (exitPath == 0) controller.OnEndMissionInternal();
        else if (exitPath == 1) controller.OnRemoveBehavior();
        else controller.Dispose();
        controller.OnEndMissionInternal();
        controller.OnRemoveBehavior();
        controller.Dispose();
        return references;
    }
}
