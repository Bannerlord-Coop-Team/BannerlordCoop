using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using GameInterface.Services.Barters;
using GameInterface.Services.Hideouts.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using static GameInterface.Services.ObjectManager.ObjectManager;

namespace GameInterface.Services.Hideouts.Handlers;

/// <summary>Applies client hideout menu consequences on the authoritative server.</summary>
internal sealed class HideoutCampaignConsequencesHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<HideoutCampaignConsequencesHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly INetworkConfig configuration;
    private readonly ISendCoalescer sendCoalescer;
    private readonly ConcurrentDictionary<string, PendingPreparation> pendingPreparations = new();

    public HideoutCampaignConsequencesHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        INetworkConfig configuration,
        ISendCoalescer sendCoalescer = null)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.configuration = configuration;
        this.sendCoalescer = sendCoalescer;

        messageBroker.Subscribe<HideoutCampaignConsequenceRequested>(Handle_HideoutCampaignConsequenceRequested);
        messageBroker.Subscribe<NetworkHideoutCampaignConsequenceRequested>(Handle_NetworkHideoutCampaignConsequenceRequested);
        messageBroker.Subscribe<NetworkHideoutCampaignConsequenceResolved>(Handle_NetworkHideoutCampaignConsequenceResolved);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<HideoutCampaignConsequenceRequested>(Handle_HideoutCampaignConsequenceRequested);
        messageBroker.Unsubscribe<NetworkHideoutCampaignConsequenceRequested>(Handle_NetworkHideoutCampaignConsequenceRequested);
        messageBroker.Unsubscribe<NetworkHideoutCampaignConsequenceResolved>(Handle_NetworkHideoutCampaignConsequenceResolved);
    }

    /// <summary>
    /// [Client] Waits until the server has prepared the hideout and the resulting defender roster deltas have
    /// reached this client. The native callback must not create its troop supplier from the pre-preparation state.
    /// </summary>
    internal bool RequestMissionPreparationBlocking(Settlement settlement, bool isDirectAssault)
    {
        if (!ModInformation.IsClient || settlement?.IsHideout != true ||
            !objectManager.TryGetIdWithLogging(settlement, out var settlementId))
            return false;

        var requestId = Guid.NewGuid().ToString();
        var pending = new PendingPreparation();
        pendingPreparations[requestId] = pending;

        try
        {
            var deadline = DateTime.UtcNow + configuration.ObjectCreationTimeout;
            var consequence = isDirectAssault
                ? HideoutCampaignConsequence.PrepareDirectAssaultMission
                : HideoutCampaignConsequence.PrepareMission;

            network.SendAll(new NetworkHideoutCampaignConsequenceRequested(
                settlementId,
                consequence,
                requestId));

            if (!GameThread.WaitWhilePumping(() => pending.Completed.IsSet, deadline))
            {
                Logger.Error(
                    "Timed out waiting for authoritative hideout mission preparation. SettlementId={SettlementId}, RequestId={RequestId}",
                    settlementId,
                    requestId);
                return false;
            }

            if (!pending.Accepted)
            {
                Logger.Warning(
                    "Server rejected hideout mission preparation. SettlementId={SettlementId}, RequestId={RequestId}",
                    settlementId,
                    requestId);
                return false;
            }

            if (!GameThread.WaitWhilePumping(
                    () => GetHealthyDefenderCount(settlement) == pending.ExpectedHealthyDefenderCount,
                    deadline))
            {
                Logger.Error(
                    "Authoritative hideout preparation did not reach roster parity before timeout. SettlementId={SettlementId}, ExpectedHealthyDefenders={ExpectedHealthyDefenders}, ActualHealthyDefenders={ActualHealthyDefenders}, RequestId={RequestId}",
                    settlementId,
                    pending.ExpectedHealthyDefenderCount,
                    GetHealthyDefenderCount(settlement),
                    requestId);
                return false;
            }

            return true;
        }
        finally
        {
            pendingPreparations.TryRemove(requestId, out _);
        }
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

        if (payload.Who is not NetPeer peer)
        {
            Logger.Warning("Rejected hideout consequence request with no originating peer");
            return;
        }

        if (!playerManager.TryGetPlayer(peer, out var player))
        {
            Logger.Warning("Rejected hideout consequence request from an unregistered peer");
            ReplyToPreparation(peer, payload.What, accepted: false, expectedHealthyDefenderCount: 0);
            return;
        }

        GameThread.RunSafe(
            () =>
            {
                var result = ApplyConsequence(player.HeroId, player.MobilePartyId, payload.What);
                ReplyToPreparation(peer, payload.What, result.Accepted, result.ExpectedHealthyDefenderCount);
            },
            blocking: !string.IsNullOrEmpty(payload.What.RequestId),
            context: nameof(Handle_NetworkHideoutCampaignConsequenceRequested));
    }

    private ConsequenceResult ApplyConsequence(
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
            return ConsequenceResult.Rejected;
        }

        var behavior = Campaign.Current?.GetCampaignBehavior<HideoutCampaignBehavior>();
        if (behavior == null)
        {
            Logger.Warning("Cannot apply hideout consequence because HideoutCampaignBehavior is unavailable");
            return ConsequenceResult.Rejected;
        }

        using var playerContext = new BarterPlayerContext(playerHero, playerParty);
        switch (request.Consequence)
        {
            case HideoutCampaignConsequence.PrepareMission:
            case HideoutCampaignConsequence.PrepareDirectAssaultMission:
                if (!settlement.Hideout.IsInfested || !settlement.Hideout.NextPossibleAttackTime.IsPast)
                    return ConsequenceResult.Rejected;

                behavior.ArrangeHideoutTroopCountsForMission();

                if (request.Consequence == HideoutCampaignConsequence.PrepareDirectAssaultMission &&
                    !EnsureDirectAssaultMinimum(settlement))
                {
                    Logger.Warning(
                        "Cannot prepare direct hideout assault because no defender can receive the minimum troop adjustment. SettlementId={SettlementId}",
                        request.SettlementId);
                    return ConsequenceResult.Rejected;
                }

                settlement.Hideout.SetNextPossibleAttackTime(
                    Campaign.Current.Models.HideoutModel.HideoutHiddenDuration);
                FlushDefenderRosters(settlement);
                return ConsequenceResult.AcceptedWith(GetHealthyDefenderCount(settlement));

            case HideoutCampaignConsequence.SetAttackCooldown:
                if (!settlement.Hideout.IsInfested || !settlement.Hideout.NextPossibleAttackTime.IsPast)
                    return ConsequenceResult.Rejected;

                settlement.Hideout.SetNextPossibleAttackTime(
                    Campaign.Current.Models.HideoutModel.HideoutHiddenDuration);
                return ConsequenceResult.AcceptedWithoutParity;

            case HideoutCampaignConsequence.GrantClearRewards:
                if (settlement.Hideout.IsInfested)
                    return ConsequenceResult.Rejected;

                behavior.SetCleanHideoutRelations(settlement);
                return ConsequenceResult.AcceptedWithoutParity;

            default:
                Logger.Warning("Rejected unknown hideout consequence {Consequence}", request.Consequence);
                return ConsequenceResult.Rejected;
        }
    }

    private void ReplyToPreparation(
        NetPeer peer,
        NetworkHideoutCampaignConsequenceRequested request,
        bool accepted,
        int expectedHealthyDefenderCount)
    {
        if (string.IsNullOrEmpty(request.RequestId))
            return;

        network.Send(peer, new NetworkHideoutCampaignConsequenceResolved(
            request.RequestId,
            accepted,
            expectedHealthyDefenderCount));
    }

    private void Handle_NetworkHideoutCampaignConsequenceResolved(
        MessagePayload<NetworkHideoutCampaignConsequenceResolved> payload)
    {
        var response = payload.What;
        if (!pendingPreparations.TryGetValue(response.RequestId, out var pending))
        {
            Logger.Warning(
                "Received hideout preparation response for unknown or expired RequestId={RequestId}",
                response.RequestId);
            return;
        }

        pending.Accepted = response.Accepted;
        pending.ExpectedHealthyDefenderCount = response.ExpectedHealthyDefenderCount;
        pending.Completed.Set();
    }

    private static bool EnsureDirectAssaultMinimum(Settlement settlement)
    {
        const int directAssaultMinimum = 25;
        var defenders = GetDefenderParties(settlement).ToList();
        var healthyCount = defenders.Sum(party => party.MemberRoster.TotalHealthyCount);
        if (healthyCount >= directAssaultMinimum)
            return true;

        var receivingParty = defenders.FirstOrDefault(
            party => party.Party?.Culture?.BanditBandit != null);
        if (receivingParty == null)
            return false;

        // Native performs the same adjustment immediately after creating the MapEvent. Its defender side is
        // populated from these hideout parties, so doing it before creation lets the authoritative roster delta
        // reach the client before native constructs the mission troop supplier.
        receivingParty.MemberRoster.AddToCounts(
            receivingParty.Party.Culture.BanditBandit,
            directAssaultMinimum - healthyCount);
        return true;
    }

    private void FlushDefenderRosters(Settlement settlement)
    {
        if (sendCoalescer == null)
            return;

        foreach (var party in GetDefenderParties(settlement))
        {
            if (!objectManager.TryGetId(party.MemberRoster, out var rosterId))
                continue;

            sendCoalescer.FlushInstance(Compact(rosterId, typeof(TroopRoster)), network);
        }
    }

    private static int GetHealthyDefenderCount(Settlement settlement) =>
        GetDefenderParties(settlement).Sum(party => party.MemberRoster.TotalHealthyCount);

    private static IEnumerable<MobileParty> GetDefenderParties(Settlement settlement) =>
        settlement.Parties.Where(party => party.IsBandit || party.IsBanditBossParty);

    private readonly struct ConsequenceResult
    {
        public static ConsequenceResult Rejected => new(false, 0);
        public static ConsequenceResult AcceptedWithoutParity => new(true, 0);
        public static ConsequenceResult AcceptedWith(int expectedHealthyDefenderCount) =>
            new(true, expectedHealthyDefenderCount);

        public bool Accepted { get; }
        public int ExpectedHealthyDefenderCount { get; }

        private ConsequenceResult(bool accepted, int expectedHealthyDefenderCount)
        {
            Accepted = accepted;
            ExpectedHealthyDefenderCount = expectedHealthyDefenderCount;
        }
    }

    private sealed class PendingPreparation
    {
        public ManualResetEventSlim Completed { get; } = new(false);
        public bool Accepted { get; set; }
        public int ExpectedHealthyDefenderCount { get; set; }
    }
}
