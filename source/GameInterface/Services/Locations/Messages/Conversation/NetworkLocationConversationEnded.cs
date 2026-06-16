using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Locations.Messages.Conversation;

/// <summary>
/// Client -&gt; Server notification that this client's location conversation finished (or an approved one
/// failed to start). The server releases the NPC held for that player, if any; the sender is identified
/// by its peer, so no payload is needed.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkLocationConversationEnded : ICommand
{
}
