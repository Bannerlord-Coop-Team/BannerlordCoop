using Common.Util;
using GameInterface.Services.GameDebug.Commands;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Services.SiegeEvents;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.GameDebug;

[Collection(nameof(CampaignCurrentCollection))]
public sealed class CameraFollowTargetTests : IDisposable
{
    private readonly Campaign previousCampaign;
    private readonly MobileParty player;

    public CameraFollowTargetTests()
    {
        GameBootStrap.Initialize();
        previousCampaign = Campaign.Current;
        Campaign.Current = ObjectHelper.SkipConstructor<Campaign>();
        player = CreateParty(10f, 20f);
        Campaign.Current.MainParty = player;
    }

    [Theory]
    [InlineData(false, false, 30f, 40f)]
    [InlineData(false, true, 30f, 40f)]
    [InlineData(true, false, 40f, 50f)]
    [InlineData(true, true, 50f, 60f)]
    public void MainPartyInSettlement_UsesNativeTownAndPortFraming(bool port, bool siege, float x, float y)
    {
        var settlement = CreateSettlement(30f, 40f);
        settlement.HasPort = port;
        settlement.PortPosition = Position(50f, 60f);
        if (siege) SetSiege(settlement, CreateParty(70f, 80f));
        player._currentSettlement = settlement;

        Assert.True(UiDebugCommands.HasReachedCameraFollowTarget(player.Party, new Vec2(x, y)));
        Assert.False(UiDebugCommands.HasReachedCameraFollowTarget(player.Party, new Vec2(10f, 20f)));
    }

    [Theory]
    [InlineData(0, 30f, 40f)]
    [InlineData(1, 50f, 60f)]
    [InlineData(2, 70f, 80f)]
    public void MainPartySettlementSelection_PreservesNativePrecedence(int absentContexts, float x, float y)
    {
        if (absentContexts == 0) player._currentSettlement = CreateSettlement(30f, 40f);
        if (absentContexts < 2)
        {
            player._besiegerCamp = ObjectHelper.SkipConstructor<BesiegerCamp>();
            player._besiegerCamp.SiegeEvent = ObjectHelper.SkipConstructor<SiegeEvent>();
            AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent.BesiegedSettlement))
                .SetValue(player._besiegerCamp.SiegeEvent, CreateSettlement(50f, 60f));
        }
        SetMapEvent(player.Party, 90f, 100f, CreateSettlement(70f, 80f));

        Assert.True(UiDebugCommands.HasReachedCameraFollowTarget(player.Party, new Vec2(x, y)));
        Assert.False(UiDebugCommands.HasReachedCameraFollowTarget(player.Party, new Vec2(90f, 100f)));
    }

    [Theory]
    [InlineData(false, false, 10f, 20f)]
    [InlineData(false, true, 90f, 100f)]
    [InlineData(true, true, 90f, 100f)]
    public void MobilePartyWithoutMainSettlementFraming_UsesPartyOrEvent(bool otherParty, bool mapEvent, float x, float y)
    {
        var followed = otherParty ? CreateParty(10f, 20f) : player;
        if (otherParty) followed._currentSettlement = CreateSettlement(30f, 40f);
        if (mapEvent) SetMapEvent(followed.Party, 90f, 100f, null);

        Assert.True(UiDebugCommands.HasReachedCameraFollowTarget(followed.Party, new Vec2(x, y)));
        Assert.False(UiDebugCommands.HasReachedCameraFollowTarget(followed.Party, new Vec2(300f, 400f)));
    }

    [Fact]
    public void NonmobileParty_UsesOwnPositionEvenWithMapEvent()
    {
        var settlement = CreateSettlement(30f, 40f);
        SetMapEvent(settlement.Party, 90f, 100f, null);
        Assert.True(UiDebugCommands.HasReachedCameraFollowTarget(settlement.Party, new Vec2(30f, 40f)));
        Assert.False(UiDebugCommands.HasReachedCameraFollowTarget(settlement.Party, new Vec2(90f, 100f)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IncompletePortSiege_RefusesReadiness(bool hasCamp)
    {
        var settlement = CreateSettlement(30f, 40f);
        settlement.HasPort = true;
        settlement.PortPosition = Position(50f, 60f);
        settlement.SiegeEvent = ObjectHelper.SkipConstructor<SiegeEvent>();
        if (hasCamp) SetSiege(settlement, null);
        player._currentSettlement = settlement;
        Assert.False(UiDebugCommands.HasReachedCameraFollowTarget(player.Party, new Vec2(40f, 50f)));
    }

    [Fact]
    public void MissingOrInvalidFollowParty_RefusesReadiness()
    {
        Assert.False(UiDebugCommands.HasReachedCameraFollowTarget(null, new Vec2(10f, 20f)));
        player.Party.Index = -1;
        Assert.False(UiDebugCommands.HasReachedCameraFollowTarget(player.Party, new Vec2(10f, 20f)));
    }

    private static CampaignVec2 Position(float x, float y) => new(new Vec2(x, y), true);

    private static MobileParty CreateParty(float x, float y)
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.Party = ObjectHelper.SkipConstructor<PartyBase>();
        party.Party.MobileParty = party;
        party._position = Position(x, y);
        return party;
    }

    private static Settlement CreateSettlement(float x, float y)
    {
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        settlement.Position = Position(x, y);
        settlement.Party = ObjectHelper.SkipConstructor<PartyBase>();
        settlement.Party.Settlement = settlement;
        return settlement;
    }

    private static void SetSiege(Settlement settlement, MobileParty leader)
    {
        settlement.SiegeEvent = ObjectHelper.SkipConstructor<SiegeEvent>();
        var camp = ObjectHelper.SkipConstructor<BesiegerCamp>();
        camp._leaderParty = leader;
        // Constructor-free fixtures need reflection for the native readonly field.
        AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent.BesiegerCamp)).SetValue(settlement.SiegeEvent, camp);
    }

    private static void SetMapEvent(PartyBase party, float x, float y, Settlement settlement)
    {
        var mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
        mapEvent.Position = Position(x, y);
        mapEvent.MapEventSettlement = settlement;
        party._mapEventSide = ObjectHelper.SkipConstructor<MapEventSide>();
        AccessTools.Field(typeof(MapEventSide), "_mapEvent").SetValue(party._mapEventSide, mapEvent);
    }

    public void Dispose() => Campaign.Current = previousCampaign;
}
