#if DEBUG
using System;
using System.Linq;
using Missions.Messages;
using NavalDLC.GauntletUI.MissionViews;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.ShipActuators;
using NavalDLC.Missions.ShipInput;
using TaleWorlds.Core;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Localization;

namespace Missions.Naval;

internal sealed partial class NavalLabBehavior
{
    internal string RequestSail(int state)
    {
        var view = Mission?.GetMissionBehavior<MissionGauntletShipControlView>();
        if (state < 0 || state > 2 || view == null || !HasNativeInputPermission(view) || !view.GetCanToggleSail())
            return "rejected:owner_helm_or_input_unavailable";
        view.SailControl = (SailInput)state;
        RouteNativeAxes(view);
        return "requested:synthetic_input_not_keyboard_evidence";
    }

    internal object InspectSailStatus()
    {
        bool ready = !terminal && CanUseNativeControls;
        var view = Mission?.GetMissionBehavior<MissionGauntletShipControlView>();
        bool ownerView = view?._dataSource != null && view._playerControlledShip == LocalShip && GetLocalControlledShip() == LocalShip;
        return new
        {
            manifest.IncarnationId, owner = ownControllerId, ship = OwnSlot,
            shipId = OwnSlot >= 0 ? (Guid?)manifest.Ships[OwnSlot] : null, electedSimulator = factoryHost,
            ready, terminal = terminal, blocked = Blocker != null, ownerView,
            requested = view == null ? (int?)null : (int)view.SailControl,
            ownerObserved = ready ? ReadSailStates() : null,
            ownerReceivedInput = lastReceivedNativeInput.Select(input => input == null ? null : new
            { input.Ship, input.Sequence, input.Sail, input.DeadlineUtcTicks }).ToArray(),
            presentation = view?._dataSource?.SailState, presentationType = view?._dataSource?.SailType,
            unavailable = ready ? null : "not_ready", presentationSource = "owned_native",
            nativeInputSequence, lastHelmPermission
        };
    }

    internal NetworkNavalLabSailState[] ReadSailStates()
    {
        if (terminal || !CanUseNativeControls) return null;
        var states = new[] { ReadSailState(LocalShip, manifest.Ships[OwnSlot]) };
        return states.Any(state => state == null) ? null : states;
    }

    private NetworkNavalLabSailState ReadSailState(MissionShip ship, Guid id)
    {
        bool lateen = false, square = false, lateenFull = true, squareFull = true;
        if (ship?.Sails == null) return null;
        foreach (var sail in ship.Sails)
        {
            if (sail?.SailObject == null || float.IsNaN(sail.TargetSailSetting) || float.IsInfinity(sail.TargetSailSetting)) return null;
            // Observe the actuator target used by the native HUD, never the requested input record.
            if (sail.SailObject.Type == SailType.Lateen)
            { lateen = true; lateenFull &= sail.TargetSailSetting > 0; }
            else if (sail.SailObject.Type == SailType.Square)
            { square = true; squareFull &= sail.TargetSailSetting > 0; }
            else return null;
        }
        if (!lateen && !square) return null;
        int type = lateen ? (square ? 2 : 1) : 0;
        var state = lateen && square
            ? (lateenFull && squareFull ? SailInput.Full : !lateenFull && !squareFull ? SailInput.Raised : SailInput.SquareSailsRaised)
            : (lateen ? lateenFull : squareFull) ? SailInput.Full : SailInput.Raised;
        return new NetworkNavalLabSailState(id, (int)state, type);
    }
}
#endif
