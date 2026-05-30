using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyComponents.BanditPartyComponents.Messages;
using GameInterface.Services.PartyComponents.BanditPartyComponents.Patches;
using HarmonyLib;
using Serilog;
using System.Reflection;
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

        if (objectManager.TryGetObject<PartyComponent>(obj.ComponentId, out var component) == false)
        {
            Logger.Error("Could not find {component} in registry", obj.ComponentId);
            return;
        }

        var banditComponent = component as BanditPartyComponent;
        if (banditComponent == null)
        {
            Logger.Error("Component is not a BanditPartyComponent");
            return;
        }

        using (new AllowedThread())
        {
            switch ((BanditPartyComponentType)obj.BanditPartyComponentType)
            {

                case BanditPartyComponentType.Hideout:
                    objectManager.TryGetObject(obj.Value, out Hideout hideout);
                    banditComponent.Hideout = hideout;
                    Logger.Information("Processing BanditPartyComponent update type={type} value={value}", obj.BanditPartyComponentType, obj.Value);
                    break;

                case BanditPartyComponentType.IsBossParty:
                    banditComponent.IsBossParty = bool.Parse(obj.Value);
                    Logger.Information("Processing BanditPartyComponent update type={type} value={value}", obj.BanditPartyComponentType, obj.Value);
                    break;
            }
            Logger.Information("Unreachable");
        }
    }

    private void Handle_BanditPartyComponentInitArgsUpdated(MessagePayload<BanditPartyComponentInitArgsUpdated> payload)
    {
        Logger.Information("Handle_BanditPartyComponentInitArgsUpdated called");
        var instance = payload.What.Instance;
        var initArgs = payload.What.InitArgs;

        if (!objectManager.TryGetIdWithLogging(instance, out var banditPartyComponentId))
        {
            Logger.Error("Failed to get id for BanditPartyComponent");
            return;
        }
        if (!objectManager.TryGetIdWithLogging(initArgs.Clan, out var clanId))
        {
            Logger.Error("Failed to get id for Clan");
            return;
        }
        if (!objectManager.TryGetIdWithLogging(initArgs.PartyTemplate, out var partyTemplateId))
        {
            Logger.Error("Failed to get id for PartyTemplate");
            return;
        }
        if (!objectManager.TryGetIdWithLogging(instance.MobileParty, out var partyId))
        {
            Logger.Error("Failed to get id for MobileParty");
            return;
        }

        network.SendAll(new NetworkUpdateBanditPartyComponentInitArgs(
            banditPartyComponentId,
            clanId,
            initArgs.InitialPosition,
            partyTemplateId,
            partyId
        ));
    }

    private void Handle_NetworkUpdateBanditPartyComponentInitArgs(MessagePayload<NetworkUpdateBanditPartyComponentInitArgs> payload)
    {
        var message = payload.What;
        if (!objectManager.TryGetObjectWithLogging<BanditPartyComponent>(message.BanditPartyComponentId, out var instance))
        {
            Logger.Error("Failed to find BanditPartyComponent {id}", message.BanditPartyComponentId);
            return;
        }
        if (!objectManager.TryGetObjectWithLogging<Clan>(message.ClanId, out var clan))
        {
            Logger.Error("Failed to find Clan {id}", message.ClanId);
            return;
        }
        if (!objectManager.TryGetObjectWithLogging<PartyTemplateObject>(message.PartyTemplateId, out var partyTemplate))
        {
            Logger.Error("Failed to find PartyTemplate {id}", message.PartyTemplateId);
            return;
        }
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(message.MobilePartyId, out var party))
        {
            Logger.Error("Failed to find MobileParty {id}", message.MobilePartyId);
            return;
        }

        using (new AllowedThread())
        {
            var initArgs = new BanditPartyComponent.InitializationArgs(clan, partyTemplate, message.InitialPosition);

            instance._initializationArgs = initArgs;

            AccessTools.Field(typeof(MobileParty), "_partyComponent").SetValue(party, instance);
            AccessTools.Field(typeof(PartyComponent), "<MobileParty>k__BackingField").SetValue(instance, party);

            typeof(BanditPartyComponent).GetMethod("OnMobilePartySetOnCreation", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(instance, null);
        }
        Logger.Information("NetworkUpdateBanditPartyComponent MobileParty={p} MapFaction={f}",
    instance.MobileParty?.StringId ?? "NULL",
    instance.MobileParty?.MapFaction?.Name?.ToString() ?? "NULL");
    }
}
