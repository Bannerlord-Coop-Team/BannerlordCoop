using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Workshops.Interfaces;
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
    public void EnsureWorkshopData_SavedSnapshot_RestoresEveryField()
    {
        Workshop workshop = CreateWorkshop(ObjectHelper.SkipConstructor<Hero>());
        WorkshopsCampaignBehavior workshopsBehavior = CreateWorkshopsBehavior();
        var savedData = new WorkshopDataSnapshot(
            isGettingInputsFromWarehouse: true,
            productionProgressForWarehouse: 0.25f,
            productionProgressForTown: 0.5f,
            stockProductionInWarehouseRatio: 0.75f);

        WorkshopsCampaignBehavior.WorkshopData workshopData =
            CreateRestorer().EnsureWorkshopData(
                workshopsBehavior,
                workshop,
                savedData);

        Assert.Same(workshop, workshopData.Workshop);
        Assert.True(workshopData.IsGettingInputsFromWarehouse);
        Assert.Equal(0.25f, workshopData.ProductionProgressForWarehouse);
        Assert.Equal(0.5f, workshopData.ProductionProgressForTown);
        Assert.Equal(0.75f, workshopData.StockProductionInWarehouseRatio);
    }

    [Fact]
    public void EnsureWorkshopData_ExistingWorkshopDataWithoutSnapshot_PreservesEntry()
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
    public void EnsureWorkshopData_NoSnapshot_CreatesDefaultEntry()
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
        var joiningSnapshot = new WorkshopDataSnapshot(true, 0.1f, 0.2f, 0.3f);
        var disconnectedSnapshot = new WorkshopDataSnapshot(false, 0.4f, 0.5f, 0.6f);
        var objectManager = new Mock<IObjectManager>();
        var playerManager = new Mock<IPlayerManager>();
        var sessionWorkshopPlayerData = new Mock<ISessionWorkshopPlayerDataInterface>();
        string joiningWorkshopId = "Workshop_Joining";
        string disconnectedWorkshopId = "Workshop_Disconnected";

        objectManager
            .Setup(manager => manager.TryGetIdWithLogging(joiningWorkshop, out joiningWorkshopId))
            .Returns(true);
        objectManager
            .Setup(manager => manager.TryGetIdWithLogging(disconnectedWorkshop, out disconnectedWorkshopId))
            .Returns(true);
        playerManager.Setup(manager => manager.Contains(joiningHero)).Returns(true);
        playerManager.Setup(manager => manager.Contains(disconnectedHero)).Returns(true);
        sessionWorkshopPlayerData
            .Setup(data => data.TryGetWorkshopData(joiningWorkshopId, out joiningSnapshot))
            .Returns(true);
        sessionWorkshopPlayerData
            .Setup(data => data.TryGetWorkshopData(disconnectedWorkshopId, out disconnectedSnapshot))
            .Returns(true);
        var restorer = new WorkshopDataRestorer(
            objectManager.Object,
            playerManager.Object,
            sessionWorkshopPlayerData.Object);

        restorer.RestoreServerWorkshopData(
            workshopsBehavior,
            new[] { joiningWorkshop, disconnectedWorkshop, nonPlayerWorkshop });

        WorkshopsCampaignBehavior.WorkshopData joiningData =
            workshopsBehavior.GetDataOfWorkshop(joiningWorkshop);
        WorkshopsCampaignBehavior.WorkshopData disconnectedData =
            workshopsBehavior.GetDataOfWorkshop(disconnectedWorkshop);
        Assert.NotNull(joiningData);
        Assert.NotNull(disconnectedData);
        Assert.Null(workshopsBehavior.GetDataOfWorkshop(nonPlayerWorkshop));
        Assert.Equal(2, workshopsBehavior._workshopData.Count(data => data != null));
        Assert.Equal(0.1f, joiningData.ProductionProgressForWarehouse);
        Assert.Equal(0.4f, disconnectedData.ProductionProgressForWarehouse);
        sessionWorkshopPlayerData.Verify(
            data => data.UpdateWorkshopData(joiningWorkshopId, joiningData),
            Times.Once);
        sessionWorkshopPlayerData.Verify(
            data => data.UpdateWorkshopData(disconnectedWorkshopId, disconnectedData),
            Times.Once);
    }

    private static WorkshopDataRestorer CreateRestorer()
    {
        return new WorkshopDataRestorer(
            Mock.Of<IObjectManager>(),
            Mock.Of<IPlayerManager>(),
            Mock.Of<ISessionWorkshopPlayerDataInterface>());
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
