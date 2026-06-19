using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Locations.Messages.Conversation;

/// <summary>
/// Server -> Client approval to open the requested settlement-location conversation. <see cref="Generation"/>
/// echoes the request's id so the client starts the approval only when it still matches its current pending
/// request, ignoring a stale approval for one it abandoned (e.g. after leaving the settlement).
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkAllowLocationConversation : ICommand
{
    [ProtoMember(1)]
    public readonly int Generation;

    public NetworkAllowLocationConversation(int generation)
    {
        Generation = generation;
    }
}
