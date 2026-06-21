using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

/// <summary>
/// Sets the absolute wounded count of an element in a roster, keyed by element identity. The client sets
/// the wounded number on the element if it has it; an absent element is skipped (its create is its own
/// earlier delta), since a placeholder would corrupt the roster's cached totals.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkTroopRosterSetWoundedNumber : ICommand
{
    [ProtoMember(1)]
    public readonly string RosterId;
    [ProtoMember(2)]
    public readonly string CharacterId;
    [ProtoMember(3)]
    public readonly int Number;

    public NetworkTroopRosterSetWoundedNumber(string rosterId, string characterId, int number)
    {
        RosterId = rosterId;
        CharacterId = characterId;
        Number = number;
    }
}
