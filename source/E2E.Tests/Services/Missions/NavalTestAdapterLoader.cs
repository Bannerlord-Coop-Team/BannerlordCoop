#if DEBUG
using E2E.Tests.Environment.MockEngine;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Services.Missions;

/// <summary>Container-owned native boundary; every simulated process gets an independent adapter.</summary>
public sealed class NavalTestAdapterLoader : INavalMissionAdapterLoader
{
    public NavalTestAdapter Adapter { get; } = new();
    public int LoadCount { get; private set; }
    public INavalMissionAdapter Load()
    {
        LoadCount++;
        return Adapter;
    }
    public void Dispose() => Adapter.Dispose();
}

/// <summary>
/// Records engine requests, not a physics model. The existing MockMission creates agent shells;
/// frame application copies supplied values and makes no claim about water, contact or native input.
/// </summary>
public sealed class NavalTestAdapter : INavalMissionAdapter, INavalNativeMissionAdapter
{
    public MockMission Mission { get; private set; } = null!;
    public NavalLabController? Controller { get; private set; }
    public NavalLabManifest? OpenedManifest { get; private set; }
    public Agent[] Agents { get; private set; } = Array.Empty<Agent>();
    public string? Blocker { get; set; }
    public object StartupDiagnostics => new { simulated = true };
    public bool FailApply { get; set; }
    public bool ThrowOnOpen { get; set; }
    public bool FailActivation { get; set; }
    public bool Authority { get; private set; }
    public int OpenCount { get; private set; }
    public int ApplyCount { get; private set; }
    public int CancelCount { get; private set; }
    public bool Disposed { get; private set; }
    public List<(int ship, float rudder, bool row)> HelmCalls { get; } = new();
    public List<(string kind, int ship, float value)> AgentControlCalls { get; } = new();
    public List<(int ship, bool take)> HeldHelmCalls { get; } = new();
    public bool HeldHelmTaken { get; private set; }
    public List<string> Calls { get; } = new();
    public NetworkNavalLabSailState[] SailStates { get; set; } = Array.Empty<NetworkNavalLabSailState>();
    public List<NetworkNavalLabFrames> SailFeedback { get; } = new();
    public int SailClears { get; private set; }
    public List<int> SailRequests { get; } = new();
    public object InspectSailStatus() => new { simulated = true, requests = SailRequests.ToArray() };
    public string RequestSail(int state) { SailRequests.Add(state); return "requested:simulated_native_boundary"; }
    public NetworkNavalLabSailState[] ReadSailStates() => SailStates;
    public void ApplySailFeedback(NetworkNavalLabFrames frames) => SailFeedback.Add(frames);
    public void ClearSailFeedback() => SailClears++;
    public MatrixFrame[] Frames { get; set; } = new[] { MatrixFrame.Identity, MatrixFrame.Identity };

    public void Bind(MockMission mission) => Mission = mission;
    public void Preflight() => Calls.Add("preflight");
    private string ownControllerId = "";
    public int MaterializeCount { get; private set; }
    public bool? FactoryHost { get; private set; }
    public bool ThrowOnMaterialize { get; set; }
    public bool IncompleteMaterialize { get; set; }
    public Action? DuringMaterialize { get; set; }
    public bool ThrowOnApply { get; set; }
    public bool ThrowOnCancel { get; set; }
    public bool ThrowOnHold { get; set; }
    public int HoldCount { get; private set; }
    public void MaterializeFactoryProbe(bool electedHost, Func<bool> authorityValid)
    {
        Assert.True(authorityValid());
        Assert.Empty(Agents);
        MaterializeCount++;
        FactoryHost = electedHost;
        Calls.Add("materialize:" + electedHost);
        DuringMaterialize?.Invoke();
        if (ThrowOnMaterialize) throw new InvalidOperationException("simulated factory failure");
        if (!authorityValid()) throw new InvalidOperationException("assignment changed during factory");
        if (!IncompleteMaterialize) SpawnActors(OpenedManifest!, ownControllerId);
    }
    public Mission Open(NavalLabManifest manifest, MissionBehavior controller, string ownControllerId)
    {
        OpenCount++;
        Calls.Add("open");
        OpenedManifest = manifest;
        Controller = Assert.IsType<NavalLabController>(controller);
        controller.Mission = Mission.Shell;
        if (ThrowOnOpen) throw new InvalidOperationException("simulated native open failure");
        this.ownControllerId = ownControllerId;
        if (manifest.Mode != NavalLabMode.FactoryAuthorityProbe && manifest.Mode != NavalLabMode.TwoClientNative) SpawnActors(manifest, ownControllerId);
        return Mission.Shell;
    }
    private void SpawnActors(NavalLabManifest manifest, string ownControllerId)
    {
        Agents = manifest.Combatants.Select((_, index) => Mission.SpawnAgent(
            new AgentBuildData(Game.Current.PlayerTroop).Controller(
                manifest.Controllers[index / NavalLabManifest.CrewPerShip] == ownControllerId
                    ? AgentControllerType.AI : AgentControllerType.None))).ToArray();
    }
    public int DeploymentCalls { get; private set; }
    public bool FailDeployment { get; set; }
    public bool DeploymentComplete { get; private set; }
    public bool TerminalHold { get; private set; }
    public string CompleteDeployment()
    {
        if (TerminalHold) return "rejected:fixture_blocked";
        if (DeploymentComplete) return "already_deployed";
        DeploymentCalls++;
        if (FailDeployment) { Blocker = "deployment.failed"; Hold(); return "failed:deployment"; }
        DeploymentComplete = true;
        return "deployed";
    }
    public void Hold()
    {
        HoldCount++;
        Calls.Add("hold");
        if (ThrowOnHold) throw new InvalidOperationException("simulated body hold failure");
        TerminalHold = true;
        Authority = false;
        try { CancelControls(); }
        catch when (OpenedManifest?.Mode == NavalLabMode.FactoryAuthorityProbe || OpenedManifest?.Mode == NavalLabMode.TwoClientNative) { }
    }
    public void SetAuthority(bool simulate)
    {
        Calls.Add("authority:" + simulate);
        if (simulate && FailActivation) Blocker = "ship.activation_unconfirmed:slot_0";
        Authority = simulate && Blocker == null && !TerminalHold
            && (OpenedManifest?.Mode != NavalLabMode.SingleClientNative || DeploymentComplete);
    }
    public MatrixFrame[] ReadFrames() => (MatrixFrame[])Frames.Clone();
    public bool ApplyFrames(MatrixFrame[] frames)
    {
        ApplyCount++;
        Calls.Add("apply");
        if (ThrowOnApply) throw new InvalidOperationException("simulated frame callback failure");
        if (FailApply) return false;
        Frames = (MatrixFrame[])frames.Clone();
        return true;
    }
    public void SetHelm(int ship, float rudder, bool row) => HelmCalls.Add((ship, rudder, row));
    public string SetHeldHelm(int ship, bool take)
    {
        HeldHelmCalls.Add((ship, take));
        if (take == HeldHelmTaken) return take ? "already_taken" : "already_released";
        HeldHelmTaken = take;
        return take ? "taken" : "released";
    }
    public string StartAgentControl(string kind, int ship, float value)
    {
        AgentControlCalls.Add((kind, ship, value));
        return "applied";
    }
    public void TickAgentControl(float dt) => Calls.Add("tick-control");
    public void CancelControls()
    {
        CancelCount++;
        Calls.Add("cancel");
        if (ThrowOnCancel) throw new InvalidOperationException("simulated control cancellation failure");
        HeldHelmTaken = false;
    }
    public object Inspect() => new { simulated = true, authority = Authority, applyCount = ApplyCount };
    public Action<NetworkNavalLabHelmInput>? SendInput { get; private set; }
    public Func<bool>? InputAuthority { get; private set; }
    public List<NetworkNavalLabHelmInput> NativeInputs { get; } = new();
    public List<int> Neutralized { get; } = new();
    public Dictionary<int, NetworkNavalLabStations> Stations { get; } = new();
    public int StationApplyCalls { get; private set; }
    public bool ThrowOnStations { get; set; }
    public bool MissingOccupancy { get; set; }
    public void ConfigureNative(Func<bool> authority, Action<NetworkNavalLabHelmInput> sendInput)
    { InputAuthority = authority; SendInput = sendInput; }
    public NetworkNavalLabStations CreateStations()
    {
        Assert.True(DeploymentComplete);
        int slot = Array.IndexOf(OpenedManifest!.Controllers, ownControllerId);
        return new NetworkNavalLabStations(OpenedManifest.IncarnationId, 1, slot, "offer",
            OpenedManifest.Combatants.Skip((slot * 5) + 1).Take(4).ToArray(), new[] { "oar-left-0/pilot", "oar-right-0/pilot", "oar-left-1/pilot", "oar-right-1/pilot" });
    }
    public void ApplyStations(NetworkNavalLabStations stations)
    {
        Assert.True(DeploymentComplete);
        if (Stations.ContainsKey(stations.Ship)) return;
        StationApplyCalls++;
        if (ThrowOnStations) throw new InvalidOperationException("simulated partial station apply failure");
        Stations.Add(stations.Ship, stations);
    }
    public bool ObserveStations(NetworkNavalLabStations stations) => !MissingOccupancy && Stations.ContainsKey(stations.Ship);
    public void ApplyNativeInput(NetworkNavalLabHelmInput input)
    { Assert.True(FactoryHost); Assert.True(InputAuthority!()); NativeInputs.Add(input); }
    public void NeutralizeNativeInput(int ship) { Assert.True(FactoryHost); Neutralized.Add(ship); }
    public void Dispose() => Disposed = true;
}
#endif
