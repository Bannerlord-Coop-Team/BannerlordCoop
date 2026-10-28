using Autofac;
using Common.Util;
using GameInterface.Services.Clans.Patches;
using System;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using Xunit;

namespace GameInterface.Tests.Services.Clans;

/// <summary>
/// Covers the clan screen identity patches leaving vanilla in charge when no coop container is active.
/// </summary>
[Collection(ContainerProviderCollection.Name)]
public class ClanManagementVMPatchesTests : IDisposable
{
    private readonly ILifetimeScope previousContainer;
    private readonly ClanManagementVM clanManagement = ObjectHelper.SkipConstructor<ClanManagementVM>();

    /// <summary>
    /// Clears the container so every test runs as a single-player load after a coop session.
    /// </summary>
    public ClanManagementVMPatchesTests()
    {
        ContainerProvider.TryGetContainer(out previousContainer);
        ContainerProvider.Clear();
    }

    /// <summary>
    /// Restores the container the test replaced.
    /// </summary>
    public void Dispose()
    {
        if (previousContainer != null) ContainerProvider.SetContainer(previousContainer);
    }

    /// <summary>
    /// Banner edits and renames stay available for the player's own clan outside a coop session.
    /// </summary>
    [Fact]
    public void IdentityPrefixes_NoContainer_RunVanilla()
    {
        Assert.True(ClanManagementVMPatches.ExecuteOpenBannerEditorPrefix(clanManagement));
        Assert.True(ClanManagementVMPatches.ExecuteChangeClanNamePrefix(clanManagement));
        Assert.True(ClanManagementVMPatches.OnChangeClanNameDonePrefix(clanManagement));
    }

    /// <summary>
    /// The refresh postfix leaves the vanilla banner and rename buttons enabled without a container.
    /// </summary>
    [Fact]
    public void RefreshValuesPostfix_NoContainer_KeepsVanillaIdentityControls()
    {
        clanManagement._canChooseBanner = true;
        clanManagement._playerCanChangeClanName = true;

        ClanManagementVMPatches.RefreshValuesPostfix(clanManagement);

        Assert.True(clanManagement.CanChooseBanner);
        Assert.True(clanManagement.PlayerCanChangeClanName);
    }
}
