using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Barters;
using GameInterface.Services.Hideouts.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Hideouts.Handlers;

/// <summary>Applies client hideout menu consequences on the authoritative server.</summary>
internal sealed class HideoutCampaignConsequencesHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<HideoutCampaignConsequencesHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;

    public HideoutCampaignConsequencesHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;

        messageBroker.Subscribe<HideoutCampaignConsequenceRequested>(Handle_HideoutCampaignConsequenceRequested);
        messageBroker.Subscribe<NetworkHideoutCampaignConsequenceRequested>(Handle_NetworkHideoutCampaignConsequenceRequested);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<HideoutCampaignConsequenceRequested>(Handle_HideoutCampaignConsequenceRequested);
        messageBroker.Unsubscribe<NetworkHideoutCampaignConsequenceRequested>(Handle_NetworkHideoutCampaignConsequenceRequested);
    }

    private void Handle_HideoutCampaignConsequenceRequested(
        MessagePayload<HideoutCampaignConsequenceRequested> payload)
    {
        if (!ModInformation.IsClient ||
            !objectManager.TryGetIdWithLogging(payload.What.Settlement, out var settlementId))
            return;

        network.SendAll(new NetworkHideoutCampaignConsequenceRequested(
            settlementId,
            payload.What.Consequence));
    }

    private void Handle_NetworkHideoutCampaignConsequenceRequested(
        MessagePayload<NetworkHideoutCampaignConsequenceRequested> payload)
    {
        if (ModInformation.IsClient)
            return;

        if (payload.Who is not NetPeer peer || !playerManager.TryGetPlayer(peer, out var player))
        {
            Logger.Warning("Rejected hideout consequence request from an unregistered peer");
            return;
        }

        GameThread.RunSafe(
            () => ApplyConsequence(player.HeroId, player.MobilePartyId, payload.What),
            context: nameof(Handle_NetworkHideoutCampaignConsequenceRequested));
    }

    private void ApplyConsequence(
        string heroId,
        string mobilePartyId,
        NetworkHideoutCampaignConsequenceRequested request)
    {
        if (!objectManager.TryGetObject<Hero>(heroId, out var playerHero) ||
            !objectManager.TryGetObject<MobileParty>(mobilePartyId, out var playerParty) ||
            !objectManager.TryGetObject<Settlement>(request.SettlementId, out var settlement) ||
            settlement?.IsHideout != true ||
            playerParty.IsActive != true ||
            playerParty.CurrentSettlement != settlement)
        {
            Logger.Warning("Rejected invalid hideout consequence request for {SettlementId}", request.SettlementId);
            return;
        }

        var behavior = Campaign.Current?.GetCampaignBehavior<HideoutCampaignBehavior>();
        if (behavior == null)
        {
            Logger.Warning("Cannot apply hideout consequence because HideoutCampaignBehavior is unavailable");
            return;
        }

        using var playerContext = new BarterPlayerContext(playerHero, playerParty);
        switch (request.Consequence)
        {
            case HideoutCampaignConsequence.PrepareMission:
                if (!settlement.Hideout.IsInfested || !settlement.Hideout.NextPossibleAttackTime.IsPast)
                    return;

                behavior.ArrangeHideoutTroopCountsForMission();
                settlement.Hideout.SetNextPossibleAttackTime(
                    Campaign.Current.Models.HideoutModel.HideoutHiddenDuration);
                break;

            case HideoutCampaignConsequence.SetAttackCooldown:
                if (!settlement.Hideout.IsInfested || !settlement.Hideout.NextPossibleAttackTime.IsPast)
                    return;

                settlement.Hideout.SetNextPossibleAttackTime(
                    Campaign.Current.Models.HideoutModel.HideoutHiddenDuration);
                break;

            case HideoutCampaignConsequence.GrantClearRewards:
                if (settlement.Hideout.IsInfested)
                    return;

                behavior.SetCleanHideoutRelations(settlement);
                break;
        }
    }
}
