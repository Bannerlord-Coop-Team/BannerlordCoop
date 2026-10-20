#if DEBUG
using Common;
using Common.Commands;
using GameInterface.Services.Players;
using System.Linq;

namespace GameInterface.Services.MapEvents.NavalScenarios;

// coop.debug.naval.scenario boarding spacing=12
// coop.debug.naval.scenario vs_pirates hulls=2 PlayerOne PlayerTwo
/// <summary>DEBUG, server: stages a whole naval battle test from a preset; the clients enter, deploy and act on their own.</summary>
public sealed class NavalScenarioCoopCommand : ICoopCommand
{
    private const int MaxOptions = 10;

    private readonly IPlayerManager playerManager;
    private readonly INavalScenarioResolver resolver;
    private readonly INavalScenarioStager stager;

    public NavalScenarioCoopCommand(IPlayerManager playerManager, INavalScenarioResolver resolver, INavalScenarioStager stager)
    {
        this.playerManager = playerManager;
        this.resolver = resolver;
        this.stager = stager;
    }

    public string Prefix => "coop.debug.naval";

    public string Name => "scenario";

    public string Description => "Stages a naval battle test preset (" + NavalScenarioPresets.Names + ") for the connected players " +
        "or the listed ones; knobs: hulls=n hull=id spacing=m ships=n delay=s reconnect=s who=host|nonhost town=id weapons=n ram=n.";

    public CoopCommandSide Side => CoopCommandSide.Server;

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[] { new ExpectedArgs("preset", "The preset name.", true) }
        .Concat(Enumerable.Range(1, MaxOptions).Select(index =>
            (IExpectedArgs)new ExpectedArgs($"option_{index}", "A key=value knob or a player controller id.", false)))
        .ToArray();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (ModInformation.IsClient) return Failed("Run this command on the server.");
        if (!ModInformation.IsNavalDlcActive) return Failed("NavalDLC is not active on the server.");

        var connected = playerManager.Players
            .Where(playerManager.IsConnected)
            .Select(player => player.ControllerId)
            .ToArray();
        if (!resolver.TryResolve(args.ToArray(), connected, out var config, out var error)) return Failed(error);

        return stager.TryStage(config, out var output) ? new CoopCommandResult(true, output) : Failed(output);
    }

    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");
}
#endif
