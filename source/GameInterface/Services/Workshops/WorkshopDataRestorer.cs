using Common.Logging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Workshops.Interfaces;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Workshops;

public interface IWorkshopDataRestorer
{
    void RestoreClientWorkshopData(
        WorkshopsCampaignBehavior workshopsCampaignBehavior,
        Hero playerHero,
        IEnumerable<Workshop> workshops,
        WorkshopPlayerData workshopPlayerData);

    void RestoreServerWorkshopData(
        WorkshopsCampaignBehavior workshopsCampaignBehavior,
        IEnumerable<Workshop> workshops);

    WorkshopsCampaignBehavior.WorkshopData EnsureWorkshopData(
        WorkshopsCampaignBehavior workshopsCampaignBehavior,
        Workshop workshop,
        WorkshopDataSnapshot savedData = null);
}

public class WorkshopDataRestorer : IWorkshopDataRestorer
{
    private static readonly ILogger Logger = LogManager.GetLogger<WorkshopDataRestorer>();

    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly ISessionWorkshopPlayerDataInterface sessionWorkshopPlayerDataInterface;

    public WorkshopDataRestorer(
        IObjectManager objectManager,
        IPlayerManager playerManager,
        ISessionWorkshopPlayerDataInterface sessionWorkshopPlayerDataInterface)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.sessionWorkshopPlayerDataInterface = sessionWorkshopPlayerDataInterface;
    }

    public void RestoreClientWorkshopData(
        WorkshopsCampaignBehavior workshopsCampaignBehavior,
        Hero playerHero,
        IEnumerable<Workshop> workshops,
        WorkshopPlayerData workshopPlayerData)
    {
        RestoreWorkshopData(
            workshopsCampaignBehavior,
            workshops,
            workshop => workshop.Owner == playerHero,
            workshopPlayerData,
            updateSession: false);
    }

    public void RestoreServerWorkshopData(
        WorkshopsCampaignBehavior workshopsCampaignBehavior,
        IEnumerable<Workshop> workshops)
    {
        RestoreWorkshopData(
            workshopsCampaignBehavior,
            workshops,
            workshop => playerManager.Contains(workshop.Owner),
            workshopPlayerData: null,
            updateSession: true);
    }

    public WorkshopsCampaignBehavior.WorkshopData EnsureWorkshopData(
        WorkshopsCampaignBehavior workshopsCampaignBehavior,
        Workshop workshop,
        WorkshopDataSnapshot savedData = null)
    {
        WorkshopsCampaignBehavior.WorkshopData[] workshopData =
            workshopsCampaignBehavior._workshopData ?? Array.Empty<WorkshopsCampaignBehavior.WorkshopData>();
        workshopsCampaignBehavior._workshopData = workshopData;

        WorkshopsCampaignBehavior.WorkshopData dataOfWorkshop =
            workshopsCampaignBehavior.GetDataOfWorkshop(workshop);
        if (dataOfWorkshop == null)
        {
            if (!workshopData.Any(data => data == null))
            {
                Array.Resize(ref workshopData, workshopData.Length + 1);
                workshopsCampaignBehavior._workshopData = workshopData;
            }

            workshopsCampaignBehavior.AddNewWorkshopData(workshop);
            dataOfWorkshop = workshopsCampaignBehavior.GetDataOfWorkshop(workshop);
        }

        if (savedData != null)
        {
            dataOfWorkshop.IsGettingInputsFromWarehouse = savedData.IsGettingInputsFromWarehouse;
            dataOfWorkshop.ProductionProgressForWarehouse = savedData.ProductionProgressForWarehouse;
            dataOfWorkshop.ProductionProgressForTown = savedData.ProductionProgressForTown;
            dataOfWorkshop.StockProductionInWarehouseRatio = savedData.StockProductionInWarehouseRatio;
        }

        return dataOfWorkshop;
    }

    private void RestoreWorkshopData(
        WorkshopsCampaignBehavior workshopsCampaignBehavior,
        IEnumerable<Workshop> workshops,
        Func<Workshop, bool> shouldRestore,
        WorkshopPlayerData workshopPlayerData,
        bool updateSession)
    {
        foreach (Workshop workshop in workshops)
        {
            if (workshop == null || !shouldRestore(workshop)) continue;
            if (!objectManager.TryGetIdWithLogging(workshop, out var workshopId)) continue;

            WorkshopDataSnapshot savedData = null;
            if (updateSession)
            {
                sessionWorkshopPlayerDataInterface.TryGetWorkshopData(workshopId, out savedData);
            }
            else
            {
                workshopPlayerData?.WorkshopDataByWorkshopId?.TryGetValue(workshopId, out savedData);
            }

            if (savedData == null && workshopsCampaignBehavior.GetDataOfWorkshop(workshop) == null)
            {
                Logger.Warning(
                    "Missing saved workshop data for {WorkshopId}; restoring production defaults",
                    workshopId);
            }

            WorkshopsCampaignBehavior.WorkshopData restoredData =
                EnsureWorkshopData(workshopsCampaignBehavior, workshop, savedData);
            if (updateSession)
            {
                sessionWorkshopPlayerDataInterface.UpdateWorkshopData(workshopId, restoredData);
            }
        }
    }
}
