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

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.HeroStringId, out var hero)) return;
            if (!playerManager.TryGetSenderHero(obj.Who, out var senderHero) || !permissions.CanRenameHero(senderHero, hero)) return;

            messageBroker.Publish(this, new ChangeHeroName(data));

            server.SendAll(new NetworkChangeHeroName(data));
        }, context: nameof(NetworkChangeHeroName));
    }
}
