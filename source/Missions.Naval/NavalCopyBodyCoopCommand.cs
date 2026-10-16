#if DEBUG
using Common.Commands;
using Missions.Battles;
using Newtonsoft.Json;
using System;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// coop.debug.naval.copy_body all steered
// coop.debug.naval.copy_body 1d93 steered deadzone=0.5 clamp=3
// coop.debug.naval.copy_body all kinematic
/// <summary>
/// DEBUG: switches how other owners' hulls exist on this client: kinematic (the default copy), steered (option B: the copy
/// keeps its dynamic body, replays the owner's helm input and is pushed toward the owner's samples by velocity) or static
/// (option C: a frame-written copy with a moveable static collider). Own hulls are refused; nothing about a copy is sent.
/// </summary>
public sealed class NavalCopyBodyCoopCommand : ICoopCommand
{
    private const int MaxKnobs = 8;

    public string Prefix => "coop.debug.naval";

    public string Name => "copy_body";

    public string Description => "Switches other owners' hull copies on this client between kinematic, steered and static. " +
        "steered only applies to copies that spawn after it, so run it during deployment.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("shipId-prefix|all", "The start of a copy's ship id, as coop.debug.naval.inspect lists it, or all.", true),
            new ExpectedArgs("mode", "kinematic, steered or static.", true),
        }
        .Concat(Enumerable.Range(1, MaxKnobs).Select(index =>
            (IExpectedArgs)new ExpectedArgs($"knob_{index}", "A steered key=value knob: " + CopySteeringKnobs.KnobNames + ".", false)))
        .ToArray();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        var controller = Mission.Current?.GetMissionBehavior<CoopBattleController>();
        if (controller == null || !CoopNavalMissionScope.IsActive) return Failed("No active coop naval battle mission.");

        var error = CopyBodyRequest.TryParse(args?.ToArray(), out var request);
        if (error != null) return Failed(error);

        var own = controller.Session.OwnControllerId;
        var ships = controller.MissionComponent.ShipRegistry.Ships
            .Select(ship => (ship.ShipId, IsOwn: ship.CurrentAuthority == own))
            .ToList();
        error = request.ResolveCopies(ships, out var copies);
        if (error != null) return Failed(error);

        NavalCopyBodies.Table.AddRule(request);
        NavalCopyBodies.Table.ReconcileAll();

        var state = NavalCopyBodies.Table.Entries
            .Where(entry => copies.Contains(entry.Value.ShipId))
            .Select(entry => new
            {
                shipId = entry.Value.ShipId,
                mode = entry.Value.Mode.ToString(),
                blocked = entry.Value.Blocked,
            })
            .ToArray();
        return new CoopCommandResult(true, "NAVAL_COPY_BODY " + JsonConvert.SerializeObject(new
        {
            target = request.Target,
            mode = request.Mode.ToString(),
            knobs = request.Mode == CopyBodyMode.Steered ? request.Knobs.ToString() : null,
            registeredCopies = copies.Count,
            copies = state,
            note = copies.Count == 0 ? "no registered copy matches yet; the rule applies to copies that spawn later" : null,
        }));
    }

    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");
}
#endif
