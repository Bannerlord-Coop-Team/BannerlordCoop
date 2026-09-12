using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Locations.BoardGames.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestPlayerBoardGame : ICommand
{
    [ProtoMember(1)]
    public readonly string TargetControllerId;
    [ProtoMember(2)]
    public readonly int BoardGameType;

    public NetworkRequestPlayerBoardGame(string targetControllerId, int boardGameType)
    {
        TargetControllerId = targetControllerId;
        BoardGameType = boardGameType;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerBoardGameChallenge : ICommand
{
    [ProtoMember(1)]
    public readonly string ChallengeId;
    [ProtoMember(2)]
    public readonly string InitiatorControllerId;
    [ProtoMember(3)]
    public readonly string InitiatorName;
    [ProtoMember(4)]
    public readonly string TargetControllerId;
    [ProtoMember(5)]
    public readonly int BoardGameType;

    public NetworkPlayerBoardGameChallenge(
        string challengeId,
        string initiatorControllerId,
        string initiatorName,
        string targetControllerId,
        int boardGameType)
    {
        ChallengeId = challengeId;
        InitiatorControllerId = initiatorControllerId;
        InitiatorName = initiatorName;
        TargetControllerId = targetControllerId;
        BoardGameType = boardGameType;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRespondPlayerBoardGameChallenge : ICommand
{
    [ProtoMember(1)]
    public readonly string ChallengeId;
    [ProtoMember(2)]
    public readonly bool Accepted;

    public NetworkRespondPlayerBoardGameChallenge(string challengeId, bool accepted)
    {
        ChallengeId = challengeId;
        Accepted = accepted;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerBoardGameStarted : ICommand
{
    [ProtoMember(1)]
    public readonly string GameId;
    [ProtoMember(2)]
    public readonly string InitiatorControllerId;
    [ProtoMember(3)]
    public readonly string ResponderControllerId;
    [ProtoMember(4)]
    public readonly int BoardGameType;

    public NetworkPlayerBoardGameStarted(
        string gameId,
        string initiatorControllerId,
        string responderControllerId,
        int boardGameType)
    {
        GameId = gameId;
        InitiatorControllerId = initiatorControllerId;
        ResponderControllerId = responderControllerId;
        BoardGameType = boardGameType;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerBoardGameChallengeDeclined : ICommand
{
    [ProtoMember(1)]
    public readonly string ChallengeId;
    [ProtoMember(2)]
    public readonly string InitiatorControllerId;

    public NetworkPlayerBoardGameChallengeDeclined(string challengeId, string initiatorControllerId)
    {
        ChallengeId = challengeId;
        InitiatorControllerId = initiatorControllerId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerBoardGameMove : ICommand
{
    [ProtoMember(1)]
    public readonly string GameId;
    [ProtoMember(2)]
    public readonly string SenderControllerId;
    [ProtoMember(3)]
    public readonly int FromIndex;
    [ProtoMember(4)]
    public readonly int ToIndex;

    public NetworkPlayerBoardGameMove(string gameId, string senderControllerId, int fromIndex, int toIndex)
    {
        GameId = gameId;
        SenderControllerId = senderControllerId;
        FromIndex = fromIndex;
        ToIndex = toIndex;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerBoardGamePawnCaptured : ICommand
{
    [ProtoMember(1)]
    public readonly string GameId;
    [ProtoMember(2)]
    public readonly string SenderControllerId;
    [ProtoMember(3)]
    public readonly int Index;

    public NetworkPlayerBoardGamePawnCaptured(string gameId, string senderControllerId, int index)
    {
        GameId = gameId;
        SenderControllerId = senderControllerId;
        Index = index;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerBoardGameFinished : ICommand
{
    [ProtoMember(1)]
    public readonly string GameId;
    [ProtoMember(2)]
    public readonly string SenderControllerId;
    [ProtoMember(3)]
    public readonly int GameOver;

    public NetworkPlayerBoardGameFinished(string gameId, string senderControllerId, int gameOver)
    {
        GameId = gameId;
        SenderControllerId = senderControllerId;
        GameOver = gameOver;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerBoardGameCancelled : ICommand
{
    [ProtoMember(1)]
    public readonly string GameId;
    [ProtoMember(2)]
    public readonly string SenderControllerId;

    public NetworkPlayerBoardGameCancelled(string gameId, string senderControllerId)
    {
        GameId = gameId;
        SenderControllerId = senderControllerId;
    }
}
