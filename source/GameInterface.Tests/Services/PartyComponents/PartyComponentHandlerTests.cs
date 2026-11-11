using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyComponents.Data;
using GameInterface.Services.PartyComponents.Handlers;
using Moq;
using System;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.Services.PartyComponents;

public class PartyComponentHandlerTests
{
    private const int VillagerTypeIndex = 7;

    [Fact]
    public void GetTypeIndex_ModuleSubclass_UsesNearestListedBaseType()
    {
        using var handler = CreateHandler();

        Assert.Equal(VillagerTypeIndex, handler.GetTypeIndex(typeof(VillagerPartyComponent)));
        Assert.Equal(VillagerTypeIndex, handler.GetTypeIndex(typeof(TestModuleVillagerPartyComponent)));
        Assert.Equal(-1, handler.GetTypeIndex(typeof(PartyComponent)));
    }

    [Fact]
    public void ResolveComponentType_WithoutConcreteTypeName_UsesListedType()
    {
        using var handler = CreateHandler();

        var data = new PartyComponentData(VillagerTypeIndex, "VillagerPartyComponent_1", 1);

        Assert.Equal(typeof(VillagerPartyComponent), handler.ResolveComponentType(data));
    }

    [Fact]
    public void ResolveComponentType_WithConcreteTypeName_UsesModuleSubclass()
    {
        using var handler = CreateHandler();

        var data = new PartyComponentData(VillagerTypeIndex, "TestModuleVillagerPartyComponent_1", 1)
        {
            ConcreteTypeName = typeof(TestModuleVillagerPartyComponent).FullName,
        };

        Assert.Equal(typeof(TestModuleVillagerPartyComponent), handler.ResolveComponentType(data));
    }

    [Theory]
    [InlineData(VillagerTypeIndex, "Missing.Module.PartyComponent")]
    [InlineData(0, "GameInterface.Tests.Services.PartyComponents.TestModuleVillagerPartyComponent")]
    [InlineData(-1, null)]
    public void ResolveComponentType_InvalidData_Throws(int typeIndex, string concreteTypeName)
    {
        using var handler = CreateHandler();

        var data = new PartyComponentData(typeIndex, "PartyComponent_1", 1)
        {
            ConcreteTypeName = concreteTypeName,
        };

        Assert.Throws<InvalidOperationException>(() => handler.ResolveComponentType(data));
    }

    private static PartyComponentHandler CreateHandler()
    {
        return new PartyComponentHandler(
            new Mock<IMessageBroker>().Object,
            new Mock<INetwork>().Object,
            new Mock<IObjectManager>().Object);
    }
}

// Stands in for a module's VillagerPartyComponent subclass without referencing the module
internal class TestModuleVillagerPartyComponent : VillagerPartyComponent
{
    private TestModuleVillagerPartyComponent(Village village)
        : base(village, null)
    {
    }
}
