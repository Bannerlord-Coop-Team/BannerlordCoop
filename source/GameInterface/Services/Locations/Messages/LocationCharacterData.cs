using ProtoBuf;

namespace GameInterface.Services.Locations.Messages;

/// <summary>
/// Serializable description of a single <see cref="TaleWorlds.CampaignSystem.Settlements.Locations.LocationCharacter"/>
/// roster entry. Carries object ids and the semantic fields needed to rebuild the entry on a client;
/// the behaviors delegate travels as a static method name because delegates cannot be serialized.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class LocationCharacterData
{
    [ProtoMember(1)]
    public string LocationId { get; }
    [ProtoMember(2)]
    public string CharacterId { get; }
    [ProtoMember(3)]
    public string OriginPartyId { get; }
    [ProtoMember(4)]
    public string SpecialItemId { get; }
    [ProtoMember(5)]
    public string SpawnTag { get; }
    [ProtoMember(6)]
    public string ActionSetCode { get; }
    [ProtoMember(7)]
    public string BehaviorsMethodName { get; }
    [ProtoMember(8)]
    public int CharacterRelation { get; }
    [ProtoMember(9)]
    public bool FixedLocation { get; }
    [ProtoMember(10)]
    public bool UseCivilianEquipment { get; }

    public LocationCharacterData(
        string locationId,
        string characterId,
        string originPartyId,
        string specialItemId,
        string spawnTag,
        string actionSetCode,
        string behaviorsMethodName,
        int characterRelation,
        bool fixedLocation,
        bool useCivilianEquipment)
    {
        LocationId = locationId;
        CharacterId = characterId;
        OriginPartyId = originPartyId;
        SpecialItemId = specialItemId;
        SpawnTag = spawnTag;
        ActionSetCode = actionSetCode;
        BehaviorsMethodName = behaviorsMethodName;
        CharacterRelation = characterRelation;
        FixedLocation = fixedLocation;
        UseCivilianEquipment = useCivilianEquipment;
    }
}
