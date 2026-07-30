using ProtoBuf;

namespace GameInterface.Services.MapEvents.TroopSupply;

/// <summary>
/// One troop the server has committed to a battle, in the authoritative spawn order. The server owns the
/// <see cref="Seed"/> so every client agrees on the spawned agent's identity. A later server roster re-flatten
/// can replace its current descriptor, so authoritative hit and casualty applies resolve <see cref="CharacterId"/>
/// against the current roster. <see cref="FormationClass"/> controls spawn prioritisation.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public struct TroopReserveEntry
{
    [ProtoMember(1)]
    public int Seed { get; }
    [ProtoMember(2)]
    public string CharacterId { get; }
    [ProtoMember(4)]
    public int FormationClass { get; }

    public TroopReserveEntry(int seed, string characterId, int formationClass)
    {
        Seed = seed;
        CharacterId = characterId;
        FormationClass = formationClass;
    }
}
