using ProtoBuf;

namespace GameInterface.Services.Party.Data;

[ProtoContract(SkipConstructor = true)]
public readonly struct UpgradedTroopHistoryElementData
{
    [ProtoMember(1)]
    public readonly uint Character1Id;

    [ProtoMember(2)]
    public readonly uint Character2Id;

    [ProtoMember(3)]
    public readonly int Number;

    public UpgradedTroopHistoryElementData(uint character1Id, uint character2Id, int number)
    {
        Character1Id = character1Id;
        Character2Id = character2Id;
        Number = number;
    }
}
