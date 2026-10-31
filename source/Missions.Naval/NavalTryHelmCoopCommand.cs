#if DEBUG
using Common.Commands;
using Missions.Battles;
using NavalDLC.Missions.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// coop.debug.naval.try_helm 72da8e4a
// coop.debug.naval.try_helm 72da8e4a force
/// <summary>
/// DEBUG: has the main agent try a hull's helm the way the use key does (vanilla's CanUseObject gate, then HandleStartUsingAction),
/// and reports whether it got the helm, so the co-op block on copy helms (an enemy copy's goes through, to capture it) can be checked.
/// </summary>
public sealed class NavalTryHelmCoopCommand : ICoopCommand
{
    internal const string Label = "NAVAL_TRY_HELM";
    internal const string ForceArg = "force";

    public string Prefix => "coop.debug.naval";

    public string Name => "try_helm";

    public string Description => "Has the main agent try a hull's helm as the use key would and reports whether it was allowed; " +
        "an allowed seat is released again.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("shipId-prefix", "The start of the hull's ship id, as coop.debug.naval.inspect lists it.", true),
        new ExpectedArgs(ForceArg, "Skips vanilla's own gates (CanUseObject, a free seat) and calls the use action anyway.", false),
    };

    // Resolves the hull, then tries its helm.
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!TryParse(args, out var prefix, out bool force, out var error)) return Failed(error);

        var controller = Mission.Current?.GetMissionBehavior<CoopBattleController>();
        if (controller == null || Mission.Current.GetMissionBehavior<CoopNavalBattleBehavior>() == null)
            return Failed("No active coop naval battle mission.");

        var registry = controller.MissionComponent.ShipRegistry;
        error = NavalReswapCoopCommand.TryResolveShipId(registry.Ships.Select(ship => ship.ShipId), prefix, out var shipId);
        if (error != null) return Failed(error);
        if (!registry.TryGet(shipId, out var info) || info.Hull is not MissionShip hull) return Failed("The hull is not a naval ship.");

        return TryHelm(hull, shipId, Agent.Main, force);
    }

    /// <summary>The hull id prefix and whether <c>force</c> was given; false with the usage when the arguments are malformed.</summary>
    internal static bool TryParse(IReadOnlyList<string> args, out string prefix, out bool force, out string error)
    {
        prefix = null;
        force = false;
        error = null;
        bool forceValid = args != null && (args.Count == 1 || (args.Count == 2 && string.Equals(args[1], ForceArg, StringComparison.OrdinalIgnoreCase)));
        if (!forceValid || string.IsNullOrWhiteSpace(args[0]))
        {
            error = "Usage: coop.debug.naval.try_helm <shipId-prefix> [force]";
            return false;
        }

        prefix = args[0].Trim();
        force = args.Count == 2;
        return true;
    }

    // [Game thread] Checks vanilla's gates as the interaction component does, then starts the use as the use key does; the co-op
    // block sits in HandleStartUsingAction (ForeignHelmUsePatch), so a refused start leaves the main agent off the helm.
    private static CoopCommandResult TryHelm(MissionShip hull, Guid shipId, Agent main, bool force)
    {
        if (main == null || !main.IsActive()) return Failed("There is no living main agent.");

        var point = hull.ShipControllerMachine?.PilotStandingPoint;
        if (point == null) return Failed("The hull has no helm.");

        bool canUse = main.CanUseObject(point);
        bool vacant = main.ObjectHasVacantPosition(point);
        bool ableToUse = main.IsAbleToUseMachine() && !main.IsUsingGameObject;
        string facts = Label + " ship=" + shipId + " enemy=" + (main.Team != null && hull.BattleSide != main.Team.Side) +
            " copy=" + NavalForeignHulls.Contains(hull) + " canUseObject=" + canUse + " vacant=" + vacant + " ableToUse=" + ableToUse;
        if (!force && !(canUse && vacant && ableToUse))
            return Succeeded(facts + " result=REFUSED_BY_VANILLA (the use key would not offer it; add force to start the use anyway)");

        if (main.IsUsingGameObject) main.StopUsingGameObject(isSuccessful: true, Agent.StopUsingGameObjectFlags.None);
        main.HandleStartUsingAction(point, -1);
        if (main.CurrentlyUsedGameObject != point) return Succeeded(facts + " result=BLOCKED (the main agent did not take the helm)");

        main.StopUsingGameObject(isSuccessful: true, Agent.StopUsingGameObjectFlags.None);
        return Succeeded(facts + " result=ALLOWED (the main agent took the helm; released it again)");
    }

    // A successful command result.
    private static CoopCommandResult Succeeded(string output) => new CoopCommandResult(true, output);

    // A failed command result.
    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");
}
#endif
