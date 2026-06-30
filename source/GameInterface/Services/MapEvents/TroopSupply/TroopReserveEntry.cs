using ProtoBuf;

namespace GameInterface.Services.MapEvents.TroopSupply;

/// <summary>
/// One troop the server has committed to a battle, in the authoritative spawn order. The server owns the
/// <see cref="Seed"/> (a stable <c>UniqueTroopDescriptor</c> seed) so troop identity never drifts across
/// clients or re-flattens — that is what lets a custom troop supplier source troops from the server and
/// have casualties match. <see cref="CharacterId"/> is the object-manager id of the troop's CharacterObject
/// (hero CharacterObjects are registered too); <see cref="FormationClass"/> is the troop's formation class for
/// spawn prioritisation.
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
