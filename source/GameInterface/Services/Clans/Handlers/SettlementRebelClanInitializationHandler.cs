using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Clans.Handlers;

internal sealed class SettlementRebelClanInitializationHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public SettlementRebelClanInitializationHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<SettlementRebelClanInitialized>(Handle);
        messageBroker.Subscribe<NetworkInitializeSettlementRebelClan>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SettlementRebelClanInitialized>(Handle);
        messageBroker.Unsubscribe<NetworkInitializeSettlementRebelClan>(Handle);
    }

    private void Handle(MessagePayload<SettlementRebelClanInitialized> payload)
    {
        Clan clan = payload.What.Clan;

        if (!objectManager.TryGetIdWithLogging(clan, out string clanId)) return;
        if (!objectManager.TryGetIdWithLogging(clan.Culture, out string cultureId)) return;
        if (!objectManager.TryGetIdWithLogging(clan.Leader, out string leaderId)) return;
        if (!objectManager.TryGetIdWithLogging(clan.InitialHomeSettlement, out string initialHomeSettlementId)) return;
        if (!objectManager.TryGetIdWithLogging(clan.HomeSettlement, out string homeSettlementId)) return;

        network.SendAll(new NetworkInitializeSettlementRebelClan(
            clanId,
            cultureId,
            leaderId,
            initialHomeSettlementId,
            homeSettlementId,
            clan.Banner?.Serialize(),
            clan.Tier,
            clan.Color,
            clan.Color2,
            clan.BannerBackgroundColorPrimary,
            clan.BannerBackgroundColorSecondary,
            clan.BannerIconColor,
            clan.IsRebelClan));
    }

    private void Handle(MessagePayload<NetworkInitializeSettlementRebelClan> payload)
    {
        NetworkInitializeSettlementRebelClan data = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging(data.ClanId, out Clan clan)) return;
            if (!objectManager.TryGetObjectWithLogging(data.CultureId, out CultureObject culture)) return;
            if (!objectManager.TryGetObjectWithLogging(data.LeaderId, out Hero leader)) return;
            if (!objectManager.TryGetObjectWithLogging(data.InitialHomeSettlementId, out Settlement initialHomeSettlement)) return;
            if (!objectManager.TryGetObjectWithLogging(data.HomeSettlementId, out Settlement homeSettlement)) return;

            using (new AllowedThread())
            {
                clan.Culture = culture;
                clan._leader = leader;
                clan._banner = data.BannerCode == null ? null : new Banner(data.BannerCode);
                clan._tier = data.Tier;
                clan.InitialHomeSettlement = initialHomeSettlement;
                clan._home = homeSettlement;
                clan.Color = data.Color;
                clan.Color2 = data.Color2;
                clan.BannerBackgroundColorPrimary = data.BannerBackgroundColorPrimary;
                clan.BannerBackgroundColorSecondary = data.BannerBackgroundColorSecondary;
                clan.BannerIconColor = data.BannerIconColor;
                clan.IsRebelClan = data.IsRebelClan;
                clan._distanceToClosestNonAllyFortificationCacheDirty = true;
            }
        }, context: nameof(NetworkInitializeSettlementRebelClan));
    }
}
