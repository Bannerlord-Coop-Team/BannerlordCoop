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
    private readonly IPlayerManager playerManager;

    public PartyHealHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerRegistry)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerRegistry;

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
        foreach (var player in playerManager.Players)
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var mobileParty)) continue;

            obj.What.PartyHealCampaignBehavior.TryHealOrWoundParty(mobileParty.Party, (float)CampaignTime.HoursInDay);
        }
    }

    private void Handle_PartyHealQuarterDailyTick(MessagePayload<PartyHealQuarterDailyTick> obj)
    {
        var mobileParty = obj.What.MobileParty;

        // Vanilla's quarter-daily tick heals every party EXCEPT the main party, which the hourly
        // tick covers instead. In coop every player party is a "main party" healed by
        // Handle_PartyHealHourlyTick, so skip them here and heal everyone else — this previously
        // filtered the wrong way around, leaving all NPC parties' wounded unhealed forever.
        if (mobileParty == null || mobileParty.IsPlayerParty()) return;

        obj.What.PartyHealCampaignBehavior.TryHealOrWoundParty(mobileParty.Party, 4f);
    }
}