using Autofac;
using Common;
using GameInterface.Services.Clans.Interfaces;
using GameInterface.Services.Clans.Patches.Disable;
using Moq;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using Xunit;

namespace GameInterface.Tests.Services.Clans;

/// <summary>
/// Covers the clan daily tick prefix, the single owner of <see cref="ClanVariablesCampaignBehavior.DailyTickClan"/>.
/// </summary>
[Collection(ContainerProviderCollection.Name)]
public class ClanVariablesCampaignBehaviorPatchesTests : IDisposable
{
    private readonly ILifetimeScope previousContainer;
    private readonly bool wasServer = ModInformation.IsServer;
    private readonly Mock<IClanVariablesCampaignBehaviorInterface> clanVariables = new();

    /// <summary>
    /// Clears the container so each test starts outside a coop session.
    /// </summary>
    public ClanVariablesCampaignBehaviorPatchesTests()
    {
        ContainerProvider.TryGetContainer(out previousContainer);
        ContainerProvider.Clear();
    }

    /// <summary>
    /// Restores the container and role the test replaced.
    /// </summary>
    public void Dispose()
    {
        ModInformation.IsServer = wasServer;
        if (previousContainer != null) ContainerProvider.SetContainer(previousContainer);
        else ContainerProvider.Clear();
    }

    /// <summary>
    /// A single-player load after a coop session has no container, so vanilla runs the clan tick.
    /// </summary>
    [Fact]
    public void DailyTickClanPrefix_NoContainer_RunsVanilla()
    {
        Assert.True(ClanVariablesCampaignBehaviorPatches.DailyTickClanPrefix(null, null));
    }

    /// <summary>
    /// In a session the server runs the coop clan tick and clients skip both it and vanilla.
    /// </summary>
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void DailyTickClanPrefix_InSession_RunsCoopTickOnServerOnly(bool isServer, int expectedTicks)
    {
        UseSession(isServer);

        bool runOriginal = ClanVariablesCampaignBehaviorPatches.DailyTickClanPrefix(null, null);

        Assert.False(runOriginal);
        clanVariables.Verify(c => c.DailyTickClan(It.IsAny<ClanVariablesCampaignBehavior>(), It.IsAny<Clan>()), Times.Exactly(expectedTicks));
    }

    /// <summary>
    /// Sets the role and a container holding the mocked clan tick service.
    /// </summary>
    private void UseSession(bool isServer)
    {
        ModInformation.IsServer = isServer;
        var builder = new ContainerBuilder();
        builder.RegisterInstance(clanVariables.Object).As<IClanVariablesCampaignBehaviorInterface>();
        ContainerProvider.SetContainer(builder.Build());
    }
}
