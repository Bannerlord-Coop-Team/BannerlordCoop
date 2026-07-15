using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.HeroDevelopers.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.HeroDevelopers.Handlers;

internal class PerkActivationHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PerkActivationHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public PerkActivationHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<UpdateRosterVersionAfterPerkActivation>(Handle_UpdateRosterVersionAfterPerkActivation);
        messageBroker.Subscribe<NetworkUpdateRosterVersionAfterPerkActivation>(Handle_NetworkUpdateRosterVersionAfterPerkActivation);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<UpdateRosterVersionAfterPerkActivation>(Handle_UpdateRosterVersionAfterPerkActivation);
        messageBroker.Unsubscribe<NetworkUpdateRosterVersionAfterPerkActivation>(Handle_NetworkUpdateRosterVersionAfterPerkActivation);
    }

    private void Handle_UpdateRosterVersionAfterPerkActivation(MessagePayload<UpdateRosterVersionAfterPerkActivation> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.MemberRoster, out var memberRosterId)) return;

            var message = new NetworkUpdateRosterVersionAfterPerkActivation(memberRosterId);
            network.SendAll(message);
        });
    }

    private void Handle_NetworkUpdateRosterVersionAfterPerkActivation(MessagePayload<NetworkUpdateRosterVersionAfterPerkActivation> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<TroopRoster>(data.MemberRosterId, out var memberRoster)) return;

            memberRoster.UpdateVersion();
        });
    }

}
