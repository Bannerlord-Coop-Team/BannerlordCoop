using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.PlayerCaptivityService.Messages;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Handlers;

/// <summary>Scans for nearby AI when players join a battle and while its join window remains open.</summary>
internal sealed class NearbyPartyReinforcementHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INearbyPartyReinforcer nearbyPartyReinforcer;

    public NearbyPartyReinforcementHandler(
        IMessageBroker messageBroker,
        INearbyPartyReinforcer nearbyPartyReinforcer)
    {
        this.messageBroker = messageBroker;
        this.nearbyPartyReinforcer = nearbyPartyReinforcer;

        messageBroker.Subscribe<PlayerJoinedBattle>(Handle_PlayerJoinedBattle);
        messageBroker.Subscribe<CampaignTick>(Handle_CampaignTick);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerJoinedBattle>(Handle_PlayerJoinedBattle);
        messageBroker.Unsubscribe<CampaignTick>(Handle_CampaignTick);
    }

    private void Handle_PlayerJoinedBattle(MessagePayload<PlayerJoinedBattle> payload)
    {
        if (!ModInformation.IsServer)
            return;

        GameThread.RunSafe(() =>
        {
            if (payload.Who is not MapEvent mapEvent)
                return;

            using (AllowedThread.Suspend())
                nearbyPartyReinforcer.Reinforce(mapEvent);
        });
    }

    private void Handle_CampaignTick(MessagePayload<CampaignTick> payload)
    {
        if (!ModInformation.IsServer)
            return;

        GameThread.RunSafe(() =>
        {
            using (AllowedThread.Suspend())
                nearbyPartyReinforcer.ReinforceOpenPlayerBattles();
        });
    }
}
