using Autofac;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Locations;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Time.UI;
using GameInterface.Services.UI.PlayerNameplates;
using Missions;
using Missions.Battles;
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
    [InlineData(typeof(ICoopBattleMissionRegistry))]
    [InlineData(typeof(IBattleAgentAuthorityRegistry))]
    [InlineData(typeof(IPuppetSpawnTracker))]
    [InlineData(typeof(IRoutedBlowTracker))]
    [InlineData(typeof(IReplicatedDeathTracker))]
    [InlineData(typeof(IRoutedPlayerHitNotificationTracker))]
    public void BattlePatchStateIsSharedAcrossMissionScopes(Type serviceType)
    {
        var client = Clients.Single();
        client.Call(() =>
        {
            var patchState = client.Container.Resolve(serviceType);
            for (int i = 0; i < 2; i++)
            {
                using var mission = client.Container.BeginLifetimeScope(MissionLifetimeFactory.MissionTag);
                Assert.Same(patchState, mission.Resolve(serviceType));
            }
        });
    }

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
            if (controller is CoopBattleController battle)
            {
                nameplates = battle.PlayerNameplateView;
                mapTime = battle.MapTimeView;
            }
            else if (controller is CoopTournamentController tournament)
            {
                nameplates = tournament.PlayerNameplateView;
                mapTime = tournament.MapTimeView;
            }
            else
                throw new InvalidOperationException($"Unsupported mission controller: {controller.GetType().Name}");
        }
        var nameplateResolver = typeof(PlayerNameplateMissionView)
            .GetField("controllerResolver", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(nameplates)!;
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
        if (exitPath == 0) controller.OnEndMissionInternal();
        else if (exitPath == 1) controller.OnRemoveBehavior();
        else controller.Dispose();
        controller.OnEndMissionInternal();
        controller.OnRemoveBehavior();
        controller.Dispose();
        return references;
    }
}
