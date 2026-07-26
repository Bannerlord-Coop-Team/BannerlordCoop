using GameInterface.Services.Players;
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
        IEnumerable<Workshop> workshops);

    void RestoreServerWorkshopData(
        WorkshopsCampaignBehavior workshopsCampaignBehavior,
        IEnumerable<Workshop> workshops);

    WorkshopsCampaignBehavior.WorkshopData EnsureWorkshopData(
        WorkshopsCampaignBehavior workshopsCampaignBehavior,
        Workshop workshop);
}

public class WorkshopDataRestorer : IWorkshopDataRestorer
{
    private readonly IPlayerManager playerManager;

    public WorkshopDataRestorer(IPlayerManager playerManager)
    {
        this.playerManager = playerManager;
    }

    public void RestoreClientWorkshopData(
        WorkshopsCampaignBehavior workshopsCampaignBehavior,
        Hero playerHero,
        IEnumerable<Workshop> workshops)
    {
        RestoreWorkshopData(
            workshopsCampaignBehavior,
            workshops,
            workshop => workshop.Owner == playerHero);
    }

    public void RestoreServerWorkshopData(
        WorkshopsCampaignBehavior workshopsCampaignBehavior,
        IEnumerable<Workshop> workshops)
    {
        RestoreWorkshopData(
            workshopsCampaignBehavior,
            workshops,
            workshop => playerManager.Contains(workshop.Owner));
    }

    public WorkshopsCampaignBehavior.WorkshopData EnsureWorkshopData(
        WorkshopsCampaignBehavior workshopsCampaignBehavior,
        Workshop workshop)
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

        return dataOfWorkshop;
    }

    private void RestoreWorkshopData(
        WorkshopsCampaignBehavior workshopsCampaignBehavior,
        IEnumerable<Workshop> workshops,
        Func<Workshop, bool> shouldRestore)
    {
        foreach (Workshop workshop in workshops)
        {
            if (workshop == null || !shouldRestore(workshop)) continue;
            EnsureWorkshopData(workshopsCampaignBehavior, workshop);
        }
    }
}
