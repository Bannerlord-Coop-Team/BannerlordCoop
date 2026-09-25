using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.StanceLinks.Messages;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.StanceLinks;

internal class StanceLinkHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public StanceLinkHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<RequestStanceLinkConstructed>(Handle_RequestStanceLinkConstructed);
        messageBroker.Subscribe<StanceLinkConstructed>(HandleStanceLinkConstructed);
        messageBroker.Subscribe<StanceLinkDeconstructed>(Handle_StanceLinkDeconstructed);
        messageBroker.Subscribe<NetworkStanceLinkDeconstructed>(Handle_NetworkStanceLinkDeconstructed);
        messageBroker.Subscribe<StanceLinkTroopCasualties>(Handle_StanceLinkTroopCasualties);
        messageBroker.Subscribe<NetworkStanceLinkTroopCasualties1>(Handle_NetworkStanceLinkTroopCasualties1);
        messageBroker.Subscribe<NetworkStanceLinkTroopCasualties2>(Handle_NetworkStanceLinkTroopCasualties2);
        messageBroker.Subscribe<StanceLinkSuccessfulSieges>(HandleStanceLinkSuccesfulSieges);
        messageBroker.Subscribe<NetworkStanceLinkSuccessfulSieges1>(Handle_NetworkStanceLinkSuccessfulSieges1);
        messageBroker.Subscribe<NetworkStanceLinkSuccessfulSieges2>(Handle_NetworkStanceLinkSuccessfulSieges2);
        messageBroker.Subscribe<StanceLinkSuccessfulRaids>(HandleStanceLinkSuccessfulRaids);
        messageBroker.Subscribe<NetworkStanceLinkSuccessfulRaids1>(Handle_NetworkStanceLinkSuccessfulRaids1);
        messageBroker.Subscribe<NetworkStanceLinkSuccessfulRaids2>(Handle_NetworkStanceLinkSuccessfulRaids2);
        messageBroker.Subscribe<StanceLinkSuccessfulTownSieges>(HandleStanceLinkSuccesfulTownSieges);
        messageBroker.Subscribe<NetworkStanceLinkSuccessfulTownSieges1>(Handle_NetworkStanceLinkSuccessfulTownSieges1);
        messageBroker.Subscribe<NetworkStanceLinkSuccessfulTownSieges2>(Handle_NetworkStanceLinkSuccessfulTownSieges2);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RequestStanceLinkConstructed>(Handle_RequestStanceLinkConstructed);
        messageBroker.Unsubscribe<StanceLinkConstructed>(HandleStanceLinkConstructed);
        messageBroker.Unsubscribe<StanceLinkDeconstructed>(Handle_StanceLinkDeconstructed);
        messageBroker.Unsubscribe<NetworkStanceLinkDeconstructed>(Handle_NetworkStanceLinkDeconstructed);
        messageBroker.Unsubscribe<StanceLinkTroopCasualties>(Handle_StanceLinkTroopCasualties);
        messageBroker.Unsubscribe<NetworkStanceLinkTroopCasualties1>(Handle_NetworkStanceLinkTroopCasualties1);
        messageBroker.Unsubscribe<NetworkStanceLinkTroopCasualties2>(Handle_NetworkStanceLinkTroopCasualties2);
        messageBroker.Unsubscribe<StanceLinkSuccessfulSieges>(HandleStanceLinkSuccesfulSieges);
        messageBroker.Unsubscribe<NetworkStanceLinkSuccessfulSieges1>(Handle_NetworkStanceLinkSuccessfulSieges1);
        messageBroker.Unsubscribe<NetworkStanceLinkSuccessfulSieges2>(Handle_NetworkStanceLinkSuccessfulSieges2);
        messageBroker.Unsubscribe<StanceLinkSuccessfulRaids>(HandleStanceLinkSuccessfulRaids);
        messageBroker.Unsubscribe<NetworkStanceLinkSuccessfulRaids1>(Handle_NetworkStanceLinkSuccessfulRaids1);
        messageBroker.Unsubscribe<NetworkStanceLinkSuccessfulRaids2>(Handle_NetworkStanceLinkSuccessfulRaids2);
        messageBroker.Unsubscribe<StanceLinkSuccessfulTownSieges>(HandleStanceLinkSuccesfulTownSieges);
        messageBroker.Unsubscribe<NetworkStanceLinkSuccessfulTownSieges1>(Handle_NetworkStanceLinkSuccessfulTownSieges1);
        messageBroker.Unsubscribe<NetworkStanceLinkSuccessfulTownSieges2>(Handle_NetworkStanceLinkSuccessfulTownSieges2);
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
                if (faction1.IsEliminated || faction2.IsEliminated) return;
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

            bool registered = ModInformation.IsServer
                ? objectManager.AddExisting($"{typeof(StanceLink).Name}_{id}", stanceLink)
                : objectManager.AddExisting($"{typeof(StanceLink).Name}_{id}", stanceLink, obj.StanceLinkHandle);
            if (!registered)
            {
                return;
            }
            if (ModInformation.IsServer)
            {
                if (!objectManager.TryGetHandleWithLogging(stanceLink, out var stanceLinkHandle)) return;
                network.SendAll(new StanceLinkConstructed(
                    obj.Faction1Id,
                    obj.Faction2Id,
                    obj.StanceType,
                    stanceLinkHandle));
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
    
    public void Handle_StanceLinkTroopCasualties(MessagePayload<StanceLinkTroopCasualties> payload)
    {
        var obj = payload.What;
        if (!objectManager.TryGetIdWithLogging(obj.StanceLink, out var stanceLinkId)) return;
        if (obj.Side==1)
        {
            network.SendAll(new NetworkStanceLinkTroopCasualties1(stanceLinkId, obj.Value));
        }
        else
        {
            network.SendAll(new NetworkStanceLinkTroopCasualties2(stanceLinkId, obj.Value));
        }
    }

    public void Handle_NetworkStanceLinkTroopCasualties1(MessagePayload<NetworkStanceLinkTroopCasualties1> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<StanceLink>(obj.StanceLinkId, out var stanceLink)) return;
            using (new AllowedThread())
            {
                stanceLink.TroopCasualties1 = obj.Value;
            }
        });
    }
    public void Handle_NetworkStanceLinkTroopCasualties2(MessagePayload<NetworkStanceLinkTroopCasualties2> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<StanceLink>(obj.StanceLinkId, out var stanceLink)) return;
            using (new AllowedThread())
            {
                stanceLink.TroopCasualties2 = obj.Value;
            }
        });
    }

    public void HandleStanceLinkSuccesfulSieges(MessagePayload<StanceLinkSuccessfulSieges> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.StanceLink, out var stanceLinkId)) return;
        if (obj.Side == 1)
        {
            network.SendAll(new NetworkStanceLinkSuccessfulSieges1(stanceLinkId, obj.Value));
        }
        else
        {
            network.SendAll(new NetworkStanceLinkSuccessfulSieges2(stanceLinkId, obj.Value));
        }
    }

    public void Handle_NetworkStanceLinkSuccessfulSieges1(MessagePayload<NetworkStanceLinkSuccessfulSieges1> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<StanceLink>(obj.StanceLinkId, out var stanceLink)) return;
            using (new AllowedThread())
            {
                stanceLink.SuccessfulSieges1 = obj.Value;
            }
        });
    }

    public void Handle_NetworkStanceLinkSuccessfulSieges2(MessagePayload<NetworkStanceLinkSuccessfulSieges2> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<StanceLink>(obj.StanceLinkId, out var stanceLink)) return;
            using (new AllowedThread())
            {
                stanceLink.SuccessfulSieges2 = obj.Value;
            }
        });
    }

    public void HandleStanceLinkSuccessfulRaids(MessagePayload<StanceLinkSuccessfulRaids> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.StanceLink, out var stanceLinkId)) return;
        if (obj.Side == 1)
        {
            network.SendAll(new NetworkStanceLinkSuccessfulRaids1(stanceLinkId, obj.Value));
        }
        else
        {
            network.SendAll(new NetworkStanceLinkSuccessfulRaids2(stanceLinkId, obj.Value));
        }
    }

    public void Handle_NetworkStanceLinkSuccessfulRaids1(MessagePayload<NetworkStanceLinkSuccessfulRaids1> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<StanceLink>(obj.StanceLinkId, out var stanceLink)) return;
            using (new AllowedThread())
            {
                stanceLink.SuccessfulRaids1 = obj.Value;
            }
        });
    }

    public void Handle_NetworkStanceLinkSuccessfulRaids2(MessagePayload<NetworkStanceLinkSuccessfulRaids2> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<StanceLink>(obj.StanceLinkId, out var stanceLink)) return;
            using (new AllowedThread())
            {
                stanceLink.SuccessfulRaids2 = obj.Value;
            }
        });
    }

    public void HandleStanceLinkSuccesfulTownSieges(MessagePayload<StanceLinkSuccessfulTownSieges> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.StanceLink, out var stanceLinkId)) return;
        if (obj.Side == 1)
        {
            network.SendAll(new NetworkStanceLinkSuccessfulTownSieges1(stanceLinkId, obj.Value));
        }
        else
        {
            network.SendAll(new NetworkStanceLinkSuccessfulTownSieges2(stanceLinkId, obj.Value));
        }
    }

    public void Handle_NetworkStanceLinkSuccessfulTownSieges1(MessagePayload<NetworkStanceLinkSuccessfulTownSieges1> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<StanceLink>(obj.StanceLinkId, out var stanceLink)) return;
            using (new AllowedThread())
            {
                stanceLink.SuccessfulTownSieges1 = obj.Value;
            }
        });
    }

    public void Handle_NetworkStanceLinkSuccessfulTownSieges2(MessagePayload<NetworkStanceLinkSuccessfulTownSieges2> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<StanceLink>(obj.StanceLinkId, out var stanceLink)) return;
            using (new AllowedThread())
            {
                stanceLink.SuccessfulTownSieges2 = obj.Value;
            }
        });
    }
}
