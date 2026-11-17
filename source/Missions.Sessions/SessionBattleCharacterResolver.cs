using Missions.Battles;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Missions.Sessions;

/// <summary>
/// Character ids for a mission session with no campaign: the troop's StringId, which every client
/// resolves from the same module data.
/// </summary>
internal sealed class SessionBattleCharacterResolver : IBattleCharacterResolver
{
    public bool TryGetCharacterId(BasicCharacterObject character, out string characterId)
    {
        characterId = character?.StringId;
        return !string.IsNullOrEmpty(characterId);
    }

    public bool TryResolveCharacter(string characterId, out BasicCharacterObject character)
    {
        // A record id is network content, so an empty one is refused before the engine lookup.
        character = string.IsNullOrEmpty(characterId) ? null : MBObjectManager.Instance?.GetObject<BasicCharacterObject>(characterId);
        return character != null;
    }
}
