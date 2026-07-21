using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.ObjectManager;
using Moq;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.MapEvents;

namespace E2E.Tests.Services.MapEvents;

public class BattleMissionStartRoutingTests
{
    /// <summary>
    /// BR-104: a battle-scoped mission-start message must not affect the current mission when it carries a
    /// previous or unrelated battle's map-event id. The routing check that gates the mission open matches only
    /// the local player's current battle id, so a message stamped with any other instance id is dropped.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-104")]
    public void MissionStartRouting_ForeignOrPreviousMapEventId_DoesNotMatchCurrentBattle()
    {
        // The local player's current battle. As above, the routing helper only reads its id via IObjectManager.
#pragma warning disable SYSLIB0050
        var currentBattle = (MapEvent)FormatterServices.GetUninitializedObject(typeof(MapEvent));
#pragma warning restore SYSLIB0050
        var objectManager = new Mock<IObjectManager>();
        string currentId = "current-battle";
        objectManager.Setup(m => m.TryGetId(currentBattle, out currentId)).Returns(true);

        // Only a message addressed to the current battle's own instance id is routed to it...
        Assert.True(BattleMissionStartHandler.MatchesMapEventId(
            objectManager.Object, currentBattle, "current-battle"));
        // ...a message carrying a previous or unrelated battle's id is dropped, so it cannot affect this mission.
        Assert.False(BattleMissionStartHandler.MatchesMapEventId(
            objectManager.Object, currentBattle, "previous-battle"));
        Assert.False(BattleMissionStartHandler.MatchesMapEventId(
            objectManager.Object, currentBattle, "unrelated-battle"));
    }

    [Fact]
    public void FailedMissionOpen_ReleasesSpawnGate()
    {
        BattleSpawnGate.BeginBattle("battle-1");
        try
        {
            BattleMissionStartHandler.UnwindSpawnGateAfterFailedOpen(spawnGateEngaged: true);

            Assert.False(BattleSpawnGate.IsCoopBattleActive);
        }
        finally
        {
            // Keep the process-global gate isolated even if the assertion exposes a regression.
            BattleSpawnGate.EndBattle();
        }
    }

    [Theory]
    [InlineData(false, "local-party", "other-party", true)]
    [InlineData(true, "local-party", "local-party", true)]
    [InlineData(true, "local-party", "other-party", false)]
    [InlineData(true, null, null, false)]
    public void MissionStart_WoundedPlayerOnlyOpensOwnBattle(
        bool isPlayerWounded, string? localPartyId, string? initiatingPartyId, bool expected)
    {
        Assert.Equal(expected, BattleMissionStartHandler.ShouldOpenBattleMission(
            isPlayerWounded, localPartyId, initiatingPartyId));
    }
}
