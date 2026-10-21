#if DEBUG
using Common.Commands;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// coop.debug.naval.immortal on
// coop.debug.naval.immortal off
/// <summary>
/// DEBUG: keeps this client's main agent from being knocked out during a test with vanilla's Immortal mortality state, under
/// which Agent.HandleBlow takes no health; routed blows replay through RegisterBlow on the owner, so they respect it too.
/// </summary>
public sealed class NavalImmortalCoopCommand : ICoopCommand
{
    internal const string Usage = "Usage: coop.debug.naval.immortal [on | off]";

    public string Prefix => "coop.debug.naval";

    public string Name => "immortal";

    public string Description => "Makes this client's main agent immortal (on) or mortal again (off) for a test; reports its state " +
        "without an argument. A ram's kill radius still kills it, as in vanilla.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("state", "on or off; omitted reports the main agent's mortality state.", false),
    };

    // Sets or reports the main agent's mortality state.
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args != null && args.Count > 1) return Failed(Usage);
        if (Mission.Current?.GetMissionBehavior<CoopNavalBattleBehavior>() == null) return Failed("No active coop naval battle mission.");

        var main = Agent.Main;
        if (main == null || !main.IsActive()) return Failed("There is no living main agent.");

        string value = args != null && args.Count == 1 ? args[0] : null;
        if (value == null) return Report(main);
        if (!TryParseState(value, out var state)) return Failed(Usage);

        main.SetMortalityState(state);
        return Report(main);
    }

    /// <summary>The mortality state on or off asks for: Immortal or Mortal; false for anything else.</summary>
    internal static bool TryParseState(string value, out Agent.MortalityState state)
    {
        state = Agent.MortalityState.Mortal;
        switch (value?.Trim().ToLowerInvariant())
        {
            case "on":
                state = Agent.MortalityState.Immortal;
                return true;
            case "off":
                return true;
            default:
                return false;
        }
    }

    // The main agent's mortality state and health as a NAVAL_IMMORTAL line.
    private static CoopCommandResult Report(Agent main) => new CoopCommandResult(true,
        $"NAVAL_IMMORTAL agent={main.Index} mortality={main.CurrentMortalityState} health={main.Health:0}/{main.HealthLimit:0}");

    // A failed command result with output.
    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");
}
#endif
