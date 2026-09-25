using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.Clans;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Actions.Handlers;

internal class ChangeGovernorHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ChangeGovernorHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly ICoopClanPermissions permissions;

    public ChangeGovernorHandler(
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

        messageBroker.Subscribe<GovernorChanged>(Handle_GovernorChanged);
        messageBroker.Subscribe<ChangeGovernor>(Handle_ChangeGovernor);
        messageBroker.Subscribe<GovernorRemoved>(Handle_GovernorRemoved);
        messageBroker.Subscribe<RemoveGovernor>(Handle_RemoveGovernor);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<GovernorChanged>(Handle_GovernorChanged);
        messageBroker.Unsubscribe<ChangeGovernor>(Handle_ChangeGovernor);
        messageBroker.Unsubscribe<GovernorRemoved>(Handle_GovernorRemoved);
        messageBroker.Unsubscribe<RemoveGovernor>(Handle_RemoveGovernor);
    }

    private void Handle_GovernorChanged(MessagePayload<GovernorChanged> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Fortification, out var fortificationId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.Governor, out var governorId)) return;

        var message = new ChangeGovernor(fortificationId, governorId);
        network.SendAll(message);
    }

    private void Handle_ChangeGovernor(MessagePayload<ChangeGovernor> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Town>(data.FortificationId, out var fortification)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.GovernorId, out var governor)) return;
            if (ModInformation.IsServer && !CanManageClan(obj.Who, fortification.OwnerClan)) return;

            ChangeGovernorAction.ApplyInternal(fortification, governor);

            messageBroker.Publish(this, new ClanManagementChanged(governor.Clan, ClanManagementRefresh.Members));
        });
    }

    private void Handle_GovernorRemoved(MessagePayload<GovernorRemoved> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Governor, out var governorId)) return;

        var message = new RemoveGovernor(governorId);
        network.SendAll(message);
    }

    private void Handle_RemoveGovernor(MessagePayload<RemoveGovernor> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.GovernorId, out var governor)) return;
            if (ModInformation.IsServer &&
                (governor.GovernorOf == null || !CanManageClan(obj.Who, governor.GovernorOf.OwnerClan))) return;

            ChangeGovernorAction.ApplyGiveUpInternal(governor);

            messageBroker.Publish(this, new ClanManagementChanged(governor.Clan, ClanManagementRefresh.Members));
        });
    }

    private bool CanManageClan(object sender, Clan clan)
    {
        return sender is NetPeer peer && playerManager.TryGetPlayer(peer, out var player) &&
            objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var actor) &&
            permissions.CanManageClan(actor, clan);
    }
}
