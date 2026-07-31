using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Players.Messages;

/// <summary>
/// Server reply to the requesting client only: the delete request was not applied. The client
/// stays connected and surfaces the reason.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkDeletePlayerDenied : IEvent
{
    [ProtoMember(1)]
    public string Reason { get; }

    public NetworkDeletePlayerDenied(string reason)
    {
        Reason = reason;
    }
}
