using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Tournaments.Messages;

/// <summary>
/// [Client -&gt; Server] Confirms that the requesting peer successfully entered the shared tournament mission. The
/// server adds spectators to the ballot only after this confirmation and elects the first entrant as NPC host.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkTournamentMissionEntered : ICommand
{
    [ProtoMember(1)]
    public readonly string SessionId;
    [ProtoMember(2)]
    public readonly long ExpectedRevision;

    public NetworkTournamentMissionEntered(string sessionId, long expectedRevision)
    {
        SessionId = sessionId;
        ExpectedRevision = expectedRevision;
    }
}
