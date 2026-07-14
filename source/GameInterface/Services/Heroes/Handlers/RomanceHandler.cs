using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Registry.Messages;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.Messages.RomanceFlow;
using GameInterface.Services.Heroes.Patches;
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
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Party;
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
        messageBroker.Subscribe<MarriageActionRequested>(Handle_MarriageActionRequested);
        messageBroker.Subscribe<NetworkRequestRomanceStateChange>(Handle_NetworkRequestRomanceStateChange);
        messageBroker.Subscribe<NetworkRequestRomanceStateSync>(Handle_NetworkRequestRomanceStateSync);
        messageBroker.Subscribe<NetworkSyncRomanceStates>(Handle_NetworkSyncRomanceStates);
        messageBroker.Subscribe<NetworkRequestRomanceMarriage>(Handle_NetworkRequestRomanceMarriage);
        messageBroker.Subscribe<NetworkRequestRomanceMarriageBarter>(Handle_NetworkRequestRomanceMarriageBarter);
        messageBroker.Subscribe<NetworkRomanceMarriageBarterAccepted>(Handle_NetworkRomanceMarriageBarterAccepted);
        messageBroker.Subscribe<NetworkRomanceRequestRejected>(Handle_NetworkRomanceRequestRejected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
        messageBroker.Unsubscribe<RomanticStateChangeRequested>(Handle_RomanticStateChangeRequested);
        messageBroker.Unsubscribe<RomanceStatesChanged>(Handle_RomanceStatesChanged);
        messageBroker.Unsubscribe<MarriageActionRequested>(Handle_MarriageActionRequested);
        messageBroker.Unsubscribe<NetworkRequestRomanceStateChange>(Handle_NetworkRequestRomanceStateChange);
        messageBroker.Unsubscribe<NetworkRequestRomanceStateSync>(Handle_NetworkRequestRomanceStateSync);
        messageBroker.Unsubscribe<NetworkSyncRomanceStates>(Handle_NetworkSyncRomanceStates);
        messageBroker.Unsubscribe<NetworkRequestRomanceMarriage>(Handle_NetworkRequestRomanceMarriage);
        messageBroker.Unsubscribe<NetworkRequestRomanceMarriageBarter>(Handle_NetworkRequestRomanceMarriageBarter);
        messageBroker.Unsubscribe<NetworkRomanceMarriageBarterAccepted>(Handle_NetworkRomanceMarriageBarterAccepted);
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

    private void Handle_MarriageActionRequested(MessagePayload<MarriageActionRequested> payload)
    {
        if (ModInformation.IsServer) return;

        var request = payload.What;
        if (!TryGetControlledPair(request.FirstHero, request.SecondHero, out _, out var targetHero)) return;
        if (!objectManager.TryGetId(targetHero, out var targetHeroId)) return;

        network.SendAll(new NetworkRequestRomanceMarriage(targetHeroId));
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

    private void Handle_NetworkRequestRomanceMarriage(MessagePayload<NetworkRequestRomanceMarriage> payload)
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

            if (!romanceAuthority.TryValidateMarriage(playerHero, targetHero, out var reason))
            {
                Reject(peer, reason);
                return;
            }

            MarriageAction.Apply(playerHero, targetHero);
            if (playerHero.Spouse != targetHero || targetHero.Spouse != playerHero)
            {
                Reject(peer, "The marriage could not be completed.");
            }
        }, context: nameof(Handle_NetworkRequestRomanceMarriage));
    }

    private void Handle_NetworkRequestRomanceMarriageBarter(MessagePayload<NetworkRequestRomanceMarriageBarter> payload)
    {
        if (ModInformation.IsClient) return;

        var sender = payload.Who;
        var request = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!TryResolveRequester(sender, out var peer, out var player, out var playerHero)) return;

            try
            {
                if (!TryResolveHero(request.TargetHeroId, out var targetHero))
                {
                    Reject(peer, "The selected hero no longer exists.");
                    return;
                }

                if (!romanceAuthority.TryValidateMarriage(playerHero, targetHero, out var reason))
                {
                    Reject(peer, reason);
                    return;
                }

                if (!TryBuildMarriageBarter(player, playerHero, targetHero, request.Terms, out var barterData, out reason))
                {
                    Reject(peer, reason);
                    return;
                }

                var barterManager = BarterManager.Instance;
                if (barterManager == null ||
                    !barterManager.IsOfferAcceptable(barterData, barterData.OtherHero, barterData.OtherParty))
                {
                    Reject(peer, "The marriage offer is not acceptable.");
                    return;
                }

                var overpayAmount = barterManager.GetOfferValue(
                    barterData.OtherHero,
                    barterData.OtherParty,
                    barterData.OffererParty,
                    barterData.GetOfferedBarterables());
                barterManager.ApplyAndFinalizePlayerBarter(playerHero, barterData.OtherHero, barterData);
                ApplyOverpayRelationBonus(playerHero, barterData.OtherHero, overpayAmount);
                if (playerHero.Spouse != targetHero || targetHero.Spouse != playerHero)
                {
                    Reject(peer, "The marriage could not be completed.");
                    return;
                }

                network.Send(peer, new NetworkRomanceMarriageBarterAccepted());
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Failed to apply an authoritative marriage barter");
                Reject(peer, "The server could not process the marriage offer.");
            }
        }, context: nameof(Handle_NetworkRequestRomanceMarriageBarter));
    }

    private void Handle_NetworkRomanceMarriageBarterAccepted(MessagePayload<NetworkRomanceMarriageBarterAccepted> payload)
    {
        if (ModInformation.IsServer) return;

        GameThread.RunSafe(() =>
        {
            RomanceMarriageBarterPatches.CompleteMarriageBarterRequest();
            if (BarterManager.Instance != null)
            {
                BarterManager.Instance.LastBarterIsAccepted = true;
                BarterManager.Instance.Close();
            }

            if (Campaign.Current?.ConversationManager?.IsConversationInProgress == true)
                Campaign.Current.ConversationManager.ContinueConversation();

            MBInformationManager.AddQuickInformation(GameTexts.FindText("str_offer_accepted"));
        }, context: nameof(Handle_NetworkRomanceMarriageBarterAccepted));
    }

    private void Handle_NetworkRomanceRequestRejected(MessagePayload<NetworkRomanceRequestRejected> payload)
    {
        if (ModInformation.IsServer) return;

        var reason = string.IsNullOrWhiteSpace(payload.What.Reason)
            ? "The server rejected the romance request."
            : payload.What.Reason;

        GameThread.RunSafe(
            () =>
            {
                RomanceMarriageBarterPatches.CompleteMarriageBarterRequest();
                InformationManager.DisplayMessage(new InformationMessage(reason));
            },
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

    private bool TryBuildMarriageBarter(
        Player player,
        Hero playerHero,
        Hero targetHero,
        RomanceBarterTerm[] terms,
        out BarterData barterData,
        out string reason)
    {
        barterData = null;
        reason = null;

        var playerParty = GetPlayerParty(player, playerHero);
        var counterpartyHero = targetHero.Clan?.Leader ?? targetHero;
        var counterpartyParty = counterpartyHero.PartyBelongedTo?.Party;
        var romanticState = Romance.GetRomanticState(playerHero, targetHero);
        var persuasionCostReduction = (int)(romanticState?.ScoreFromPersuasion ?? 0f);
        var marriageBarterable = new MarriageBarterable(playerHero, playerParty, targetHero, playerHero);

        barterData = new BarterData(
            playerHero,
            counterpartyHero,
            playerParty,
            counterpartyParty,
            null,
            persuasionCostReduction,
            false);

        barterData.AddBarterGroup(new DefaultsBarterGroup());
        marriageBarterable.SetIsOffered(true);
        barterData.AddBarterable<OtherBarterGroup>(marriageBarterable, true);
        marriageBarterable.SetIsOffered(true);
        CampaignEventDispatcher.Instance.OnBarterablesRequested(barterData);

        return TryApplyRequestedTerms(
            playerHero,
            counterpartyHero,
            barterData,
            terms ?? Array.Empty<RomanceBarterTerm>(),
            out reason);
    }

    private PartyBase GetPlayerParty(Player player, Hero playerHero)
    {
        if (!string.IsNullOrEmpty(player.MobilePartyId) &&
            objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var mobileParty))
        {
            return mobileParty.Party;
        }

        return playerHero.PartyBelongedTo?.Party;
    }

    private bool TryApplyRequestedTerms(
        Hero playerHero,
        Hero counterpartyHero,
        BarterData barterData,
        RomanceBarterTerm[] terms,
        out string reason)
    {
        var usedBarterables = new HashSet<Barterable>();
        foreach (var term in terms)
        {
            if (!global::System.Enum.IsDefined(typeof(RomanceBarterTermType), term.Type) || term.Amount <= 0)
            {
                reason = "The marriage offer contains an invalid term.";
                return false;
            }

            if (string.IsNullOrEmpty(term.OwnerHeroId))
            {
                reason = "The marriage offer does not identify the owner of a barter term.";
                return false;
            }

            var termType = (RomanceBarterTermType)term.Type;
            var barterable = barterData.GetBarterables().FirstOrDefault(candidate =>
                (candidate.OriginalOwner == playerHero || candidate.OriginalOwner == counterpartyHero) &&
                objectManager.TryGetId(candidate.OriginalOwner, out var ownerHeroId) &&
                ownerHeroId == term.OwnerHeroId &&
                MatchesTerm(candidate, termType, term));

            if (barterable == null || !usedBarterables.Add(barterable) || term.Amount > barterable.MaxAmount)
            {
                reason = "The marriage offer no longer matches the server's available barter terms.";
                return false;
            }

            barterable.CurrentAmount = term.Amount;
            barterable.SetIsOffered(true);
        }

        reason = null;
        return true;
    }

    private bool MatchesTerm(Barterable barterable, RomanceBarterTermType type, RomanceBarterTerm term)
    {
        switch (type)
        {
            case RomanceBarterTermType.Gold:
                return barterable is GoldBarterable;
            case RomanceBarterTermType.Item:
                return barterable is ItemBarterable itemBarterable &&
                       MatchesItem(itemBarterable, term);
            case RomanceBarterTermType.Fief:
                return barterable is FiefBarterable fiefBarterable &&
                       objectManager.TryGetId(fiefBarterable.TargetSettlement, out var settlementId) &&
                       settlementId == term.ObjectId;
            case RomanceBarterTermType.Prisoner:
                return barterable is TransferPrisonerBarterable prisonerBarterable &&
                       MatchesPrisoner(prisonerBarterable, term);
            default:
                return false;
        }
    }

    private bool MatchesItem(ItemBarterable barterable, RomanceBarterTerm term)
    {
        var equipmentElement = barterable.ItemRosterElement.EquipmentElement;
        if (!objectManager.TryGetId(equipmentElement.Item, out var itemId) || itemId != term.ObjectId)
            return false;

        var modifier = equipmentElement.ItemModifier;
        if ((modifier == null) != term.ItemModifierNull) return false;
        if (modifier == null) return true;

        return objectManager.TryGetId(modifier, out var modifierId) && modifierId == term.ItemModifierId;
    }

    private bool MatchesPrisoner(TransferPrisonerBarterable barterable, RomanceBarterTerm term)
    {
        var prisoner = barterable._prisonerCharacter;
        return prisoner?.CharacterObject != null &&
               objectManager.TryGetId(prisoner.CharacterObject, out var characterId) &&
               characterId == term.ObjectId;
    }

    private static void ApplyOverpayRelationBonus(Hero playerHero, Hero otherHero, float overpayAmount)
    {
        var campaign = Campaign.Current;
        if (playerHero == Hero.MainHero ||
            otherHero == null ||
            overpayAmount <= 0f ||
            playerHero.MapFaction == null ||
            otherHero.MapFaction == null ||
            otherHero.MapFaction.IsAtWarWith(playerHero.MapFaction) ||
            campaign == null)
        {
            return;
        }

        var relationBonus = campaign.Models.BarterModel.CalculateOverpayRelationIncreaseCosts(otherHero, overpayAmount);
        if (relationBonus > 0)
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(playerHero, otherHero, relationBonus);
    }
}
