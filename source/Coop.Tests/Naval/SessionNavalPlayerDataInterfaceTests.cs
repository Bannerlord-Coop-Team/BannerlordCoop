#if DEBUG
using Common.Messaging;
using Coop.Naval.CoopSession.Interfaces;
using GameInterface.CoopSessionData;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Services.ObjectManager;
using Moq;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace Coop.Tests.Naval;

public class SessionNavalPlayerDataInterfaceTests
{
    private readonly CoopSession session = CoopSession.Empty;
    private readonly SessionNavalPlayerDataInterface navalDataInterface;

    public SessionNavalPlayerDataInterfaceTests()
    {
        var sessionProvider = new Mock<ICoopSessionProvider>();
        sessionProvider.SetupGet(provider => provider.CoopSession).Returns(session);
        navalDataInterface = new SessionNavalPlayerDataInterface(
            sessionProvider.Object,
            Mock.Of<IObjectManager>(),
            Mock.Of<IMessageBroker>());
    }

    [Fact]
    public void AddPlayerKeys_NewPlayer_AddsEmptyFigureheadsAndZeroLootTime()
    {
        navalDataInterface.AddPlayerKeys("Hero_Player");

        Assert.Empty(session.NavalPlayerData.PlayerUnlockedFigureHeads["Hero_Player"]);
        Assert.Equal(0, session.NavalPlayerData.PlayerLastFigureheadLootTimes["Hero_Player"]);
    }

    [Fact]
    public void AddPlayerKeys_ExistingPlayer_KeepsTheirData()
    {
        navalDataInterface.TryRecordFigureheadUnlock("Hero_Player", "figurehead_1", 1351);

        navalDataInterface.AddPlayerKeys("Hero_Player");

        Assert.Equal(new[] { "figurehead_1" }, session.NavalPlayerData.PlayerUnlockedFigureHeads["Hero_Player"]);
        Assert.Equal(1351, session.NavalPlayerData.PlayerLastFigureheadLootTimes["Hero_Player"]);
    }

    [Fact]
    public void TryRecordFigureheadUnlock_NewFigurehead_RecordsItAndLootTime()
    {
        Assert.True(navalDataInterface.TryRecordFigureheadUnlock("Hero_Player", "figurehead_1", 1351));

        Assert.Equal(new[] { "figurehead_1" }, session.NavalPlayerData.PlayerUnlockedFigureHeads["Hero_Player"]);
        Assert.Equal(1351, session.NavalPlayerData.PlayerLastFigureheadLootTimes["Hero_Player"]);
    }

    [Fact]
    public void TryRecordFigureheadUnlock_AlreadyUnlocked_ReturnsFalseAndKeepsLootTime()
    {
        navalDataInterface.TryRecordFigureheadUnlock("Hero_Player", "figurehead_1", 1351);

        Assert.False(navalDataInterface.TryRecordFigureheadUnlock("Hero_Player", "figurehead_1", 2462));

        Assert.Equal(new[] { "figurehead_1" }, session.NavalPlayerData.PlayerUnlockedFigureHeads["Hero_Player"]);
        Assert.Equal(1351, session.NavalPlayerData.PlayerLastFigureheadLootTimes["Hero_Player"]);
    }

    [Fact]
    public void GetLastFigureheadLootTime_UnknownPlayer_ReturnsZero()
    {
        Assert.Equal(CampaignTime.Zero, navalDataInterface.GetLastFigureheadLootTime("Hero_Player"));
    }

    [Fact]
    public void SetLastFigureheadLootTime_NewPlayer_StoresTimeAndAddsKeys()
    {
        navalDataInterface.SetLastFigureheadLootTime("Hero_Player", new CampaignTime(1351));

        Assert.Equal(new CampaignTime(1351), navalDataInterface.GetLastFigureheadLootTime("Hero_Player"));
        Assert.Empty(session.NavalPlayerData.PlayerUnlockedFigureHeads["Hero_Player"]);
    }

    [Fact]
    public void SetLastFigureheadLootTime_ExistingPlayer_KeepsUnlockedFigureheads()
    {
        navalDataInterface.TryRecordFigureheadUnlock("Hero_Player", "figurehead_1", 1351);

        navalDataInterface.SetLastFigureheadLootTime("Hero_Player", new CampaignTime(2462));

        Assert.Equal(new CampaignTime(2462), navalDataInterface.GetLastFigureheadLootTime("Hero_Player"));
        Assert.Equal(new[] { "figurehead_1" }, session.NavalPlayerData.PlayerUnlockedFigureHeads["Hero_Player"]);
    }

    [Fact]
    public void SetLastFigureheadLootTime_DifferentPlayers_KeepsTimesSeparate()
    {
        navalDataInterface.SetLastFigureheadLootTime("Hero_Player1", new CampaignTime(1351));
        navalDataInterface.SetLastFigureheadLootTime("Hero_Player2", new CampaignTime(2462));

        Assert.Equal(new CampaignTime(1351), navalDataInterface.GetLastFigureheadLootTime("Hero_Player1"));
        Assert.Equal(new CampaignTime(2462), navalDataInterface.GetLastFigureheadLootTime("Hero_Player2"));
    }

    [Fact]
    public void TryRecordFigureheadUnlock_DifferentPlayers_KeepsUnlocksSeparate()
    {
        navalDataInterface.TryRecordFigureheadUnlock("Hero_Player1", "figurehead_1", 1351);
        navalDataInterface.TryRecordFigureheadUnlock("Hero_Player2", "figurehead_2", 2462);

        Assert.Equal(new[] { "figurehead_1" }, session.NavalPlayerData.PlayerUnlockedFigureHeads["Hero_Player1"]);
        Assert.Equal(new[] { "figurehead_2" }, session.NavalPlayerData.PlayerUnlockedFigureHeads["Hero_Player2"]);
        Assert.Equal(1351, session.NavalPlayerData.PlayerLastFigureheadLootTimes["Hero_Player1"]);
        Assert.Equal(2462, session.NavalPlayerData.PlayerLastFigureheadLootTimes["Hero_Player2"]);
    }
}
#endif
