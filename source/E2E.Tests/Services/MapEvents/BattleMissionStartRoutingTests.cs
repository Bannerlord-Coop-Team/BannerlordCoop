using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.ObjectManager;
using Moq;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.MapEvents;

namespace E2E.Tests.Services.MapEvents;

public class BattleMissionStartRoutingTests
{
    [Fact]
    public void SiegeStart_MatchesOnlyTheLocalBattleMapEventId()
    {
        // Identity-only test double: MapEvent's real constructor enters native campaign state, while this
        // routing helper only passes the reference to IObjectManager and never reads MapEvent internals.
#pragma warning disable SYSLIB0050
        var battle = (MapEvent)FormatterServices.GetUninitializedObject(typeof(MapEvent));
#pragma warning restore SYSLIB0050
        var objectManager = new Mock<IObjectManager>();
        string actualId = "local-battle";
        objectManager.Setup(m => m.TryGetId(battle, out actualId)).Returns(true);

        Assert.True(BattleMissionStartHandler.MatchesMapEventId(objectManager.Object, battle, "local-battle"));
        Assert.False(BattleMissionStartHandler.MatchesMapEventId(objectManager.Object, battle, "other-battle"));
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
}
