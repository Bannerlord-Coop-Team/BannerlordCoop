using Common.Messaging;
using GameInterface.Services.Tournaments.Data;
using ProtoBuf;

namespace GameInterface.Services.Tournaments.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRequestTournamentState : ICommand
{
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkTournamentStateSnapshot : ICommand
{
    [ProtoMember(1)]
    public readonly TournamentNativeGameData[] NativeTournaments;
    [ProtoMember(2)]
    public readonly TournamentLeaderboardEntryData[] Leaderboard;
    [ProtoMember(3)]
    public readonly TournamentSessionSnapshot[] Sessions;

    public NetworkTournamentStateSnapshot(
        TournamentNativeGameData[] nativeTournaments,
        TournamentLeaderboardEntryData[] leaderboard,
        TournamentSessionSnapshot[] sessions)
    {
        NativeTournaments = nativeTournaments ?? new TournamentNativeGameData[0];
        Leaderboard = leaderboard ?? new TournamentLeaderboardEntryData[0];
        Sessions = sessions ?? new TournamentSessionSnapshot[0];
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkTournamentSessionRemoved : ICommand
{
    [ProtoMember(1)]
    public readonly string SessionId;
    [ProtoMember(2)]
    public readonly string TownId;

    public NetworkTournamentSessionRemoved(string sessionId, string townId)
    {
        SessionId = sessionId;
        TownId = townId;
    }
}

public sealed class TournamentSessionRemoved : IEvent
{
    public string SessionId { get; }
    public string TownId { get; }

    public TournamentSessionRemoved(string sessionId, string townId)
    {
        SessionId = sessionId;
        TownId = townId;
    }
}

public sealed class TournamentNativeStateChanged : IEvent
{
}
