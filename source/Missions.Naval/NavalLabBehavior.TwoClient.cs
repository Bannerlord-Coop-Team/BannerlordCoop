#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Missions.Battles;
using Missions.Messages;
using NavalDLC.Missions;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using NavalDLC.Missions.ShipControl;
using NavalDLC.Missions.ShipInput;
using NavalDLC.View.MissionViews;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;

namespace Missions.Naval;

internal sealed partial class NavalLabBehavior
{
    internal int OwnSlot => Array.IndexOf(manifest.Controllers, ownControllerId);
    internal MissionShip LocalShip => OwnSlot >= 0 && OwnSlot < Ships.Length ? Ships[OwnSlot] : null;
    internal Agent LocalCaptain => OwnSlot >= 0 && (OwnSlot * NavalLabManifest.CrewPerShip) < Agents.Length
        ? Agents[OwnSlot * NavalLabManifest.CrewPerShip] : null;
    internal Action<NetworkNavalLabHelmInput> SendNativeInput;
    private long nativeInputSequence;
    private readonly NetworkNavalLabHelmInput[] lastReceivedNativeInput = new NetworkNavalLabHelmInput[2];
    private double nextNativeInput;
    private bool lastHelmPermission;
    private readonly Dictionary<int, NetworkNavalLabStations> appliedStations = new();
    internal bool CanPrepareTwoClientDeployment => IsTwoClientNative && factoryMaterialized && factoryReleased
        && !factoryTerminal && factoryAuthorityValid?.Invoke() == true;

    internal bool OwnsFixedStationOrder(ShipOrder order) => IsTwoClientNative && order?._ownerShip?.ShipOrigin is NavalLabShipOrigin
        && order._ownerShip.ShipsLogic?.Mission == Mission;

    internal MissionShip GetLocalControlledShip()
    {
        var captain = LocalCaptain;
        var point = LocalShip?.ShipControllerMachine?.PilotStandingPoint;
        return nativeDeploymentComplete && !factoryTerminal && captain != null && point != null
            && captain == Mission.MainAgent && captain.IsPlayerControlled
            && captain.CurrentlyUsedGameObject == point && point.UserAgent == captain ? LocalShip : null;
    }

    private bool HasNativeInputPermission(MissionShipControlView view) => CanUseNativeControls && GetLocalControlledShip() != null
            && view.ControllerMachine == LocalShip.ShipControllerMachine && view.MissionScreen != null
            && ScreenManager._isWindowFocused && ScreenManager.TopScreen == view.MissionScreen
            && !view.MissionScreen.IsCheatGhostMode && !view.MissionScreen.IsPhotoModeEnabled && !view.IsDisplayingADialog;

    internal void RouteNativeAxes(MissionShipControlView view)
    {
        bool permission = HasNativeInputPermission(view);
        var input = ShipInputRecord.Stop();
        if (permission)
        {
            var axes = new Vec2(view.Input.GetGameKeyAxis("MovementAxisX"), view.Input.GetGameKeyAxis("MovementAxisY"));
            if (Math.Abs(axes.x) <= 0.2f) axes.x = 0;
            if (Math.Abs(axes.y) <= 0.2f) axes.y = 0;
            view.TickRowerInput(axes, out var longitudinal, out var doubleTap, out var lateral);
            input = new ShipInputRecord(lateral, longitudinal, doubleTap, view.TickRudderInput(axes), view.SailControl);
        }
        if (ControlNow < nextNativeInput && permission == lastHelmPermission) return;
        nextNativeInput = ControlNow + 0.05;
        lastHelmPermission = permission;
        SendNativeInput?.Invoke(new NetworkNavalLabHelmInput(manifest.IncarnationId, 1, OwnSlot, ++nativeInputSequence,
            DateTime.UtcNow.AddSeconds(1).Ticks, permission, (int)input.RowerLateral, (int)input.RowerLongitudinal,
            (int)input.RowerLongitudinalDoubleTap, input.RudderLateral, (int)input.Sail));
    }

    internal void ApplyNativeInput(NetworkNavalLabHelmInput input)
    {
        if (!IsTwoClientNative || !CanUseNativeControls || !factoryHost || !input.IsValid) return;
        var record = new ShipInputRecord((RowerLateralInput)input.Lateral, (RowerLongitudinalInput)input.Longitudinal,
            (RowerLongitudinalInput)input.DoubleTap, input.Rudder, (SailInput)input.Sail);
        Ships[input.Ship].PlayerController.SetInput(in record);
        lastReceivedNativeInput[input.Ship] = input;
    }

    internal void NeutralizeNativeInput(int slot)
    {
        if (!IsTwoClientNative || !factoryHost || slot < 0 || slot >= Ships.Length || Ships[slot]?.Controller is not PlayerShipController player) return;
        var stop = ShipInputRecord.Stop();
        player.SetInput(in stop);
    }

    private string StationKey(ShipOarMachine machine, MissionShip ship)
    {
        // Named child paths are content identities, never process-local native pointers.
        var entity = machine.PilotStandingPoint.GameEntity;
        var parts = new List<string>();
        while (entity.IsValid && entity != ship.GameEntity && parts.Count < 16)
        {
            var parent = entity.Parent;
            if (!parent.IsValid) throw new InvalidOperationException("native.station_outside_hull");
            int index = parent.GetChildren().ToList().IndexOf(entity);
            if (index < 0) throw new InvalidOperationException("native.station_child_missing");
            parts.Add(index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + entity.Name);
            entity = parent;
        }
        if (!entity.IsValid || entity != ship.GameEntity) throw new InvalidOperationException("native.station_outside_hull");
        parts.Reverse();
        return string.Join("/", parts);
    }

    private Dictionary<string, ShipOarMachine> StationInventory(int slot)
    {
        if (!IsTwoClientNative || !nativeDeploymentComplete || factoryTerminal || Blocker != null
            || slot < 0 || slot >= Ships.Length) throw new InvalidOperationException("native.station_lifecycle");
        var ship = Ships[slot];
        if (!ship.IsDeployed || ship.ShipOrigin is not NavalLabShipOrigin || ship.ShipOrigin.Hull != hull)
            throw new InvalidOperationException("native.station_hull");
        var result = new Dictionary<string, ShipOarMachine>(StringComparer.Ordinal);
        foreach (var machine in ship.ShipOarMachines)
        {
            var point = machine.PilotStandingPoint;
            if (machine.GetType() != typeof(ShipOarMachine) || point == null || point.GetType() != typeof(StandingPoint) || !point.GameEntity.IsValid
                || point.GetComponent<ResetAnimationOnStopUsageComponent>() == null || machine._oar == null)
                throw new InvalidOperationException("native.station_prerequisite");
            var key = StationKey(machine, ship);
            if (string.IsNullOrWhiteSpace(key) || key.Length > 256 || result.ContainsKey(key))
                throw new InvalidOperationException("native.ambiguous_station_identity");
            result.Add(key, machine);
        }
        if (result.Count < 4) throw new InvalidOperationException("native.insufficient_oar_stations");
        return result;
    }

    internal NetworkNavalLabStations CreateStations()
    {
        StationInventory(OwnSlot);
        if (LocalShip.LeftSideShipOarMachines.Count < 2 || LocalShip.RightSideShipOarMachines.Count < 2)
            throw new InvalidOperationException("native.insufficient_paired_oars");
        // Fixed initial crew only; automatic weapon/helm detachment allocation is disabled in this mode.
        return new NetworkNavalLabStations(manifest.IncarnationId, 1, OwnSlot, "offer",
            manifest.Combatants.Skip((OwnSlot * 5) + 1).Take(4).ToArray(),
            LocalShip.LeftSideShipOarMachines.Take(2).Concat(LocalShip.RightSideShipOarMachines.Take(2))
                .Select(machine => StationKey(machine, LocalShip)).ToArray());
    }

    internal void ApplyStations(NetworkNavalLabStations value)
    {
        var inventory = StationInventory(value.Ship);
        if (value.IncarnationId != manifest.IncarnationId || value.Epoch != 1 || value.Phase != "commit"
            || value.Combatants == null || value.Keys == null || value.Keys.Length != 4 || value.Combatants.Length != 4
            || value.Keys.Distinct().Count() != 4
            || !value.Combatants.SequenceEqual(manifest.Combatants.Skip((value.Ship * 5) + 1).Take(4)))
            throw new InvalidOperationException("native.invalid_station_commit");
        if (appliedStations.TryGetValue(value.Ship, out var prior))
        {
            if (!prior.Keys.SequenceEqual(value.Keys) || !ObserveStations(prior)) throw new InvalidOperationException("native.conflicting_station_commit");
            return;
        }
        for (int i = 0; i < 4; i++)
        {
            var agent = Agents[(value.Ship * 5) + i + 1];
            if (!inventory.TryGetValue(value.Keys[i], out var machine) || agent == null || !agent.IsActive()
                || agent.Formation != Ships[value.Ship].Formation || agent.CurrentlyUsedGameObject != null
                || (manifest.Controllers[value.Ship] == ownControllerId ? !agent.IsAIControlled : agent.Controller != AgentControllerType.None)
                || machine.PilotStandingPoint.UserAgent != null || machine.PilotStandingPoint.MovingAgent != null
                || machine.PilotStandingPoint.IsDeactivated || machine._oar.OwnerShip != Ships[value.Ship])
                throw new InvalidOperationException("native.occupied_or_invalid_station");
        }
        appliedStations.Add(value.Ship, value);
        for (int i = 0; i < 4; i++)
        {
            var machine = inventory[value.Keys[i]];
            var agent = Agents[(value.Ship * 5) + i + 1];
            agent.UseGameObject(machine.PilotStandingPoint);
            machine.OnPilotAssignedDuringSpawn();
        }
        if (!ObserveStations(value)) throw new InvalidOperationException("native.station_apply_not_observed");
    }

    internal bool ObserveStations(NetworkNavalLabStations value)
    {
        var inventory = StationInventory(value.Ship);
        for (int i = 0; i < 4; i++)
        {
            if (!inventory.TryGetValue(value.Keys[i], out var machine)) return false;
            var agent = Agents[(value.Ship * 5) + i + 1];
            if (agent == null || !agent.IsActive()
                || (manifest.Controllers[value.Ship] == ownControllerId ? !agent.IsAIControlled : agent.Controller != AgentControllerType.None)
                || machine.PilotAgent != agent || machine.PilotStandingPoint.UserAgent != agent
                || agent.CurrentlyUsedGameObject != machine.PilotStandingPoint || !machine._isPilotSitting || machine._lastPilotAgent != agent)
                return false;
        }
        return true;
    }
}
#endif
