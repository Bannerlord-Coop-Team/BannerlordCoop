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
/// Tests for the forced-visible override on the IsVisible getter. On the server (and when debug visibility is on) live
/// parties are always visible, but a destroyed party must read as hidden, or the map nameplate
/// machinery keeps re-creating its banner after the destruction replicates. Release clients keep
/// whatever the native visibility logic computed.
/// </summary>
[Collection(ModInformationRoleCollection.Name)]
public class PartyIsVisibleGetterServerPatchTests : IDisposable
{
    private readonly bool wasServer;
    private readonly bool wasForceAllVisible;

    public PartyIsVisibleGetterServerPatchTests()
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
        PartyIsVisibleGetterServerPatch.Postfix(party, ref result);

        Assert.True(result);
    }

    [Fact]
    public void Postfix_OnServer_DestroyedParty_IsNotVisible()
    {
        // SkipConstructor leaves IsActive false, matching a party after RemoveParty ran.
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        ModInformation.IsServer = true;

        var result = true;
        PartyIsVisibleGetterServerPatch.Postfix(party, ref result);

        Assert.False(result);
    }

    [Fact]
    public void Postfix_OnReleaseClient_KeepsNativeResult()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.IsActive = true;
        ModInformation.IsServer = false;

        var hidden = false;
        PartyIsVisibleGetterServerPatch.Postfix(party, ref hidden);
        Assert.False(hidden);

        var visible = true;
        PartyIsVisibleGetterServerPatch.Postfix(party, ref visible);
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
        PartyIsVisibleGetterServerPatch.Postfix(party, ref result);

        Assert.True(result);
    }
}
