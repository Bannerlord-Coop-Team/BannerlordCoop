using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.ObjectManager;
using Missions.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;

namespace Missions.Battles;

/// <summary>
/// Owner-side death reporting for a coop battle: when one of OUR authoritative agents dies
/// (<see cref="BattleAgentDied"/>, published by <c>BattleAgentRemovedPatch</c>), tell every client to kill its
/// puppet of it and tell the server to account the casualty against the map-event party roster, then drop the
/// agent from the registry so the movement handler stops broadcasting it. A puppet death (from an applied
/// broadcast) is ignored — its authority is the owner, not us — so there is no echo. Registered MOUNTS flow
/// through here too (BattleAgentRemovedPatch fires for any agent): they broadcast like troops so the horse dies
/// on every client, but carry no casualty attribution, so no server roster report is sent for them.
/// </summary>
public interface IAgentDeathReporter : IDisposable
{
}

/// <inheritdoc cref="IAgentDeathReporter"/>
public class AgentDeathReporter : IAgentDeathReporter
{
    private static readonly ILogger Logger = LogManager.GetLogger<AgentDeathReporter>();

    private readonly IBattleNetwork network;
    private readonly INetwork relayNetwork;
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly IBattleSession session;
    private readonly ICasualtyAttributionMap casualties;

    public AgentDeathReporter(
        IBattleNetwork network,
        INetwork relayNetwork,
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        ICoopMissionComponent coopMissionComponent,
        IBattleSession session,
        ICasualtyAttributionMap casualties)
    {
        this.network = network;
        this.relayNetwork = relayNetwork;
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.coopMissionComponent = coopMissionComponent;
        this.session = session;
        this.casualties = casualties;

        messageBroker.Subscribe<BattleAgentDied>(Handle_BattleAgentDied);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<BattleAgentDied>(Handle_BattleAgentDied);
    }

    private void Handle_BattleAgentDied(MessagePayload<BattleAgentDied> payload)
    {
        var registry = coopMissionComponent.AgentRegistry;

        GameThread.RunSafe(() =>
        {
            if (!registry.TryGetAgentInfo(payload.What.Agent, out var info))
            {
                Logger.Information("[DeathDiag] An agent died but is not in our registry — not ours to broadcast (a puppet or an uncaptured agent)");
                return;
            }
            if (info.CurrentAuthority != session.OwnControllerId)
            {
                Logger.Information("[DeathDiag] Agent {AgentId} died but its authority is {Auth}, not us ({Us}) — not broadcasting", info.AgentId, info.CurrentAuthority, session.OwnControllerId);
                return;
            }

            Guid affectorAgentId = Guid.Empty;
            if (payload.What.AffectorAgent != null
                && registry.TryGetAgentInfo(payload.What.AffectorAgent, out var affectorInfo))
            {
                affectorAgentId = affectorInfo.AgentId;
            }

            var attribution = casualties.GetOrDefault(info.AgentId);

            // Only wound player heros
            bool wounded = payload.What.Wounded;
            if (attribution.TroopCharacterId != null
                && objectManager.TryGetObject<CharacterObject>(attribution.TroopCharacterId, out var troop)
                && troop.HeroObject?.IsPlayerHero() == true)
            {
                wounded = true;
            }

            Logger.Information("[DeathDiag] Broadcasting death of agent {AgentId} (wounded={Wounded}) to the battle mesh", info.AgentId, wounded);
            network.SendAll(new NetworkBattleAgentDied(info.AgentId, wounded, affectorAgentId, payload.What.KillingBlow));

            // Owner-authoritative casualty: tell the server to account this troop's death/wound against its
            // map-event party roster. The server-side mission accounting is suppressed during a coop battle
            // (MapEventPartyPatches), so this is the single source. On a client, SendAll targets the server.
            if (attribution.MapEventPartyId != null)
                relayNetwork.SendAll(new NetworkRequestBattleCasualty(attribution.MapEventPartyId, attribution.TroopCharacterId, wounded));

            casualties.Forget(info.AgentId);
            registry.RemoveAgent(info.AgentId);
        });
    }
}
