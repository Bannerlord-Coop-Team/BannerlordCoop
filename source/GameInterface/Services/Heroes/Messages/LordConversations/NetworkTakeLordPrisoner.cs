using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Heroes.Messages.LordConversations;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkTakeLordPrisoner : ICommand
{
    [ProtoMember(1)]
    public readonly string MainPartyId;

    [ProtoMember(2)]
    public readonly string ConversationHeroId;

    public NetworkTakeLordPrisoner(
        string mainPartyId,
        string conversationHeroId)
    {
        MainPartyId = mainPartyId;
        ConversationHeroId = conversationHeroId;
    }
}
