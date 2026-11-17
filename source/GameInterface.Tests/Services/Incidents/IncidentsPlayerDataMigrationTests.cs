using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.CoopSessionData;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Services.ObjectManager;
using Moq;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Incidents;

public class IncidentsPlayerDataMigrationTests
{
    private const string OldHeroId = "Hero_Player1";
    private const string HeirId = "Hero_Player1_Heir";
    private const string OtherPlayerHeroId = "Hero_Player2";
    private const string ArrowProofingId = "Incident_incident_arrow_proofing";

    private readonly CoopSession session = CoopSession.Empty;
    private readonly Mock<INetwork> network = new();
    private readonly CoopSessionMigrator migrator;
    private readonly Hero oldHero = ObjectHelper.SkipConstructor<Hero>();
    private readonly Hero heir = ObjectHelper.SkipConstructor<Hero>();

    public IncidentsPlayerDataMigrationTests()
    {
        var sessionProvider = new Mock<ICoopSessionProvider>();
        sessionProvider.SetupGet(provider => provider.CoopSession).Returns(session);

        var objectManager = new Mock<IObjectManager>();
        string oldHeroId = OldHeroId;
        string heirId = HeirId;
        objectManager.Setup(manager => manager.TryGetIdWithLogging(oldHero, out oldHeroId)).Returns(true);
        objectManager.Setup(manager => manager.TryGetIdWithLogging(heir, out heirId)).Returns(true);

        migrator = new CoopSessionMigrator(sessionProvider.Object, objectManager.Object, network.Object);
    }

    [Fact]
    public void MigratePlayerData_ClearsEveryIncidentEntryForTheOldHero()
    {
        AddIncidentData(OldHeroId);

        migrator.MigratePlayerData(oldHero, heir);

        var data = session.IncidentsPlayerData;
        Assert.False(data.PlayerIncidentsOnCooldown.ContainsKey(OldHeroId));
        Assert.False(data.PlayerLastGlobalIncidentCooldown.ContainsKey(OldHeroId));
        Assert.False(data.PlayerActiveIncidentSeed.ContainsKey(OldHeroId));
        Assert.False(data.PlayerCanInvokeSettlementEvent.ContainsKey(OldHeroId));
        Assert.False(data.PlayerPendingIncident.ContainsKey(OldHeroId));

        // Cleared rather than moved, matching vanilla OnHeirSelectionOver resetting incident cooldowns for the heir
        Assert.False(data.PlayerIncidentsOnCooldown.ContainsKey(HeirId));
        Assert.False(data.PlayerPendingIncident.ContainsKey(HeirId));
    }

    [Fact]
    public void MigratePlayerData_LeavesOtherPlayersIncidentDataAlone()
    {
        AddIncidentData(OldHeroId);
        AddIncidentData(OtherPlayerHeroId);

        migrator.MigratePlayerData(oldHero, heir);

        var data = session.IncidentsPlayerData;
        Assert.True(data.PlayerIncidentsOnCooldown.ContainsKey(OtherPlayerHeroId));
        Assert.True(data.PlayerLastGlobalIncidentCooldown.ContainsKey(OtherPlayerHeroId));
        Assert.True(data.PlayerActiveIncidentSeed.ContainsKey(OtherPlayerHeroId));
        Assert.True(data.PlayerCanInvokeSettlementEvent.ContainsKey(OtherPlayerHeroId));
        Assert.True(data.PlayerPendingIncident.ContainsKey(OtherPlayerHeroId));
    }

    [Fact]
    public void MigratePlayerData_SendsUpdatedSessionOnce()
    {
        AddIncidentData(OldHeroId);

        migrator.MigratePlayerData(oldHero, heir);

        // Only sent when every migration rule resolved, so this also fails if an incident rule names a missing property
        network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Once);
    }

    private void AddIncidentData(string playerHeroId)
    {
        var data = session.IncidentsPlayerData;
        data.PlayerIncidentsOnCooldown[playerHeroId] = new() { [ArrowProofingId] = 1351L };
        data.PlayerLastGlobalIncidentCooldown[playerHeroId] = 2462L;
        data.PlayerActiveIncidentSeed[playerHeroId] = 4242L;
        data.PlayerCanInvokeSettlementEvent[playerHeroId] = true;
        data.PlayerPendingIncident[playerHeroId] = ArrowProofingId;
    }
}
