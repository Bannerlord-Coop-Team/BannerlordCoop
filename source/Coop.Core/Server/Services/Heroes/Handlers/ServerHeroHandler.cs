using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using GameInterface.Services.Clans;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace Coop.Core.Server.Services.Heroes.Handlers;

/// <summary>
/// Server side handler for hero related messages.
/// </summary>
internal class ServerHeroHandler : IHandler
{
    private readonly ILogger Logger = LogManager.GetLogger<ServerHeroHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly ICoopServer server;
    private readonly INetworkConfig configuration;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly ICoopClanPermissions permissions;

    public ServerHeroHandler(IMessageBroker messageBroker, ICoopServer server, INetworkConfig configuration,
        IObjectManager objectManager, IPlayerManager playerManager, ICoopClanPermissions permissions)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.permissions = permissions;
        this.messageBroker = messageBroker;
        this.server = server;
        this.configuration = configuration;
        messageBroker.Subscribe<HeroNameChanged>(Handle_HeroNameChanged);
        messageBroker.Subscribe<NetworkChangeHeroName>(Handle_NetworkChangeHeroName);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<HeroNameChanged>(Handle_HeroNameChanged);
        messageBroker.Unsubscribe<NetworkChangeHeroName>(Handle_NetworkChangeHeroName);
    }

    private void Handle_HeroNameChanged(MessagePayload<HeroNameChanged> obj)
    {
        var payload = obj.What;

        var message = new NetworkChangeHeroName(payload.Data);

        server.SendAll(message);
    }

    private void Handle_NetworkChangeHeroName(MessagePayload<NetworkChangeHeroName> obj)
    {
        var data = obj.What.Data;
        var sender = obj.Who;

        GameThread.RunSafe(() =>
        {
            if (!CanSenderRename(sender, data.HeroStringId)) return;

            messageBroker.Publish(this, new ChangeHeroName(data));

            server.SendAll(new NetworkChangeHeroName(data));
        }, context: nameof(NetworkChangeHeroName));
    }

    // Same rule as the client's rename gate, for the hero the sending peer plays.
    private bool CanSenderRename(object sender, string heroId)
    {
        if (objectManager.TryGetObject<Hero>(heroId, out var hero) &&
            playerManager.TryGetSenderHero(sender, out var actor) && permissions.CanRenameHero(actor, hero)) return true;

        Logger.Warning("Ignoring a rename of hero {HeroId} from a peer that may not rename it", heroId);
        return false;
    }
}
