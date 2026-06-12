using Common;
using Common.Util;
using GameInterface.Services.MobileParties.Patches;
using System;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

/// <summary>
/// Tests for the forced-spotted override. On the server (and in debug sessions) live parties are
/// always spotted, but a destroyed party must read as unspotted, or the map nameplate machinery
/// keeps re-creating its banner after the destruction replicates.
/// </summary>
public class PartyIsSpottedServerPatchTests : IDisposable
{
    private readonly bool wasServer;

    public PartyIsSpottedServerPatchTests()
    {
        wasServer = ModInformation.IsServer;
    }

    public void Dispose()
    {
        ModInformation.IsServer = wasServer;
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
}
