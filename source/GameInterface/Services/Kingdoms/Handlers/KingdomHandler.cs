using Common;
using Common.Extensions;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Helpers;
using Serilog;
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.Kingdoms.Handlers;

/// <summary>
/// Handler for <see cref="Kingdom"/> messages
/// </summary>
public class KingdomHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<KingdomHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IKingdomDecisionVoteManager decisionVoteManager;
    private readonly IKingdomMembershipState kingdomMembershipState;
    private readonly IKingdomInterface kingdomInterface;
    private readonly IKingdomCreator kingdomCreator;

    public KingdomHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IKingdomDecisionVoteManager decisionVoteManager,
        IKingdomMembershipState kingdomMembershipState,
        IKingdomInterface kingdomInterface,
        IKingdomCreator kingdomCreator)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.decisionVoteManager = decisionVoteManager;
        this.kingdomMembershipState = kingdomMembershipState;
        this.kingdomInterface = kingdomInterface;
        this.kingdomCreator = kingdomCreator;
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

            if (!objectManager.TryGetObjectWithLogging<Clan>(player.ClanId, out var clan))
            {
                FailCreateKingdomRequest(payload, $"clan {player.ClanId} was not found");
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
        if (string.IsNullOrWhiteSpace(player.ClanId) || !objectManager.TryGetObject(player.ClanId, out Clan clan))
        {
            RejectKingdomNameChange(payload, $"clan {player.ClanId} was not found.");
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
        RunKingdomMutation(() =>
        {
            if (!TryGetDecisionKingdom(payload, out Kingdom kingdom))
            {
                Logger.Debug("Kingdom not found in KingdomDecisionHandler with KingdomId: {id}", payload.KingdomId);
                return;
            }

            HydrateInboundAllianceProposer(payload.Data, kingdom);

            if (!payload.Data.TryGetKingdomDecision(objectManager, out KingdomDecision kingdomDecision))
            {
                Logger.Warning("KingdomDecision could not be deserialized in KingdomDecisionHandler.");
                return;
            }

            bool isPendingPlayerAllianceOffer = payload.Data is StartAllianceDecisionData { IsProposedByOpponent: true };
            kingdomInterface.RunAddDecision(
                kingdom,
                kingdomDecision,
                payload.IgnoreInfluenceCost,
                payload.RandomNumber,
                isPendingPlayerAllianceOffer);
        });
    }

    private bool TryGetDecisionKingdom(AddDecision payload, out Kingdom kingdom)
    {
        if (payload.Data is StartAllianceDecisionData startAllianceDecisionData)
        {
            return startAllianceDecisionData.TryGetProposerClanAndDecisionKingdom(objectManager, out _, out kingdom);
        }

        return objectManager.TryGetObject(payload.KingdomId, out kingdom);
    }

    private void HydrateInboundAllianceProposer(KingdomDecisionData data, Kingdom kingdom)
    {
        if (!ModInformation.IsClient) return;
        if (data is not StartAllianceDecisionData { IsProposedByOpponent: true } startAllianceDecisionData) return;
        if (!objectManager.TryGetObject(startAllianceDecisionData.ProposerClanId, out Clan proposerClan)) return;
        if (proposerClan.Kingdom != null) return;

        using (new AllowedThread())
        {
            kingdomMembershipState.EnsureClanInKingdom(kingdom, proposerClan, publishCollectionChanges: false);
        }
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
    }
}
