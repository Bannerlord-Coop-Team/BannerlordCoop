using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

/// <summary>
/// Sent from the authority to every client to add (or subtract) counts for a single element in a
/// roster, keyed by the element's identity rather than its array index. The client applies it via
/// vanilla <c>AddToCounts</c>, which finds-or-creates the element by character and keeps the cached
/// totals correct, so a positive add creates the element if the client is missing it. A subtract for an
/// element the client does not have is skipped, since vanilla cannot create one from a non-positive add.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkTroopRosterAddCounts : ICommand
{
    [ProtoMember(1)]
    public readonly string RosterId;
    [ProtoMember(2)]
    public readonly string CharacterId;
    [ProtoMember(3)]
    public readonly int Count;
    [ProtoMember(4)]
    public readonly int WoundedCount;
    [ProtoMember(5)]
    public readonly int XpChange;
    [ProtoMember(6)]
    public readonly bool RemoveDepleted;

    public NetworkTroopRosterAddCounts(string rosterId, string characterId, int count, int woundedCount, int xpChange, bool removeDepleted)
    {
        RosterId = rosterId;
        CharacterId = characterId;
        Count = count;
        WoundedCount = woundedCount;
        XpChange = xpChange;
        RemoveDepleted = removeDepleted;
    }
}
