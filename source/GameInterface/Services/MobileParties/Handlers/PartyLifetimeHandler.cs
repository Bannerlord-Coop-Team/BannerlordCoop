using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.MobileParties.Messages.Lifetime;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Handlers;

/// <summary>
/// Lifetime handler for parties
/// </summary>
internal class PartyLifetimeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyLifetimeHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public PartyLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        messageBroker.Subscribe<DestroyPartyApplied>(Handle_PartyDestroyed);
        messageBroker.Subscribe<NetworkApplyDestroyParty>(Handle_DestroyParty);

        messageBroker.Subscribe<DestroyPartyRequested>(Handle_DestroyPartyRequested);
        messageBroker.Subscribe<NetworkRequestDestroyParty>(Handle_NetworkRequestDestroyParty);

        messageBroker.Subscribe<PartyDisbanded>(Handle_PartyDisbanded);
        messageBroker.Subscribe<NetworkPartyDisbanded>(Handle_NetworkPartyDisbanded);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<DestroyPartyApplied>(Handle_PartyDestroyed);
        messageBroker.Unsubscribe<NetworkApplyDestroyParty>(Handle_DestroyParty);

        messageBroker.Unsubscribe<DestroyPartyRequested>(Handle_DestroyPartyRequested);
        messageBroker.Unsubscribe<NetworkRequestDestroyParty>(Handle_NetworkRequestDestroyParty);

        messageBroker.Unsubscribe<PartyDisbanded>(Handle_PartyDisbanded);
        messageBroker.Unsubscribe<NetworkPartyDisbanded>(Handle_NetworkPartyDisbanded);
    }

    internal void Handle_PartyDestroyed(MessagePayload<DestroyPartyApplied> payload)
    {
        var victoriousPartyBase = payload.What.VictoriousPartyBase;
        var defeatedParty = payload.What.DefeatedParty;

        // Vanilla destroys parties with a null destroyer for despawn-style cleanup (e.g. patrol
        // culling). A null victor is legitimate, so send a null id rather than dropping the
        // replication and leaving clients with a zombie party. A non-null victor that cannot be
        // resolved is still a real error and bails as before.
        string victoriousPartyBaseId = null;
        if (victoriousPartyBase != null &&
            !objectManager.TryGetIdWithLogging(victoriousPartyBase, out victoriousPartyBaseId))
            return;

        if (!objectManager.TryGetIdWithLogging(defeatedParty, out var defeatedPartyId))
            return;

        network.SendAll(new NetworkApplyDestroyParty(
            victoriousPartyBaseId,
            defeatedPartyId));

        messageBroker.Publish(this, new InstanceDestroyed<MobileParty>(defeatedParty));
    }

    private void Handle_DestroyParty(MessagePayload<NetworkApplyDestroyParty> payload)
    {
        var message = payload.What;

        var victoriousPartyBaseId = message.VictoriousPartyId;
        var defeatedPartyId = message.DefeatedPartyId;

        var role = ModInformation.IsServer ? "Server" : "Client";

        Logger.Debug(
            "[{Role}] Applying destroy party. VictoriousPartyBaseId={VictoriousPartyBaseId}, DefeatedPartyId={DefeatedPartyId}",
            role,
            victoriousPartyBaseId,
            defeatedPartyId);

        if (!objectManager.TryGetObjectWithLogging<MobileParty>(defeatedPartyId, out var defeatedParty))
            return;

        // An inactive party was already destroyed locally — e.g. this destruction happened inside
        // a parent action that was also replayed here (a settlement ownership change destroying
        // its garrison). Applying it again would double-run the vanilla destruction.
        if (!defeatedParty.IsActive)
        {
            Logger.Debug(
                "[{Role}] Skipping destroy for already-inactive party {DefeatedPartyId}",
                role,
                defeatedPartyId);
            return;
        }

        // A null victor id means the server destroyed the party with a null destroyer (e.g. patrol
        // culling). Pass null through to match the server; vanilla supports a null destroyer. A
        // non-null id that cannot be resolved is still a real error and bails as before.
        PartyBase victoriousPartyBase = null;
        if (victoriousPartyBaseId != null &&
            !objectManager.TryGetObjectWithLogging<PartyBase>(victoriousPartyBaseId, out victoriousPartyBase))
            return;


        RunOnGameThreadSkippingPatches(
            "DestroyPartyAction.Apply",
            () =>
            {
                DestroyPartyAction.Apply(victoriousPartyBase, defeatedParty);
            }
        );
    }

    internal void Handle_DestroyPartyRequested(MessagePayload<DestroyPartyRequested> payload)
    {
        var destroyerParty = payload.What.DestroyerParty;
        var defeatedParty = payload.What.DefeatedParty;

        // The defeated party can be a client-local object with no network id — e.g. a quest-spawned
        // party the server never registered. Those are destroyed locally only and have nothing for
        // the server to replicate, so skip silently (no logging) rather than reporting a missing id.
        if (!objectManager.TryGetId(defeatedParty, out var defeatedPartyId))
            return;

        if (!objectManager.TryGetIdWithLogging(destroyerParty, out var destroyerPartyId))
            return;

        network.SendAll(new NetworkRequestDestroyParty(destroyerPartyId, defeatedPartyId));
    }

    internal void Handle_NetworkRequestDestroyParty(MessagePayload<NetworkRequestDestroyParty> payload)
    {
        // Only the server applies the destroy authoritatively; clients receive the resulting
        // NetworkApplyDestroyParty broadcast instead.
        if (!ModInformation.IsServer)
            return;

        var message = payload.What;

        // Resolve and apply on the game thread with patches LIVE (no AllowedThread): the request
        // arrives on the network (poller) thread, and the party can be destroyed or unregistered
        // between resolving it and the queued action draining, so resolve it at drain time. The
        // server's DestroyPartyActionPatch.PrefixApply then publishes DestroyPartyApplied, and
        // Handle_PartyDestroyed replicates the destruction to every client.
        GameThread.Run(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<PartyBase>(message.DestroyerPartyId, out var destroyerParty))
                    return;

                if (!objectManager.TryGetObjectWithLogging<MobileParty>(message.DefeatedPartyId, out var defeatedParty))
                    return;

                if (!defeatedParty.IsActive)
                    return;

                DestroyPartyAction.Apply(destroyerParty, defeatedParty);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[Server] Failed to apply requested destroy for party {DefeatedPartyId}", message.DefeatedPartyId);
            }
        });
    }

    private void Handle_PartyDisbanded(MessagePayload<PartyDisbanded> payload)
    {
        var party = payload.What.DisbandedParty;
        var settlement = payload.What.RelatedSettlement;

        if (!objectManager.TryGetIdWithLogging(party, out var disbandedPartyId))
            return;

        if (!objectManager.TryGetIdWithLogging(settlement, out var settlementId))
            return;

        Logger.Debug(
            "Sending party disbanded. DisbandedPartyId={DisbandedPartyId}, RelatedSettlementId={RelatedSettlementId}",
            disbandedPartyId,
            settlementId);

        network.SendAll(new NetworkPartyDisbanded(
            disbandedPartyId,
            settlementId));

        messageBroker.Publish(this, new InstanceDestroyed<MobileParty>(party));
    }

    private void Handle_NetworkPartyDisbanded(MessagePayload<NetworkPartyDisbanded> payload)
    {
        var message = payload.What;

        var disbandedPartyId = message.DisbandedPartyId;
        var settlementId = message.RelatedSettlementId;

        var role = ModInformation.IsClient ? "Client" : "Server";

        Logger.Debug(
            "[{Role}] Applying party disband. DisbandedPartyId={DisbandedPartyId}, RelatedSettlementId={RelatedSettlementId}",
            role,
            disbandedPartyId,
            settlementId);

        if (!objectManager.TryGetObjectWithLogging<MobileParty>(disbandedPartyId, out var party))
            return;

        // An inactive party was already disbanded locally — e.g. the disband happened inside a
        // parent action that was also replayed here. Applying it again would double-run the
        // vanilla disband.
        if (!party.IsActive)
        {
            Logger.Debug(
                "[{Role}] Skipping disband for already-inactive party {DisbandedPartyId}",
                role,
                disbandedPartyId);
            return;
        }

        if (!objectManager.TryGetObjectWithLogging<Settlement>(settlementId, out var settlement))
            return;

        RunOnGameThreadSkippingPatches(
            "DestroyPartyAction.ApplyForDisbanding",
            () =>
            {
                DestroyPartyAction.ApplyForDisbanding(party, settlement);

                Logger.Debug(
                    "[{Role}] Applied party disband. DisbandedPartyId={DisbandedPartyId}, RelatedSettlementId={RelatedSettlementId}",
                    role,
                    disbandedPartyId,
                    settlementId);
            }
        );
    }

    private static string Role => ModInformation.IsServer ? "Server" : "Client";

    private void RunOnGameThreadSkippingPatches(
        string operation,
        Action action,
        params object[] logArgs)
    {
        GameThread.Run(() =>
        {
            using (new AllowedThread())
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Logger.Error(
                        ex,
                        "[{Role}] Failed operation {Operation}. {@Context}",
                        Role,
                        operation,
                        logArgs);
                }
            }
        });
    }
}
