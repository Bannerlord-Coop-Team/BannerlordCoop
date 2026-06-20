using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Data;

[ProtoContract(SkipConstructor = true)]
public readonly struct TroopRosterElementData
{
    [ProtoMember(1)]
    public readonly string CharacterId;

    [ProtoMember(2)]
    public readonly int Number;

    [ProtoMember(3)]
    public readonly int WoundedNumber;

    [ProtoMember(4)]
    public readonly int Xp;

    public TroopRosterElementData(string characterId, int number, int woundedNumber, int xp)
    {
        CharacterId = characterId;
        Number = number;
        WoundedNumber = woundedNumber;
        Xp = xp;
    }
}