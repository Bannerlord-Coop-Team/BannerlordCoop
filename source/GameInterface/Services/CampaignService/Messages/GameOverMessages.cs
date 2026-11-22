using Common.Messaging;
using GameInterface.Services.CampaignService.Statistics.Data;
using ProtoBuf;

namespace GameInterface.Services.CampaignService.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkClientGameOver : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;

    [ProtoMember(2)]
    public readonly string AppointedLeaderId;

    [ProtoMember(3)]
    public readonly bool ClanSurvives;

    [ProtoMember(4)]
    public readonly StatisticsData Statistics;

    public NetworkClientGameOver(
        string playerHeroId,
        string appointedLeaderId = null,
        bool clanSurvives = false,
        StatisticsData statistics = null)
    {
        PlayerHeroId = playerHeroId;
        AppointedLeaderId = appointedLeaderId;
        ClanSurvives = clanSurvives;
        Statistics = statistics;
    }
}
