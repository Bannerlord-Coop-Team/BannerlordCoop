using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyComponents.Data;
using GameInterface.Services.PartyComponents.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.PartyComponents.Handlers;

internal class PartyComponentHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyComponentHandler>();

    private static readonly FieldInfo HomeSettlementField =
        AccessTools.Field(typeof(PatrolPartyComponent), "_homeSettlement");
    private static readonly MethodInfo PatrolInitializeProperties =
        AccessTools.Method(typeof(PatrolPartyComponent), "InitializePartyComponentProperties");

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    private readonly Type[] partyTypes = new Type[]
    {
        typeof(BanditPartyComponent),    // 0
        typeof(CaravanPartyComponent),   // 1
        typeof(CustomPartyComponent),    // 2
        typeof(GarrisonPartyComponent),  // 3
        typeof(LordPartyComponent),      // 4
        typeof(MilitiaPartyComponent),   // 5
        typeof(PatrolPartyComponent),    // 6
        typeof(VillagerPartyComponent),  // 7
    };

    public PartyComponentHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<PartyComponentCreated>(Handle);
        messageBroker.Subscribe<NetworkCreatePartyComponent>(Handle);

        messageBroker.Subscribe<PartyComponentMobilePartyUpdated>(Handle_PartyComponentMobilePartyUpdated);
        messageBroker.Subscribe<NetworkPartyComponentMobilePartyUpdated>(Handle_NetworkPartyComponentMobilePartyUpdated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyComponentCreated>(Handle);
        messageBroker.Unsubscribe<NetworkCreatePartyComponent>(Handle);
    }

    private void Handle(MessagePayload<PartyComponentCreated> payload)
    {
        objectManager.AddNewObject(payload.What.Instance, out var id);

        var typeIndex = partyTypes.IndexOf(payload.What.Instance.GetType());
        var data = new PartyComponentData(typeIndex, id)
        {
            HomeSettlementId = payload.What.SettlementId,
            IsNaval = payload.What.IsNaval,
        };

        network.SendAll(new NetworkCreatePartyComponent(data));
    }

    private void Handle(MessagePayload<NetworkCreatePartyComponent> payload)
    {
        var data = payload.What.Data;
        var obj = (PartyComponent)ObjectHelper.SkipConstructor(partyTypes[data.TypeIndex]);

        switch (data.TypeIndex)
        {
            case 0:
                objectManager.AddExisting(data.Id, obj);
                break;
            case 1:
                objectManager.AddExisting(data.Id, obj);
                break;
            case 2:
                objectManager.AddExisting(data.Id, obj);
                break;
            case 3:
                objectManager.AddExisting(data.Id, obj);
                break;
            case 4:
                objectManager.AddExisting(data.Id, obj);
                break;
            case 5:
                objectManager.AddExisting(data.Id, obj);
                break;
            case 6:
                objectManager.AddExisting(data.Id, obj);

                var patrolComponent = (PatrolPartyComponent)obj;

                if (data.HomeSettlementId is null) break;

                // Reconstitute fields that are normally set in the constructor and
                // InitializePartyComponentProperties. These are bundled in the creation
                // message to avoid any dependency on DynamicSync field-update ordering.
                if (!objectManager.TryGetObject(data.HomeSettlementId, out Settlement homeSettlement))
                {
                    Logger.Warning(
                        "PatrolPartyComponent {Id}: could not find Settlement '{SettlementId}' in ObjectManager; " +
                        "Settlement.PatrolParty will not be set on client",
                        data.Id, data.HomeSettlementId);
                    break;
                }

                HomeSettlementField.SetValue(patrolComponent, homeSettlement);
                // Reflect IsNaval through its private setter so that IsNaval is correct
                // before InitializePartyComponentProperties checks it.
                AccessTools.Property(typeof(PatrolPartyComponent), nameof(PatrolPartyComponent.IsNaval))
                    .SetValue(patrolComponent, data.IsNaval);
                PatrolInitializeProperties.Invoke(patrolComponent, null);

                break;
            case 7:
                objectManager.AddExisting(data.Id, obj);
                break;
            default:
                Logger.Error(
                    "Invalid TypeIndex {TypeIndex}. Valid range: [0..{MaxIndex}] (0=Bandit,1=Caravan,2=Custom,3=Garrison,4=Lord,5=Militia,6=Patrol,7=Villager). PacketId: {Id}",
                    data.TypeIndex,
                    partyTypes.Length - 1,
                    data.Id);
                break;
        }
    }

    private void Handle_PartyComponentMobilePartyUpdated(MessagePayload<PartyComponentMobilePartyUpdated> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Instance, out var partyComponentId))
            return;
        if (!objectManager.TryGetIdWithLogging(obj.MobileParty, out var partyId))
            return;

        var message = new NetworkPartyComponentMobilePartyUpdated(partyComponentId, partyId);
        network.SendAll(message);
    }

    private void Handle_NetworkPartyComponentMobilePartyUpdated(MessagePayload<NetworkPartyComponentMobilePartyUpdated> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetObjectWithLogging<PartyComponent>(obj.InstanceId, out var partyComponent))
            return;

        if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.MobilePartyId, out var mobileParty))
            return;

        using (new AllowedThread())
        {
            partyComponent.MobileParty = mobileParty;
        }
    }
}