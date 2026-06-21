using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Handlers;

/// <summary>
/// Replicates the <see cref="TroopRoster.OwnerParty"/> back-reference. The server publishes
/// <see cref="PartyOwnerSet"/> when the owner is set and broadcasts <see cref="NetworkPartyOwnerSet"/>;
/// clients resolve the roster and party by id and assign the owner locally.
/// </summary>
internal class TroopRosterOwnerPartyHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    private static readonly ILogger Logger = LogManager.GetLogger<TroopRosterOwnerPartyHandler>();

    public TroopRosterOwnerPartyHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<PartyOwnerSet>(Handle_PartyOwnerSet);
        messageBroker.Subscribe<NetworkPartyOwnerSet>(Handle_NetworkPartyOwnerSet);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyOwnerSet>(Handle_PartyOwnerSet);
        messageBroker.Unsubscribe<NetworkPartyOwnerSet>(Handle_NetworkPartyOwnerSet);
    }

    private void Handle_PartyOwnerSet(MessagePayload<PartyOwnerSet> payload)
    {
        var roster = payload.What.Roster;
        var ownerParty = payload.What.OwnerParty;

        if (!objectManager.TryGetIdWithLogging(roster, out var rosterId)) return;

        // OwnerParty may legitimately be null.
        string ownerPartyId = null;
        if (ownerParty != null && !objectManager.TryGetIdWithLogging(ownerParty, out ownerPartyId)) return;

        network.SendAll(new NetworkPartyOwnerSet(rosterId, ownerPartyId));
    }

    private void Handle_NetworkPartyOwnerSet(MessagePayload<NetworkPartyOwnerSet> payload)
    {
        var message = payload.What;

        GameThread.RunSafe(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<TroopRoster>(message.RosterId, out var roster)) return;

                PartyBase ownerParty = null;
                if (message.OwnerPartyId != null &&
                    !objectManager.TryGetObjectWithLogging(message.OwnerPartyId, out ownerParty)) return;

                using (new AllowedThread())
                {
                    roster.OwnerParty = ownerParty;
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message}", nameof(NetworkPartyOwnerSet));
            }
        });
    }
}
