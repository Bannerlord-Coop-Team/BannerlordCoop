using ProtoBuf;

namespace GameInterface.Services.Tournaments.Data;

[ProtoContract(SkipConstructor = true)]
public sealed class TournamentTeamScoreData
{
    [ProtoMember(1)]
    public readonly string TeamId;
    [ProtoMember(2)]
    public readonly int Score;

    public TournamentTeamScoreData(string teamId, int score)
    {
        TeamId = teamId;
        Score = score;
    }
}
