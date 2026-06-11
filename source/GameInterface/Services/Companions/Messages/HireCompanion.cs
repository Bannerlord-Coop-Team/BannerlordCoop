using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Companions.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct HireCompanion : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string OneToOneConversationHeroId;

    [ProtoMember(3)]
    public readonly int HiringPrice;

    [ProtoMember(4)]
    public readonly string PlayerClanId;

    [ProtoMember(5)]
    public readonly string MainPartyId;

    public HireCompanion(
        string mainHeroId,
        string oneToOneConversationHeroId,
        int hiringPrice,
        string playerClanId,
        string mainPartyId)
    {
        MainHeroId = mainHeroId;
        OneToOneConversationHeroId = oneToOneConversationHeroId;
        HiringPrice = hiringPrice;
        PlayerClanId = playerClanId;
        MainPartyId = mainPartyId;
    }
}