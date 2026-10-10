using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.UI.PlayerList;
using Moq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.UI;

/// <summary>Protects encounter classification and attached-party activity precedence.</summary>
public class PlayerActivityReaderTests
{
    // Special encounter types must not become generic battles, including for army followers.
    [Theory]
    [InlineData(MapEvent.BattleTypes.FieldBattle, PlayerActivity.Battle)]
    [InlineData(MapEvent.BattleTypes.Hideout, PlayerActivity.Hideout)]
    [InlineData(MapEvent.BattleTypes.Siege, PlayerActivity.Siege)]
    [InlineData(MapEvent.BattleTypes.SallyOut, PlayerActivity.Siege)]
    [InlineData(MapEvent.BattleTypes.SiegeOutside, PlayerActivity.Siege)]
    public void ClassifiesEncounterForAttachedParty(MapEvent.BattleTypes type, PlayerActivity expected)
    {
        var battle = ObjectHelper.SkipConstructor<MapEvent>();
        battle._mapEventType = type;
        var leader = ObjectHelper.SkipConstructor<MobileParty>();
        leader.Party = ObjectHelper.SkipConstructor<PartyBase>();
        leader.Party._mapEventSide = new MapEventSide(battle, BattleSideEnum.Attacker, leader.Party);
        var follower = ObjectHelper.SkipConstructor<MobileParty>();
        follower._attachedTo = leader;
        var reader = new PlayerActivityReader(Mock.Of<IObjectManager>());
        Assert.Equal(expected, reader.ReadActivity(follower, moving: true));
    }

    // Settlement and siege preparation take priority over movement on both siege sides.
    [Theory]
    [InlineData(PlayerActivity.Town)]
    [InlineData(PlayerActivity.Castle)]
    [InlineData(PlayerActivity.Village)]
    public void SettlementAndSiegePreparationOverrideMovement(PlayerActivity expected)
    {
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        if (expected == PlayerActivity.Village)
            settlement.Village = ObjectHelper.SkipConstructor<Village>();
        else
        {
            settlement.Town = ObjectHelper.SkipConstructor<Town>();
            settlement.Town._isCastle = expected == PlayerActivity.Castle;
        }
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.Party = ObjectHelper.SkipConstructor<PartyBase>();
        party._currentSettlement = settlement;
        var reader = new PlayerActivityReader(Mock.Of<IObjectManager>());
        Assert.Equal(expected, reader.ReadActivity(party, moving: true));
        settlement.SiegeEvent = ObjectHelper.SkipConstructor<SiegeEvent>();
        Assert.Equal(PlayerActivity.Siege, reader.ReadActivity(party, moving: true));
        party._currentSettlement = null;
        party._besiegerCamp = ObjectHelper.SkipConstructor<BesiegerCamp>();
        Assert.Equal(PlayerActivity.Siege, reader.ReadActivity(party, moving: true));
    }

    // A movement target alone cannot make a stationary or paused party appear to be travelling.
    [Fact]
    public void StationarySamplesAreIdleAndReconnectStartsFresh()
    {
        var reader = new PlayerActivityReader(Mock.Of<IObjectManager>());
        var start = new Vec2(10, 20);
        var moved = new Vec2(11, 20);
        Assert.False(reader.ObserveMovement("player", start));
        Assert.False(reader.ObserveMovement("player", start));
        Assert.True(reader.ObserveMovement("player", moved));
        Assert.False(reader.ObserveMovement("player", moved));
        Assert.False(reader.ObserveMovement("player", null));
        Assert.False(reader.ObserveMovement("player", start));
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.Party = ObjectHelper.SkipConstructor<PartyBase>();
        Assert.Equal(PlayerActivity.Idle, reader.ReadActivity(party, moving: false));
        Assert.Equal(PlayerActivity.Travelling, reader.ReadActivity(party, moving: true));
    }
}
