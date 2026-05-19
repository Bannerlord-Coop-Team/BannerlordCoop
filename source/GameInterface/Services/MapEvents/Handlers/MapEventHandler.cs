using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Handlers;

internal class MapEventHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public MapEventHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<MapEventSidesArrayUpdated>(Handle);
        messageBroker.Subscribe<NetworkUpdateMapSidesArray>(Handle);

        messageBroker.Subscribe<MapEventInitialize>(Handle);
        messageBroker.Subscribe<NetworkMapEventInitialize>(Handle);

        messageBroker.Subscribe<LeaveBattleAttempted>(Handle);
        messageBroker.Subscribe<NetworkLeaveBattle>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MapEventSidesArrayUpdated>(Handle);
        messageBroker.Unsubscribe<NetworkUpdateMapSidesArray>(Handle);

        messageBroker.Unsubscribe<MapEventInitialize>(Handle);
        messageBroker.Unsubscribe<NetworkMapEventInitialize>(Handle);

        messageBroker.Unsubscribe<LeaveBattleAttempted>(Handle);
        messageBroker.Unsubscribe<NetworkLeaveBattle>(Handle);
    }

    private void Handle(MessagePayload<NetworkMapEventInitialize> payload)
    {
        if (!objectManager.TryGetObjectWithLogging<MapEvent>(payload.What.MapEventId, out var mapEvent)) return;
        if (!objectManager.TryGetObjectWithLogging<PartyBase>(payload.What.AttackerPartyId, out var attackerParty)) return;
        if (!objectManager.TryGetObjectWithLogging<PartyBase>(payload.What.DefenderPartyId, out var defenderParty)) return;

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                //MapEventComponent component = null;

                //switch ((MapEvent.BattleTypes)payload.What.BattleType)
                //{
                //    case MapEvent.BattleTypes.FieldBattle:
                //        component = new FieldBattleEventComponent(mapEvent);
                //        break;
                //    case MapEvent.BattleTypes.Raid:
                //        component = new RaidEventComponent(mapEvent);
                //        break;
                //    case MapEvent.BattleTypes.Siege:
                //        break;
                //    case MapEvent.BattleTypes.Hideout:
                //        component = new HideoutEventComponent(mapEvent, false);
                //        break;
                //    case MapEvent.BattleTypes.SallyOut:
                //        break;
                //    case MapEvent.BattleTypes.SiegeOutside:
                //        break;
                //    case MapEvent.BattleTypes.BlockadeSallyOutBattle:
                //    case MapEvent.BattleTypes.BlockadeBattle:
                //        component = new BlockadeBattleMapEvent(mapEvent);
                //        break;
                //}

                mapEvent.MapEventVisual.Initialize(mapEvent.Position, mapEvent.GetBattleSizeValue(), mapEvent.IsVisible);
                mapEvent.Component.InitializeComponent();

                //mapEvent.Initialize(attackerParty, defenderParty, component, (MapEvent.BattleTypes)payload.What.BattleType);
            }
        });
    }

    private void Handle(MessagePayload<MapEventInitialize> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.MapEvent, out var mapEventId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.AttackerParty, out var attackerPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.DefenderParty, out var defenderPartyId)) return;

        network.SendAll(new NetworkMapEventInitialize(mapEventId, (int)obj.BattleType, attackerPartyId, defenderPartyId));
    }

    private void Handle(MessagePayload<LeaveBattleAttempted> payload)
    {
        var what = payload.What;
        if (!objectManager.TryGetIdWithLogging(what.MobileParty, out var mobilePartyId)) return;
        if (!objectManager.TryGetIdWithLogging(what.MapEvent, out var mapEventId)) return;

        network.SendAll(new NetworkLeaveBattle(mobilePartyId, mapEventId));
    }

    private void Handle(MessagePayload<NetworkLeaveBattle> payload)
    {
        var what = payload.What;
        if (!objectManager.TryGetObjectWithLogging<MapEvent>(what.MapEventId, out var mapEvent)) return;

        using (new AllowedThread())
        {
            mapEvent.FinalizeEvent();
        }
    }

    private void Handle(MessagePayload<MapEventSidesArrayUpdated> payload)
    {
        var mapEvent = payload.What.Instance;
        if (!objectManager.TryGetIdWithLogging(mapEvent, out var instanceId)) return;

        var value = payload.What.Value;
        if (!objectManager.TryGetIdWithLogging(value, out var valueId)) return;


        network.SendAll(new NetworkUpdateMapSidesArray(instanceId, valueId, payload.What.Index));
    }

    private void Handle(MessagePayload<NetworkUpdateMapSidesArray> payload)
    {
        var instanceId = payload.What.InstanceId;
        var valueId = payload.What.ValueId;
        var index = payload.What.Index;

        if (!objectManager.TryGetObjectWithLogging<MapEvent>(instanceId, out var mapEvent)) return;

        if (!objectManager.TryGetObjectWithLogging<MapEventSide>(valueId, out var mapEventSide)) return;

        mapEvent._sides[index] = mapEventSide;
    }
}
