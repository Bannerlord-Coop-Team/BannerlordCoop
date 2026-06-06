using Common.Logging;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Villages.Messages;
using GameInterface.Services.Villages.Patches;
using Serilog;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Villages.Handlers;

/// <summary>
/// Handles VillageState Changes (e.g. Raided, Pillaged, Normal).
/// </summary>
public class VillageHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<VillageHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    public VillageHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker; 
        this.objectManager = objectManager;
 
        messageBroker.Subscribe<ChangeVillageState>(HandleVillageState);
        messageBroker.Subscribe<ChangeVillageTradeBound>(HandleTradeBound);
        messageBroker.Subscribe<ChangeVillageHearth>(HandleHearth);
        messageBroker.Subscribe<ChangeVillageTradeTaxAccumulated>(HandleTradeTax);
        messageBroker.Subscribe<ChangeVillageLastDemandTime>(HandleLastDemandTime);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ChangeVillageState>(HandleVillageState);
        messageBroker.Unsubscribe<ChangeVillageTradeBound>(HandleTradeBound);
        messageBroker.Unsubscribe<ChangeVillageHearth>(HandleHearth);
        messageBroker.Unsubscribe<ChangeVillageTradeTaxAccumulated>(HandleTradeTax);
        messageBroker.Unsubscribe<ChangeVillageLastDemandTime>(HandleLastDemandTime);
    }

    private void HandleLastDemandTime(MessagePayload<ChangeVillageLastDemandTime> payload)
    {
        var obj = payload.What;


        if (objectManager.TryGetObject<Village>(obj.VillageId, out var village) == false)
        {
            Logger.Error("Unable to find Village ({villageId})", obj.VillageId);
            return;
        }

        VillagePatches.RunLastDemandTimeSatisified(village, obj.LastDemandSatifiedTime);
    }

    private void HandleTradeTax(MessagePayload<ChangeVillageTradeTaxAccumulated> payload)
    {
        var obj = payload.What;


        if (objectManager.TryGetObject<Village>(obj.VillageId, out var village) == false)
        {
            Logger.Error("Unable to find Village ({villageId})", obj.VillageId);
            return;
        }

        VillagePatches.RunTradeTaxChange(village, obj.TradeTaxAccumulated);

    }

    private void HandleHearth(MessagePayload<ChangeVillageHearth> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetObject<Village>(obj.VillageId, out var village) == false)
        {
            Logger.Error("Unable to find Village ({villageId})", obj.VillageId);
            return;
        }

        VillagePatches.ChangeHearth(village, obj.Hearth);
    }

    private void HandleTradeBound(MessagePayload<ChangeVillageTradeBound> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetObject<Village>(obj.VillageId, out var village) == false)
        {
            Logger.Error("Unable to find Village ({villageId})", obj.VillageId);
            return;
        }

        if (objectManager.TryGetObject<Settlement>(obj.TradeBoundID, out var settlement) == false)
        {
            Logger.Error("Unable to find Village ({villageId})", obj.VillageId);
            return;
        }


        VillagePatches.RunTradeBoundChange(village, settlement);
    }

    private void HandleVillageState(MessagePayload<ChangeVillageState> payload)
    {
        var obj = payload.What;

        if(objectManager.TryGetObject<Village>(obj.VillageId, out var village) == false)
        {
            Logger.Error("Unable to find Village ({villageId})", obj.VillageId);
            return;
        }

        VillagePatches.RunVillageStateChange(village, (Village.VillageStates)obj.State);
    }
}