using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyComponents.Data;
using GameInterface.Services.PartyComponents.Messages;
using GameInterface.Services.PartyComponents.Patches;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.PartyComponents.Handlers;
internal class PartyComponentHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyComponentHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    private readonly Type[] partyTypes = new Type[]
    {
        typeof(BanditPartyComponent),
        typeof(CaravanPartyComponent),
        typeof(CustomPartyComponent),
        typeof(GarrisonPartyComponent),
        typeof(LordPartyComponent),
        typeof(MilitiaPartyComponent),
        typeof(VillagerPartyComponent),
    };

    public PartyComponentHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<PartyComponentCreated>(Handle);
        messageBroker.Subscribe<NetworkCreatePartyComponent>(Handle);
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
        var data = new PartyComponentData(typeIndex, id);
        
        network.SendAll(new NetworkCreatePartyComponent(data));
    }

    private void Handle(MessagePayload<NetworkCreatePartyComponent> payload)
    {
        var data = payload.What.Data;
        var obj = ObjectHelper.SkipConstructor(partyTypes[data.TypeIndex]);

        switch (data.TypeIndex)
        {
            case 0:
                objectManager.AddExisting(data.Id, (BanditPartyComponent)obj);
                break;
            case 1:
                objectManager.AddExisting(data.Id, (CaravanPartyComponent)obj);
                break;
            case 2:
                objectManager.AddExisting(data.Id, (CustomPartyComponent)obj);
                break;
            case 3:
                objectManager.AddExisting(data.Id, (GarrisonPartyComponent)obj);
                break;
            case 4:
                objectManager.AddExisting(data.Id, (LordPartyComponent)obj);
                break;
            case 5:
                objectManager.AddExisting(data.Id, (MilitiaPartyComponent)obj);
                break;
            case 6:
                objectManager.AddExisting(data.Id, (VillagerPartyComponent)obj);
                break;
            default:
                Logger.Error(
                    "Invalid TypeIndex {TypeIndex}. Valid range: [0..{MaxIndex}]. PacketId: {Id}",
                    data.TypeIndex,
                    partyTypes.Length - 1,
                    data.Id);
                break;
        }
    }
}