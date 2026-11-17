using Common.Messaging;
using GameInterface.Services.CampaignService.Statistics.Data;
using ProtoBuf;

namespace GameInterface.Services.Players.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestPlayerRetirement : ICommand {}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerRetired : IEvent
{
    [ProtoMember(1)]
    public readonly string ControllerId;

    [ProtoMember(2)]
    public readonly string HeroId;

    // Only sent to the retiring player
    [ProtoMember(3)]
    public readonly StatisticsData Statistics;

    public NetworkPlayerRetired(string controllerId, string heroId, StatisticsData statistics = null)
    {
        ControllerId = controllerId;
        HeroId = heroId;
        Statistics = statistics;
    }
}
