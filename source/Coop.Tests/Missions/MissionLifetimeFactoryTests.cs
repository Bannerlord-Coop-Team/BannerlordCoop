using Autofac;
using Autofac.Core;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Missions;
using Missions.Agents.Handlers;
using Missions.Messages;
using Missions.Missiles.Handlers;
using Moq;
using System;
using Xunit;

namespace Coop.Tests.Missions;

/// <summary>Checks scope ownership independently of native mission startup.</summary>
public class MissionLifetimeFactoryTests
{
    [Fact]
    public void RepeatedTeardownReleasesScopeOnceAndNextMissionGetsNewDependencies()
    {
        using var session = BuildSession();
        var factory = session.Resolve<IMissionLifetimeFactory>();
        var first = factory.Create<ProbeController>(Array.Empty<Parameter>());
        var firstDependency = first.Dependency;
        first.OnEndMissionInternal();
        first.OnRemoveBehavior();
        first.Dispose();
        Assert.Equal(1, firstDependency.Disposals);
        using var second = factory.Create<ProbeController>(Array.Empty<Parameter>());
        Assert.NotSame(firstDependency, second.Dependency);
        Assert.Equal(0, second.Dependency.Disposals);
    }

    [Fact]
    public void FailedControllerConstructionDisposesAlreadyResolvedDependencies()
    {
        using var session = BuildSession();
        ProbeDependency dependency = null!;
        session.ChildLifetimeScopeBeginning += (_, args) =>
            dependency = args.LifetimeScope.Resolve<ProbeDependency>();
        var factory = session.Resolve<IMissionLifetimeFactory>();
        Assert.Throws<DependencyResolutionException>(() => factory.Create<FailingController>(Array.Empty<Parameter>()));
        Assert.Equal(1, dependency.Disposals);
    }

    [Fact]
    public void FailedPartialStartStillReleasesDependencies()
    {
        using var session = BuildSession();
        var controller = session.Resolve<IMissionLifetimeFactory>()
            .Create<ProbeController>(Array.Empty<Parameter>());
        controller.FailLeaving = true;
        Assert.Throws<InvalidOperationException>(() => controller.OnEndMissionInternal());
        Assert.Equal(1, controller.Dependency.Disposals);
        controller.Dispose();
        Assert.Equal(1, controller.Dependency.Disposals);
    }

    [Fact]
    public void SessionShutdownDisposesAnUnfinishedMission()
    {
        var session = BuildSession();
        var controller = session.Resolve<IMissionLifetimeFactory>()
            .Create<ProbeController>(Array.Empty<Parameter>());
        session.Dispose();
        Assert.Equal(1, controller.Dependency.Disposals);
        controller.Dispose();
        Assert.Equal(1, controller.Dependency.Disposals);
    }

    [Fact]
    public void SessionShutdownEndsMissionBeforeDisposingLaterSessionDependencies()
    {
        var session = BuildSession();
        var controller = session.Resolve<IMissionLifetimeFactory>()
            .Create<ProbeController>(Array.Empty<Parameter>());
        var dependency = session.Resolve<ProbeDependency>();
        controller.Leaving = () => Assert.Equal(0, dependency.Disposals);
        session.Dispose();
        Assert.True(controller.Left);
        Assert.Equal(1, dependency.Disposals);
    }

    [Fact]
    public void FailedBaseConstructionRemovesAlreadyRegisteredSubscriptions()
    {
        var broker = new Mock<IMessageBroker>();
        broker.Setup(x => x.Subscribe(It.IsAny<Action<MessagePayload<NetworkMissionJoinInfo>>>()))
            .Throws(new InvalidOperationException("subscription failed"));
        Assert.Throws<InvalidOperationException>(() => new ProbeController(new ProbeDependency(), broker.Object));
        broker.Verify(x => x.Unsubscribe(It.IsAny<Action<MessagePayload<NetworkMissionPeerEntered>>>()), Times.Once);
        broker.Verify(x => x.Unsubscribe(It.IsAny<Action<MessagePayload<NetworkMissionJoinInfo>>>()), Times.Once);
    }

    [Fact]
    public void AbandonedGraphReleasesDependenciesWithoutLeavingTheActiveMission()
    {
        using var session = BuildSession();
        var controller = session.Resolve<IMissionLifetimeFactory>()
            .Create<ProbeController>(Array.Empty<Parameter>());
        controller.Abandon();
        controller.OnRemoveBehavior();
        controller.Dispose();
        Assert.False(controller.Left);
        Assert.Equal(1, controller.Dependency.Disposals);
    }

    private static IContainer BuildSession()
    {
        var builder = new ContainerBuilder();
        builder.RegisterType<MissionLifetimeFactory>().As<IMissionLifetimeFactory>().SingleInstance();
        builder.RegisterType<ProbeDependency>().InstancePerLifetimeScope();
        builder.RegisterType<ProbeController>().Keyed<ProbeController>(MissionLifetimeFactory.MissionTag)
            .InstancePerMatchingLifetimeScope(MissionLifetimeFactory.MissionTag).ExternallyOwned();
        builder.RegisterType<FailingController>().Keyed<FailingController>(MissionLifetimeFactory.MissionTag)
            .InstancePerMatchingLifetimeScope(MissionLifetimeFactory.MissionTag).ExternallyOwned();
        return builder.Build();
    }

    /// <summary>Records whether its owning mission scope released it.</summary>
    public sealed class ProbeDependency : IDisposable
    {
        public int Disposals { get; private set; }
        public void Dispose() => Disposals++;
    }

    /// <summary>Exercises production teardown without creating native mission state.</summary>
    public class ProbeController : CoopMissionController
    {
        public ProbeDependency Dependency { get; }
        public bool FailLeaving { get; set; }
        public Action? Leaving { get; set; }
        public bool Left { get; private set; }

        public ProbeController(ProbeDependency dependency, IMessageBroker? broker = null)
            : base(Mock.Of<IBattleNetwork>(), broker ?? Mock.Of<IMessageBroker>(), Mock.Of<IObjectManager>(),
                NewComponent(), MovementCadenceProfile.Battle)
        {
            Dependency = dependency;
        }

        protected override void OnLeaving()
        {
            Leaving?.Invoke();
            Left = true;
            if (FailLeaving) throw new InvalidOperationException("partial mission start");
        }

        protected override void SendJoinInfo(string controllerId) { }
        protected override void HandleJoinInfo(NetPeer peer, NetworkMissionJoinInfo joinInfo) { }

        private static ICoopMissionComponent NewComponent()
        {
            var component = new Mock<ICoopMissionComponent>();
            component.SetupGet(x => x.AgentRegistry).Returns(Mock.Of<INetworkAgentRegistry>());
            component.SetupGet(x => x.AgentMovementHandler).Returns(Mock.Of<IAgentMovementHandler>());
            component.SetupGet(x => x.AgentActionHandler).Returns(Mock.Of<IAgentActionHandler>());
            component.SetupGet(x => x.AgentVoiceHandler).Returns(Mock.Of<IAgentVoiceHandler>());
            component.SetupGet(x => x.MissileHandler).Returns(Mock.Of<IMissileHandler>());
            component.SetupGet(x => x.WeaponDropHandler).Returns(Mock.Of<IWeaponDropHandler>());
            component.SetupGet(x => x.WeaponPickupHandler).Returns(Mock.Of<IWeaponPickupHandler>());
            component.SetupGet(x => x.ShieldDamageHandler).Returns(Mock.Of<IShieldDamageHandler>());
            component.SetupGet(x => x.CombatHitPresentationHandler).Returns(Mock.Of<ICombatHitPresentationHandler>());
            component.SetupGet(x => x.AgentDeathHandler).Returns(Mock.Of<IAgentDeathHandler>());
            return component.Object;
        }
    }

    /// <summary>Fails after Autofac has created a mission-owned dependency.</summary>
    public sealed class FailingController : ProbeController
    {
        public FailingController(ProbeDependency dependency) : base(dependency)
        {
            throw new InvalidOperationException("controller construction failed");
        }
    }
}
