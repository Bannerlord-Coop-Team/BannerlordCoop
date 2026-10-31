#if DEBUG
using Common;
using Common.Commands;
using System.Linq;

namespace GameInterface.Services.MapEvents.NavalScenarios;

// coop.debug.map_event.start_naval PlayerOne PlayerTwo PlayerThree
/// <summary>DEBUG, server: moves the players' parties onto the nearest hostile AI party at sea and starts a naval battle with it.</summary>
public sealed class StartNavalCoopCommand : ICoopCommand
{
    private readonly INavalBattleStaging staging;

    public StartNavalCoopCommand(INavalBattleStaging staging)
    {
        this.staging = staging;
    }

    public string Prefix => "coop.debug.map_event";

    public string Name => "start_naval";

    public string Description => "Starts a naval battle between the players and the nearest hostile AI fleet at sea.";

    public CoopCommandSide Side => CoopCommandSide.Server;

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "The player the AI fleet attacks.", true),
        new ExpectedArgs("ally_controller_id", "A second player who joins the defending side.", false),
        new ExpectedArgs("ally_controller_id_3", "A third player who joins the defending side.", false),
        new ExpectedArgs("ally_controller_id_4", "A fourth player who joins the defending side.", false),
        new ExpectedArgs("ally_controller_id_5", "A fifth player who joins the defending side.", false),
        new ExpectedArgs("ally_controller_id_6", "A sixth player who joins the defending side.", false),
    };

    // Refuses a client, a server without NavalDLC or a repeated player, then starts the battle through the staging moves.
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (ModInformation.IsClient) return Failed("Run this command on the server.");
        if (!ModInformation.IsNavalDlcActive) return Failed("NavalDLC is not active on the server.");

        var controllerIds = args.ToArray();
        if (controllerIds.Distinct().Count() != controllerIds.Length) return Failed("The players must be different.");

        if (!staging.TryResolvePlayerFleets(controllerIds, out var playerParties, out var error)) return Failed(error);

        var enemyParty = staging.FindNavalEnemy(playerParties[0]);
        if (enemyParty == null) return Failed($"No hostile AI party with ships is at sea for player {controllerIds[0]}.");

        if (!staging.TryStart(playerParties, enemyParty, out var mapEvent, out var mapEventId, out error)) return Failed(error);

        return new CoopCommandResult(true, $"Started naval map event {mapEventId} (naval={mapEvent.IsNavalMapEvent}): " +
            $"{enemyParty.StringId} with {enemyParty.Ships.Count} ships attacks player {controllerIds[0]}" +
            (controllerIds.Length == 1 ? "." : $", joined by player {string.Join(", ", controllerIds.Skip(1))}."));
    }

    // A failed result with the command_failed code.
    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");
}
#endif
