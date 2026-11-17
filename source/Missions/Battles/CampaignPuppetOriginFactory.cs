using Common.Logging;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using Missions.Messages;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>Builds the origin a replicated puppet spawns with.</summary>
public interface IPuppetOriginFactory
{
    /// <summary>[Game thread] The puppet's origin; false keeps the record buffered until its identity arrives.</summary>
    bool TryCreate(BasicCharacterObject character, BattleAgentSpawnData data, Team team, out IAgentOriginBase origin);
}

/// <inheritdoc cref="IPuppetOriginFactory"/>
public class CampaignPuppetOriginFactory : IPuppetOriginFactory
{
    private static readonly ILogger Logger = LogManager.GetLogger<CampaignPuppetOriginFactory>();

    private readonly IObjectManager objectManager;

    public CampaignPuppetOriginFactory(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public bool TryCreate(BasicCharacterObject character, BattleAgentSpawnData data, Team team, out IAgentOriginBase origin)
    {
        origin = null;

        // Carry the troop's party so the agent has a real BattleCombatant, the battle observer/scoreboard
        // reads origin.BattleCombatant, and SimpleAgentOrigin leaves it null for non-hero troops.
        var party = ResolvePuppetParty(data.MapEventPartyId);

        if (party == null)
        {
            // World-state registration and mission-mesh spawns use different channels. An explicit party id
            // can therefore arrive first; retain it until the game-thread registry apply catches up.
            if (!string.IsNullOrEmpty(data.MapEventPartyId)) return false;

            // An unattributed spawn record must still produce a body, a puppet that never spawns is an
            // invisible enemy (and re-buffering forever spams the log every tick). Fall back to any
            // involved party on the agent's side; only the scoreboard attribution degrades.
            party = ResolveFallbackParty(data.Side);
            if (party == null)
            {
                Logger.Warning("[BattleSync] Puppet skipped: unresolved party {Party} for agent {AgentId}", data.MapEventPartyId, data.AgentId);
                return false;
            }

            Logger.Warning("[BattleSync] Puppet {AgentId} spawned with a fallback {Side} party; {Party} unresolved", data.AgentId, data.Side, data.MapEventPartyId);
        }

        origin = new CoopAgentOrigin((CharacterObject)character, party, -1, null, new UniqueTroopDescriptor(data.TroopSeed), data.MapEventPartyId);
        return true;
    }

    // The PartyBase for a battle party id (a MapEventParty object-manager id), used for a puppet's origin.
    private PartyBase ResolvePuppetParty(string mapEventPartyId)
    {
        if (mapEventPartyId != null && objectManager.TryGetObject<MapEventParty>(mapEventPartyId, out var mapEventParty))
            return mapEventParty?.Party;
        return null;
    }

    // Any involved party of the given side, for a spawn record whose own party never resolved here.
    private static PartyBase ResolveFallbackParty(BattleSideEnum side)
    {
        var mapEvent = MobileParty.MainParty?.MapEvent;
        if (mapEvent == null || side == BattleSideEnum.None) return null;

        foreach (var involved in mapEvent.GetMapEventSide(side).Parties)
        {
            if (involved?.Party != null) return involved.Party;
        }

        return null;
    }
}
