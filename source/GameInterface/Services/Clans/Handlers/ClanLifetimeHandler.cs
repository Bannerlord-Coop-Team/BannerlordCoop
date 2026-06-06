using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Clans.Messages.Lifetime;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Clans.Handlers;

/// <summary>
/// Lifetime handler for the Clan class. Purpose of managing clan creation and deletion across the network
/// </summary>
internal class ClanLifetimeHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ILogger Logger = LogManager.GetLogger<ClanLifetimeHandler>();

    public ClanLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<ClanDestroyed>(Handle);
        messageBroker.Subscribe<NetworkDestroyClan>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ClanDestroyed>(Handle);
        messageBroker.Unsubscribe<NetworkDestroyClan>(Handle);
    }

    private void Handle(MessagePayload<ClanDestroyed> payload)
    {
        if (objectManager.TryGetId(payload.What.Clan, out string clanId) == false)
        {
            Logger.Error("Failed to get {type} id", typeof(Clan));
            return;
        }

        if (objectManager.Remove(payload.What.Clan) == false)
        {
            Logger.Error("Failed to remove {type} from registry", typeof(Clan));
            return;
        }    

        network.SendAll(new NetworkDestroyClan(clanId, payload.What.Details));
    }

    private void Handle(MessagePayload<NetworkDestroyClan> obj)
    {
        var payload = obj.What;
        if (objectManager.TryGetObject<Clan>(payload.ClanId, out var clan) == false)
        {
            Logger.Error("Unable to find clan with string id {stringId}", payload.ClanId);
            return;
        }

        if (objectManager.Remove(clan) == false)
        {
            Logger.Error("Failed to remove {type} from registry", typeof(Clan));
            return;
        }

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                DestroyClanAction.ApplyInternal(clan, (DestroyClanAction.DestroyClanActionDetails)payload.Details);
            }
        });
    }
}
