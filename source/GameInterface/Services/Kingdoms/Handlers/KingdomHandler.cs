using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Entity;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Helpers;
using LiteNetLib;
using SandBox.GauntletUI;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Kingdoms.Handlers;

/// <summary>
/// Handler for <see cref="Kingdom"/> messages on both the server and the client
/// </summary>
public class KingdomHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<KingdomHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IKingdomDecisionVoteManager decisionVoteManager;
    private readonly IKingdomMembershipState kingdomMembershipState;
    private readonly IKingdomInterface kingdomInterface;
    private readonly IKingdomCreator kingdomCreator;
    private readonly IKingdomDecisionDataConverter decisionDataConverter;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IKingdomCreationSettlementTracker settlementTracker;
    // Server side, keyed by the creating controller
    private readonly Dictionary<string, PendingSettlementRestore> pendingKingdomCreationSettlements = new();
    // Client side, for this client's own kingdom creation
    private PendingSettlementRestore? pendingKingdomCreationSettlement;
    private readonly HashSet<Type> warnedDecisionTypes = new();

    public KingdomHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IKingdomDecisionVoteManager decisionVoteManager,
        IKingdomMembershipState kingdomMembershipState,
        IKingdomInterface kingdomInterface,
        IKingdomCreator kingdomCreator,
        IKingdomDecisionDataConverter decisionDataConverter,
        IControllerIdProvider controllerIdProvider,
        IKingdomCreationSettlementTracker settlementTracker)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.decisionVoteManager = decisionVoteManager;
        this.kingdomMembershipState = kingdomMembershipState;
        this.kingdomInterface = kingdomInterface;
        this.kingdomCreator = kingdomCreator;
        this.decisionDataConverter = decisionDataConverter;
        this.controllerIdProvider = controllerIdProvider;
        this.settlementTracker = settlementTracker;
        messageBroker.Subscribe<AddDecision>(HandleAddDecision);
        messageBroker.Subscribe<RemoveDecision>(HandleRemoveDecision);
        messageBroker.Subscribe<ChangeKingdomPolicy>(HandleChangeKingdomPolicy);
        messageBroker.Subscribe<ChangeKingdomDecisionVote>(HandleChangeKingdomDecisionVote);
        messageBroker.Subscribe<ApplyKingdomDecisionVote>(HandleApplyKingdomDecisionVote);
        messageBroker.Subscribe<ApplyKingdomDecisionRoundStatus>(HandleApplyKingdomDecisionRoundStatus);
        messageBroker.Subscribe<ApplyKingdomDecisionResolved>(HandleApplyKingdomDecisionResolved);
        messageBroker.Subscribe<CreateKingdom>(HandleCreateKingdom);
        messageBroker.Subscribe<PlayerKingdomCreated>(HandlePlayerKingdomCreated);
        messageBroker.Subscribe<NetworkDestroyKingdom>(HandleNetworkDestroyKingdom);
        messageBroker.Subscribe<NetworkRulingClanChanged>(HandleNetworkRulingClanChanged);
        messageBroker.Subscribe<ChangeKingdomName>(HandleChangeKingdomName);
        messageBroker.Subscribe<GiftSettlementOwnership>(HandleGiftSettlementOwnership);
        messageBroker.Subscribe<NetworkGiftSettlementOwnership>(HandleNetworkGiftSettlementOwnership);

        messageBroker.Subscribe<DecisionAdded>(HandleLocalDecisionAdded);
        messageBroker.Subscribe<NetworkAddDecision>(HandleNetworkAddDecision);

        // Server side
        messageBroker.Subscribe<DecisionRemoved>(HandleLocalDecisionRemoved);
        messageBroker.Subscribe<KingdomPolicyChanged>(HandleLocalKingdomPolicyChanged);
        messageBroker.Subscribe<NetworkRequestKingdomDecisionVote>(HandleNetworkRequestKingdomDecisionVote);
        messageBroker.Subscribe<KingdomDecisionVoteChanged>(HandleLocalKingdomDecisionVoteChanged);
        messageBroker.Subscribe<KingdomDecisionRoundStatusChanged>(HandleLocalKingdomDecisionRoundStatusChanged);
        messageBroker.Subscribe<KingdomDecisionResolved>(HandleLocalKingdomDecisionResolved);
        messageBroker.Subscribe<NetworkRequestCreateKingdom>(HandleNetworkRequestCreateKingdom);
        messageBroker.Subscribe<NetworkRequestChangeKingdomName>(HandleNetworkRequestChangeKingdomName);
        messageBroker.Subscribe<KingdomNameChanged>(HandleLocalKingdomNameChanged);
        messageBroker.Subscribe<PeaceOfferPendingStatusChanged>(Handle_PeaceOfferPendingStatusChanged);
        messageBroker.Subscribe<AllianceOfferPendingStatusChanged>(Handle_AllianceOfferPendingStatusChanged);

        // Client side
        messageBroker.Subscribe<NetworkRemoveDecision>(HandleNetworkRemoveDecision);
        messageBroker.Subscribe<NetworkChangeKingdomPolicy>(HandleNetworkChangeKingdomPolicy);
        messageBroker.Subscribe<NetworkChangeKingdomDecisionVote>(HandleNetworkChangeKingdomDecisionVote);
        messageBroker.Subscribe<NetworkKingdomDecisionRoundStatus>(HandleNetworkKingdomDecisionRoundStatus);
        messageBroker.Subscribe<NetworkKingdomDecisionResolved>(HandleNetworkKingdomDecisionResolved);
        messageBroker.Subscribe<NetworkPlayerKingdomCreated>(HandleNetworkPlayerKingdomCreated);
        messageBroker.Subscribe<KingdomDecisionVoteRequested>(HandleKingdomDecisionVoteRequested);
        messageBroker.Subscribe<KingdomCreationRequested>(HandleKingdomCreationRequested);
        messageBroker.Subscribe<DestroyKingdom>(HandleDestroyKingdom);
        messageBroker.Subscribe<RulingClanChanged>(HandleRulingClanChanged);
        messageBroker.Subscribe<KingdomNameChangeRequested>(HandleKingdomNameChangeRequested);
        messageBroker.Subscribe<NetworkKingdomNameChanged>(HandleNetworkKingdomNameChanged);
        messageBroker.Subscribe<NetworkPeaceOfferPendingStatusChanged>(Handle_NetworkPeaceOfferPendingStatusChanged);
        messageBroker.Subscribe<NetworkAllianceOfferPendingStatusChanged>(Handle_NetworkAllianceOfferPendingStatusChanged);
    }

    private void HandleCreateKingdom(MessagePayload<CreateKingdom> obj)
    {
        if (!ModInformation.IsServer)
        {
            Logger.Debug("Skipping kingdom creation request because this instance is not the server.");
            return;
        }

        if (Campaign.Current?.KingdomManager == null)
        {
            Logger.Debug("Skipping kingdom creation request because no campaign is loaded.");
            return;
        }

        var payload = obj.What;
        GameThread.RunSafe(() => ApplyCreateKingdomRequest(payload), context: nameof(KingdomHandler));
    }

    private void HandleChangeKingdomName(MessagePayload<ChangeKingdomName> obj)
    {
        if (!ModInformation.IsServer)
        {
            Logger.Debug("Skipping kingdom rename request because this instance is not the server.");
            return;
        }

        var payload = obj.What;
        RunKingdomMutation(() => ApplyKingdomNameChange(payload));
    }

    private void ApplyCreateKingdomRequest(CreateKingdom payload)
    {
        try
        {
            if (!playerManager.TryGetPlayer(payload.ControllerId, out var player))
            {
                FailCreateKingdomRequest(payload, $"player not found for controller {payload.ControllerId}");
                return;
            }

            if (!objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero))
            {
                FailCreateKingdomRequest(payload, $"hero {player.HeroId} was not found");
                return;
            }

            var clan = hero.Clan;
            if (clan == null)
            {
                FailCreateKingdomRequest(payload, $"hero {player.HeroId} has no clan");
                return;
            }

            if (!TryGetCulture(payload.CultureId, out var culture))
            {
                FailCreateKingdomRequest(payload, $"culture {payload.CultureId} was not found");
                return;
            }

            if (!CanCreateKingdomForClan(clan, payload.KingdomName, out string reason))
            {
                Logger.Warning(
                    "Unable to create kingdom {KingdomName} for controller {ControllerId}: {Reason}",
                    payload.KingdomName,
                    payload.ControllerId,
                    reason);
                ShowInformationMessage($"Unable to create kingdom {payload.KingdomName}: {reason}");
                return;
            }

            if (!kingdomCreator.TryCreateKingdom(clan, payload.KingdomName, culture, payload.ControllerId, out _, out string createError))
            {
                FailCreateKingdomRequest(payload, createError);
                return;
            }
        }
        catch (Exception e)
        {
            Logger.Error(
                e,
                "Unable to create kingdom {KingdomName} for controller {ControllerId}: {Error}. {StackTrace}",
                payload.KingdomName,
                payload.ControllerId,
                e.Message,
                e.StackTrace);
            ShowInformationMessage($"Unable to create kingdom {payload.KingdomName}: {e.Message}");
        }
    }

    private void ApplyKingdomNameChange(ChangeKingdomName payload)
    {
        if (!playerManager.TryGetPlayer(payload.ControllerId, out var player))
        {
            RejectKingdomNameChange(payload, $"player not found for controller {payload.ControllerId}");
            return;
        }
        if (!objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero))
        {
            RejectKingdomNameChange(payload, $"hero {player.HeroId} was not found.");
            return;
        }
        var clan = hero.Clan;
        if (clan == null)
        {
            RejectKingdomNameChange(payload, $"hero {player.HeroId} has no clan.");
            return;
        }
        if (string.IsNullOrWhiteSpace(payload.KingdomId) || !objectManager.TryGetObject(payload.KingdomId, out Kingdom kingdom))
        {
            RejectKingdomNameChange(payload, $"Kingdom {payload.KingdomId} was not found.");
            return;
        }

        if (!CanChangeKingdomName(clan, kingdom, payload.Name, out string reason))
        {
            RejectKingdomNameChange(payload, reason);
            return;
        }

        if (!IsKingdomNameAvailable(kingdom, payload.Name, out string validationReason))
        {
            RejectKingdomNameChange(payload, validationReason);
            return;
        }
        
        ApplyNativeKingdomNameChange(kingdom, payload.Name);
        messageBroker.Publish(this, new KingdomNameChanged(payload.ControllerId, payload.KingdomId));
    }

    // FactionHelper.IsKIngdomNameApplicable relies on Clan.PlayerClan.Kingdom
    // But Clan.PlayerClan is null on a dedicated server. Applied a new name so others are not confused
    private static bool IsKingdomNameAvailable(Kingdom kingdom, string requestedName, out string reason)
    {
        var validationErr = FactionHelper.IsFactionNameApplicable(requestedName);
        
        bool nameAlreadyExists = Kingdom.All?.Any(
            otherKingdom => !ReferenceEquals(otherKingdom, kingdom) && string.Equals(otherKingdom.Name.ToString(), requestedName, StringComparison.InvariantCultureIgnoreCase)) == true;
        
        if (nameAlreadyExists)
        {
            validationErr.Add(GameTexts.FindText("str_kingdom_name_invalid_already_exist", null));
        }

        if (validationErr.Count == 0)
        {
            reason = null;
            return true;
        }
        
        reason = string.Join(Environment.NewLine + Environment.NewLine, validationErr.Select(error => error.ToString()));
        return false;
    }

    private static void ApplyNativeKingdomNameChange(Kingdom kingdom, string requestedName)
    {
        var rawName = new TextObject(requestedName);
        
        var fullName = GameTexts.FindText("str_generic_kingdom_name", null);
        fullName.SetTextVariable("KINGDOM_NAME", rawName);

        var shortName = GameTexts.FindText("str_generic_kingdom_short_name", null);
        shortName.SetTextVariable("KINGDOM_SHORT_NAME", rawName);
        
        kingdom.ChangeKingdomName(fullName, shortName);
    }

    private static void RejectKingdomNameChange(ChangeKingdomName payload, string reason)
    {
        Logger.Warning("Unable to rename {KingdomId} to {KingdomName} for controller {ControllerId}: {Reason}",
            payload.KingdomId,
            payload.Name,
            payload.ControllerId,
            reason);
    }

    private bool TryGetCulture(string cultureId, out CultureObject culture)
    {
        return objectManager.TryGetObject(cultureId, out culture);
    }

    internal static bool CanCreateKingdomForClan(Clan clan, string kingdomName, out string reason)
    {
        if (clan == null)
        {
            reason = "clan was null";
            return false;
        }

        if (string.IsNullOrWhiteSpace(kingdomName))
        {
            reason = "kingdom name was empty";
            return false;
        }

        // The governor dialog runs the native eligibility checks before this request is emitted.
        // Repeating tier/fief/troop checks here can reject a valid dialog result when mirrored clan
        // collections lag behind the server ownership state.
        if (clan.Kingdom != null)
        {
            reason = "clan is already in a kingdom";
            return false;
        }

        reason = null;
        return true;
    }

    internal static bool CanChangeKingdomName(Clan clan, Kingdom kingdom, string requestedName, out string reason)
    {
        if (clan == null)
        {
            reason = "clan was null";
            return false;
        }
        if (kingdom == null)
        {
            reason = "kingdom was null";
            return false;
        }
        if (!ReferenceEquals(clan.Kingdom, kingdom))
        {
            reason = "clan is not a member of the kingdom";
            return false;
        }
        if (!ReferenceEquals(kingdom.RulingClan, clan))
        {
            reason = "clan is not the ruling clan of the kingdom";
            return false;
        }
        if (string.IsNullOrWhiteSpace(requestedName))
        {
            reason = "kingdom name was empty";
            return false;
        }

        reason = null;
        return true;
    }

    private void FailCreateKingdomRequest(CreateKingdom payload, string reason)
    {
        Logger.Warning(
            "Unable to create kingdom {KingdomName} for controller {ControllerId}: {Reason}",
            payload.KingdomName,
            payload.ControllerId,
            reason);
        ShowInformationMessage($"Unable to create kingdom {payload.KingdomName}: {reason}");
    }

    private void HandlePlayerKingdomCreated(MessagePayload<PlayerKingdomCreated> obj)
    {
        var payload = obj.What;

        if (ModInformation.IsServer)
        {
            SendPlayerKingdomCreated(payload);
        }

        GameThread.RunSafe(() =>
        {
            EnsurePlayerKingdomCreatedState(payload);

            string kingdomName = string.IsNullOrWhiteSpace(payload.KingdomName)
                ? payload.KingdomId
                : payload.KingdomName;

            ShowInformationMessageImmediate($"Kingdom {kingdomName} created for clan {payload.ClanId}");
        }, context: nameof(KingdomHandler));
    }

    private void EnsurePlayerKingdomCreatedState(PlayerKingdomCreated payload)
    {
        if (!objectManager.TryGetObject(payload.KingdomId, out Kingdom kingdom))
        {
            Logger.Debug("Created kingdom {KingdomId} was not available when the creation notification arrived.", payload.KingdomId);
            return;
        }

        if (!objectManager.TryGetObject(payload.ClanId, out Clan clan))
        {
            Logger.Debug("Created kingdom clan {ClanId} was not available when the creation notification arrived.", payload.ClanId);
            return;
        }

        using (new AllowedThread())
        {
            KingdomRegistry.EnsureRuntimeCollections(kingdom);
        }

        KingdomCreator.EnsureKingdomRegisteredInCampaign(kingdom, Campaign.Current?.CampaignObjectManager);

        using (new AllowedThread())
        {
            ApplyKingdomCreatedPayload(kingdom, payload);

            if (kingdom.RulingClan != clan)
            {
                kingdom._rulingClan = clan;
            }

            if (clan.Kingdom != kingdom)
            {
                clan.Kingdom = kingdom;
            }

            kingdomMembershipState.EnsureClanInKingdom(kingdom, clan, publishCollectionChanges: false);
        }
    }

    private void ApplyKingdomCreatedPayload(Kingdom kingdom, PlayerKingdomCreated payload)
    {
        if (kingdom == null) return;

        if (!string.IsNullOrWhiteSpace(payload.KingdomName) &&
            (kingdom.Name == null || string.IsNullOrWhiteSpace(kingdom.Name.ToString())))
        {
            TextObject kingdomName = new TextObject(payload.KingdomName);
            kingdom.Name = kingdomName;
            kingdom.InformalName = kingdomName;
            kingdom.EncyclopediaTitle = kingdomName;
            kingdom.EncyclopediaText ??= TextObject.GetEmpty();
            kingdom.EncyclopediaRulerTitle ??= TextObject.GetEmpty();
        }

        if (kingdom.Culture == null &&
            !string.IsNullOrWhiteSpace(payload.CultureId) &&
            objectManager.TryGetObject(payload.CultureId, out CultureObject culture))
        {
            kingdom.Culture = culture;
        }
    }

    private static void ShowInformationMessage(string text)
    {
        GameThread.RunSafe(() =>
        {
            ShowInformationMessageImmediate(text);
        }, context: nameof(KingdomHandler));
    }

    private static void ShowInformationMessageImmediate(string text)
    {
        try
        {
            InformationManager.DisplayMessage(new InformationMessage(text));
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to display kingdom information message: {Message}", text);
        }
    }

    private void HandleApplyKingdomDecisionResolved(MessagePayload<ApplyKingdomDecisionResolved> obj)
    {
        var payload = obj.What;

        RunKingdomMutation(() =>
        {
            decisionVoteManager.ApplyResolved(
                payload.KingdomId,
                payload.DecisionIndex,
                payload.OutcomeIndex,
                payload.IsPlayerDecision,
                payload.OutcomeKey,
                payload.NotificationText);
        });
    }

    private void HandleApplyKingdomDecisionVote(MessagePayload<ApplyKingdomDecisionVote> obj)
    {
        var payload = obj.What;

        RunKingdomMutation(() =>
        {
            decisionVoteManager.ApplyRemoteVote(payload.ClanId, payload.VoteData);
        });
    }

    private void HandleApplyKingdomDecisionRoundStatus(MessagePayload<ApplyKingdomDecisionRoundStatus> obj)
    {
        RunKingdomMutation(() => decisionVoteManager.ApplyRoundStatus(obj.What.Status));
    }

    private void HandleChangeKingdomDecisionVote(MessagePayload<ChangeKingdomDecisionVote> obj)
    {
        var payload = obj.What;

        RunKingdomMutation(() =>
        {
            decisionVoteManager.HandleVoteRequest(payload.ControllerId, payload.VoteData);
        });
    }

    private void HandleChangeKingdomPolicy(MessagePayload<ChangeKingdomPolicy> obj)
    {
        var payload = obj.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObject(payload.KingdomId, out Kingdom kingdom))
            {
                Logger.Debug("Kingdom not found in KingdomHandler with KingdomId: {id}", payload.KingdomId);
                return;
            }

            if (!objectManager.TryGetObject(payload.PolicyId, out PolicyObject policy))
            {
                Logger.Debug("PolicyObject not found in KingdomHandler with PolicyId: {id}", payload.PolicyId);
                return;
            }

            kingdomInterface.ChangeKingdomPolicy(kingdom, policy, payload.IsAdd);
        });
    }

    private void HandleRemoveDecision(MessagePayload<RemoveDecision> obj)
    {
        var payload = obj.What;

        RunKingdomMutation(() =>
        {
            decisionVoteManager.ClearDecisionState(payload.KingdomId, payload.Index);
            if (!objectManager.TryGetObject(payload.KingdomId, out Kingdom kingdom))
            {
                Logger.Debug("Kingdom not found in KingdomDecisionHandler with KingdomId: {id}", payload.KingdomId);
                return;
            }

            // Kingdoms created on clients skip the constructor, so the list can be null.
            var decisions = kingdom._unresolvedDecisions;
            if (decisions == null)
            {
                Logger.Debug("Kingdom {id} has no unresolved decision list.", payload.KingdomId);
                return;
            }

            if (payload.Index < 0 || decisions.Count <= payload.Index)
            {
                Logger.Warning("Index is out of bounds of the list.");
                return;
            }

            KingdomDecision decision = decisions[payload.Index];
            decisionVoteManager.CloseDecision(payload.KingdomId, payload.Index);
            kingdomInterface.RemoveDecision(kingdom, decision);
        });
    }

    private void HandleAddDecision(MessagePayload<AddDecision> obj)
    {
        var payload = obj.What;

        if (!objectManager.TryGetObject(payload.KingdomId, out Kingdom kingdom))
        {
            Logger.Debug("Kingdom not found in KingdomDecisionHandler with KingdomId: {id}", payload.KingdomId);
            return;
        }

        if (!decisionDataConverter.TryGetKingdomDecision(payload.Data, out KingdomDecision kingdomDecision))
        {
            Logger.Warning("KingdomDecision could not be deserialized in KingdomDecisionHandler.");
            return;
        }

        kingdomInterface.RunAddDecision(kingdom, kingdomDecision, payload.IgnoreInfluenceCost, payload.RandomNumber);
    }

    private static void RunKingdomMutation(Action action)
    {
        if (!GameThread.Instance.IsInitialized)
        {
            action();
            return;
        }

        GameThread.RunSafe(action, blocking: true, context: nameof(KingdomHandler));
    }
    private void HandleNetworkDestroyKingdom(MessagePayload<NetworkDestroyKingdom> payload)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(payload.What.KingdomId, out var kingdom)) return;

            Clan rulingClan = kingdom.RulingClan;
            if (rulingClan?.Kingdom == kingdom)
            {
                ChangeKingdomAction.ApplyByLeaveKingdom(rulingClan, true);
            }
            foreach (Kingdom kingdom2 in Kingdom.All)
            {
                if (kingdom2.IsAtWarWith(kingdom))
                {
                    if (!kingdom2.IsAtWarWith(rulingClan))
                    {
                        DeclareWarAction.ApplyByDefault(kingdom2, rulingClan);
                    }
                }
                else if (kingdom2.IsAtWarWith(rulingClan))
                {
                    Debug.FailedAssert("Deviation in peace states between ruling clan & kingdom in abdication", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\KingdomManager.cs", "AbdicateTheThrone", 236);
                }
            }
            // Player mercenary clans must leave first, otherwise DestroyKingdomAction destroys them and removes their heroes
            foreach (Clan playerClan in kingdom.Clans.Where(clan => clan.IsPlayerClan()).ToList())
            {
                ChangeKingdomAction.ApplyByLeaveByKingdomDestruction(playerClan);
            }
            if (!kingdom.IsEliminated)
            {
                DestroyKingdomAction.Apply(kingdom);
            }
        });
    }

    private void HandleNetworkRulingClanChanged(MessagePayload<NetworkRulingClanChanged> payload)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(payload.What.KingdomId, out var kingdom)) return;
            if (!objectManager.TryGetObjectWithLogging<Clan>(payload.What.ClanId, out var clan)) return;

            kingdom.Banner = new Banner(kingdom.Banner);
            ChangeRulingClanAction.Apply(kingdom, clan);
        });
    }

    private void HandleGiftSettlementOwnership(MessagePayload<GiftSettlementOwnership> obj)
    {
        var payload = obj.What;
        if (!objectManager.TryGetId(payload.SettlementToGive, out string settlementId))
        {
            Logger.Warning("Settlement not found in GiftSettlementOwnershipHandler with SettlementId: {id}", payload.SettlementToGive.Id);
            return;
        }

        if (!objectManager.TryGetId(payload.ReceiverClan, out string clanId))
        {
            Logger.Warning("Clan not found in GiftSettlementOwnershipHandler with ClanId: {id}", payload.ReceiverClan.Id);
            return;
        }

        network.SendAll(new NetworkGiftSettlementOwnership(settlementId, clanId));
    }

    private void HandleNetworkGiftSettlementOwnership(MessagePayload<NetworkGiftSettlementOwnership> obj)
    {
        if (obj.Who is not NetPeer peer || !playerManager.TryGetPlayer(peer, out var player))
        {
            Logger.Warning("Ignoring GiftSettlementOwnership {Instance}: sender has no registered player",
                obj.What.SettlementId);
            return;
        }
        var payload = obj.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Settlement>(payload.SettlementId, out var settlement)) return;
            if (!objectManager.TryGetObjectWithLogging<Clan>(payload.ReceiverClanId, out var receiverClan)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var playerHero)) return;
            var playerClan = playerHero.Clan;
            if (playerClan == null) return;
            if (playerClan != playerClan.Kingdom?.RulingClan || playerHero != playerClan.Leader)
            {
                Logger.Warning("Ignoring GiftSettlementOwnership {Instance}: sender's clan {SenderClan} is not the ruling clan of their kingdom",
                    obj.What.SettlementId, playerClan.StringId);
                return;
            }
            if(receiverClan.IsUnderMercenaryService || receiverClan.Kingdom != playerClan.Kingdom || receiverClan == playerClan)
            {
                Logger.Warning("Ignoring GiftSettlementOwnership {Instance}: receiver's clan {ReceiverClan} invalid (Mecenary: {Mecenary}, Kingdom: {Kingdom})",
                    obj.What.SettlementId, payload.ReceiverClanId, receiverClan.IsUnderMercenaryService, receiverClan.Kingdom.StringId);
                return;
            }
            if (!settlement.IsFortification || settlement.Town.IsOwnerUnassigned || settlement.OwnerClan != playerClan)
            { 
                Logger.Warning("Ignoring GiftSettlementOwnership {Instance}: settlement {Settlement} is not a fortification, currently unassigned, or is not owned by the sender's clan {SenderClan}",
                       obj.What.SettlementId, settlement.Id, playerClan.StringId);
                return;
            }
            Campaign.Current.KingdomManager.GiftSettlementOwnership(settlement, receiverClan);
        });
    }

    private void HandleLocalDecisionAdded(MessagePayload<DecisionAdded> obj)
    {
        var payload = obj.What;

        if (!TryGetKingdomId(payload.Kingdom, out var kingdomId)) return;

        if (ModInformation.IsClient)
        {
            var clientData = decisionDataConverter.Convert(payload.Decision);
            network.SendAll(new NetworkAddDecision(kingdomId, clientData, payload.IgnoreInfluenceCost, payload.RandomNumber));
            return;
        }

        // KingdomInterface already resolved an unsupported type on the server, so clients have nothing to add.
        if (!decisionDataConverter.IsSupported(payload.Decision))
        {
            Type decisionType = payload.Decision.GetType();
            if (warnedDecisionTypes.Add(decisionType))
            {
                Logger.Warning(
                    "Kingdom decision type {DecisionType} is not synced, so the server AI decides it instead of a player vote; " +
                    "a compat mod can sync it with KingdomDecisionTypeRegistry.Register",
                    decisionType.FullName);
            }
            return;
        }

        var data = decisionDataConverter.Convert(payload.Decision);
        var message = new NetworkAddDecision(kingdomId, data, payload.IgnoreInfluenceCost, payload.RandomNumber);
        network.SendAll(message);
    }

    private void HandleNetworkAddDecision(MessagePayload<NetworkAddDecision> obj)
    {
        var payload = obj.What;

        // The server relays a decision to clients, and a client sends its own proposals to the server.
        if (ModInformation.IsClient)
        {
            GameThread.RunSafe(() =>
            {
                if (!ShouldApplyNetworkDecision(payload.KingdomId)) return;

                var clientMessage = new AddDecision(payload.KingdomId, payload.Data, payload.IgnoreInfluenceCost, payload.RandomNumber);
                messageBroker.Publish(this, clientMessage);
            }, context: nameof(HandleNetworkAddDecision));
            return;
        }

        messageBroker.Publish(
            this,
            new AddDecision(payload.KingdomId, payload.Data, payload.IgnoreInfluenceCost, payload.RandomNumber));

        var message = new NetworkAddDecision(
            payload.KingdomId,
            payload.Data,
            payload.IgnoreInfluenceCost,
            payload.RandomNumber);

        if (obj.Who is NetPeer peer)
        {
            network.SendAllBut(peer, message);
            return;
        }

        network.SendAll(message);
    }

    private bool TryGetKingdomId(Kingdom kingdom, out string kingdomId)
    {
        return objectManager.TryGetIdWithLogging(kingdom, out kingdomId);
    }

    private static bool TryCreatePendingSettlementRestore(
        string partyId,
        string settlementId,
        out PendingSettlementRestore pending)
    {
        pending = default;
        if (string.IsNullOrWhiteSpace(partyId) || string.IsNullOrWhiteSpace(settlementId)) return false;

        pending = new PendingSettlementRestore(partyId, settlementId);
        return true;
    }

    private bool TryGetSettlement(string settlementId, out Settlement settlement)
    {
        return objectManager.TryGetObjectWithLogging(settlementId, out settlement);
    }

    private bool TryGetSettlementId(Settlement settlement, out string settlementId)
    {
        return objectManager.TryGetIdWithLogging(settlement, out settlementId);
    }

    // --- Server side ---

    private void HandleNetworkRequestCreateKingdom(MessagePayload<NetworkRequestCreateKingdom> obj)
    {
        var payload = obj.What;
        var partyId = payload.PartyId;
        if (obj.Who is not NetPeer peer) return;
        if (!playerManager.TryGetPlayer(peer, out var player)) return;
        var controllerId = player.ControllerId;

        if (string.IsNullOrWhiteSpace(partyId))
        {
            partyId = player.MobilePartyId;
        }

        if (TryCreatePendingSettlementRestore(partyId, payload.SettlementId, out var pending))
        {
            pendingKingdomCreationSettlements[controllerId] = pending;
            settlementTracker.Track(pending.PartyId, pending.SettlementId);
        }

        RunKingdomMutation(() =>
        {
            RestoreCreatingPartySettlement(partyId, payload.SettlementId);
        });

        messageBroker.Publish(this, new CreateKingdom(controllerId, payload.KingdomName, payload.CultureId));
    }

    private void HandleNetworkRequestChangeKingdomName(MessagePayload<NetworkRequestChangeKingdomName> obj)
    {
        if (obj.Who is not NetPeer peer) return;
        if (!playerManager.TryGetPlayer(peer, out var player)) return;

        var payload = obj.What;
        messageBroker.Publish(this, new ChangeKingdomName(player.ControllerId, payload.KingdomId, payload.Name));
    }

    private void SendPlayerKingdomCreated(PlayerKingdomCreated payload)
    {
        TryGetPlayerSettlementContext(payload.ControllerId, out var partyId, out var settlementId);

        if ((string.IsNullOrWhiteSpace(partyId) || string.IsNullOrWhiteSpace(settlementId)) &&
            pendingKingdomCreationSettlements.TryGetValue(payload.ControllerId, out var pending))
        {
            partyId = pending.PartyId;
            settlementId = pending.SettlementId;
        }

        RunKingdomMutation(() =>
        {
            RestoreCreatingPartySettlement(partyId, settlementId);
            settlementTracker.Complete(partyId);
            pendingKingdomCreationSettlements.Remove(payload.ControllerId);
        });

        var message = new NetworkPlayerKingdomCreated(
            payload.ControllerId,
            payload.KingdomId,
            payload.KingdomName,
            payload.ClanId,
            partyId,
            settlementId,
            payload.CultureId);
        network.SendAll(message);
    }

    private void HandleLocalKingdomNameChanged(MessagePayload<KingdomNameChanged> obj)
    {
        if (ModInformation.IsClient) return;
        var payload = obj.What;

        // Broadcasts the notification to all clients
        network.SendAll(new NetworkKingdomNameChanged(payload.KingdomId));
    }

    private void RestoreCreatingPartySettlement(string partyId, string settlementId)
    {
        if (string.IsNullOrWhiteSpace(partyId) || string.IsNullOrWhiteSpace(settlementId)) return;
        if (!objectManager.TryGetObject<MobileParty>(partyId, out var party)) return;
        if (!TryGetSettlement(settlementId, out var settlement)) return;
        settlementTracker.TrackParty(party, partyId, settlement, settlementId);
        if (party.CurrentSettlement == settlement) return;

        RunKingdomMutation(() =>
        {
            using (new AllowedThread())
            {
                try
                {
                    party.CurrentSettlement = settlement;
                }
                catch (NullReferenceException)
                {
                    party.SetCurrentSettlementDirectly(settlement);
                }
            }
        });

        if (!objectManager.TryGetHandleWithLogging(settlement, out var settlementHandle)) return;
        if (!objectManager.TryGetHandleWithLogging(party, out var partyHandle)) return;
        network.SendAll(new NetworkPartyEnterSettlement(settlementHandle, partyHandle));
    }

    private bool TryGetPlayerSettlementContext(
        string controllerId,
        out string partyId,
        out string settlementId)
    {
        partyId = null;
        settlementId = null;

        if (!playerManager.TryGetPlayer(controllerId, out var player)) return false;
        if (!objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party)) return false;
        if (party.CurrentSettlement == null) return false;
        if (!TryGetSettlementId(party.CurrentSettlement, out settlementId)) return false;

        partyId = player.MobilePartyId;
        return true;
    }

    private void HandleLocalKingdomDecisionResolved(MessagePayload<KingdomDecisionResolved> obj)
    {
        if (ModInformation.IsClient) return;
        var payload = obj.What;

        var message = new NetworkKingdomDecisionResolved(
            payload.KingdomId,
            payload.DecisionIndex,
            payload.OutcomeIndex,
            payload.IsPlayerDecision,
            payload.OutcomeKey,
            payload.NotificationText);
        network.SendAll(message);
    }

    private void HandleLocalKingdomDecisionVoteChanged(MessagePayload<KingdomDecisionVoteChanged> obj)
    {
        if (ModInformation.IsClient) return;
        var payload = obj.What;

        var message = new NetworkChangeKingdomDecisionVote(payload.ClanId, payload.VoteData);
        network.SendAll(message);
    }

    private void HandleLocalKingdomDecisionRoundStatusChanged(MessagePayload<KingdomDecisionRoundStatusChanged> obj)
    {
        if (ModInformation.IsClient) return;
        network.SendAll(new NetworkKingdomDecisionRoundStatus(obj.What.Status));
    }

    private void HandleNetworkRequestKingdomDecisionVote(MessagePayload<NetworkRequestKingdomDecisionVote> obj)
    {
        var payload = obj.What;
        if (obj.Who is not NetPeer peer) return;
        if (!playerManager.TryGetPlayer(peer, out var player)) return;
        var controllerId = player.ControllerId;

        // Interim support changes arrive while a player is still choosing, so only a confirmed vote is
        // reported here.
        if (payload.VoteData != null && payload.VoteData.IsFinal)
        {
            Logger.Information(
                "Received kingdom decision vote from controller {ControllerId} for {KingdomId} " +
                "decision {DecisionIndex}; final {IsFinal}",
                controllerId,
                payload.VoteData.KingdomId,
                payload.VoteData.DecisionIndex,
                payload.VoteData.IsFinal);
        }

        messageBroker.Publish(this, new ChangeKingdomDecisionVote(controllerId, payload.VoteData));
    }

    private void HandleLocalKingdomPolicyChanged(MessagePayload<KingdomPolicyChanged> obj)
    {
        if (ModInformation.IsClient) return;
        var payload = obj.What;

        if (!objectManager.TryGetIdWithLogging(payload.Kingdom, out var kingdomId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.Policy, out var policyId)) return;

        var message = new NetworkChangeKingdomPolicy(kingdomId, policyId, payload.IsAdd);
        network.SendAll(message);
    }

    private void HandleLocalDecisionRemoved(MessagePayload<DecisionRemoved> obj)
    {
        if (ModInformation.IsClient) return;
        var payload = obj.What;

        if (!objectManager.TryGetIdWithLogging(payload.Kingdom, out var kingdomId)) return;

        var message = new NetworkRemoveDecision(kingdomId, payload.Index);
        network.SendAll(message);
    }

    private void Handle_PeaceOfferPendingStatusChanged(MessagePayload<PeaceOfferPendingStatusChanged> payload)
    {
        if (ModInformation.IsClient) return;
        var obj = payload.What;
        if (!objectManager.TryGetIdWithLogging(obj.RequestingKingdom, out var requestingKingdomId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.TargetKingdom, out var targetKingdomId)) return;
        PeaceOfferPendingRegistry.Set(obj.RequestingKingdom.StringId, obj.TargetKingdom.StringId, obj.IsPending);
        network.SendAll(new NetworkPeaceOfferPendingStatusChanged(requestingKingdomId, targetKingdomId, obj.IsPending));
    }

    private void Handle_AllianceOfferPendingStatusChanged(MessagePayload<AllianceOfferPendingStatusChanged> payload)
    {
        if (ModInformation.IsClient) return;
        var obj = payload.What;
        if (!objectManager.TryGetIdWithLogging(obj.RequestingKingdom, out var requestingKingdomId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.TargetKingdom, out var targetKingdomId)) return;
        AllianceOfferPendingRegistry.Set(obj.RequestingKingdom.StringId, obj.TargetKingdom.StringId, obj.IsPending);
        network.SendAll(new NetworkAllianceOfferPendingStatusChanged(requestingKingdomId, targetKingdomId, obj.IsPending));
    }

    // --- Client side ---

    private void HandleKingdomCreationRequested(MessagePayload<KingdomCreationRequested> obj)
    {
        if (ModInformation.IsServer) return;
        var payload = obj.What;
        pendingKingdomCreationSettlement = CapturePendingSettlementRestore();
        string partyId = null;
        string settlementId = null;
        if (pendingKingdomCreationSettlement.HasValue)
        {
            partyId = pendingKingdomCreationSettlement.Value.PartyId;
            settlementId = pendingKingdomCreationSettlement.Value.SettlementId;
            settlementTracker.Track(partyId, settlementId);
            RestoreSettlementContext(pendingKingdomCreationSettlement.Value, notifyServer: false);
        }

        var message = new NetworkRequestCreateKingdom(
            payload.KingdomName,
            payload.CultureId,
            partyId,
            settlementId);
        network.SendAll(message);
    }

    private void HandleKingdomNameChangeRequested(MessagePayload<KingdomNameChangeRequested> obj)
    {
        if (ModInformation.IsServer) return;
        var payload = obj.What;

        if (!TryGetKingdomId(payload.Kingdom, out var kingdomId)) return;

        var message = new NetworkRequestChangeKingdomName(kingdomId, payload.Name);
        network.SendAll(message);
    }

    private void HandleNetworkKingdomNameChanged(MessagePayload<NetworkKingdomNameChanged> obj)
    {
        var kingdomId = obj.What.KingdomId;

        GameThread.RunSafe(() =>
        {
            if (ScreenManager.TopScreen is not GauntletKingdomScreen kingdomScreen) return;

            var dataSource = kingdomScreen.DataSource;
            if (dataSource == null) return;

            if (!objectManager.TryGetObject(kingdomId, out Kingdom kingdom)) return;

            if (!ReferenceEquals(dataSource.Kingdom, kingdom)) return;

            dataSource.OnRefresh();
            dataSource.RefreshValues();
        }, context: nameof(KingdomHandler));
    }

    private void HandleNetworkPlayerKingdomCreated(MessagePayload<NetworkPlayerKingdomCreated> obj)
    {
        var payload = obj.What;
        var message = new PlayerKingdomCreated(
            payload.ControllerId,
            payload.KingdomId,
            payload.KingdomName,
            payload.ClanId,
            payload.CultureId);
        messageBroker.Publish(this, message);

        RunKingdomMutation(() =>
        {
            if (payload.ControllerId == controllerIdProvider.ControllerId)
            {
                RestorePendingSettlementAfterKingdomCreation(payload.PartyId, payload.SettlementId);
            }
            else if (TryCreatePendingSettlementRestore(payload.PartyId, payload.SettlementId, out var notificationRestore))
            {
                RestoreSettlementContext(notificationRestore, notifyServer: false);
                ClearRemoteSettlementRestore(notificationRestore);
            }
        });
    }

    private PendingSettlementRestore? CapturePendingSettlementRestore()
    {
        var party = ResolveCreatingPlayerParty();
        var settlement = GetPartyCurrentSettlement(party)
            ?? GetCurrentSettlement()
            ?? GetEncounterSettlement();

        if (party == null || settlement == null) return null;
        if (!objectManager.TryGetIdWithLogging(party, out var partyId)) return null;
        if (!TryGetSettlementId(settlement, out var settlementId)) return null;

        settlementTracker.TrackParty(party, partyId, settlement, settlementId);
        return new PendingSettlementRestore(partyId, settlementId);
    }

    private static Settlement GetPartyCurrentSettlement(MobileParty party)
    {
        try
        {
            return party?.CurrentSettlement;
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    private static Settlement GetCurrentSettlement()
    {
        try
        {
            return Settlement.CurrentSettlement;
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    private static Settlement GetEncounterSettlement()
    {
        try
        {
            return PlayerEncounter.EncounterSettlement;
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    private MobileParty ResolveCreatingPlayerParty()
    {
        if (playerManager.TryGetPlayer(controllerIdProvider.ControllerId, out var player) &&
            objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party))
        {
            return party;
        }

        try
        {
            return MobileParty.MainParty;
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    private void RestorePendingSettlementAfterKingdomCreation(string partyId, string settlementId)
    {
        var pending = pendingKingdomCreationSettlement;
        pendingKingdomCreationSettlement = null;

        if (!pending.HasValue &&
            TryCreatePendingSettlementRestore(partyId, settlementId, out var notificationPending))
        {
            pending = notificationPending;
        }

        if (!pending.HasValue)
        {
            return;
        }

        RunKingdomMutation(() =>
        {
            RestoreSettlementContext(pending.Value, notifyServer: true);
            settlementTracker.Complete(pending.Value.PartyId);
        });
    }

    private void RestoreSettlementContext(PendingSettlementRestore pending, bool notifyServer)
    {
        if (!objectManager.TryGetObject<MobileParty>(pending.PartyId, out var party)) return;
        if (!TryGetSettlement(pending.SettlementId, out var settlement)) return;

        settlementTracker.TrackParty(party, pending.PartyId, settlement, pending.SettlementId);

        bool restoredSettlement = party.CurrentSettlement != settlement;
        if (restoredSettlement)
        {
            EnsurePartySettlement(party, settlement);
        }

        bool needsEncounter = PlayerEncounter.Current == null;
        if (notifyServer && (restoredSettlement || needsEncounter))
        {
            messageBroker.Publish(this, new StartSettlementEncounterAttempted(party, settlement));
        }
    }

    private void ClearRemoteSettlementRestore(PendingSettlementRestore pending)
    {
        if (objectManager.TryGetObject<MobileParty>(pending.PartyId, out var party))
        {
            settlementTracker.Clear(party, pending.PartyId);
            return;
        }

        settlementTracker.Clear(null, pending.PartyId);
    }

    private static void EnsurePartySettlement(MobileParty party, Settlement settlement)
    {
        if (party.CurrentSettlement == settlement) return;

        RunKingdomMutation(() =>
        {
            using (new AllowedThread())
            {
                party.CurrentSettlement = settlement;
            }
        });
    }

    private void HandleKingdomDecisionVoteRequested(MessagePayload<KingdomDecisionVoteRequested> obj)
    {
        if (ModInformation.IsServer) return;
        var payload = obj.What;
        var message = new NetworkRequestKingdomDecisionVote(payload.VoteData);
        network.SendAll(message);
    }

    private void HandleNetworkKingdomDecisionResolved(MessagePayload<NetworkKingdomDecisionResolved> obj)
    {
        var payload = obj.What;
        var message = new ApplyKingdomDecisionResolved(
            payload.KingdomId,
            payload.DecisionIndex,
            payload.OutcomeIndex,
            payload.IsPlayerDecision,
            payload.OutcomeKey,
            payload.NotificationText);
        messageBroker.Publish(this, message);
    }

    private void HandleNetworkChangeKingdomDecisionVote(MessagePayload<NetworkChangeKingdomDecisionVote> obj)
    {
        var payload = obj.What;
        var message = new ApplyKingdomDecisionVote(payload.ClanId, payload.VoteData);
        messageBroker.Publish(this, message);
    }

    private void HandleNetworkKingdomDecisionRoundStatus(MessagePayload<NetworkKingdomDecisionRoundStatus> obj)
    {
        messageBroker.Publish(this, new ApplyKingdomDecisionRoundStatus(obj.What.Status));
    }

    private void HandleNetworkChangeKingdomPolicy(MessagePayload<NetworkChangeKingdomPolicy> obj)
    {
        var payload = obj.What;
        var message = new ChangeKingdomPolicy(payload.KingdomId, payload.PolicyId, payload.IsAdd);
        messageBroker.Publish(this, message);
    }

    private void HandleNetworkRemoveDecision(MessagePayload<NetworkRemoveDecision> obj)
    {
        var payload = obj.What;
        var message = new RemoveDecision(payload.KingdomId, payload.Index);
        messageBroker.Publish(this, message);
    }

    private bool ShouldApplyNetworkDecision(string kingdomId)
    {
        if (string.IsNullOrWhiteSpace(kingdomId)) return false;
        if (!objectManager.TryGetObject(kingdomId, out Kingdom kingdom)) return true;
        if (!playerManager.TryGetPlayer(controllerIdProvider.ControllerId, out var player)) return true;
        if (string.IsNullOrWhiteSpace(player.HeroId)) return false;
        if (!objectManager.TryGetObject(player.HeroId, out Hero hero)) return false;

        return hero.Clan?.Kingdom == kingdom;
    }

    private void HandleDestroyKingdom(MessagePayload<DestroyKingdom> obj)
    {
        if (ModInformation.IsServer) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.Kingdom, out var kingdomId)) return;

        network.SendAll(new NetworkDestroyKingdom(kingdomId));
    }

    private void HandleRulingClanChanged(MessagePayload<RulingClanChanged> obj)
    {
        if (ModInformation.IsServer) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.Kingdom, out var kingdomId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.Clan, out var clanId)) return;

        network.SendAll(new NetworkRulingClanChanged(kingdomId, clanId));
    }

    private void Handle_NetworkPeaceOfferPendingStatusChanged(MessagePayload<NetworkPeaceOfferPendingStatusChanged> payload)
    {
        if (ModInformation.IsServer) return;
        var obj = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.RequestingKingdomId, out var requestingKingdom)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.TargetKingdomId, out var targetKingdom)) return;
            PeaceOfferPendingRegistry.Set(requestingKingdom.StringId, targetKingdom.StringId, obj.IsPending);
        });
    }

    private void Handle_NetworkAllianceOfferPendingStatusChanged(MessagePayload<NetworkAllianceOfferPendingStatusChanged> payload)
    {
        if (ModInformation.IsServer) return;
        var obj = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.RequestingKingdomId, out var requestingKingdom)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.TargetKingdomId, out var targetKingdom)) return;
            AllianceOfferPendingRegistry.Set(requestingKingdom.StringId, targetKingdom.StringId, obj.IsPending);
        });
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AddDecision>(HandleAddDecision);
        messageBroker.Unsubscribe<RemoveDecision>(HandleRemoveDecision);
        messageBroker.Unsubscribe<ChangeKingdomPolicy>(HandleChangeKingdomPolicy);
        messageBroker.Unsubscribe<ChangeKingdomDecisionVote>(HandleChangeKingdomDecisionVote);
        messageBroker.Unsubscribe<ApplyKingdomDecisionVote>(HandleApplyKingdomDecisionVote);
        messageBroker.Unsubscribe<ApplyKingdomDecisionRoundStatus>(HandleApplyKingdomDecisionRoundStatus);
        messageBroker.Unsubscribe<ApplyKingdomDecisionResolved>(HandleApplyKingdomDecisionResolved);
        messageBroker.Unsubscribe<CreateKingdom>(HandleCreateKingdom);
        messageBroker.Unsubscribe<PlayerKingdomCreated>(HandlePlayerKingdomCreated);
        messageBroker.Unsubscribe<NetworkDestroyKingdom>(HandleNetworkDestroyKingdom);
        messageBroker.Unsubscribe<NetworkRulingClanChanged>(HandleNetworkRulingClanChanged);
        messageBroker.Unsubscribe<ChangeKingdomName>(HandleChangeKingdomName);
        messageBroker.Unsubscribe<GiftSettlementOwnership>(HandleGiftSettlementOwnership);
        messageBroker.Unsubscribe<NetworkGiftSettlementOwnership>(HandleNetworkGiftSettlementOwnership);

        messageBroker.Unsubscribe<DecisionAdded>(HandleLocalDecisionAdded);
        messageBroker.Unsubscribe<NetworkAddDecision>(HandleNetworkAddDecision);

        messageBroker.Unsubscribe<DecisionRemoved>(HandleLocalDecisionRemoved);
        messageBroker.Unsubscribe<KingdomPolicyChanged>(HandleLocalKingdomPolicyChanged);
        messageBroker.Unsubscribe<NetworkRequestKingdomDecisionVote>(HandleNetworkRequestKingdomDecisionVote);
        messageBroker.Unsubscribe<KingdomDecisionVoteChanged>(HandleLocalKingdomDecisionVoteChanged);
        messageBroker.Unsubscribe<KingdomDecisionRoundStatusChanged>(HandleLocalKingdomDecisionRoundStatusChanged);
        messageBroker.Unsubscribe<KingdomDecisionResolved>(HandleLocalKingdomDecisionResolved);
        messageBroker.Unsubscribe<NetworkRequestCreateKingdom>(HandleNetworkRequestCreateKingdom);
        messageBroker.Unsubscribe<NetworkRequestChangeKingdomName>(HandleNetworkRequestChangeKingdomName);
        messageBroker.Unsubscribe<KingdomNameChanged>(HandleLocalKingdomNameChanged);
        messageBroker.Unsubscribe<PeaceOfferPendingStatusChanged>(Handle_PeaceOfferPendingStatusChanged);
        messageBroker.Unsubscribe<AllianceOfferPendingStatusChanged>(Handle_AllianceOfferPendingStatusChanged);

        messageBroker.Unsubscribe<NetworkRemoveDecision>(HandleNetworkRemoveDecision);
        messageBroker.Unsubscribe<NetworkChangeKingdomPolicy>(HandleNetworkChangeKingdomPolicy);
        messageBroker.Unsubscribe<NetworkChangeKingdomDecisionVote>(HandleNetworkChangeKingdomDecisionVote);
        messageBroker.Unsubscribe<NetworkKingdomDecisionRoundStatus>(HandleNetworkKingdomDecisionRoundStatus);
        messageBroker.Unsubscribe<NetworkKingdomDecisionResolved>(HandleNetworkKingdomDecisionResolved);
        messageBroker.Unsubscribe<NetworkPlayerKingdomCreated>(HandleNetworkPlayerKingdomCreated);
        messageBroker.Unsubscribe<KingdomDecisionVoteRequested>(HandleKingdomDecisionVoteRequested);
        messageBroker.Unsubscribe<KingdomCreationRequested>(HandleKingdomCreationRequested);
        messageBroker.Unsubscribe<DestroyKingdom>(HandleDestroyKingdom);
        messageBroker.Unsubscribe<RulingClanChanged>(HandleRulingClanChanged);
        messageBroker.Unsubscribe<KingdomNameChangeRequested>(HandleKingdomNameChangeRequested);
        messageBroker.Unsubscribe<NetworkKingdomNameChanged>(HandleNetworkKingdomNameChanged);
        messageBroker.Unsubscribe<NetworkPeaceOfferPendingStatusChanged>(Handle_NetworkPeaceOfferPendingStatusChanged);
        messageBroker.Unsubscribe<NetworkAllianceOfferPendingStatusChanged>(Handle_NetworkAllianceOfferPendingStatusChanged);
    }

    private readonly struct PendingSettlementRestore
    {
        public readonly string PartyId;
        public readonly string SettlementId;

        public PendingSettlementRestore(string partyId, string settlementId)
        {
            PartyId = partyId;
            SettlementId = settlementId;
        }
    }
}
