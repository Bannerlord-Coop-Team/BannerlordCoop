using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Heroes.Messages.LordConversations;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkLordFreedToRelease : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string ConversationHeroId;

    public NetworkLordFreedToRelease(
        string mainHeroId,
        string conversationHeroId)
    {
        MainHeroId = mainHeroId;
        ConversationHeroId = conversationHeroId;
    }
}
