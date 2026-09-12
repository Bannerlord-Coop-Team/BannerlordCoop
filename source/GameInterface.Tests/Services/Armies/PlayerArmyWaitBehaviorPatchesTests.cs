using Common.Util;
using GameInterface.Services.Armies.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using Xunit;

namespace GameInterface.Tests.Services.Armies;

public class PlayerArmyWaitBehaviorPatchesTests
{
    [Fact]
    public void GetAttachedArmySiegeState_CoherentSiegeGraph_IsReady()
    {
        var fixture = CreateAttachedArmy();
        var settlement = AttachSiege(fixture);

        var state = PlayerArmyWaitBehaviorPatches.GetAttachedArmySiegeState(
            fixture.MainParty,
            out var resolvedSettlement);

        Assert.Equal(AttachedArmySiegeState.Ready, state);
        Assert.Same(settlement, resolvedSettlement);
    }

    [Fact]
    public void GetAttachedArmySiegeState_MembershipBeforeAttachment_IsIncomplete()
    {
        var fixture = CreateAttachedArmy();
        fixture.MainParty._attachedTo = null;

        var state = PlayerArmyWaitBehaviorPatches.GetAttachedArmySiegeState(
            fixture.MainParty,
            out var settlement);

        Assert.Equal(AttachedArmySiegeState.Incomplete, state);
        Assert.Null(settlement);
    }

    [Fact]
    public void GetAttachedArmySiegeState_MismatchedInheritedCamp_IsIncomplete()
    {
        var fixture = CreateAttachedArmy();
        AttachSiege(fixture);
        fixture.MainParty._besiegerCamp = ObjectHelper.SkipConstructor<BesiegerCamp>();

        var state = PlayerArmyWaitBehaviorPatches.GetAttachedArmySiegeState(
            fixture.MainParty,
            out var settlement);

        Assert.Equal(AttachedArmySiegeState.Incomplete, state);
        Assert.Null(settlement);
    }

    [Fact]
    public void GetAttachedArmySiegeState_OtherPartyLeadsSharedSiege_IsReady()
    {
        var fixture = CreateAttachedArmy();
        var settlement = AttachSiege(fixture);
        fixture.MainParty.BesiegerCamp._leaderParty = ObjectHelper.SkipConstructor<MobileParty>();

        var state = PlayerArmyWaitBehaviorPatches.GetAttachedArmySiegeState(
            fixture.MainParty,
            out var resolvedSettlement);

        Assert.Equal(AttachedArmySiegeState.Ready, state);
        Assert.Same(settlement, resolvedSettlement);
    }

    [Fact]
    public void GetAttachedArmySiegeState_OrdinaryAttachedArmy_IsNotSiegeTransition()
    {
        var fixture = CreateAttachedArmy();

        var state = PlayerArmyWaitBehaviorPatches.GetAttachedArmySiegeState(
            fixture.MainParty,
            out var settlement);

        Assert.Equal(AttachedArmySiegeState.None, state);
        Assert.Null(settlement);
        Assert.True(PlayerArmyWaitBehaviorPatches.IsStableArmyWait(fixture.MainParty));
    }

    [Fact]
    public void GetAttachedArmySiegeState_ForeignSiegeAtTarget_IsNotSiegeTransition()
    {
        var fixture = CreateAttachedArmy();
        AttachSiege(fixture);
        fixture.MainParty._besiegerCamp = null;
        fixture.LeaderParty._besiegerCamp = null;

        var state = PlayerArmyWaitBehaviorPatches.GetAttachedArmySiegeState(
            fixture.MainParty,
            out var settlement);

        Assert.Equal(AttachedArmySiegeState.None, state);
        Assert.Null(settlement);
    }

    [Fact]
    public void IsStableArmyWait_PartialMembership_IsFalse()
    {
        var fixture = CreateAttachedArmy();
        fixture.Army.LeaderParty = null;

        Assert.False(PlayerArmyWaitBehaviorPatches.IsStableArmyWait(fixture.MainParty));
    }

    private static ArmyFixture CreateAttachedArmy()
    {
        var mainParty = ObjectHelper.SkipConstructor<MobileParty>();
        var leaderParty = ObjectHelper.SkipConstructor<MobileParty>();
        var army = ObjectHelper.SkipConstructor<Army>();

        army.LeaderParty = leaderParty;
        mainParty._army = army;
        mainParty._attachedTo = leaderParty;
        leaderParty._army = army;

        return new ArmyFixture(mainParty, leaderParty, army);
    }

    private static Settlement AttachSiege(ArmyFixture fixture)
    {
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var siegeEvent = ObjectHelper.SkipConstructor<SiegeEvent>();
        var camp = ObjectHelper.SkipConstructor<BesiegerCamp>();

        fixture.Army._aiBehaviorObject = settlement;
        fixture.MainParty._besiegerCamp = camp;
        fixture.LeaderParty._besiegerCamp = camp;
        settlement.SiegeEvent = siegeEvent;
        AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent.BesiegedSettlement))
            .SetValue(siegeEvent, settlement);
        AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent.BesiegerCamp))
            .SetValue(siegeEvent, camp);
        camp.SiegeEvent = siegeEvent;
        camp._leaderParty = fixture.LeaderParty;

        return settlement;
    }

    private sealed class ArmyFixture
    {
        public MobileParty MainParty { get; }
        public MobileParty LeaderParty { get; }
        public Army Army { get; }

        public ArmyFixture(MobileParty mainParty, MobileParty leaderParty, Army army)
        {
            MainParty = mainParty;
            LeaderParty = leaderParty;
            Army = army;
        }
    }
}
