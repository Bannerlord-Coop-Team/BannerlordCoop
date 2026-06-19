using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

/// <summary>
/// Sets the absolute count of an element in a roster, keyed by element identity. Sent as an absolute
/// value (not a delta) so a missed intermediate change cannot drift. The client finds the element,
/// creating it first if absent, then sets its number. <see cref="IsHero"/> as in
/// <see cref="NetworkTroopRosterAddCounts"/>.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkTroopRosterSetNumber : ICommand
{
    [ProtoMember(1)]
    public readonly string RosterId;
    [ProtoMember(2)]
    public readonly string CharacterId;
    [ProtoMember(3)]
    public readonly bool IsHero;
    [ProtoMember(4)]
    public readonly int Number;

    public NetworkTroopRosterSetNumber(string rosterId, string characterId, bool isHero, int number)
    {
        RosterId = rosterId;
        CharacterId = characterId;
        IsHero = isHero;
        Number = number;
    }
}
