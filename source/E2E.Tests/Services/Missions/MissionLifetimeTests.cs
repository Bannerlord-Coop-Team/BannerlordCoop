using Autofac;
using Common.Messaging;
using Common.Util;
using Common.Network;
using Common.PacketHandlers;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Locations;
using GameInterface.Services.Time.UI;
using GameInterface.Services.UI.PlayerNameplates;
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
using TaleWorlds.MountAndBlade;
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
    [InlineData(typeof(CoopBattleController), 3)]
    [InlineData(typeof(CoopLocationsController), 0)]
    [InlineData(typeof(CoopLocationsController), 1)]
    [InlineData(typeof(CoopLocationsController), 2)]
    [InlineData(typeof(CoopLocationsController), 3)]
    [InlineData(typeof(CoopTournamentController), 0)]
    [InlineData(typeof(CoopTournamentController), 1)]
    [InlineData(typeof(CoopTournamentController), 2)]
    [InlineData(typeof(CoopTournamentController), 3)]
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
        CoopMissionController controller;
        PlayerNameplateMissionView nameplates;
        MissionMapTimeView mapTime;
        if (controllerType == typeof(CoopLocationsController))
        {
            var behaviors = client.Resolve<IEnumerable<ILocationMissionBehavior>>().ToArray();
            controller = behaviors.OfType<CoopLocationsController>().Single();
            nameplates = behaviors.OfType<PlayerNameplateMissionView>().Single();
            mapTime = behaviors.OfType<MissionMapTimeView>().Single();
        }
        else
        {
            controller = (CoopMissionController)client.Container.Resolve(controllerType);
            nameplates = controller.ResolveMissionBehavior<PlayerNameplateMissionView>();
            mapTime = controller.ResolveMissionBehavior<MissionMapTimeView>();
        }
        var nameplateResolver = typeof(PlayerNameplateMissionView)
            .GetField("controllerResolver", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(nameplates)!;
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
            new WeakReference(nameplates), new WeakReference(mapTime), new WeakReference(nameplateResolver),
            new WeakReference(component.AgentMovementHandler), new WeakReference(component.AgentActionHandler),
            new WeakReference(component.MissileHandler), new WeakReference(component.WeaponDropHandler),
            new WeakReference(component.WeaponPickupHandler), new WeakReference(component.AgentDeathHandler)
        };
        var registry = client.Resolve<INetworkAgentRegistry>();
        var priorAgent = ObjectHelper.SkipConstructor<Agent>();
        var priorId = Guid.NewGuid();
        if (exitPath == 3)
            Assert.True(registry.TryRegisterAgent("prior", "prior", "prior", priorId, 42, priorAgent));
        if (exitPath == 0) controller.OnEndMissionInternal();
        else if (exitPath == 1) controller.OnRemoveBehavior();
        else if (exitPath == 2) controller.Dispose();
        else controller.Abandon();
        controller.OnEndMissionInternal();
        controller.OnRemoveBehavior();
        controller.Dispose();
        if (exitPath == 3)
        {
            Assert.True(registry.TryGetAgentInfo(priorId, out _));
            registry.Clear();
        }
        return references;
    }
}
