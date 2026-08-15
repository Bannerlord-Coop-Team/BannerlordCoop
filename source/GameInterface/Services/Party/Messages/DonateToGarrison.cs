using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Party.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct DonateToGarrison : ICommand
{
    [ProtoMember(1)]
    public readonly string CurrentSettlementId;

    [ProtoMember(2)]
    public readonly List<DonateTroop> Troops;

    public DonateToGarrison(
        string currentSettlementId,
        List<DonateTroop> troops)
    {
        CurrentSettlementId = currentSettlementId;
        Troops = troops;
    }
}
[ProtoContract(SkipConstructor = true)]
internal readonly struct DonateTroop
{
    [ProtoMember(1)]
    public readonly string CharacterId;

    [ProtoMember(2)]
    public readonly int Count;

    public DonateTroop(string characterId, int count)
    {
        CharacterId = characterId;
        Count = count;
    }
}