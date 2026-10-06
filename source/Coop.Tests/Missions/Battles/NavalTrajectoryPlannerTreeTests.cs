using NavalDLC.DWA;
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>
/// Pins the vanilla planner behavior the AI hull swap depends on: removing one ship and adding one leaves the
/// simulator's agent count unchanged, so its k-d tree is not rebuilt and still holds the removed ship's agent.
/// </summary>
public class NavalTrajectoryPlannerTreeTests
{
    [Fact]
    public void RemoveThenAdd_KeepsTheRemovedAgentInTheTreeAndMissesTheNewOne()
    {
        var simulator = CreateSimulator();
        var removed = new FakeShipDelegate();
        simulator.AddAgent(removed);
        simulator.AddAgent(new FakeShipDelegate());
        BuildTree(simulator);

        simulator.RemoveAgent(removed);
        var added = new FakeShipDelegate();
        simulator.AddAgent(added);
        BuildTree(simulator);

        var tree = TreeDelegates(simulator);
        Assert.Contains(removed, tree);
        Assert.DoesNotContain(added, tree);
    }

    [Fact]
    public void ClearAndReAdd_AsForceReinitializeDoes_TreeHoldsOnlyTheLiveAgents()
    {
        var simulator = CreateSimulator();
        var removed = new FakeShipDelegate();
        var kept = new FakeShipDelegate();
        simulator.AddAgent(removed);
        simulator.AddAgent(kept);
        BuildTree(simulator);

        var added = new FakeShipDelegate();
        simulator.Clear();
        simulator.AddAgent(kept);
        simulator.AddAgent(added);
        BuildTree(simulator);

        var tree = TreeDelegates(simulator);
        Assert.Contains(kept, tree);
        Assert.Contains(added, tree);
        Assert.DoesNotContain(removed, tree);
    }

    private static DWASimulator CreateSimulator()
    {
        var simulator = new DWASimulator();
        var parameters = DWASimulatorParameters.Create();
        simulator.SetParameters(in parameters);
        return simulator;
    }

    // DWAKdTree is internal to NavalDLC, which this test project does not publicize.
    private static object Tree(DWASimulator simulator) =>
        typeof(DWASimulator).GetField("_kdTree", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(simulator);

    private static void BuildTree(DWASimulator simulator)
    {
        var tree = Tree(simulator);
        tree.GetType().GetMethod("BuildAgentTree", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tree, null);
    }

    private static IDWAAgentDelegate[] TreeDelegates(DWASimulator simulator)
    {
        var tree = Tree(simulator);
        var agents = (DWAAgent[])tree.GetType().GetField("_agents", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tree);
        return agents.Where(agent => agent != null).Select(agent => agent.Delegate).ToArray();
    }

    private sealed class FakeShipDelegate : IDWAAgentDelegate
    {
        private DWAAgentState state;

        public ref readonly DWAAgentState State => ref state;
        public float NeighborDistance => 100f;
        public float MaxLinearSpeed => 5f;
        public float MaxLinearAcceleration => 1f;
        public float MaxAngularSpeed => 1f;
        public float MaxAngularAcceleration => 1f;
        public bool AvoidAgentCollisions => true;
        public bool AvoidObstacleCollisions => false;

        public void Initialize(int id) { }
        public void SetParameters(in DWASimulatorParameters parameters) { }
        public float GetSafetyFactor() => 1f;
        public bool CanPlanTrajectory() => false;
        public bool HasArrivedAtTarget() => true;
        public bool IsAgentEligibleNeighbor(int targetAgentId, IDWAAgentDelegate targetAgentDelegate) => true;
        public bool IsObstacleSegmentEligibleNeighbor(IDWAObstacleVertex obstacle1, IDWAObstacleVertex obstacle2) => false;
        public void OnStateUpdate() { }
        public void UpdateSelectedAction(float dV, float dOmega) { }

        public float GetGoalDirection(out Vec2 goalDir)
        {
            goalDir = Vec2.Zero;
            return 0f;
        }

        public (float dV, float dOmega) GetSelectedAction() => (0f, 0f);

        public void ComputeExternalAccelerationsOnState(float dt, in DWAAgentState state, out Vec2 extLinearAcc, out float extAngularAcc)
        {
            extLinearAcc = Vec2.Zero;
            extAngularAcc = 0f;
        }

        public float ComputeGoalCost(int sampleIndex, in DWAAgentState atState, (float distance, float amount) targetOcclusion) => 0f;
    }
}
