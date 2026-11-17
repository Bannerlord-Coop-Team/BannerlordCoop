using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Locations.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.PrisonBreaks.Interfaces;
using GameInterface.Services.PrisonBreaks.Messages;
using LiteNetLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.PrisonBreaks.Handlers;

internal class PrisonBreakHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PrisonBreakHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IPrisonBreakCampaignBehaviorInterface behaviorInterface;

    // Client only. Drops a second click while the server answers the first.
    private bool isStartPending;

    // Client only. Settlement whose prison roster is requested back after the mission.
    private string rosterRefreshSettlementId;

    // Server only. Settlement id of each running prison break, with its player and prisoner.
    private readonly Dictionary<string, (NetPeer Peer, string PlayerHeroId, string PrisonerId)> activeBreaks = new();

    public PrisonBreakHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        IPrisonBreakCampaignBehaviorInterface behaviorInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.behaviorInterface = behaviorInterface;

        messageBroker.Subscribe<PrisonBreakStartRequested>(Handle_PrisonBreakStartRequested);
        messageBroker.Subscribe<NetworkRequestPrisonBreakStart>(Handle_NetworkRequestPrisonBreakStart);
        messageBroker.Subscribe<NetworkPrisonBreakStartResolved>(Handle_NetworkPrisonBreakStartResolved);

        messageBroker.Subscribe<PrisonBreakEnded>(Handle_PrisonBreakEnded);
        messageBroker.Subscribe<NetworkRequestPrisonBreakEnd>(Handle_NetworkRequestPrisonBreakEnd);

        messageBroker.Subscribe<PrisonBreakCooldownSet>(Handle_PrisonBreakCooldownSet);
        messageBroker.Subscribe<NetworkPrisonBreakCooldownChanged>(Handle_NetworkPrisonBreakCooldownChanged);

        messageBroker.Subscribe<NetworkPrisonBreakActiveChanged>(Handle_NetworkPrisonBreakActiveChanged);

        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Subscribe<PlayerConnectionStateChanged>(Handle_PlayerConnectionStateChanged);
        messageBroker.Subscribe<CampaignTick>(Handle_CampaignTick);
        messageBroker.Subscribe<NetworkPrisonBreakPrisonerLost>(Handle_NetworkPrisonBreakPrisonerLost);
        messageBroker.Subscribe<PrisonBreakMissionEnded>(Handle_PrisonBreakMissionEnded);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PrisonBreakStartRequested>(Handle_PrisonBreakStartRequested);
        messageBroker.Unsubscribe<NetworkRequestPrisonBreakStart>(Handle_NetworkRequestPrisonBreakStart);
        messageBroker.Unsubscribe<NetworkPrisonBreakStartResolved>(Handle_NetworkPrisonBreakStartResolved);

        messageBroker.Unsubscribe<PrisonBreakEnded>(Handle_PrisonBreakEnded);
        messageBroker.Unsubscribe<NetworkRequestPrisonBreakEnd>(Handle_NetworkRequestPrisonBreakEnd);

        messageBroker.Unsubscribe<PrisonBreakCooldownSet>(Handle_PrisonBreakCooldownSet);
        messageBroker.Unsubscribe<NetworkPrisonBreakCooldownChanged>(Handle_NetworkPrisonBreakCooldownChanged);

        messageBroker.Unsubscribe<NetworkPrisonBreakActiveChanged>(Handle_NetworkPrisonBreakActiveChanged);

        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Unsubscribe<PlayerConnectionStateChanged>(Handle_PlayerConnectionStateChanged);
        messageBroker.Unsubscribe<CampaignTick>(Handle_CampaignTick);
        messageBroker.Unsubscribe<NetworkPrisonBreakPrisonerLost>(Handle_NetworkPrisonBreakPrisonerLost);
        messageBroker.Unsubscribe<PrisonBreakMissionEnded>(Handle_PrisonBreakMissionEnded);
    }

    private void Handle_PrisonBreakStartRequested(MessagePayload<PrisonBreakStartRequested> obj)
    {
        if (ModInformation.IsServer || isStartPending) return;

        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.PlayerHero, out var playerHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.Settlement, out var settlementId)) return;
        if (!objectManager.TryGetIdWithLogging(data.Prisoner, out var prisonerId)) return;

        isStartPending = true;
        network.SendAll(new NetworkRequestPrisonBreakStart(playerHeroId, settlementId, prisonerId));
    }

    private void Handle_NetworkRequestPrisonBreakStart(MessagePayload<NetworkRequestPrisonBreakStart> obj)
    {
        if (ModInformation.IsClient) return;

        var data = obj.What;
        var peer = obj.Who as NetPeer;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.IsSenderControllingHero(obj.Who, data.PlayerHeroId)) return;

            // Always answer, the requesting client ignores further clicks until it does.
            var result = TryStartPrisonBreak(data, peer);

            Logger.Debug("Prison break by {HeroId} for {PrisonerId} in {SettlementId}: {Result}",
                data.PlayerHeroId, data.PrisonerId, data.SettlementId, result);

            network.Send(peer, new NetworkPrisonBreakStartResolved(data.SettlementId, data.PrisonerId, result));
        });
    }

    private PrisonBreakStartResult TryStartPrisonBreak(NetworkRequestPrisonBreakStart data, NetPeer peer)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(data.PlayerHeroId, out var playerHero)) return PrisonBreakStartResult.PrisonerUnavailable;
        if (!objectManager.TryGetObjectWithLogging<Settlement>(data.SettlementId, out var settlement)) return PrisonBreakStartResult.PrisonerUnavailable;
        if (!objectManager.TryGetObjectWithLogging<Hero>(data.PrisonerId, out var prisoner)) return PrisonBreakStartResult.PrisonerUnavailable;

        // A player hero is only controlled by their own player, never as the rescuer's AI prisoner agent.
        if (prisoner.IsPlayerHero() || !IsHeldIn(prisoner, settlement)) return PrisonBreakStartResult.PrisonerUnavailable;
        if (behaviorInterface.IsOnCooldown(settlement)) return PrisonBreakStartResult.OnCooldown;

        int bribeCost;
        using (new MainHeroSubstitutionScope(playerHero, playerHero.PartyBelongedTo))
        {
            // The world keeps running while the rescuer sits in the bribe menu, so a capture or war can end the vanilla menu checks.
            if (!Campaign.Current.Models.PrisonBreakModel.CanPlayerStagePrisonBreak(settlement) ||
                FactionManager.IsAtWarAgainstFaction(playerHero.MapFaction, prisoner.MapFaction))
            {
                return PrisonBreakStartResult.PrisonerUnavailable;
            }

            bribeCost = Campaign.Current.Models.PrisonBreakModel.GetPrisonBreakStartCost(prisoner);
        }

        if (playerHero.Gold < bribeCost) return PrisonBreakStartResult.NotEnoughGold;

        GiveGoldAction.ApplyBetweenCharacters(playerHero, null, bribeCost, false);

        // Sent before the approval so the requester's menu sees the cooldown it just started.
        var cooldown = behaviorInterface.AddCooldown(settlement);
        network.SendAll(new NetworkPrisonBreakCooldownChanged(data.SettlementId, cooldown.NumTicks));

        activeBreaks[data.SettlementId] = (peer, data.PlayerHeroId, data.PrisonerId);
        network.SendAll(new NetworkPrisonBreakActiveChanged(data.SettlementId, true));

        return PrisonBreakStartResult.Approved;
    }

    private void Handle_NetworkPrisonBreakStartResolved(MessagePayload<NetworkPrisonBreakStartResolved> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            isStartPending = false;

            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.SettlementId, out var settlement)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.PrisonerId, out var prisoner)) return;

            if (data.Result != PrisonBreakStartResult.Approved)
            {
                behaviorInterface.CancelPrisonBreak(data.Result);
                return;
            }

            if (Settlement.CurrentSettlement != settlement)
            {
                Logger.Warning("Prison break in {SettlementId} was approved after leaving it", data.SettlementId);
                network.SendAll(new NetworkRequestPrisonBreakEnd(PrisonBreakOutcome.Cancelled));
                return;
            }

            behaviorInterface.OpenPrisonBreakMission(settlement, prisoner);
        });
    }

    // Caller is game thread
    private void Handle_PrisonBreakEnded(MessagePayload<PrisonBreakEnded> obj)
    {
        if (ModInformation.IsServer) return;

        network.SendAll(new NetworkRequestPrisonBreakEnd(obj.What.Outcome));
    }

    private void Handle_NetworkRequestPrisonBreakEnd(MessagePayload<NetworkRequestPrisonBreakEnd> obj)
    {
        if (ModInformation.IsClient) return;

        var data = obj.What;
        var peer = obj.Who as NetPeer;

        GameThread.RunSafe(() =>
        {
            // The ids come from the break the server approved, so an end can't be replayed or aimed at another hero.
            var activeBreak = activeBreaks.FirstOrDefault(pair => pair.Value.Peer == peer);
            var settlementId = activeBreak.Key;
            if (settlementId == null)
            {
                Logger.Warning("Ignored a prison break end ({Outcome}) from a player with no prison break running", data.Outcome);
                return;
            }

            var (_, playerHeroId, prisonerId) = activeBreak.Value;
            EndActiveBreak(settlementId);

            if (data.Outcome == PrisonBreakOutcome.Cancelled) return;

            if (!objectManager.TryGetObjectWithLogging<Hero>(playerHeroId, out var playerHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(prisonerId, out var prisoner)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(settlementId, out var settlement)) return;

            Logger.Debug("Prison break by {HeroId} for {PrisonerId} in {SettlementId} ended: {Outcome}",
                playerHeroId, prisonerId, settlementId, data.Outcome);

            var model = Campaign.Current.Models.PrisonBreakModel;
            // The mission only runs on the rescuer's client, so its outcome is trusted like other client-run missions.
            bool succeeded = data.Outcome == PrisonBreakOutcome.Succeeded || data.Outcome == PrisonBreakOutcome.SucceededJoinedParty;

            // Vanilla SkillLevelingManager.OnPrisonBreakEnd, keyed off the player's hero instead of Hero.MainHero.
            float xp = model.GetRogueryRewardOnPrisonBreak(prisoner, succeeded);
            if (xp > 0f) playerHero.AddSkillXp(DefaultSkills.Roguery, xp);

            if (data.Outcome == PrisonBreakOutcome.PlayerCaptured)
            {
                if (playerHero.IsPrisoner) return;

                // Same capture as the sneak-in failure, the player's party waits in the settlement with its troops.
                playerHero.PartyBelongedTo?.Party.AddElementToMemberRoster(playerHero.CharacterObject, -1, true);
                TakePrisonerAction.Apply(settlement.Party, playerHero);
                return;
            }

            if (!succeeded) return;

            // The prisoner may have been ransomed or moved while the mission ran.
            if (!IsHeldIn(prisoner, settlement))
            {
                Logger.Warning("Prison break freed {PrisonerId} but they are no longer held in {SettlementId}",
                    prisonerId, settlementId);
                return;
            }

            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(playerHero, prisoner, model.GetRelationRewardOnPrisonBreak(prisoner));
            EndCaptivityAction.ApplyByEscape(prisoner, playerHero);

            var playerParty = playerHero.PartyBelongedTo;
            if (data.Outcome == PrisonBreakOutcome.SucceededJoinedParty && prisoner.CompanionOf == playerHero.Clan && playerParty != null)
            {
                prisoner.ChangeState(Hero.CharacterStates.Active);
                AddHeroToPartyAction.Apply(prisoner, playerParty);
            }
        });
    }

    // Caller is game thread
    private void Handle_PrisonBreakCooldownSet(MessagePayload<PrisonBreakCooldownSet> obj)
    {
        if (ModInformation.IsClient) return;

        var data = obj.What;
        if (!objectManager.TryGetIdWithLogging(data.Settlement, out var settlementId)) return;

        behaviorInterface.SetCooldown(data.Settlement, data.Until);
        network.SendAll(new NetworkPrisonBreakCooldownChanged(settlementId, data.Until.NumTicks));
    }

    private void Handle_NetworkPrisonBreakCooldownChanged(MessagePayload<NetworkPrisonBreakCooldownChanged> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.SettlementId, out var settlement)) return;

            behaviorInterface.SetCooldown(settlement, new CampaignTime(data.CooldownEndTicks));
        });
    }

    private void Handle_NetworkPrisonBreakActiveChanged(MessagePayload<NetworkPrisonBreakActiveChanged> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.SettlementId, out var settlement)) return;

            behaviorInterface.SetPrisonBreakActive(settlement, data.IsActive);

            // Re-run the option conditions so an open dungeon menu greys out or unlocks without being reopened.
            var menuContext = Campaign.Current.CurrentMenuContext;
            if (Settlement.CurrentSettlement == settlement && menuContext?.GameMenu != null)
            {
                Campaign.Current.GameMenuManager.RefreshMenuOptionConditions(menuContext);
            }
        });
    }

    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> obj)
    {
        if (ModInformation.IsClient) return;

        var peer = obj.What.PlayerId;

        GameThread.RunSafe(() =>
        {
            EndActiveBreaks(peer);
        });
    }

    private void Handle_PlayerConnectionStateChanged(MessagePayload<PlayerConnectionStateChanged> obj)
    {
        if (ModInformation.IsClient) return;

        GameThread.RunSafe(() =>
        {
            // Joining clients missed the start broadcasts, so the running breaks are sent again.
            foreach (var settlementId in activeBreaks.Keys)
            {
                network.SendAll(new NetworkPrisonBreakActiveChanged(settlementId, true));
            }
        });
    }

    private void EndActiveBreaks(NetPeer peer)
    {
        foreach (var settlementId in activeBreaks.Where(pair => pair.Value.Peer == peer).Select(pair => pair.Key).ToList())
        {
            EndActiveBreak(settlementId);
        }
    }

    private void EndActiveBreak(string settlementId)
    {
        activeBreaks.Remove(settlementId);
        network.SendAll(new NetworkPrisonBreakActiveChanged(settlementId, false));
    }

    private void Handle_PrisonBreakMissionEnded(MessagePayload<PrisonBreakMissionEnded> obj)
    {
        if (ModInformation.IsServer) return;

        if (!objectManager.TryGetIdWithLogging(obj.What.Settlement, out var settlementId)) return;

        rosterRefreshSettlementId = settlementId;
    }

    private void Handle_CampaignTick(MessagePayload<CampaignTick> obj)
    {
        if (ModInformation.IsClient) RequestPrisonRosterAfterMission();
        else EndBreaksWithPrisonerGone();
    }

    // Waits for the mission to finish tearing down, otherwise the snapshot is dropped as a prison break roster change.
    private void RequestPrisonRosterAfterMission()
    {
        if (rosterRefreshSettlementId == null || Mission.Current != null) return;

        network.SendAll(new NetworkRequestLocationRosterSnapshot(rosterRefreshSettlementId));
        rosterRefreshSettlementId = null;
    }

    // Covers every way a prisoner leaves the prison mid-mission: ransom, peace, transfer, death.
    private void EndBreaksWithPrisonerGone()
    {
        if (activeBreaks.Count == 0) return;

        foreach (var pair in activeBreaks.ToList())
        {
            var settlementId = pair.Key;
            var (peer, _, prisonerId) = pair.Value;

            if (objectManager.TryGetObject<Settlement>(settlementId, out var settlement) &&
                objectManager.TryGetObject<Hero>(prisonerId, out var prisoner) &&
                IsHeldIn(prisoner, settlement)) continue;

            Logger.Debug("Prisoner {PrisonerId} left {SettlementId} during a prison break", prisonerId, settlementId);

            EndActiveBreak(settlementId);
            network.Send(peer, new NetworkPrisonBreakPrisonerLost(prisonerId));
        }
    }

    private void Handle_NetworkPrisonBreakPrisonerLost(MessagePayload<NetworkPrisonBreakPrisonerLost> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.PrisonerId, out var prisoner)) return;

            behaviorInterface.AbortPrisonBreakMission(prisoner);
        });
    }

    private static bool IsHeldIn(Hero prisoner, Settlement settlement)
    {
        if (settlement == null || !prisoner.IsPrisoner) return false;

        return settlement.Party.PrisonRoster.Contains(prisoner.CharacterObject) ||
            settlement.Town?.GarrisonParty?.PrisonRoster.Contains(prisoner.CharacterObject) == true;
    }
}
