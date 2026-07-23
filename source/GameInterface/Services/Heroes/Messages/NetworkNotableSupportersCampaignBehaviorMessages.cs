using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Heroes.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct AcceptNotableSupport : IEvent
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string NotableId;

    [ProtoMember(3)]
    public readonly string PlayerClanId;

    [ProtoMember(4)]
    public readonly int Cost;

    public AcceptNotableSupport(string mainHeroId, string notableId, string playerClanId, int cost)
    {
        MainHeroId = mainHeroId;
        NotableId = notableId;
        PlayerClanId = playerClanId;
        Cost = cost;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct EndNotableSupportByAgreement : IEvent
{
    [ProtoMember(1)]
    public readonly string NotableId;

    public EndNotableSupportByAgreement(string notableId)
    {
        NotableId = notableId;
    }
}