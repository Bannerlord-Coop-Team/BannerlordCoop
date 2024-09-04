using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Clans.Messages.Lifetime;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;

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
        network.SendAll(new NetworkCreateClan(payload.What.Data));
    }

    private void Handle(MessagePayload<NetworkCreateClan> payload)
    {
        ClanLifetimePatches.OverrideCreateNewClan(payload.What.Data.ClanId);
    }

    private void Handle(MessagePayload<ClanDestroyed> payload)
    {
        network.SendAll(new NetworkDestroyClan(payload.What.Data));
    }

    private void Handle(MessagePayload<NetworkDestroyClan> payload)
    {
        var data = payload.What.Data;
        if (objectManager.TryGetObject<Clan>(data.ClanId, out var clan) == false)
        {
            Logger.Error("Unable to find clan with string id {stringId}", data.ClanId);
            return;
        }

        ClanLifetimePatches.OverrideDestroyClan(clan, data.Details);
    }
}
