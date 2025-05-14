using Autofac;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using System;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.Parties.Patches;

public class PartyBehaviorPatchTests : IDisposable
{
    private readonly PatchBootstrap bootstrap;
    private IContainer Container => bootstrap.Container;

    public PartyBehaviorPatchTests()
    {
        bootstrap = new PatchBootstrap();
    }

    public void Dispose() => bootstrap.Dispose();

    [Fact]
    public void UncontrolledParty_SkipsMethod()
    {
        // Arrange
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.StringId = "TestEntityId";

        // Act
        var runMethod = EncounterManagerPatches.HandleEncounterForMobilePartyPatch(ref party);

        // Assert
        Assert.False(runMethod);
    }

    [Fact]
    public void ControlledParty_RunsMethod()
    {
        // Arrange
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.StringId = "TestEntityId";
        var controllerId = "TestId";

        var idProvider = ContainerProvider.Container.Resolve<IControllerIdProvider>();
        var entityRegistry = ContainerProvider.Container.Resolve<IControlledEntityRegistry>();
        var objectManager = ContainerProvider.Container.Resolve<IObjectManager>();

        idProvider.SetControllerId(controllerId);
        entityRegistry.RegisterAsControlled(controllerId, party.StringId);
        objectManager.AddExisting(party.StringId, party);

        // Act
        var runMethod = EncounterManagerPatches.HandleEncounterForMobilePartyPatch(ref party);

        // Assert
        Assert.True(runMethod);
    }
}
