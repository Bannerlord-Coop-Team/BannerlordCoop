using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.Locations;
using Missions.Taverns;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Services.Locations;

/// <summary>
/// One client's active headless settlement mission. The dedicated server never owns one of these.
/// </summary>
public sealed class SettlementClientFixture : IDisposable
{
    private bool disposed;

    public EnvironmentInstance Instance { get; }
    public string ControllerId { get; }
    public string InstanceId { get; }
    public Settlement Settlement { get; }
    public Location Location { get; }
    public MockMission Mission { get; }
    public CoopLocationsController Controller { get; }
    public Agent PlayerAgent { get; }
    public MockBattleNetwork Mesh { get; }

    internal SettlementClientFixture(
        EnvironmentInstance instance,
        string controllerId,
        string instanceId,
        Settlement settlement,
        Location location,
        MockMission mission,
        CoopLocationsController controller,
        Agent playerAgent,
        MockBattleNetwork mesh)
    {
        Instance = instance;
        ControllerId = controllerId;
        InstanceId = instanceId;
        Settlement = settlement;
        Location = location;
        Mission = mission;
        Controller = controller;
        PlayerAgent = playerAgent;
        Mesh = mesh;
    }

    public void Tick(float elapsedSeconds)
    {
        ThrowIfDisposed();
        Instance.Call(() => Controller.OnMissionTick(elapsedSeconds));
    }

    public void Leave()
    {
        if (disposed) return;

        Instance.Call(() => Controller.OnEndMissionInternal());
        Instance.CampaignMissionContext = null;
        disposed = true;
    }

    internal void Disconnect()
    {
        if (disposed) return;

        Instance.Call(() =>
        {
            LocationNpcGate.EndMission();
            Controller.Dispose();
            Mesh.Stop();
        });
        Instance.CampaignMissionContext = null;
        disposed = true;
    }

    public void Dispose()
    {
        Leave();
    }

    private void ThrowIfDisposed()
    {
        if (disposed) throw new ObjectDisposedException(nameof(SettlementClientFixture));
    }
}
