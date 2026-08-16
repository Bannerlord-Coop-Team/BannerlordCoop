using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.StanceLinks.Messages;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.StanceLinks;

internal class StanceLinkHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private static readonly ILogger Logger = LogManager.GetLogger<StanceLinkHandler>();

    public StanceLinkHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<RequestStanceLinkConstructed>(Handle_RequestStanceLinkConstructed);
        messageBroker.Subscribe<StanceLinkConstructed>(HandleStanceLinkConstructed);
        messageBroker.Subscribe<StanceLinkDeconstructed>(Handle_StanceLinkDeconstructed);
        messageBroker.Subscribe<NetworkStanceLinkDeconstructed>(Handle_NetworkStanceLinkDeconstructed);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RequestStanceLinkConstructed>(Handle_RequestStanceLinkConstructed);
        messageBroker.Unsubscribe<StanceLinkConstructed>(HandleStanceLinkConstructed);
        messageBroker.Unsubscribe<StanceLinkDeconstructed>(Handle_StanceLinkDeconstructed);
        messageBroker.Unsubscribe<NetworkStanceLinkDeconstructed>(Handle_NetworkStanceLinkDeconstructed);
    }

    private void Handle_RequestStanceLinkConstructed(MessagePayload<RequestStanceLinkConstructed> payload)
    {
        var obj = payload.What;

        var stanceLink = obj.StanceLink;

        if (!objectManager.TryGetIdWithLogging(stanceLink.Faction1, out var faction1Id))
            return;

        if (!objectManager.TryGetIdWithLogging(stanceLink.Faction2, out var faction2Id))
            return;

        if (ModInformation.IsClient)
        {
            network.SendAll(new StanceLinkConstructed(faction1Id, faction2Id, stanceLink.StanceType));
        }
        else
        {
            messageBroker.Publish(stanceLink, new StanceLinkConstructed(faction1Id, faction2Id, stanceLink.StanceType));
        }
    }

    private void HandleStanceLinkConstructed(MessagePayload<StanceLinkConstructed> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<IFaction>(obj.Faction1Id, out var faction1)) return;

            if (!objectManager.TryGetObjectWithLogging<IFaction>(obj.Faction2Id, out var faction2)) return;

            var stanceLink = FactionManager.Instance._stances.GetStance(faction1, faction2);

            if (stanceLink == null)
            {
                if (ModInformation.IsServer)
                {
                    stanceLink = new StanceLink(obj.StanceType, faction1, faction2);

                    FactionManager.Instance.AddStance(faction1, faction2, stanceLink);
                }
                else
                {
                    using (new AllowedThread())
                    {
                        stanceLink = new StanceLink(obj.StanceType, faction1, faction2);

                        FactionManager.Instance.AddStance(faction1, faction2, stanceLink);
                    }
                }
            }

            var id = GetStanceLinkKey(faction1, faction2);

            if (!objectManager.AddExisting($"{typeof(StanceLink).Name}_{id}", stanceLink))
            {
                Logger.Error("Unable to register StanceLink with id {Id}", id);
                return;
            }
            if (ModInformation.IsServer)
            {
                network.SendAll(new StanceLinkConstructed(obj.Faction1Id, obj.Faction2Id, obj.StanceType));
            }
        });
    }

    internal static string GetStanceLinkKey(IFaction faction1, IFaction faction2)
    {
        return faction1.Id > faction2.Id
            ? $"{faction1.StringId}_{faction2.StringId}"
            : $"{faction2.StringId}_{faction1.StringId}";
    }

    public void Handle_StanceLinkDeconstructed(MessagePayload<StanceLinkDeconstructed> payload)
    {
        if (ModInformation.IsClient) return;
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Faction1, out var faction1Id)) return;

        var removedStanceLinkIds = new List<string>();
        foreach (var stanceLink in obj.RemovedStanceLink)
        {
            if (!objectManager.TryGetIdWithLogging(stanceLink, out var stanceLinkId)) continue;
            removedStanceLinkIds.Add(stanceLinkId);
            objectManager.Remove(stanceLink);
        }

        network.SendAll(new NetworkStanceLinkDeconstructed(faction1Id, removedStanceLinkIds.ToArray()));
    }

    public void Handle_NetworkStanceLinkDeconstructed(MessagePayload<NetworkStanceLinkDeconstructed> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<IFaction>(obj.Faction1Id, out var faction1)) return;

            foreach (string stanceId in obj.RemovedStanceLinkIds)
            {
                if (!objectManager.TryGetObjectWithLogging<StanceLink>(stanceId, out var stance)) continue;
                FactionManager.Instance.RemoveStance(stance);
                objectManager.Remove(stance);
            }

            foreach (IFaction faction2 in faction1.FactionsAtWarWith)
            {
                faction2.UpdateFactionsAtWarWith();
            }
            faction1.UpdateFactionsAtWarWith();
        });
    }
}