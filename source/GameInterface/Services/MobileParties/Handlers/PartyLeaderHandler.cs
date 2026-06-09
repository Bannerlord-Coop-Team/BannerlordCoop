using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers;

/// <summary>
/// Replicates party leader changes server -&gt; client.
/// </summary>
internal class PartyLeaderHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyLeaderHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public PartyLeaderHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<PartyLeaderChanged>(Handle_PartyLeaderChanged);
        messageBroker.Subscribe<NetworkChangePartyLeader>(Handle_NetworkChangePartyLeader);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyLeaderChanged>(Handle_PartyLeaderChanged);
        messageBroker.Unsubscribe<NetworkChangePartyLeader>(Handle_NetworkChangePartyLeader);
    }

    /// <summary>[Server] Forward the leader change to clients.</summary>
    private void Handle_PartyLeaderChanged(MessagePayload<PartyLeaderChanged> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.Party, out var mobilePartyId))
            return;

        string leaderHeroId = null;
        if (payload.What.NewLeader != null && !objectManager.TryGetIdWithLogging(payload.What.NewLeader, out leaderHeroId))
            return;

        network.SendAll(new NetworkChangePartyLeader(mobilePartyId, leaderHeroId));
    }

    /// <summary>[Client] Apply the leader change.</summary>
    private void Handle_NetworkChangePartyLeader(MessagePayload<NetworkChangePartyLeader> payload)
    {
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(payload.What.MobilePartyId, out var mobileParty))
            return;

        Hero newLeader = null;
        if (payload.What.LeaderHeroId != null && !objectManager.TryGetObjectWithLogging(payload.What.LeaderHeroId, out newLeader))
            return;

        using (new AllowedThread())
        {
            mobileParty.ChangePartyLeader(newLeader);
        }

        // The leader drives the party's map figure; rebuild it so the change is reflected visually.
        mobileParty.Party.SetVisualAsDirty();
    }
}
