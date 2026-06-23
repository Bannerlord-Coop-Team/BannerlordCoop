using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// One troop entry of a defeated party's simulation casualties (<c>DiedInBattle</c> / <c>WoundedInBattle</c>),
/// resolved to a network id so the client can rebuild the roster the loot model reads from.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct BattleSimCasualty
{
    [ProtoMember(1)]
    public readonly string CharacterId;
    [ProtoMember(2)]
    public readonly bool IsHero;
    [ProtoMember(3)]
    public readonly int Number;
    [ProtoMember(4)]
    public readonly int WoundedNumber;

    public BattleSimCasualty(string characterId, bool isHero, int number, int woundedNumber)
    {
        CharacterId = characterId;
        IsHero = isHero;
        Number = number;
        WoundedNumber = woundedNumber;
    }
}
