using Common;
using Common.Util;
using GameInterface.Services.MobileParties;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Tests;
using System;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

/// <summary>
/// Inactive parties read hidden on every side so clients stop rebuilding their nameplates.
/// For active parties, the server and debug overrides force visibility and release clients keep the native result.
/// </summary>
[Collection(ModInformationRoleCollection.Name)]
public class PartyIsVisibleGetterPatchTests : IDisposable
{
    private readonly bool wasServer;
    private readonly bool wasForceAllVisible;

    public PartyIsVisibleGetterPatchTests()
    {
        wasServer = ModInformation.IsServer;
        wasForceAllVisible = DebugPartyVisibility.ForceAllVisible;

        // Debug test builds default the flag on; pin it off so each test controls one dimension.
        DebugPartyVisibility.ForceAllVisible = false;
    }

    public void Dispose()
    {
        ModInformation.IsServer = wasServer;
        DebugPartyVisibility.ForceAllVisible = wasForceAllVisible;
    }

    [Fact]
    public void Postfix_OnServer_ActiveParty_IsVisible()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.IsActive = true;
        ModInformation.IsServer = true;

        var result = false;
        PartyIsVisibleGetterPatch.Postfix(party, ref result);

        Assert.True(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Postfix_InactiveParty_IsNotVisible(bool isServer)
    {
        // SkipConstructor leaves IsActive false, matching a party after RemoveParty ran.
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        ModInformation.IsServer = isServer;

        var result = true;
        PartyIsVisibleGetterPatch.Postfix(party, ref result);

        Assert.False(result);
    }

    [Fact]
    public void Postfix_OnReleaseClient_KeepsNativeResult()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.IsActive = true;
        ModInformation.IsServer = false;

        var hidden = false;
        PartyIsVisibleGetterPatch.Postfix(party, ref hidden);
        Assert.False(hidden);

        var visible = true;
        PartyIsVisibleGetterPatch.Postfix(party, ref visible);
        Assert.True(visible);
    }

    [Fact]
    public void Postfix_DebugVisibility_ActiveParty_IsVisible()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.IsActive = true;
        ModInformation.IsServer = false;
        DebugPartyVisibility.ForceAllVisible = true;

        var result = false;
        PartyIsVisibleGetterPatch.Postfix(party, ref result);

        Assert.True(result);
    }
}
