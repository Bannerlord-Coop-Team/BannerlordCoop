using System.Collections.Generic;
using GameInterface.Services.MapEvents.Handlers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// Exercises the shared simulation teardown instead of substituting direct map-event finalization.
/// </summary>
public class BattleSimulationCompletionTests : MapEventTestBase
{
    public BattleSimulationCompletionTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(BattleState.DefenderVictory)]
    [InlineData(BattleState.AttackerVictory)]
    public void CompletedSimulation_FinalizesMapEventOnce(BattleState result)
    {
        var context = CreateServerMapEvent();
        var ended = new List<MapEvent>();

        Server.Call(() =>
        {
            var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);
            mapEvent.BattleState = result;
            var simulation = new BattleSimulationRunHandler.ActiveSimulation { MapEvent = mapEvent };
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, ended.Add);
            try
            {
                BattleSimulationRunHandler.EndSimulationSession(simulation);

                Assert.True(mapEvent.IsFinalized);
                Assert.Same(mapEvent, Assert.Single(ended));
            }
            finally
            {
                CampaignEvents.MapEventEnded.ClearListeners(this);
            }
        }, MapEventDisabledMethods);

        AssertMapEventRemoved(Server, context.MapEventId);
        foreach (var client in Clients)
            AssertMapEventRemoved(client, context.MapEventId);
    }
}
