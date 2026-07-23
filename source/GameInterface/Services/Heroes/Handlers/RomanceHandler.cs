using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Registry.Messages;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.Messages.RomanceFlow;
using GameInterface.Services.Heroes.RomanceFlow;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Romance = TaleWorlds.CampaignSystem.Romance;

namespace GameInterface.Services.Heroes.Handlers;

internal class RomanceHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<RomanceHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IRomanceAuthority romanceAuthority;

    public RomanceHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IRomanceAuthority romanceAuthority)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.romanceAuthority = romanceAuthority;

        messageBroker.Subscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
        messageBroker.Subscribe<RomanticStateChangeRequested>(Handle_RomanticStateChangeRequested);
        messageBroker.Subscribe<RomanceStatesChanged>(Handle_RomanceStatesChanged);
        messageBroker.Subscribe<NetworkRequestRomanceStateChange>(Handle_NetworkRequestRomanceStateChange);
        messageBroker.Subscribe<NetworkRequestRomanceStateSync>(Handle_NetworkRequestRomanceStateSync);
        messageBroker.Subscribe<NetworkSyncRomanceStates>(Handle_NetworkSyncRomanceStates);
        messageBroker.Subscribe<NetworkRomanceRequestRejected>(Handle_NetworkRomanceRequestRejected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
        messageBroker.Unsubscribe<RomanticStateChangeRequested>(Handle_RomanticStateChangeRequested);
        messageBroker.Unsubscribe<RomanceStatesChanged>(Handle_RomanceStatesChanged);
        messageBroker.Unsubscribe<NetworkRequestRomanceStateChange>(Handle_NetworkRequestRomanceStateChange);
        messageBroker.Unsubscribe<NetworkRequestRomanceStateSync>(Handle_NetworkRequestRomanceStateSync);
        messageBroker.Unsubscribe<NetworkSyncRomanceStates>(Handle_NetworkSyncRomanceStates);
        messageBroker.Unsubscribe<NetworkRomanceRequestRejected>(Handle_NetworkRomanceRequestRejected);
    }

    private void Handle_AllGameObjectsRegistered(MessagePayload<AllGameObjectsRegistered> payload)
    {
        if (ModInformation.IsServer) return;

        network.SendAll(new NetworkRequestRomanceStateSync());
    }

    private void Handle_RomanticStateChangeRequested(MessagePayload<RomanticStateChangeRequested> payload)
    {
        if (ModInformation.IsServer) return;

        var request = payload.What;
        if (!TryGetControlledPair(request.Person1, request.Person2, out _, out var targetHero)) return;
        if (!objectManager.TryGetId(targetHero, out var targetHeroId)) return;

        network.SendAll(new NetworkRequestRomanceStateChange(
            targetHeroId,
            request.RequestedLevel,
            request.ProgressToNextLevel,
            request.LastVisit,
            request.ScoreFromPersuasion));
    }

    private void Handle_RomanceStatesChanged(MessagePayload<RomanceStatesChanged> payload)
    {
        if (ModInformation.IsClient) return;

        network.SendAll(new NetworkSyncRomanceStates(BuildSnapshot()));
    }

    private void Handle_NetworkRequestRomanceStateChange(MessagePayload<NetworkRequestRomanceStateChange> payload)
    {
        if (ModInformation.IsClient) return;

        var sender = payload.Who;
        var request = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!TryResolveRequester(sender, out var peer, out _, out var playerHero)) return;

            if (!TryResolveHero(request.TargetHeroId, out var targetHero))
            {
                Reject(peer, "The selected hero no longer exists.");
                return;
            }

            if (!global::System.Enum.IsDefined(typeof(Romance.RomanceLevelEnum), request.RequestedLevel))
            {
                Reject(peer, "The requested romance state is invalid.");
                return;
            }

            var requestedLevel = (Romance.RomanceLevelEnum)request.RequestedLevel;
            if (!romanceAuthority.TryValidateStateChange(playerHero, targetHero, requestedLevel, out var reason))
            {
                Reject(peer, reason);
                return;
            }

            if (!TryApplyClientStateFields(playerHero, targetHero, request, out reason))
            {
                Reject(peer, reason);
                return;
            }

            ChangeRomanticStateAction.Apply(playerHero, targetHero, requestedLevel);
        }, context: nameof(Handle_NetworkRequestRomanceStateChange));
    }

    private void Handle_NetworkRequestRomanceStateSync(MessagePayload<NetworkRequestRomanceStateSync> payload)
    {
        if (ModInformation.IsClient) return;
        if (payload.Who is not NetPeer peer) return;

        GameThread.RunSafe(() => SendSnapshot(peer), context: nameof(Handle_NetworkRequestRomanceStateSync));
    }

    private void Handle_NetworkSyncRomanceStates(MessagePayload<NetworkSyncRomanceStates> payload)
    {
        if (ModInformation.IsServer) return;

        var snapshot = payload.What;
        GameThread.RunSafe(
            () => ApplySnapshot(snapshot.States ?? Array.Empty<RomanceStateData>()),
            context: nameof(Handle_NetworkSyncRomanceStates));
    }

    private void Handle_NetworkRomanceRequestRejected(MessagePayload<NetworkRomanceRequestRejected> payload)
    {
        if (ModInformation.IsServer) return;

        var reason = string.IsNullOrWhiteSpace(payload.What.Reason)
            ? "The server rejected the romance request."
            : payload.What.Reason;

        GameThread.RunSafe(
            () => InformationManager.DisplayMessage(new InformationMessage(reason)),
            context: nameof(Handle_NetworkRomanceRequestRejected));
    }

    private bool TryResolveRequester(object sender, out NetPeer peer, out Player player, out Hero playerHero)
    {
        peer = sender as NetPeer;
        player = null;
        playerHero = null;

        if (peer == null)
        {
            Logger.Error("Received romance request without an originating peer");
            return false;
        }

        if (!playerManager.TryGetPlayer(peer, out player))
        {
            Logger.Warning("Received romance request from unregistered peer {Peer}", peer.Id);
            return false;
        }

        if (!TryResolveHero(player.HeroId, out playerHero))
        {
            Logger.Warning("Unable to resolve player hero {HeroId} for peer {Peer}", player.HeroId, peer.Id);
            return false;
        }

        return true;
    }

    private bool TryResolveHero(string heroId, out Hero hero)
    {
        hero = null;
        return !string.IsNullOrEmpty(heroId) && objectManager.TryGetObject(heroId, out hero);
    }

    private bool TryGetControlledPair(Hero firstHero, Hero secondHero, out Hero playerHero, out Hero targetHero)
    {
        playerHero = null;
        targetHero = null;

        if (firstHero.IsControlledByThisInstance())
        {
            playerHero = firstHero;
            targetHero = secondHero;
        }
        else if (secondHero.IsControlledByThisInstance())
        {
            playerHero = secondHero;
            targetHero = firstHero;
        }

        return playerHero != null && targetHero != null && !targetHero.IsPlayerHero();
    }

    private void Reject(NetPeer peer, string reason)
    {
        network.Send(peer, new NetworkRomanceRequestRejected(reason));
        SendSnapshot(peer);
    }

    private void SendSnapshot(NetPeer peer)
        => network.Send(peer, new NetworkSyncRomanceStates(BuildSnapshot()));

    private RomanceStateData[] BuildSnapshot()
    {
        if (Romance.RomanticStateList == null) return Array.Empty<RomanceStateData>();

        var result = new List<RomanceStateData>(Romance.RomanticStateList.Count);
        foreach (var state in Romance.RomanticStateList)
        {
            if (state?.Person1 == null || state.Person2 == null) continue;
            if (!objectManager.TryGetId(state.Person1, out var person1Id) ||
                !objectManager.TryGetId(state.Person2, out var person2Id))
            {
                Logger.Warning("Could not snapshot romance state for {Person1} and {Person2}", state.Person1, state.Person2);
                continue;
            }

            result.Add(new RomanceStateData(
                person1Id,
                person2Id,
                state.Level,
                state.ProgressToNextLevel,
                state.LastVisit,
                state.ScoreFromPersuasion));
        }

        return result.ToArray();
    }

    private void ApplySnapshot(RomanceStateData[] snapshot)
    {
        if (Campaign.Current == null || Romance.RomanticStateList == null) return;

        var resolvedStates = new List<(Hero Person1, Hero Person2, RomanceStateData Data)>(snapshot.Length);
        foreach (var state in snapshot)
        {
            if (!global::System.Enum.IsDefined(typeof(Romance.RomanceLevelEnum), state.Level))
            {
                Logger.Warning("Ignoring romance snapshot with invalid level {Level}", state.Level);
                return;
            }

            if (!TryResolveHero(state.Person1Id, out var person1) || !TryResolveHero(state.Person2Id, out var person2))
            {
                Logger.Warning(
                    "Waiting to apply romance snapshot until heroes {Person1Id} and {Person2Id} exist",
                    state.Person1Id,
                    state.Person2Id);
                return;
            }

            resolvedStates.Add((person1, person2, state));
        }

        using (new AllowedThread())
        {
            Romance.RomanticStateList.Clear();

            foreach (var state in resolvedStates)
            {
                ChangeRomanticStateAction.Apply(
                    state.Person1,
                    state.Person2,
                    (Romance.RomanceLevelEnum)state.Data.Level);

                var romanticState = Romance.GetRomanticState(state.Person1, state.Person2);
                if (romanticState == null) continue;

                romanticState.ProgressToNextLevel = state.Data.ProgressToNextLevel;
                romanticState.LastVisit = state.Data.LastVisit;
                romanticState.ScoreFromPersuasion = state.Data.ScoreFromPersuasion;
            }
        }
    }

    private static bool TryApplyClientStateFields(
        Hero playerHero,
        Hero targetHero,
        NetworkRequestRomanceStateChange request,
        out string reason)
    {
        if (float.IsNaN(request.LastVisit) || float.IsInfinity(request.LastVisit) ||
            float.IsNaN(request.ScoreFromPersuasion) || float.IsInfinity(request.ScoreFromPersuasion))
        {
            reason = "The romance progress data is invalid.";
            return false;
        }

        var state = Romance.GetRomanticState(playerHero, targetHero);
        if (state == null)
        {
            if (request.ProgressToNextLevel != 0 || request.LastVisit != 0f || request.ScoreFromPersuasion != 0f)
            {
                reason = "The romance progress does not match the server state.";
                return false;
            }

            reason = null;
            return true;
        }

        state.ProgressToNextLevel = request.ProgressToNextLevel;
        state.LastVisit = request.LastVisit;
        state.ScoreFromPersuasion = request.ScoreFromPersuasion;
        reason = null;
        return true;
    }

}
