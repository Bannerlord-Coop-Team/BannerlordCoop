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
        messageBroker.Subscribe<ClanCreated>(Handle);
        messageBroker.Subscribe<NetworkCreateClan>(Handle);

        messageBroker.Subscribe<ClanDestroyed>(Handle);
        messageBroker.Subscribe<NetworkDestroyClan>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ClanCreated>(Handle);
        messageBroker.Unsubscribe<NetworkCreateClan>(Handle);
    }

    private void Handle(MessagePayload<ClanCreated> payload)
    {
        if (objectManager.AddNewObject(payload.What.Clan, out string newId) == false)
        {
            Logger.Error("Failed to add {type} to manager", typeof(CultureObject));
            return;
        }

        network.SendAll(new NetworkCreateClan(newId));
    }

    private void Handle(MessagePayload<NetworkCreateClan> obj)
    {
        var newCultureObject = ObjectHelper.SkipConstructor<Clan>();

        var payload = obj.What;

        if (objectManager.AddExisting(payload.ClanId, newCultureObject) == false)
        {
            Logger.Error("Failed to add {type} to manager with id {id}", typeof(CultureObject), payload.ClanId);
            return;
        }
    }

    private void Handle(MessagePayload<ClanDestroyed> payload)
    {
        if (objectManager.TryGetId(payload.What.Clan, out string clanId) == false)
        {
            Logger.Error("Failed to add {type} to manager", typeof(CultureObject));
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

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                DestroyClanAction.ApplyInternal(clan, (DestroyClanAction.DestroyClanActionDetails)payload.Details);
            }
        });
    }
}
