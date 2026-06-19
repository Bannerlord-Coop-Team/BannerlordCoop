using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

/// <summary>
/// Sets the absolute xp of an element in a roster, keyed by element identity. The client finds the
/// element, creating it first if absent, then sets its xp. <see cref="IsHero"/> as in
/// <see cref="NetworkTroopRosterAddCounts"/>.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkTroopRosterSetXp : ICommand
{
    [ProtoMember(1)]
    public readonly string RosterId;
    [ProtoMember(2)]
    public readonly string CharacterId;
    [ProtoMember(3)]
    public readonly bool IsHero;
    [ProtoMember(4)]
    public readonly int Xp;

    public NetworkTroopRosterSetXp(string rosterId, string characterId, bool isHero, int xp)
    {
        RosterId = rosterId;
        CharacterId = characterId;
        IsHero = isHero;
        Xp = xp;
    }
}
