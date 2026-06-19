using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

/// <summary>
/// Sent from the authority to every client to add (or subtract) counts for a single element in a
/// roster, keyed by the element's identity rather than its array index. The client applies it via
/// vanilla <c>AddToCounts</c>, which finds-or-creates the element by character, so it stays correct
/// regardless of the client roster's layout and self-heals an under-populated client roster.
/// </summary>
/// <remarks>
/// <see cref="IsHero"/> is true when <see cref="CharacterId"/> is a Hero id (a hero serving in the
/// roster), false when it is a basic troop's CharacterObject id, mirroring how the whole-roster snapshot
/// resolved elements (a hero's CharacterObject is not reliably id-registered).
/// </remarks>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkTroopRosterAddCounts : ICommand
{
    [ProtoMember(1)]
    public readonly string RosterId;
    [ProtoMember(2)]
    public readonly string CharacterId;
    [ProtoMember(3)]
    public readonly bool IsHero;
    [ProtoMember(4)]
    public readonly int Count;
    [ProtoMember(5)]
    public readonly int WoundedCount;
    [ProtoMember(6)]
    public readonly int XpChange;
    [ProtoMember(7)]
    public readonly bool RemoveDepleted;

    public NetworkTroopRosterAddCounts(string rosterId, string characterId, bool isHero, int count, int woundedCount, int xpChange, bool removeDepleted)
    {
        RosterId = rosterId;
        CharacterId = characterId;
        IsHero = isHero;
        Count = count;
        WoundedCount = woundedCount;
        XpChange = xpChange;
        RemoveDepleted = removeDepleted;
    }
}
