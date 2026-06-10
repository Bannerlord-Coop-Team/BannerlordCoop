using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

/// <summary>
/// Server -&gt; Client notification that a conversation request was denied because the targeted party is engaged in
/// another player's conversation. The client shows the player why their interaction did nothing; carrying no
/// payload, it identifies the request implicitly (one outstanding request per client at a time).
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkConversationDenied : ICommand
{
}
