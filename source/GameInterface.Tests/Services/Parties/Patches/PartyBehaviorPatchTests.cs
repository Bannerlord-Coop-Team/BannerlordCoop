using Autofac;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents.Patches;
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

        var objectManager = Container.Resolve<IObjectManager>();
        objectManager.AddExisting(party.StringId, party);

        var dt = 0.1f;

        // Act
        var runMethod = EncounterManagerPatches.HandleEncounterForMobilePartyPatch(ref party, ref dt);

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

        var idProvider = Container.Resolve<IControllerIdProvider>();
        var entityRegistry = Container.Resolve<IControlledEntityRegistry>();
        var objectManager = Container.Resolve<IObjectManager>();

        objectManager.AddExisting(party.StringId, party);
        objectManager.TryGetId(party, out var partyId);

        idProvider.SetControllerId(controllerId);
        entityRegistry.RegisterAsControlled(controllerId, partyId);

        var dt = 0.1f;

        // Act
        var runMethod = EncounterManagerPatches.HandleEncounterForMobilePartyPatch(ref party, ref dt);

        // Assert
        Assert.True(runMethod);
    }
}
