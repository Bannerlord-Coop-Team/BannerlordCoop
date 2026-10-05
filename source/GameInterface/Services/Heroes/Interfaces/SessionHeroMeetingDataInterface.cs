using Common.Logging;
using GameInterface.CoopSessionData;
using Serilog;
using System.Collections.Generic;

namespace GameInterface.Services.Heroes.Interfaces;

public interface ISessionHeroMeetingDataInterface
{
    /// <summary>Records a meeting; returns true when it is the player's first meeting with that hero.</summary>
    bool RecordMeeting(string playerHeroId, string metHeroId, long lastMeetingTimeTicks);
}

public class SessionHeroMeetingDataInterface : ISessionHeroMeetingDataInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<SessionHeroMeetingDataInterface>();
    private readonly ICoopSessionProvider coopSessionProvider;

    public SessionHeroMeetingDataInterface(ICoopSessionProvider coopSessionProvider)
    {
        this.coopSessionProvider = coopSessionProvider;
    }

    public bool RecordMeeting(string playerHeroId, string metHeroId, long lastMeetingTimeTicks)
    {
        var heroMeetingData = coopSessionProvider.CoopSession?.HeroMeetingData;
        if (heroMeetingData?.PlayerLastMeetingTimes == null)
        {
            Logger.Error("HeroMeetingData was null; cannot record meeting for {PlayerHeroId}", playerHeroId);
            return false;
        }

        if (!heroMeetingData.PlayerLastMeetingTimes.TryGetValue(playerHeroId, out var meetingTimes) || meetingTimes == null)
        {
            meetingTimes = new Dictionary<string, long>();
            heroMeetingData.PlayerLastMeetingTimes[playerHeroId] = meetingTimes;
        }

        var firstMeeting = !meetingTimes.ContainsKey(metHeroId);
        meetingTimes[metHeroId] = lastMeetingTimeTicks;
        return firstMeeting;
    }
}
