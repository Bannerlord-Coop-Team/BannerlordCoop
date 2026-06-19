using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Serilog;
using System;
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

    public ChangeGovernorHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

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

        GameThread.Run(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<Town>(data.FortificationId, out var fortification)) return;
                if (!objectManager.TryGetObjectWithLogging<Hero>(data.GovernorId, out var governor)) return;

                ChangeGovernorAction.ApplyInternal(fortification, governor);

                network.Send(obj.Who as NetPeer, new RefreshClanMembersList());
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message}", nameof(ChangeGovernor));
            }
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

        GameThread.Run(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(data.GovernorId, out var governor)) return;

                ChangeGovernorAction.ApplyGiveUpInternal(governor);

                network.Send(obj.Who as NetPeer, new RefreshClanMembersList());
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message}", nameof(RemoveGovernor));
            }
        });
    }
}