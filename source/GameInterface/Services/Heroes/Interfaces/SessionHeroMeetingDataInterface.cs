using Common.Logging;
using GameInterface.CoopSessionData;
using Serilog;
using System.Collections.Generic;

namespace GameInterface.Services.Heroes.Interfaces;

public interface ISessionHeroMeetingDataInterface
{
    void RecordMeeting(string playerHeroId, string metHeroId, long lastMeetingTimeTicks);
}

public class SessionHeroMeetingDataInterface : ISessionHeroMeetingDataInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<SessionHeroMeetingDataInterface>();
    private readonly ICoopSessionProvider coopSessionProvider;

    public SessionHeroMeetingDataInterface(ICoopSessionProvider coopSessionProvider)
    {
        this.coopSessionProvider = coopSessionProvider;
    }

    public void RecordMeeting(string playerHeroId, string metHeroId, long lastMeetingTimeTicks)
    {
        var heroMeetingData = coopSessionProvider.CoopSession?.HeroMeetingData;
        if (heroMeetingData?.PlayerLastMeetingTimes == null)
        {
            Logger.Error("HeroMeetingData was null; cannot record meeting for {PlayerHeroId}", playerHeroId);
            return;
        }

        if (!heroMeetingData.PlayerLastMeetingTimes.TryGetValue(playerHeroId, out var meetingTimes) || meetingTimes == null)
        {
            meetingTimes = new Dictionary<string, long>();
            heroMeetingData.PlayerLastMeetingTimes[playerHeroId] = meetingTimes;
        }

        meetingTimes[metHeroId] = lastMeetingTimeTicks;
    }
}
