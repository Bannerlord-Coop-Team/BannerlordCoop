#if DEBUG
using Common.Commands;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Missions.Naval;

// coop.debug.naval.copy_steer
// coop.debug.naval.copy_steer softzone=1 tau=0.3 lead=0.15
/// <summary>
/// DEBUG: shows or tunes this client's steered copy knobs; every copy reads them on its next physics step, and they hold
/// across battles until this client leaves the session.
/// </summary>
public sealed class NavalCopySteerCoopCommand : ICoopCommand
{
    private const int MaxKnobs = 7;

    private readonly CopySteeringKnobs knobs;

    /// <summary>A command over this client's shared copy steering knobs.</summary>
    public NavalCopySteerCoopCommand(CopySteeringKnobs knobs)
    {
        this.knobs = knobs;
    }

    public string Prefix => "coop.debug.naval";

    public string Name => "copy_steer";

    public string Description => "Shows or sets the steering knobs of other owners' hull copies on this client (" +
        CopySteeringKnobs.KnobNames + "); without arguments it shows them.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = Enumerable.Range(1, MaxKnobs)
        .Select(index => (IExpectedArgs)new ExpectedArgs($"knob_{index}", "A key=value knob: " + CopySteeringKnobs.KnobNames + ".", false))
        .ToArray();

    // Applies every key=value argument to the client's shared knobs, all or none, and echoes the result.
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        var error = Validate(args?.ToArray() ?? Array.Empty<string>());
        if (error != null) return Failed(error);

        foreach (var (key, value) in Split(args?.ToArray() ?? Array.Empty<string>()))
            knobs.TryApply(key, value);

        return new CoopCommandResult(true, "NAVAL_COPY_STEER " + knobs);
    }

    /// <summary>Why the arguments can't apply, checked on a scratch copy so a bad knob changes nothing; null when they can.</summary>
    internal static string Validate(IReadOnlyList<string> args)
    {
        var scratch = new CopySteeringKnobs();
        foreach (var arg in args)
        {
            int separator = arg?.IndexOf('=') ?? -1;
            if (separator <= 0) return $"Expected key=value, got {arg}; knobs: {CopySteeringKnobs.KnobNames}.";

            var error = scratch.TryApply(arg.Substring(0, separator), arg.Substring(separator + 1));
            if (error != null) return error;
        }

        return null;
    }

    // The key and value of each key=value argument.
    private static IEnumerable<(string Key, string Value)> Split(IEnumerable<string> args) =>
        args.Select(arg => (arg.Substring(0, arg.IndexOf('=')), arg.Substring(arg.IndexOf('=') + 1)));

    // A failed result with the reason shown to the console.
    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");
}
#endif
