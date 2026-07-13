using Common.Messaging;
using GameInterface.Services.Entity;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using Xunit;

namespace Coop.Tests.Missions;

/// <summary>
/// Unit tests for <see cref="MissionContext"/> — the instance scoping of the client-side membership mirror.
/// The context lives for the whole session while missions come and go, so membership must begin/end with the
/// instance and introductions for any other instance must be ignored; otherwise members of a finished mission
/// linger and every broadcast of the next one is relayed at them (the server-side
/// "Failed to get peer for instance" spam).
/// </summary>
public class MissionContextTests
{
    private readonly MessageBroker messageBroker = new();
    private readonly Mock<IControllerIdProvider> controllerIdProvider = new();
    private readonly MissionContext context;

    public MissionContextTests()
    {
        controllerIdProvider.SetupGet(p => p.ControllerId).Returns("us");
        context = new MissionContext(messageBroker, controllerIdProvider.Object);
    }

    private void Introduce(string controllerId, string instanceId) =>
        messageBroker.Publish(this, new NetworkMissionPeerEntered(controllerId, instanceId));

    [Fact]
    public void PeerEntered_ForTheCurrentInstance_IsMirrored()
    {
        context.BeginInstance("instance1");

        Introduce("them", "instance1");

        Assert.Equal(new[] { "them" }, context.ControllersInMission);
    }

    [Fact]
    public void PeerEntered_ForAnotherInstance_IsIgnored()
    {
        context.BeginInstance("instance1");

        Introduce("them", "instance2");

        Assert.Empty(context.ControllersInMission);
    }

    [Fact]
    public void PeerEntered_WhileNotInAnInstance_IsIgnored()
    {
        Introduce("them", "instance1");

        Assert.Empty(context.ControllersInMission);
    }

    [Fact]
    public void PeerEntered_WithoutAnInstanceId_IsAccepted()
    {
        // Null is tolerated as a wildcard for locally published legacy/test messages — the server
        // fan-out always carries the instance id.
        context.BeginInstance("instance1");

        Introduce("them", null);

        Assert.Equal(new[] { "them" }, context.ControllersInMission);
    }

    [Fact]
    public void BeginInstance_DropsMembershipOfThePreviousInstance()
    {
        // The reported failure: members of a finished battle lingered into the next one, so every
        // broadcast was relayed at a controller the server had no mapping for in the new instance.
        context.BeginInstance("battle1");
        Introduce("them", "battle1");

        context.BeginInstance("battle2");

        Assert.Empty(context.ControllersInMission);
    }

    [Fact]
    public void BeginInstance_ForTheSameInstance_KeepsMembership()
    {
        // Entry handlers can fire more than once per mission; a repeat must not wipe the gathered set.
        context.BeginInstance("instance1");
        Introduce("them", "instance1");

        context.BeginInstance("instance1");

        Assert.Equal(new[] { "them" }, context.ControllersInMission);
    }

    [Fact]
    public void EndInstance_DropsMembership_AndStopsAcceptingIntroductions()
    {
        context.BeginInstance("instance1");
        Introduce("them", "instance1");

        context.EndInstance();

        Assert.Empty(context.ControllersInMission);

        Introduce("straggler", "instance1");
        Assert.Empty(context.ControllersInMission);
    }

    [Fact]
    public void PeerLeft_Removes_RegardlessOfInstance()
    {
        // Departures are unfiltered on purpose: removing heals a stale entry no matter where it leaked from.
        context.BeginInstance("instance1");
        Introduce("them", "instance1");

        messageBroker.Publish(this, new MissionPeerLeft("them", "instance2"));

        Assert.Empty(context.ControllersInMission);
    }

    [Fact]
    public void PeerDisconnected_Removes_RegardlessOfInstance()
    {
        context.BeginInstance("instance1");
        Introduce("them", "instance1");

        messageBroker.Publish(this, new MissionPeerDisconnected("them", "instance2"));

        Assert.Empty(context.ControllersInMission);
    }

    [Fact]
    public void OwnController_IsExcludedFromTheView()
    {
        context.BeginInstance("instance1");

        Introduce("us", "instance1");
        Introduce("them", "instance1");

        Assert.Equal(new[] { "them" }, context.ControllersInMission);
    }
}
