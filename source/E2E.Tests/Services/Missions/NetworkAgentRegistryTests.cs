using Common.Util;
using GameInterface.Services.Entity;
using Missions;
using Moq;
using System;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Unit tests for <see cref="NetworkAgentRegistry.TryTransferAuthority"/> — the per-agent authority move that
/// underpins host migration (a successor adopting the old host's agents) and control transfer. Uses an
/// uninitialized <see cref="Agent"/> as a registry key; no agent behaviour is exercised, so no live mission
/// is needed.
/// </summary>
public class NetworkAgentRegistryTests
{
    private static NetworkAgentRegistry NewRegistry(string localControllerId)
    {
        var provider = new Mock<IControllerIdProvider>();
        provider.SetupGet(p => p.ControllerId).Returns(localControllerId);
        return new NetworkAgentRegistry(provider.Object);
    }

    private static (NetworkAgentRegistry registry, Agent agent, Guid id) RegisterAgent(string ownerControllerId, string localControllerId)
    {
        var registry = NewRegistry(localControllerId);
        var agent = ObjectHelper.SkipConstructor<Agent>();
        var id = Guid.NewGuid();

        Assert.True(registry.TryRegisterAgent(ownerControllerId, id, agent));
        return (registry, agent, id);
    }

    [Fact]
    public void TransferAuthority_MovesCurrentAuthority_PreservingOriginalOwner()
    {
        var (registry, _, id) = RegisterAgent(ownerControllerId: "host", localControllerId: "me");

        Assert.True(registry.TryGetAgentInfo(id, out var before));
        Assert.Equal("host", before.CurrentAuthority);
        Assert.Equal("host", before.OriginalOwner);

        Assert.True(registry.TryTransferAuthority("me", id));

        Assert.True(registry.TryGetAgentInfo(id, out var after));
        Assert.Equal("me", after.CurrentAuthority);   // authority moved to the successor
        Assert.Equal("host", after.OriginalOwner);     // original owner is preserved
    }

    [Fact]
    public void TransferAuthority_ToLocalController_MakesAgentLocallyControlled()
    {
        var (registry, agent, id) = RegisterAgent(ownerControllerId: "host", localControllerId: "me");

        Assert.False(registry.IsLocallyControlled(id));

        Assert.True(registry.TryTransferAuthority("me", id));

        Assert.True(registry.IsLocallyControlled(id));
        Assert.True(registry.IsLocallyControlled(agent));
    }

    [Fact]
    public void TransferAuthority_ReindexesControllerLists()
    {
        var (registry, _, id) = RegisterAgent(ownerControllerId: "host", localControllerId: "me");

        Assert.Single(registry.GetAgents("host"));
        Assert.Empty(registry.GetAgents("me"));

        Assert.True(registry.TryTransferAuthority("me", id));

        Assert.Empty(registry.GetAgents("host"));
        Assert.Single(registry.GetAgents("me"));
    }

    [Fact]
    public void TransferAuthority_IsIdempotent_WhenTargetAlreadyHoldsIt()
    {
        var (registry, _, id) = RegisterAgent(ownerControllerId: "host", localControllerId: "me");

        Assert.True(registry.TryTransferAuthority("host", id));

        Assert.True(registry.TryGetAgentInfo(id, out var info));
        Assert.Equal("host", info.CurrentAuthority);
    }

    [Fact]
    public void TransferAuthority_ForUnknownAgent_Fails()
    {
        var registry = NewRegistry(localControllerId: "me");

        Assert.False(registry.TryTransferAuthority("me", Guid.NewGuid()));
    }

    [Fact]
    public void CompactMovementIdentity_StaysInMovementScopeAfterTransfer()
    {
        var registry = NewRegistry(localControllerId: "successor");
        var agent = ObjectHelper.SkipConstructor<Agent>();
        Guid agentId = Guid.NewGuid();

        Assert.True(registry.TryRegisterAgent("host", agentId, 42, agent));
        Assert.True(registry.TryTransferAuthority("successor", agentId));

        Assert.True(registry.TryGetAgentInfo("host", 42, out var resolved));
        Assert.Same(agent, resolved.Agent);
        Assert.Equal("host", resolved.OriginalOwner);
        Assert.Equal("host", resolved.MovementScopeId);
        Assert.Equal("successor", resolved.CurrentAuthority);
    }

    [Fact]
    public void CompactMovementIdentity_IsUniqueWithinMovementScope()
    {
        var registry = NewRegistry(localControllerId: "me");

        Assert.True(registry.TryRegisterAgent(
            "host", "host", "host:epoch-1", Guid.NewGuid(), 7,
            ObjectHelper.SkipConstructor<Agent>()));
        Assert.False(registry.TryRegisterAgent(
            "host", "host", "host:epoch-1", Guid.NewGuid(), 7,
            ObjectHelper.SkipConstructor<Agent>()));
        Assert.True(registry.TryRegisterAgent(
            "host", "host", "host:epoch-2", Guid.NewGuid(), 7,
            ObjectHelper.SkipConstructor<Agent>()));
    }

    [Fact]
    public void CatchUpRegistration_PreservesSeparateRiderAndMountMovementScopes()
    {
        var registry = NewRegistry(localControllerId: "observer");
        var rider = ObjectHelper.SkipConstructor<Agent>();
        var mount = ObjectHelper.SkipConstructor<Agent>();

        Assert.True(registry.TryRegisterAgent(
            "successor", "rider-origin", "rider-origin:epoch-1",
            Guid.NewGuid(), 11, rider));
        Assert.True(registry.TryRegisterAgent(
            "successor", "mount-origin", "mount-origin:epoch-4",
            Guid.NewGuid(), 9, mount));

        Assert.True(registry.TryGetAgentInfo("rider-origin:epoch-1", 11, out var resolvedRider));
        Assert.Same(rider, resolvedRider.Agent);
        Assert.Equal("rider-origin", resolvedRider.OriginalOwner);
        Assert.Equal("successor", resolvedRider.CurrentAuthority);

        Assert.True(registry.TryGetAgentInfo("mount-origin:epoch-4", 9, out var resolvedMount));
        Assert.Same(mount, resolvedMount.Agent);
        Assert.Equal("mount-origin", resolvedMount.OriginalOwner);
        Assert.Equal("successor", resolvedMount.CurrentAuthority);
    }
}
