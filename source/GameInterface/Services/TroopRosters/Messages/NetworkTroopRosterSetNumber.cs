using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

/// <summary>
/// Sets the absolute count of an element in a roster, keyed by element identity. Sent as an absolute
/// value (not a delta) so a missed intermediate change cannot drift. The client sets the number on the
/// element if it has it; an absent element is skipped (its create is its own earlier delta), since a
/// placeholder would corrupt the roster's cached totals.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkTroopRosterSetNumber : ICommand
{
    [ProtoMember(1)]
    public readonly string RosterId;
    [ProtoMember(2)]
    public readonly string CharacterId;
    [ProtoMember(3)]
    public readonly int Number;

    public NetworkTroopRosterSetNumber(string rosterId, string characterId, int number)
    {
        RosterId = rosterId;
        CharacterId = characterId;
        Number = number;
    }
}
