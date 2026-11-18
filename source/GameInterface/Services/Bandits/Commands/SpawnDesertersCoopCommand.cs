using Common.Commands;
using GameInterface.Utils.Commands;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Bandits.Commands;

// coop.debug.bandits.spawn_deserters <settlement> [troop_count]
/// <summary>
/// Spawns a deserter looter party at a settlement through the server's deserter spawn path.
/// </summary>
public sealed class SpawnDesertersCoopCommand : ICoopCommand
{
    private const int DefaultTroopCount = 5;

    private readonly ICommandObjectResolver resolver;

    public SpawnDesertersCoopCommand(ICommandObjectResolver resolver) => this.resolver = resolver;

    public string Prefix => "coop.debug.bandits";

    public string Name => "spawn_deserters";

    public string Description => "Spawns a deserter looter party near a settlement and prints its position.";

    public CoopCommandSide Side => CoopCommandSide.Server;

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("settlement_id", "Settlement name or id; quote names with spaces."),
        new ExpectedArgs("troop_count", "Basic troops of the settlement culture before the looter party size multiplier, 5 by default.", false),
    };

    // Runs DesertersCampaignBehavior.SpawnDesertersParty, as a battle's deserters would, and reports the new party.
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!resolver.TryResolveSettlement(args[0], out var settlement, out var error)) return Failed(error);

        int troopCount = DefaultTroopCount;
        if (args.Count > 1 && !int.TryParse(args[1], out troopCount)) return Failed("troop_count must be a number.");
        if (troopCount <= 0) return Failed("troop_count must be positive.");

        var behavior = Campaign.Current.GetCampaignBehavior<DesertersCampaignBehavior>();
        if (behavior?.DeserterClan == null) return Failed("The deserters clan is not available.");

        var troop = settlement.Culture?.BasicTroop;
        if (troop == null) return Failed($"{settlement.Name} has no culture basic troop.");

        var troops = TroopRoster.CreateDummyTroopRoster();
        troops.AddToCounts(troop, troopCount);

        // SpawnDesertersParty returns nothing, so diff the deserter clan's parties to find the one it created.
        var existing = new HashSet<WarPartyComponent>(behavior.DeserterClan.WarPartyComponents);
        behavior.SpawnDesertersParty(mapEvent: null, troops, settlement);

        var created = behavior.DeserterClan.WarPartyComponents.FirstOrDefault(component => !existing.Contains(component));
        if (created == null) return Failed("No deserter party was created.");

        var party = created.MobileParty;
        return Succeeded(
            $"Spawned {party.Name} id={resolver.DisplayId(party)} stringId={party.StringId} " +
            $"position={party.Position.ToVec2()} near {settlement.Name}.");
    }

    // Builds a successful command result.
    private static CoopCommandResult Succeeded(string output) => new CoopCommandResult(true, output);

    // Builds a failed command result with the shared failure code.
    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");
}
