using LiteNetLib;
using GameInterface.Services.Players;
using GameInterface.Services.Clans;
using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers;

internal class PartyWageLimitHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyWageLimitHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly ICoopClanPermissions permissions;

    public PartyWageLimitHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        ICoopClanPermissions permissions)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.permissions = permissions;

        messageBroker.Subscribe<WagePaymentLimitSet>(Handle_WagePaymentLimitSet);
        messageBroker.Subscribe<SetWagePaymentLimit>(Handle_SetWagePaymentLimit);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<WagePaymentLimitSet>(Handle_WagePaymentLimitSet);
        messageBroker.Unsubscribe<SetWagePaymentLimit>(Handle_SetWagePaymentLimit);
    }

    private void Handle_WagePaymentLimitSet(MessagePayload<WagePaymentLimitSet> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

        var message = new SetWagePaymentLimit(mobilePartyId, obj.What.NewValue);
        network.SendAll(message);
    }

    private void Handle_SetWagePaymentLimit(MessagePayload<SetWagePaymentLimit> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;
            if (mobileParty.IsPlayerParty()) return;
            if (!CanSenderManage(obj.Who, mobileParty)) return;

            mobileParty.SetWagePaymentLimit(obj.What.NewValue);
        });
    }

    // Wage limits of a clan party are set by its clan leader.
    private bool CanSenderManage(object sender, MobileParty target)
    {
        if (!ModInformation.IsServer || sender is not NetPeer) return true;
        if (playerManager.TryGetSenderHero(sender, out var actor) && permissions.CanManageParty(actor, target)) return true;

        Logger.Warning("Ignoring a request for {Target} from a peer that may not change it", target?.StringId);
        return false;
    }
}
