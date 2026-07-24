#if DEBUG
using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Tournaments.Messages;

public enum TournamentCombatFixtureAction
{
    Initialize,
    AiShieldStrike,
    PlayerShieldStrike,
    JavelinThrow,
    MountedPolearmGuard,
    MountedPolearmStrike,
    Restore
}

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkTournamentCombatFixtureCommand : IEvent
{
    [ProtoMember(1)]
    public TournamentCombatFixtureAction Action { get; }

    [ProtoMember(2)]
    public string PlayerOneControllerId { get; }

    [ProtoMember(3)]
    public string PlayerTwoControllerId { get; }

    public NetworkTournamentCombatFixtureCommand(
        TournamentCombatFixtureAction action,
        string playerOneControllerId,
        string playerTwoControllerId)
    {
        Action = action;
        PlayerOneControllerId = playerOneControllerId;
        PlayerTwoControllerId = playerTwoControllerId;
    }
}
#endif
