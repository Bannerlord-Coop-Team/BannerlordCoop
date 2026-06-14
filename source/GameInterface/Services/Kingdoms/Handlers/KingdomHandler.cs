using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Reflection;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Library;
using Common.Extensions;

namespace GameInterface.Services.Kingdoms.Handlers;

/// <summary>
/// Handler for <see cref="Kingdom"/> messages
/// </summary>
public class KingdomHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<KingdomHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public KingdomHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        messageBroker.Subscribe<AddDecision>(HandleAddDecision);
        messageBroker.Subscribe<RemoveDecision>(HandleRemoveDecision);
        messageBroker.Subscribe<ChangeKingdomPolicy>(HandleChangeKingdomPolicy);
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

    public void Dispose()
    {
        messageBroker.Unsubscribe<AddDecision>(HandleAddDecision);
        messageBroker.Unsubscribe<RemoveDecision>(HandleRemoveDecision);
        messageBroker.Unsubscribe<ChangeKingdomPolicy>(HandleChangeKingdomPolicy);
    }
}
