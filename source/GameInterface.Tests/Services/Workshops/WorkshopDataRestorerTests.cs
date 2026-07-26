using Common.Util;
using GameInterface.Services.Players;
using GameInterface.Services.Workshops;
using Moq;
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using Xunit;

namespace GameInterface.Tests.Services.Workshops;

public class WorkshopDataRestorerTests
{
    [Fact]
    public void EnsureWorkshopData_ExistingVanillaData_PreservesEntryAndFields()
    {
        Workshop workshop = CreateWorkshop(ObjectHelper.SkipConstructor<Hero>());
        WorkshopsCampaignBehavior workshopsBehavior = CreateWorkshopsBehavior();
        var existingData = new WorkshopsCampaignBehavior.WorkshopData(workshop)
        {
            IsGettingInputsFromWarehouse = true,
            ProductionProgressForWarehouse = 0.25f,
            ProductionProgressForTown = 0.5f,
            StockProductionInWarehouseRatio = 0.75f,
        };
        workshopsBehavior._workshopData = new[] { existingData };

        WorkshopsCampaignBehavior.WorkshopData workshopData =
            CreateRestorer().EnsureWorkshopData(workshopsBehavior, workshop);

        Assert.Same(existingData, Assert.Single(workshopsBehavior._workshopData));
        Assert.Same(existingData, workshopData);
        Assert.True(workshopData.IsGettingInputsFromWarehouse);
        Assert.Equal(0.25f, workshopData.ProductionProgressForWarehouse);
        Assert.Equal(0.5f, workshopData.ProductionProgressForTown);
        Assert.Equal(0.75f, workshopData.StockProductionInWarehouseRatio);
    }

    [Fact]
    public void EnsureWorkshopData_FullStorage_ExpandsForWorkshop()
    {
        Workshop existingWorkshop = CreateWorkshop(ObjectHelper.SkipConstructor<Hero>());
        Workshop playerWorkshop = CreateWorkshop(ObjectHelper.SkipConstructor<Hero>());
        WorkshopsCampaignBehavior workshopsBehavior = CreateWorkshopsBehavior();
        workshopsBehavior._workshopData =
            new[] { new WorkshopsCampaignBehavior.WorkshopData(existingWorkshop) };

        CreateRestorer().EnsureWorkshopData(workshopsBehavior, playerWorkshop);

        Assert.Equal(2, workshopsBehavior._workshopData.Length);
        Assert.Contains(workshopsBehavior._workshopData, data => data?.Workshop == existingWorkshop);
        Assert.Contains(workshopsBehavior._workshopData, data => data?.Workshop == playerWorkshop);
    }

    [Fact]
    public void EnsureWorkshopData_MissingEntry_CreatesDefaultEntry()
    {
        Workshop workshop = CreateWorkshop(ObjectHelper.SkipConstructor<Hero>());
        WorkshopsCampaignBehavior workshopsBehavior = CreateWorkshopsBehavior();

        WorkshopsCampaignBehavior.WorkshopData workshopData =
            CreateRestorer().EnsureWorkshopData(workshopsBehavior, workshop);

        Assert.False(workshopData.IsGettingInputsFromWarehouse);
        Assert.Equal(0f, workshopData.ProductionProgressForWarehouse);
        Assert.Equal(0f, workshopData.ProductionProgressForTown);
        Assert.Equal(0f, workshopData.StockProductionInWarehouseRatio);
    }

    [Fact]
    public void RestoreServerWorkshopData_OneRejoinTrigger_RestoresEveryRegisteredPlayerWorkshop()
    {
        Hero joiningHero = ObjectHelper.SkipConstructor<Hero>();
        Hero disconnectedHero = ObjectHelper.SkipConstructor<Hero>();
        Hero nonPlayerHero = ObjectHelper.SkipConstructor<Hero>();
        Workshop joiningWorkshop = CreateWorkshop(joiningHero);
        Workshop disconnectedWorkshop = CreateWorkshop(disconnectedHero);
        Workshop nonPlayerWorkshop = CreateWorkshop(nonPlayerHero);
        WorkshopsCampaignBehavior workshopsBehavior = CreateWorkshopsBehavior();
        var joiningData = new WorkshopsCampaignBehavior.WorkshopData(joiningWorkshop)
        {
            IsGettingInputsFromWarehouse = true,
            ProductionProgressForWarehouse = 0.1f,
            ProductionProgressForTown = 0.2f,
            StockProductionInWarehouseRatio = 0.3f,
        };
        workshopsBehavior._workshopData = new[] { joiningData };
        var playerManager = new Mock<IPlayerManager>();

        playerManager.Setup(manager => manager.Contains(joiningHero)).Returns(true);
        playerManager.Setup(manager => manager.Contains(disconnectedHero)).Returns(true);
        var restorer = new WorkshopDataRestorer(playerManager.Object);

        restorer.RestoreServerWorkshopData(
            workshopsBehavior,
            new[] { joiningWorkshop, disconnectedWorkshop, nonPlayerWorkshop });

        WorkshopsCampaignBehavior.WorkshopData disconnectedData =
            workshopsBehavior.GetDataOfWorkshop(disconnectedWorkshop);
        Assert.Same(joiningData, workshopsBehavior.GetDataOfWorkshop(joiningWorkshop));
        Assert.NotNull(disconnectedData);
        Assert.Null(workshopsBehavior.GetDataOfWorkshop(nonPlayerWorkshop));
        Assert.Equal(2, workshopsBehavior._workshopData.Count(data => data != null));
        Assert.True(joiningData.IsGettingInputsFromWarehouse);
        Assert.Equal(0.1f, joiningData.ProductionProgressForWarehouse);
        Assert.Equal(0.2f, joiningData.ProductionProgressForTown);
        Assert.Equal(0.3f, joiningData.StockProductionInWarehouseRatio);
        Assert.False(disconnectedData.IsGettingInputsFromWarehouse);
        Assert.Equal(0f, disconnectedData.ProductionProgressForWarehouse);
        Assert.Equal(0f, disconnectedData.ProductionProgressForTown);
        Assert.Equal(0f, disconnectedData.StockProductionInWarehouseRatio);
    }

    private static WorkshopDataRestorer CreateRestorer()
    {
        return new WorkshopDataRestorer(Mock.Of<IPlayerManager>());
    }

    private static Workshop CreateWorkshop(Hero owner)
    {
        Workshop workshop = ObjectHelper.SkipConstructor<Workshop>();
        typeof(Workshop).GetField("_owner", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(workshop, owner);
        return workshop;
    }

    private static WorkshopsCampaignBehavior CreateWorkshopsBehavior()
    {
        WorkshopsCampaignBehavior workshopsBehavior =
            ObjectHelper.SkipConstructor<WorkshopsCampaignBehavior>();
        workshopsBehavior._workshopData = Array.Empty<WorkshopsCampaignBehavior.WorkshopData>();
        return workshopsBehavior;
    }
}
