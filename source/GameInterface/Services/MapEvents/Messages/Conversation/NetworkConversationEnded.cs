using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

/// <summary>
/// Client to Server notification that this client's player encounter finished (or an approved one failed to
/// start). The server releases the AI party held for that player's conversation, if any; the sender is identified
/// by its peer, so no payload is needed.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkConversationEnded : ICommand
{
}
