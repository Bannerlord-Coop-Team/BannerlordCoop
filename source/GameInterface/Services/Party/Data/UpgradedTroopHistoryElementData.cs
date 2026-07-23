using ProtoBuf;

namespace GameInterface.Services.Party.Data;

[ProtoContract(SkipConstructor = true)]
public readonly struct UpgradedTroopHistoryElementData
{
    [ProtoMember(1)]
    public readonly string Character1Id;

    [ProtoMember(2)]
    public readonly string Character2Id;

    [ProtoMember(3)]
    public readonly int Number;

    public UpgradedTroopHistoryElementData(string character1Id, string character2Id, int number)
    {
        Character1Id = character1Id;
        Character2Id = character2Id;
        Number = number;
    }
}