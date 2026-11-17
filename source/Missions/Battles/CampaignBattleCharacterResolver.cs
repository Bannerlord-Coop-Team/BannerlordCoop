using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace Missions.Battles;

/// <summary>Maps a battle agent's character to the id its spawn record carries, and back on the peer.</summary>
public interface IBattleCharacterResolver
{
    /// <summary>The wire id of a replicated agent's character; false means the agent is not replicated.</summary>
    bool TryGetCharacterId(BasicCharacterObject character, out string characterId);

    /// <summary>[Game thread] The character a received spawn record names.</summary>
    bool TryResolveCharacter(string characterId, out BasicCharacterObject character);
}

/// <inheritdoc cref="IBattleCharacterResolver"/>
public class CampaignBattleCharacterResolver : IBattleCharacterResolver
{
    private readonly IObjectManager objectManager;

    public CampaignBattleCharacterResolver(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public bool TryGetCharacterId(BasicCharacterObject character, out string characterId)
    {
        characterId = null;
        return character is CharacterObject campaignCharacter && objectManager.TryGetId(campaignCharacter, out characterId);
    }

    public bool TryResolveCharacter(string characterId, out BasicCharacterObject character)
    {
        bool resolved = objectManager.TryGetObjectWithLogging(characterId, out CharacterObject campaignCharacter);
        character = campaignCharacter;
        return resolved;
    }
}
