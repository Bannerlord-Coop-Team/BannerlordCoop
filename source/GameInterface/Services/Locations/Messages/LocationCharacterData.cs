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
    // Bone-attached carry props (baskets, pitchers, carried goods): LocationCharacter.PrefabNamesForBones
    // as parallel arrays. Natively the AgentNavigator attaches these at spawn; puppets have no navigator,
    // so the receiver attaches them straight from this data — without it every carrier plays its carry
    // action set with empty hands.
    [ProtoMember(11)]
    public int[] PrefabBones { get; }
    [ProtoMember(12)]
    public string[] PrefabNames { get; }

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
        bool useCivilianEquipment,
        int[] prefabBones = null,
        string[] prefabNames = null)
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
        PrefabBones = prefabBones;
        PrefabNames = prefabNames;
    }
}
