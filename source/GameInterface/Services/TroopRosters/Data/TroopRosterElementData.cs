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

    /// <summary>
    /// True when <see cref="CharacterId"/> is a Hero id (a hero serving in the roster), false when it is
    /// a basic troop's CharacterObject id. Lets the receiver resolve each element as its actual type
    /// instead of probing for a Hero first, which would log a failed cast for every basic troop.
    /// </summary>
    [ProtoMember(5)]
    public readonly bool IsHero;

    public TroopRosterElementData(string characterId, int number, int woundedNumber, int xp, bool isHero)
    {
        CharacterId = characterId;
        Number = number;
        WoundedNumber = woundedNumber;
        Xp = xp;
        IsHero = isHero;
    }
}