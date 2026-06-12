using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;

namespace GameInterface.Services.Locations.Messages;

/// <summary>
/// Event for when a character is added to a <see cref="Location"/>'s character list.
/// Carries the extracted fields of the <see cref="LocationCharacter"/> instead of the instance
/// itself so message-flow tests can construct it without a live game.
/// </summary>
public readonly struct LocationCharacterAdded : IEvent
{
    public readonly Location Location;
    public readonly CharacterObject Character;
    public readonly MobileParty OriginParty;
    public readonly ItemObject SpecialItem;
    public readonly string SpawnTag;
    public readonly string ActionSetCode;
    public readonly string BehaviorsMethodName;
    public readonly int CharacterRelation;
    public readonly bool FixedLocation;
    public readonly bool UseCivilianEquipment;

    public LocationCharacterAdded(
        Location location,
        CharacterObject character,
        MobileParty originParty,
        ItemObject specialItem,
        string spawnTag,
        string actionSetCode,
        string behaviorsMethodName,
        int characterRelation,
        bool fixedLocation,
        bool useCivilianEquipment)
    {
        Location = location;
        Character = character;
        OriginParty = originParty;
        SpecialItem = specialItem;
        SpawnTag = spawnTag;
        ActionSetCode = actionSetCode;
        BehaviorsMethodName = behaviorsMethodName;
        CharacterRelation = characterRelation;
        FixedLocation = fixedLocation;
        UseCivilianEquipment = useCivilianEquipment;
    }
}
