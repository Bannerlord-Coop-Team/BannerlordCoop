using GameInterface.CoopSessionData;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Services.Heroes.Interfaces;
using Moq;
using ProtoBuf;
using Xunit;

namespace GameInterface.Tests.Services.Heroes;

public class SessionHeroMeetingDataInterfaceTests
{
    private readonly CoopSession session = CoopSession.Empty;
    private readonly SessionHeroMeetingDataInterface meetingDataInterface;

    public SessionHeroMeetingDataInterfaceTests()
    {
        var sessionProvider = new Mock<ICoopSessionProvider>();
        sessionProvider.SetupGet(provider => provider.CoopSession).Returns(session);
        meetingDataInterface = new SessionHeroMeetingDataInterface(sessionProvider.Object);
    }

    [Fact]
    public void RecordMeeting_NewPlayerAndHero_AddsMeetingTime()
    {
        meetingDataInterface.RecordMeeting("Hero_Player", "lord_6_1", 1351);

        Assert.Equal(
            1351,
            session.HeroMeetingData.PlayerLastMeetingTimes["Hero_Player"]["lord_6_1"]);
    }

    [Fact]
    public void RecordMeeting_ExistingHero_UpdatesMeetingTime()
    {
        meetingDataInterface.RecordMeeting("Hero_Player", "lord_6_1", 1351);

        meetingDataInterface.RecordMeeting("Hero_Player", "lord_6_1", 2462);

        Assert.Equal(
            2462,
            session.HeroMeetingData.PlayerLastMeetingTimes["Hero_Player"]["lord_6_1"]);
    }

    [Fact]
    public void RecordMeeting_DifferentPlayers_KeepsMeetingTimesSeparate()
    {
        meetingDataInterface.RecordMeeting("Hero_Player1", "lord_6_1", 1351);
        meetingDataInterface.RecordMeeting("Hero_Player2", "lord_6_1", 2462);

        Assert.Equal(
            1351,
            session.HeroMeetingData.PlayerLastMeetingTimes["Hero_Player1"]["lord_6_1"]);
        Assert.Equal(
            2462,
            session.HeroMeetingData.PlayerLastMeetingTimes["Hero_Player2"]["lord_6_1"]);
    }

    [Fact]
    public void ProtobufRoundTrip_PreservesPlayerMeetingTimes()
    {
        meetingDataInterface.RecordMeeting("Hero_Player", "lord_6_1", 1351);

        var clone = Serializer.DeepClone(session.HeroMeetingData);

        Assert.Equal(
            1351,
            clone.PlayerLastMeetingTimes["Hero_Player"]["lord_6_1"]);
    }
}
