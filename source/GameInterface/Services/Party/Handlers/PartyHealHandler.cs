using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.Players;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Party.Handlers;

internal class PartyHealHandler : IHandler
{
    private static readonly ILogger logger = LogManager.GetLogger<PartyHealHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerRegistry playerRegistry;

    public PartyHealHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerRegistry playerRegistry)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerRegistry = playerRegistry;

        messageBroker.Subscribe<PartyHealHourlyTick>(Handle_PartyHealHourlyTick);
        messageBroker.Subscribe<PartyHealQuarterDailyTick>(Handle_PartyHealQuarterDailyTick);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyHealHourlyTick>(Handle_PartyHealHourlyTick);
        messageBroker.Unsubscribe<PartyHealQuarterDailyTick>(Handle_PartyHealQuarterDailyTick);
    }

    private void Handle_PartyHealHourlyTick(MessagePayload<PartyHealHourlyTick> obj)
    {
        foreach (var player in playerRegistry)
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var mobileParty)) continue;

            obj.What.PartyHealCampaignBehavior.TryHealOrWoundParty(mobileParty.Party, (float)CampaignTime.HoursInDay);
        }
    }

    private void Handle_PartyHealQuarterDailyTick(MessagePayload<PartyHealQuarterDailyTick> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

        if (obj.What.MobileParty?.IsPlayerParty() != true) return;

        obj.What.PartyHealCampaignBehavior.TryHealOrWoundParty(obj.What.MobileParty.Party, 4f);
    }
}