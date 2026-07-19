using Common;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Workshops.Commands;
using GameInterface.Tests;
using Moq;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Workshops;

[Collection(ModInformationRoleCollection.Name)]
public class WorkshopDebugCommandTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;

    public void Dispose()
    {
        ModInformation.IsServer = wasServer;
    }

    [Fact]
    public void SetWorkshopOwner_WhenClient_ReturnsServerOnlyError()
    {
        ModInformation.IsServer = false;

        Assert.Equal(
            "Run coop.debug.workshop.set_workshop_owner on the server (host) only",
            WorkshopDebugCommand.SetWorkshopOwner(new List<string>()));
    }

    [Fact]
    public void ResolveHero_RegistryId_ReturnsRegisteredPlayerHero()
    {
        Hero playerHero = ObjectHelper.SkipConstructor<Hero>();
        var objectManager = new Mock<IObjectManager>();
        objectManager
            .Setup(manager => manager.TryGetObject<Hero>("Hero_Player2863", out playerHero))
            .Returns(true);

        Hero result = WorkshopDebugCommand.ResolveHero("Hero_Player2863", objectManager.Object);

        Assert.Same(playerHero, result);
    }
}
