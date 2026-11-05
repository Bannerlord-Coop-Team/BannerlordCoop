using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Clans.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using SandBox.GauntletUI;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Clans.Handlers;

public class ClanNameHandler : IHandler
{
    private readonly ILogger Logger = LogManager.GetLogger<ClanNameHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly ICoopClanPermissions permissions;

    public ClanNameHandler(
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

        messageBroker.Subscribe<ChangeClanName>(Handle_ChangeClanName);
        messageBroker.Subscribe<NetworkChangeClanName>(Handle_NetworkChangeClanName);
        messageBroker.Subscribe<NetworkRefreshAfterClanNameChange>(Handle_NetworkRefreshAfterClanNameChange);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ChangeClanName>(Handle_ChangeClanName);
        messageBroker.Unsubscribe<NetworkChangeClanName>(Handle_NetworkChangeClanName);
        messageBroker.Unsubscribe<NetworkRefreshAfterClanNameChange>(Handle_NetworkRefreshAfterClanNameChange);
    }

    private void Handle_ChangeClanName(MessagePayload<ChangeClanName> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.Clan, out var clanId)) return;

        if (ModInformation.IsServer)
        {
            ClanNameChangePatch.ChangeClanNameOverride(data.Clan, data.Name, data.InformalName);

            network.SendAll(new NetworkRefreshAfterClanNameChange(clanId, data.Name, data.InformalName));

            return;
        }

        network.SendAll(new NetworkChangeClanName(clanId, data.Name, data.InformalName));
    }

    private void Handle_NetworkChangeClanName(MessagePayload<NetworkChangeClanName> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Clan>(data.ClanId, out var clan)) return;
            if (!playerManager.IsSenderControllingClan(obj.Who, clan)) return;
            if (!playerManager.TryGetSenderHero(obj.Who, out var senderHero) || !permissions.CanManageClan(senderHero, clan)) return;

            ClanNameChangePatch.ChangeClanNameOverride(clan, data.Name, data.InformalName);

            network.SendAll(new NetworkRefreshAfterClanNameChange(data.ClanId, data.Name, data.InformalName));
        });
    }

    private void Handle_NetworkRefreshAfterClanNameChange(MessagePayload<NetworkRefreshAfterClanNameChange> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Clan>(data.ClanId, out var clan)) return;

            using (new AllowedThread())
            {
                ClanNameChangePatch.ChangeClanNameOverride(clan, data.Name, data.InformalName);
            }

            if (ScreenManager.TopScreen is GauntletClanScreen clanScreen && clan == Clan.PlayerClan)
            {
                clanScreen._dataSource?.RefreshValues();
            }
        });
    }
}
