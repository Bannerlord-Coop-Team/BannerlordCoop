using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyComponents.BanditPartyComponents.Messages;
using GameInterface.Services.PartyComponents.BanditPartyComponents.Patches;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyComponents.BanditPartyComponents.Handlers;

internal class BanditPartyComponentHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    private static readonly ILogger Logger = LogManager.GetLogger<BanditPartyComponentHandler>();

    public BanditPartyComponentHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<BanditPartyComponentUpdated>(Handle);
        messageBroker.Subscribe<NetworkUpdateBanditPartyComponent>(Handle);

        messageBroker.Subscribe<BanditPartyComponentInitArgsUpdated>(Handle_BanditPartyComponentInitArgsUpdated);
        messageBroker.Subscribe<NetworkUpdateBanditPartyComponentInitArgs>(Handle_NetworkUpdateBanditPartyComponentInitArgs);
    }



    public void Dispose()
    {
        messageBroker.Unsubscribe<BanditPartyComponentUpdated>(Handle);
        messageBroker.Unsubscribe<NetworkUpdateBanditPartyComponent>(Handle);
    }

    private void Handle(MessagePayload<BanditPartyComponentUpdated> payload)
    {
        var obj = payload.What;

        if(objectManager.TryGetId(obj.Component, out string componentId) == false)
        {
            Logger.Error("Could not find {component} in registry", obj.Component);
            return;
        }

        NetworkUpdateBanditPartyComponent message = new(componentId, (int)obj.ComponentType, obj.NewValue);

        network.SendAll(message);
    }

    private void Handle(MessagePayload<NetworkUpdateBanditPartyComponent> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetObject<BanditPartyComponent>(obj.ComponentId, out var component) == false)
        {
            Logger.Error("Could not find {component} in registry", obj.ComponentId);
            return;
        }

        using (new AllowedThread())
        {
            switch ((BanditPartyComponentType)obj.BanditPartyComponentType)
            {
                case BanditPartyComponentType.Hideout:
                    objectManager.TryGetObject(obj.Value, out Hideout hideout);
                    component.Hideout = hideout;
                    break;

                case BanditPartyComponentType.IsBossParty:
                    component.IsBossParty = bool.Parse(obj.Value);
                    break;
            }
        }
    }

    private void Handle_BanditPartyComponentInitArgsUpdated(MessagePayload<BanditPartyComponentInitArgsUpdated> payload)
    {
        var instance = payload.What.Instance;
        var initArgs = payload.What.InitArgs;

        if (!objectManager.TryGetIdWithLogging(instance, out var banditPartyComponentId)) return;
        if (!objectManager.TryGetIdWithLogging(initArgs.Clan, out var clanId)) return;
        if (!objectManager.TryGetIdWithLogging(initArgs.PartyTemplate, out var partyTemplateId)) return;

        network.SendAll(new NetworkUpdateBanditPartyComponentInitArgs(
            banditPartyComponentId,
            clanId,
            initArgs.InitialPosition,
            partyTemplateId
        ));
    }

    private void Handle_NetworkUpdateBanditPartyComponentInitArgs(MessagePayload<NetworkUpdateBanditPartyComponentInitArgs> payload)
    {
        var message = payload.What;

        if (!objectManager.TryGetObjectWithLogging<BanditPartyComponent>(message.BanditPartyComponentId, out var instance)) return;
        if (!objectManager.TryGetObjectWithLogging<Clan>(message.ClanId, out var clan)) return;
        if (!objectManager.TryGetObjectWithLogging<PartyTemplateObject>(message.PartyTemplateId, out var partyTemplate)) return;

        using (new AllowedThread())
        {
            var initArgs = new BanditPartyComponent.InitializationArgs(clan, partyTemplate, message.InitialPosition);

            instance._initializationArgs = initArgs;
        }
    }
}
