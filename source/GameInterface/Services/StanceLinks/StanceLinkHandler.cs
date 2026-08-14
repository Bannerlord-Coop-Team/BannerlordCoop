using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.StanceLinks.Messages;
using Serilog;
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
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RequestStanceLinkConstructed>(Handle_RequestStanceLinkConstructed);
        messageBroker.Unsubscribe<StanceLinkConstructed>(HandleStanceLinkConstructed);
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
                using (new AllowedThread())
                {
                    stanceLink = new StanceLink(obj.StanceType, faction1, faction2);

                    FactionManager.Instance.AddStance(faction1, faction2, stanceLink);
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


}