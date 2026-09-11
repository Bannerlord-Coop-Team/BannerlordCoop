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
        if (!IsTwoClientNative || state < 0 || state > 2 || view == null || !HasNativeInputPermission(view) || !view.GetCanToggleSail())
            return "rejected:owner_helm_or_input_unavailable";
        view.SailControl = (SailInput)state;
        nextNativeInput = 0;
        RouteNativeAxes(view);
        return "requested:synthetic_input_not_keyboard_evidence";
    }

    internal object InspectSailStatus()
    {
        if (!IsTwoClientNative) return new { unavailable = "wrong_mode" };
        bool ready = !factoryTerminal && CanUseNativeControls;
        var view = Mission?.GetMissionBehavior<MissionGauntletShipControlView>();
        bool fresh = ready && sailFeedback != null && ControlNow < sailFeedbackDeadline;
        bool ownerView = view?._dataSource != null && view._playerControlledShip == LocalShip && GetLocalControlledShip() == LocalShip;
        return new
        {
            manifest.IncarnationId, owner = ownControllerId, ship = OwnSlot,
            shipId = OwnSlot >= 0 ? (Guid?)manifest.Ships[OwnSlot] : null, electedSimulator = factoryHost,
            ready, terminal = factoryTerminal, blocked = Blocker != null, feedbackFresh = fresh, ownerView,
            requested = view == null ? (int?)null : (int)view.SailControl,
            hostObserved = ready && factoryHost ? ReadSailStates() : null,
            hostReceivedInput = factoryHost ? lastReceivedNativeInput.Select(input => input == null ? null : new
            { input.Ship, input.Sequence, input.Sail, input.DeadlineUtcTicks }).ToArray() : null,
            received = fresh ? sailFeedback : null, sequence = sailFeedbackSequence,
            remainingSeconds = fresh ? Math.Max(0, sailFeedbackDeadline - ControlNow) : 0,
            presentation = view?._dataSource?.SailState, presentationType = view?._dataSource?.SailType,
            unavailable = factoryHost ? (ready ? null : "not_ready") : fresh ? (ownerView ? null : "owner_helm_or_view_unavailable") : "missing_stale_or_not_ready",
            unavailableLabelVisible = sailUnavailableLabel?.IsVisible == true,
            nativeInputSequence, lastHelmPermission
        };
    }

    private NetworkNavalLabSailState sailFeedback;
    private long sailFeedbackSequence;
    private double sailFeedbackDeadline;
    private MissionGauntletShipControlView sailPresentationView;
    private TextWidget sailUnavailableLabel;

    internal NetworkNavalLabSailState[] ReadSailStates()
    {
        if (!IsTwoClientNative || !factoryHost || factoryTerminal || !CanUseNativeControls) return null;
        var states = Ships.Select((ship, slot) => ReadSailState(ship, manifest.Ships[slot])).ToArray();
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

    internal void ApplySailFeedback(NetworkNavalLabFrames frames)
    {
        ClearSailFeedback();
        long now = DateTime.UtcNow.Ticks;
        if (!IsTwoClientNative || factoryHost || factoryTerminal || !CanUseNativeControls
            || frames.IncarnationId != manifest.IncarnationId || frames.Epoch != 1
            || frames.Sequence <= sailFeedbackSequence || frames.SailStates == null || frames.SailStates.Length != 2
            || frames.SailDeadlineUtcTicks <= now || frames.SailDeadlineUtcTicks > now + TimeSpan.TicksPerSecond
            || OwnSlot < 0 || OwnSlot >= 2) return;
        for (int i = 0; i < 2; i++)
            if (frames.SailStates[i] != null && (!frames.SailStates[i].IsValid || frames.SailStates[i].ShipId != manifest.Ships[i])) return;
        sailFeedbackSequence = frames.Sequence;
        sailFeedback = frames.SailStates[OwnSlot];
        sailFeedbackDeadline = ControlNow + TimeSpan.FromTicks(frames.SailDeadlineUtcTicks - now).TotalSeconds;
    }

    internal void ClearSailFeedback()
    {
        sailFeedback = null;
        sailFeedbackDeadline = 0;
    }

    internal void UpdateSailPresentation(MissionGauntletShipControlView view)
    {
        if (!IsTwoClientNative || view.Mission != Mission || view._dataSource == null) return;
        if (factoryHost) { DetachSailPresentation(view); return; }
        AttachSailPresentation(view);
        if (factoryTerminal || !CanUseNativeControls || ControlNow >= sailFeedbackDeadline) ClearSailFeedback();
        bool available = sailFeedback != null && view._playerControlledShip == LocalShip && GetLocalControlledShip() == LocalShip;
        var vm = view._dataSource;
        // SetSailState maintains its own visual cache; use it for both invalidation and restoration.
        vm.SetSailState(available ? (SailInput)sailFeedback.State : (SailInput)(-1));
        vm.SailType = available ? sailFeedback.Type : -1;
        if (sailUnavailableLabel != null)
            sailUnavailableLabel.IsVisible = !available && (view._playerControlledShip != null || factoryTerminal);
    }

    private void AttachSailPresentation(MissionGauntletShipControlView view)
    {
        var context = view._gauntletLayer?.UIContext;
        if (context?.Root == null) return;
        if (sailPresentationView == view && sailUnavailableLabel?.ParentWidget == context.Root) return;
        if (sailPresentationView != null) DetachSailPresentation(sailPresentationView);
        sailUnavailableLabel = new TextWidget(context)
        {
            Text = new TextObject("{=coop_lab_sail_unavailable}Sail status unavailable").ToString(),
            WidthSizePolicy = SizePolicy.Fixed, HeightSizePolicy = SizePolicy.Fixed,
            SuggestedWidth = 300, SuggestedHeight = 30,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
            MarginRight = 55, MarginBottom = 310, DoNotAcceptEvents = true, IsFocusable = false,
            Brush = context.GetBrush("Naval.Mission.ShipControl.Key.Description"), IsVisible = false
        };
        context.Root.AddChild(sailUnavailableLabel);
        sailPresentationView = view;
    }

    internal void DetachSailPresentation(MissionGauntletShipControlView view)
    {
        if (view != sailPresentationView) return;
        sailUnavailableLabel?.ParentWidget?.RemoveChild(sailUnavailableLabel);
        sailUnavailableLabel = null;
        sailPresentationView = null;
        ClearSailFeedback();
    }
}
#endif
