using Common;
using Common.Util;
using GameInterface.Services.MobileParties;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Tests;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

/// <summary>
/// Tests for the forced-visibility setter overrides. The server (and debug visibility) coerces
/// every IsVisible write to the party's live state and every IsInspected write to true; release
/// clients keep the native fog-of-war values so distant parties actually hide.
/// </summary>
[Collection(ModInformationRoleCollection.Name)]
public class PartyVisibilityOnServerPatchTests : IDisposable
{
    private readonly bool wasServer;
    private readonly bool wasForceAllVisible;

    public PartyVisibilityOnServerPatchTests()
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
    public void PrefixIsVisible_OnServer_CoercesToLiveState()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.IsActive = true;
        ModInformation.IsServer = true;

        var value = false;
        PartyVisibilityOnServerPatch.PrefixIsVisible(party, ref value);

        Assert.True(value);
    }

    [Fact]
    public void PrefixIsVisible_OnServer_DestroyedParty_CoercesToHidden()
    {
        // SkipConstructor leaves IsActive false, matching a party after RemoveParty ran.
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        ModInformation.IsServer = true;

        var value = true;
        PartyVisibilityOnServerPatch.PrefixIsVisible(party, ref value);

        Assert.False(value);
    }

    [Fact]
    public void PrefixIsVisible_OnReleaseClient_KeepsNativeValue()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.IsActive = true;
        ModInformation.IsServer = false;

        var hidden = false;
        PartyVisibilityOnServerPatch.PrefixIsVisible(party, ref hidden);
        Assert.False(hidden);

        var visible = true;
        PartyVisibilityOnServerPatch.PrefixIsVisible(party, ref visible);
        Assert.True(visible);
    }

    [Fact]
    public void PrefixIsVisible_DebugVisibility_CoercesToLiveState()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.IsActive = true;
        ModInformation.IsServer = false;
        DebugPartyVisibility.ForceAllVisible = true;

        var value = false;
        PartyVisibilityOnServerPatch.PrefixIsVisible(party, ref value);

        Assert.True(value);
    }

    [Fact]
    public void PrefixIsInspected_OnServer_ForcesInspected()
    {
        ModInformation.IsServer = true;

        var value = false;
        PartyVisibilityOnServerPatch.PrefixIsInspected(ref value);

        Assert.True(value);
    }

    [Fact]
    public void PrefixIsInspected_OnReleaseClient_KeepsNativeValue()
    {
        ModInformation.IsServer = false;

        var value = false;
        PartyVisibilityOnServerPatch.PrefixIsInspected(ref value);

        Assert.False(value);
    }

    [Fact]
    public void PrefixIsInspected_DebugVisibility_ForcesInspected()
    {
        ModInformation.IsServer = false;
        DebugPartyVisibility.ForceAllVisible = true;

        var value = false;
        PartyVisibilityOnServerPatch.PrefixIsInspected(ref value);

        Assert.True(value);
    }
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void IsSpotted_ReleaseClientRequiresActiveAndNativeVisibility(bool active, bool nativeVisible, bool expected)
    {
        ModInformation.IsServer = false;
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.IsActive = active;
        var result = nativeVisible;

        PartyIsSpottedServerPatch.Postfix(party, ref result);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(false, true, false, true, 1)]
    [InlineData(false, true, false, false, 1)]
    [InlineData(false, false, true, true, 1)]
    [InlineData(false, false, true, false, 1)]
    [InlineData(false, true, true, true, 0)]
    [InlineData(false, false, false, false, 0)]
    [InlineData(true, true, false, true, 0)]
    [InlineData(true, false, true, false, 0)]
    public void ActivationChange_NotifiesClientNameplatesWithoutChangingNativeVisibility(
        bool server, bool before, bool after, bool nativeVisible, int expectedEvents)
    {
        var previousCampaign = Campaign.Current;
        var campaign = ObjectHelper.SkipConstructor<Campaign>();
        var receiver = new VisibilityReceiver();
        campaign.CampaignEventDispatcher = new CampaignEventDispatcher(new[] { receiver });
        Campaign.Current = campaign;
        try
        {
            ModInformation.IsServer = server;
            var party = ObjectHelper.SkipConstructor<MobileParty>();
            party.Party = ObjectHelper.SkipConstructor<PartyBase>();
            party.IsActive = before;
            party._isVisible = nativeVisible;
            PartyVisibilityOnServerPatch.PrefixIsActive(party, out var previousActive);

            party.IsActive = after;
            PartyVisibilityOnServerPatch.PostfixIsActive(party, previousActive);

            Assert.Equal(expectedEvents, receiver.Events);
            if (expectedEvents != 0) Assert.Same(party.Party, receiver.Party);
            Assert.Equal(nativeVisible, party.IsVisible);
        }
        finally
        {
            Campaign.Current = previousCampaign;
        }
    }

    private sealed class VisibilityReceiver : CampaignEventReceiver
    {
        public int Events { get; private set; }
        public PartyBase Party { get; private set; }

        public override void OnPartyVisibilityChanged(PartyBase party)
        {
            Events++;
            Party = party;
        }
    }

}
