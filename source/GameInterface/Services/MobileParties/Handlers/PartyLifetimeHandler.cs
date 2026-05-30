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
using TaleWorlds.CampaignSystem;
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

        messageBroker.Subscribe<PartyDisbanded>(Handle_PartyDisbanded);
        messageBroker.Subscribe<NetworkPartyDisbanded>(Handle_NetworkPartyDisbanded);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<DestroyPartyApplied>(Handle_PartyDestroyed);
        messageBroker.Unsubscribe<NetworkApplyDestroyParty>(Handle_DestroyParty);

        messageBroker.Unsubscribe<PartyDisbanded>(Handle_PartyDisbanded);
        messageBroker.Unsubscribe<NetworkPartyDisbanded>(Handle_NetworkPartyDisbanded);
    }

    private void Handle_PartyDestroyed(MessagePayload<DestroyPartyApplied> payload)
    {
        var victoriousPartyBase = payload.What.VictoriousPartyBase;
        var defeatedParty = payload.What.DefeatedParty;

        if (!objectManager.TryGetIdWithLogging(victoriousPartyBase, out var victoriousPartyBaseId))
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

        if (!objectManager.TryGetObjectWithLogging<PartyBase>(victoriousPartyBaseId, out var victoriousPartyBase))
            return;


        RunOnGameThreadSkippingPatches(
            "DestroyPartyAction.Apply",
            () =>
            {
                DestroyPartyAction.Apply(victoriousPartyBase, defeatedParty);
            }
        );
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
        GameLoopRunner.RunOnMainThread(() =>
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
