using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// [Game thread] Finds a player's party and hero by controller id, and tells which battle agents belong to that party.
/// </summary>
public interface IBattlePlayerPartyResolver
{
    /// <summary>The player party and hero behind a controller id, from the session-scoped player registry.</summary>
    bool TryGetPlayerParty(string controllerId, out PartyBase party, out Hero hero);

    /// <summary>Whether an agent is the player's hero or a troop whose origin party is the player's party.</summary>
    bool IsOwnPartyAgent(Agent agent, PartyBase playerParty, Hero playerHero);
}

/// <inheritdoc cref="IBattlePlayerPartyResolver"/>
public class BattlePlayerPartyResolver : IBattlePlayerPartyResolver
{
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;

    public BattlePlayerPartyResolver(IPlayerManager playerManager, IObjectManager objectManager)
    {
        this.playerManager = playerManager;
        this.objectManager = objectManager;
    }

    // The party is how own-party agents are told apart on every client: puppets carry their origin party (see
    // PuppetSpawner), so a PartyBase comparison holds whatever the map event's membership is now.
    public bool TryGetPlayerParty(string controllerId, out PartyBase party, out Hero hero)
    {
        party = null;
        hero = null;

        if (!playerManager.TryGetPlayer(controllerId, out var player)) return false;
        if (objectManager.TryGetObject<Hero>(player.HeroId, out var playerHero))
            hero = playerHero;

        if (!objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var mobileParty)) return false;
        party = mobileParty?.Party;
        return party != null;
    }

    // The remote counterpart of OwnedAgentReplicator.IsOwnPartyAgent, which compares against PartyBase.MainParty. The hero
    // check covers a hero agent whose origin party did not resolve at spawn.
    public bool IsOwnPartyAgent(Agent agent, PartyBase playerParty, Hero playerHero)
    {
        if (playerHero != null && agent.Character is CharacterObject character && character.IsHero && character.HeroObject == playerHero)
            return true;
        return agent.Origin is CoopAgentOrigin origin && origin.Party == playerParty;
    }
}
