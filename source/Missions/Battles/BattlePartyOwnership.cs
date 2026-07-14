using GameInterface.Services.MapEvents.TroopSupply;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Shared "does this agent belong to a given player's own party" predicate. One implementation for both the
/// local flavor (<c>OwnedAgentReplicator</c>, comparing against <see cref="PartyBase.MainParty"/> /
/// <see cref="Hero.MainHero"/>) and the remote flavor (<c>BattleAuthorityMigrator</c>, comparing against a
/// resolved player's party/hero). Own-party troops are the ones withheld until deployment commit and the ones
/// that withdraw with a retreating player rather than being adopted.
/// </summary>
internal static class BattlePartyOwnership
{
    /// <summary>
    /// True when <paramref name="agent"/> belongs to <paramref name="party"/>: the party's hero itself, or a
    /// troop whose origin party is it. The hero check is a belt for a hero agent whose origin party did not
    /// resolve at spawn.
    /// </summary>
    internal static bool IsOwnPartyAgent(Agent agent, PartyBase party, Hero hero)
    {
        if (hero != null && agent.Character is CharacterObject character && character.IsHero && character.HeroObject == hero)
            return true;
        return agent.Origin is CoopAgentOrigin origin && origin.Party == party;
    }
}
