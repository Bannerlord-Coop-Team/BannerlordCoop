#if COOP_NAVAL
using Common.Util;
using Coop.Naval.PartyVisuals.Patches;
using HarmonyLib;
using NavalDLC.View.Map.Visuals;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace Coop.Tests.Naval;

/// <summary>
/// Covers dropping a stale raft model from a party's naval visual when the party leaves raft state.
/// </summary>
public class NavalRaftExitVisualPatchesTests
{
    // The postfix must bind to the installed game's IsInRaftState setter, otherwise the raft exit crash comes back silently.
    [Fact]
    public void Patch_BindsToTheIsInRaftStateSetter()
    {
        var harmony = new Harmony("coop.tests.naval.raft_exit_visual");
        try
        {
            Assert.NotEmpty(harmony.CreateClassProcessor(typeof(NavalRaftExitVisualPatches)).Patch());
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    // Only a raft model left on a party that is no longer on a raft is dropped.
    [Theory]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    public void ShouldDropRaftVisual_OnlyForARaftModelOfAPartyOffItsRaft(bool partyInRaftState, bool visualInRaftState, bool expected)
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party._isInRaftState = partyInRaftState;
        var visual = ObjectHelper.SkipConstructor<NavalMobilePartyVisual>();
        visual._isVisualInRaftState = visualInRaftState;

        Assert.Equal(expected, NavalRaftExitVisualPatches.ShouldDropRaftVisual(party, visual));
    }

    // A headless instance or one without the campaign map loaded has no party visuals, so leaving a raft there does nothing.
    [Fact]
    public void Postfix_WithoutAMapView_LeavesThePartyAlone()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party._isInRaftState = false;

        var exception = Record.Exception(() => NavalRaftExitVisualPatches.IsInRaftStateSetterPostfix(party));

        Assert.Null(exception);
        Assert.False(party.IsInRaftState);
    }
}
#endif
