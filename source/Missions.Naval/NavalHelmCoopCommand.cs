#if DEBUG
using Common.Commands;
using HarmonyLib;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.ShipControl;
using NavalDLC.Missions.ShipInput;
using NavalDLC.View.MissionViews;
using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// coop.debug.naval.helm 0 1 1 10
/// <summary>DEBUG: drives the local player's own hull with keyboard-equivalent helm input for a few seconds.</summary>
public sealed class NavalHelmCoopCommand : ICoopCommand
{
    internal const int DefaultSeconds = 5;
    internal const int MaxSeconds = 30;

    private readonly INavalDebugHulls hulls;

    public NavalHelmCoopCommand(INavalDebugHulls hulls)
    {
        this.hulls = hulls;
    }

    public string Prefix => "coop.debug.naval";

    public string Name => "helm";

    public string Description => "Steers, rows and sets the sail of this client's own hull as the keyboard would, for a few seconds.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("rudder", "Steering axis from -1 (left) to 1 (right), as the A/D keys.", true),
        new ExpectedArgs("row", "Rowing axis from -1 (backward) to 1 (forward), as the S/W keys.", true),
        new ExpectedArgs("sail", "0 raises the sails, 1 sets them full.", true),
        new ExpectedArgs("seconds", $"How long to hold the input, 1 to {MaxSeconds}; {DefaultSeconds} when omitted.", false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!TryParse(args, out var request, out var error)) return Failed(error);

        error = hulls.TryGetOwnHull(out var hull);
        if (error != null) return Failed(error);
        if (!hull.IsPlayerControlled || hull.Controller is not PlayerShipController)
            return Failed("The own hull is not player controlled; take its helm first.");
        if (Agent.Main == null || hull.ShipControllerMachine?.PilotAgent != Agent.Main)
            return Failed("The main agent is not at the own hull's helm.");

        var record = KeyboardRecord(request.Rudder, request.Row, request.Sail);
        NavalHelmOverride.Start(hull, record, request.Seconds);
        return new CoopCommandResult(true, string.Format(CultureInfo.InvariantCulture,
            "NAVAL_HELM ship={0} rudder={1} rowerLongitudinal={2} rowerLateral={3} sail={4} seconds={5}",
            hulls.ShipIdOf(hull), record.RudderLateral, record.RowerLongitudinal, record.RowerLateral, record.Sail, request.Seconds));
    }

    internal readonly struct HelmRequest
    {
        public HelmRequest(float rudder, float row, int sail, int seconds)
        {
            Rudder = rudder;
            Row = row;
            Sail = sail;
            Seconds = seconds;
        }

        public float Rudder { get; }
        public float Row { get; }
        public int Sail { get; }
        public int Seconds { get; }
    }

    internal static bool TryParse(IReadOnlyList<string> args, out HelmRequest request, out string error)
    {
        request = default;
        error = null;
        if (args == null || args.Count < 3 || args.Count > 4)
            error = "Usage: coop.debug.naval.helm <rudder -1..1> <row -1..1> <sail 0|1> [seconds 1..30]";
        else if (!TryParseAxis(args[0], out float rudder))
            error = "rudder must be a number from -1 to 1.";
        else if (!TryParseAxis(args[1], out float row))
            error = "row must be a number from -1 to 1.";
        else if (args[2] != "0" && args[2] != "1")
            error = "sail must be 0 or 1.";
        else
        {
            int seconds = DefaultSeconds;
            if (args.Count == 4 && (!int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds)
                    || seconds < 1 || seconds > MaxSeconds))
            {
                error = $"seconds must be a whole number from 1 to {MaxSeconds}.";
                return false;
            }

            request = new HelmRequest(rudder, row, args[2] == "1" ? 1 : 0, seconds);
            return true;
        }

        return false;
    }

    private static bool TryParseAxis(string value, out float axis) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out axis)
        && !float.IsNaN(axis) && axis >= -1f && axis <= 1f;

    /// <summary>
    /// The record MissionShipControlView.HandleShipControls builds from the movement axes (x = rudder, y = row),
    /// without its double-tap boost.
    /// </summary>
    internal static ShipInputRecord KeyboardRecord(float rudder, float row, int sail)
    {
        float x = Math.Abs(rudder) <= 0.2f ? 0f : rudder;
        float y = Math.Abs(row) <= 0.2f ? 0f : row;
        var axes = new Vec2(x, y);
        int longitudinal = 0;
        int lateral = 0;
        if (axes.LengthSquared > 0f)
        {
            axes.Normalize();
            float degrees = MBMath.ToDegrees(axes.RotationInRadians);
            bool right = degrees < 0f;
            degrees = Math.Abs(degrees);
            if (degrees <= 22.5f) longitudinal = 1;
            else if (degrees <= 67.5f) { longitudinal = 1; lateral = 1; }
            else if (degrees <= 112.5f) lateral = 1;
            else if (degrees < 157.5f) { longitudinal = -1; lateral = 1; }
            else longitudinal = -1;
            if (right) lateral = -lateral;
        }

        float rudderLateral = Math.Min(Math.Abs(x) * 1.4f, 1f) * Math.Sign(x);
        return new ShipInputRecord((RowerLateralInput)lateral, (RowerLongitudinalInput)longitudinal, RowerLongitudinalInput.None,
            rudderLateral, sail == 1 ? SailInput.Full : SailInput.Raised);
    }

    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");
}

/// <summary>
/// [Game thread] The DEBUG helm input held on the own hull. Static because the control view patch reads it; it
/// lasts for its seconds and then the keyboard input (neutral without a human) applies again.
/// </summary>
internal static class NavalHelmOverride
{
    private static MissionShip ship;
    private static ShipInputRecord record;
    private static float remaining;

    internal static void Start(MissionShip hull, ShipInputRecord input, float seconds)
    {
        ship = hull;
        record = input;
        remaining = seconds;
    }

    internal static void Clear()
    {
        ship = null;
        remaining = 0f;
    }

    internal static void Apply(MissionShip playerShip, float dt)
    {
        if (ship == null || playerShip != ship) return;
        if (remaining <= 0f || !ship.IsPlayerControlled)
        {
            Clear();
            return;
        }

        remaining -= dt;
        ship.PlayerController.SetInput(in record);
    }
}

// Writes the held input through the keyboard's own seam, right after the control view wrote the keyboard's record.
[HarmonyPatch(typeof(MissionShipControlView), "HandleShipControls")]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class NavalHelmOverridePatch
{
    [HarmonyPostfix]
    private static void Postfix(MissionShipControlView __instance, float dt) =>
        NavalHelmOverride.Apply(__instance.NavalShipsLogic?.PlayerControlledShip, dt);
}
#endif
