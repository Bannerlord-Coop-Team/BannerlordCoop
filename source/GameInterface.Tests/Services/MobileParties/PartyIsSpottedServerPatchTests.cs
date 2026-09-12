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
/// Tests for the forced-spotted override. On the server (and when debug visibility is on) live
/// parties are always spotted, but a destroyed party must read as unspotted, or the map nameplate
/// machinery keeps re-creating its banner after the destruction replicates. Release clients keep
/// whatever the native spotting logic computed.
/// </summary>
[Collection(ModInformationRoleCollection.Name)]
public class PartyIsSpottedServerPatchTests : IDisposable
{
    private readonly bool wasServer;
    private readonly bool wasForceAllVisible;

    public PartyIsSpottedServerPatchTests()
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
    public void Postfix_OnServer_ActiveParty_IsSpotted()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.IsActive = true;
        ModInformation.IsServer = true;

        var result = false;
        PartyIsSpottedServerPatch.Postfix(party, ref result);

        Assert.True(result);
    }

    [Fact]
    public void Postfix_OnServer_DestroyedParty_IsNotSpotted()
    {
        // SkipConstructor leaves IsActive false, matching a party after RemoveParty ran.
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        ModInformation.IsServer = true;

        var result = true;
        PartyIsSpottedServerPatch.Postfix(party, ref result);

        Assert.False(result);
    }

    [Fact]
    public void Postfix_OnReleaseClient_KeepsNativeResult()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.IsActive = true;
        ModInformation.IsServer = false;

        var unspotted = false;
        PartyIsSpottedServerPatch.Postfix(party, ref unspotted);
        Assert.False(unspotted);

        var spotted = true;
        PartyIsSpottedServerPatch.Postfix(party, ref spotted);
        Assert.True(spotted);
    }

    [Fact]
    public void Postfix_DebugVisibility_ActiveParty_IsSpotted()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.IsActive = true;
        ModInformation.IsServer = false;
        DebugPartyVisibility.ForceAllVisible = true;

        var result = false;
        PartyIsSpottedServerPatch.Postfix(party, ref result);

        Assert.True(result);
    }
}
