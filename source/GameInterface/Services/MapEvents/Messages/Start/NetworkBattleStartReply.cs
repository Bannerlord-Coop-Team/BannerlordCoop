using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// [Server -&gt; Client] Answer to <see cref="NetworkBattleStartRequest"/>: whether the server accepted starting the
/// battle in the requested mode. The requesting client blocks on this before opening anything, so a rejected request
/// opens nothing and leaves the encounter menu in place.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkBattleStartReply : ICommand
{
    [ProtoMember(1)]
    public readonly string RequestId;

    [ProtoMember(2)]
    public readonly bool Accepted;

    public NetworkBattleStartReply(string requestId, bool accepted)
    {
        RequestId = requestId;
        Accepted = accepted;
    }
}
