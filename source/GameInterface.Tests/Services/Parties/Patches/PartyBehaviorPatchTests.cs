using Autofac;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.Settlements.Patches;
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
        var runMethod = SettlementEncounterPatch.HandleEncounterForMobilePartyPatch(ref party);

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

        idProvider.SetControllerId(controllerId);
        entityRegistry.RegisterAsControlled(controllerId, party.StringId);

        // Act
        var runMethod = SettlementEncounterPatch.HandleEncounterForMobilePartyPatch(ref party);

        // Assert
        Assert.True(runMethod);
    }
}
