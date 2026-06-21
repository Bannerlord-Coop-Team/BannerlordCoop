using Common.Logging;
using Common.Messaging;
using Common;
using Common.Extensions;
using Common.Util;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Registry.Auto;
using Serilog;
using System.Reflection;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
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

    public KingdomHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IKingdomDecisionVoteManager decisionVoteManager,
        IKingdomMembershipState kingdomMembershipState)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.decisionVoteManager = decisionVoteManager;
        this.kingdomMembershipState = kingdomMembershipState;
        messageBroker.Subscribe<AddDecision>(HandleAddDecision);
        messageBroker.Subscribe<RemoveDecision>(HandleRemoveDecision);
        messageBroker.Subscribe<ChangeKingdomPolicy>(HandleChangeKingdomPolicy);
        messageBroker.Subscribe<ChangeKingdomDecisionVote>(HandleChangeKingdomDecisionVote);
        messageBroker.Subscribe<ApplyKingdomDecisionVote>(HandleApplyKingdomDecisionVote);
        messageBroker.Subscribe<ApplyKingdomDecisionResolved>(HandleApplyKingdomDecisionResolved);
        messageBroker.Subscribe<CreateKingdom>(HandleCreateKingdom);
        messageBroker.Subscribe<PlayerKingdomCreated>(HandlePlayerKingdomCreated);
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

            var campaign = Campaign.Current;
            var campaignObjectManager = campaign.CampaignObjectManager;
            var kingdomManager = campaign.KingdomManager;

            TextObject kingdomName = new TextObject(payload.KingdomName);
            Kingdom createdKingdom = null;

            try
            {
                kingdomManager.CreateKingdom(
                    kingdomName,
                    kingdomName,
                    culture,
                    clan,
                    culture.DefaultPolicyList,
                    TextObject.GetEmpty(),
                    kingdomName,
                    TextObject.GetEmpty());
            }
            catch (Exception e)
            {
                Logger.Warning(
                    e,
                    "Native kingdom creation failed for {KingdomName}; falling back to coop kingdom state creation.",
                    payload.KingdomName);
            }

            createdKingdom = clan.Kingdom ?? campaignObjectManager.Kingdoms
                .FirstOrDefault(kingdom => kingdom?.RulingClan == clan && kingdom.Name?.ToString() == payload.KingdomName)
                ?? CreateCoopKingdom(kingdomName, culture, clan);

            if (createdKingdom == null)
            {
                FailCreateKingdomRequest(payload, "native creation completed but no kingdom was assigned to the clan");
                return;
            }

            EnsureKingdomRegisteredInCampaign(createdKingdom, campaignObjectManager);

            string kingdomId = null;
            if (!objectManager.TryGetId(createdKingdom, out kingdomId))
            {
                messageBroker.Publish(this, new InstanceCreated<Kingdom>(createdKingdom));
            }

            SyncCreatedKingdomProperties(createdKingdom, kingdomName, culture);

            if (!objectManager.TryGetId(createdKingdom, out kingdomId))
            {
                FailCreateKingdomRequest(payload, "created kingdom could not be registered with the coop object manager");
                return;
            }

            kingdomMembershipState.EnsureClanInKingdom(createdKingdom, clan, publishCollectionChanges: true);

            messageBroker.Publish(
                this,
                new PlayerKingdomCreated(payload.ControllerId, kingdomId, payload.KingdomName, player.ClanId, payload.CultureId));
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

    private static Kingdom CreateCoopKingdom(TextObject kingdomName, CultureObject culture, Clan clan)
    {
        var kingdom = new Kingdom();

        kingdom._rulingClan = clan;
        SyncCreatedKingdomProperties(kingdom, kingdomName, culture);
        return kingdom;
    }

    private static void SyncCreatedKingdomProperties(Kingdom kingdom, TextObject kingdomName, CultureObject culture)
    {
        KingdomRegistry.EnsureRuntimeCollections(kingdom);

        kingdom.Name = kingdomName;
        kingdom.InformalName = kingdomName;
        kingdom.Culture = culture;
        kingdom.EncyclopediaText = TextObject.GetEmpty();
        kingdom.EncyclopediaTitle = kingdomName;
        kingdom.EncyclopediaRulerTitle = TextObject.GetEmpty();
        kingdom._isEliminated = false;
    }

    private static void EnsureKingdomRegisteredInCampaign(Kingdom kingdom, CampaignObjectManager campaignObjectManager)
    {
        if (campaignObjectManager == null || campaignObjectManager.Kingdoms.Contains(kingdom)) return;

        using (new AllowedThread())
        {
            kingdom._isEliminated = false;
        }

        campaignObjectManager.AddKingdom(kingdom);
        if (!campaignObjectManager.Kingdoms.Contains(kingdom)
            && campaignObjectManager._kingdoms != null
            && !campaignObjectManager._kingdoms.Contains(kingdom))
        {
            campaignObjectManager._kingdoms.Add(kingdom);
        }
    }

    private bool TryGetCulture(string cultureId, out CultureObject culture)
    {
        if (objectManager.TryGetObject(cultureId, out culture)) return true;

        culture = Campaign.Current?.ObjectManager?.GetObject<CultureObject>(cultureId);
        return culture != null;
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

        EnsureKingdomRegisteredInCampaign(kingdom, Campaign.Current?.CampaignObjectManager);

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

        KingdomPatches.RunChangeKingdomPolicy(kingdom, policy, payload.IsAdd);
    }

    private void HandleRemoveDecision(MessagePayload<RemoveDecision> obj)
    {
        var payload = obj.What;

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

        RunKingdomMutation(() =>
        {
            decisionVoteManager.ClearDecisionState(payload.KingdomId, payload.Index);
        });

        if (payload.Index >= 0 && decisions.Count > payload.Index)
        {
            KingdomPatches.RunOriginalRemoveDecision(kingdom, decisions[payload.Index]);
        }
        else
        {
            Logger.Warning("Index is out of bounds of the list.");
            return;
        }
    }

    private void HandleAddDecision(MessagePayload<AddDecision> obj)
    {
        var payload = obj.What;

        if (!objectManager.TryGetObject(payload.KingdomId, out Kingdom kingdom))
        {
            Logger.Debug("Kingdom not found in KingdomDecisionHandler with KingdomId: {id}", payload.KingdomId);
            return;
        }

        if (!payload.Data.TryGetKingdomDecision(objectManager, out KingdomDecision kingdomDecision))
        {
            Logger.Warning("KingdomDecision could not be deserialized in KingdomDecisionHandler.");
            return;
        }

        KingdomPatches.RunCoopAddDecision(kingdom, kingdomDecision, payload.IgnoreInfluenceCost, payload.RandomNumber);
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

    public void Dispose()
    {
        messageBroker.Unsubscribe<AddDecision>(HandleAddDecision);
        messageBroker.Unsubscribe<RemoveDecision>(HandleRemoveDecision);
        messageBroker.Unsubscribe<ChangeKingdomPolicy>(HandleChangeKingdomPolicy);
        messageBroker.Unsubscribe<ChangeKingdomDecisionVote>(HandleChangeKingdomDecisionVote);
        messageBroker.Unsubscribe<ApplyKingdomDecisionVote>(HandleApplyKingdomDecisionVote);
        messageBroker.Unsubscribe<ApplyKingdomDecisionResolved>(HandleApplyKingdomDecisionResolved);
        messageBroker.Unsubscribe<CreateKingdom>(HandleCreateKingdom);
        messageBroker.Unsubscribe<PlayerKingdomCreated>(HandlePlayerKingdomCreated);
    }
}
