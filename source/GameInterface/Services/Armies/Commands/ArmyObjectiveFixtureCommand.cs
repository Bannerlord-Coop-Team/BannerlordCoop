#if DEBUG
using Common;
using Common.Commands;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using Newtonsoft.Json;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Armies.Commands;

public sealed class ArmyObjectiveFixtureCommand : ICoopCommand
{
    private readonly IObjectManager objects;
    private readonly IMobilePartyBehaviorSnapshot snapshots;
    private MobileParty leader;
    private PartyBehaviorUpdateData original;
    private bool rethink;
    private bool needsUpdate;

    public ArmyObjectiveFixtureCommand(IObjectManager objects, IMobilePartyBehaviorSnapshot snapshots)
    {
        this.objects = objects;
        this.snapshots = snapshots;
    }

    public string Prefix => "coop.debug.army";
    public string Name => "objective_fixture";
    public string Description => "Prepare, inspect, or restore an AI lord for army objective verification.";
    public CoopCommandSide Side => CoopCommandSide.Server;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("operation", "prepare, state, or restore; destroy the temporary army before restore."),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (ModInformation.IsClient) return Failed("Run on the server.");
        if (args[0] == "prepare")
        {
            if (leader != null) return Failed("Restore the previous fixture first.");
            if (!objects.TryGetObject<Settlement>("town_ES1", out _)) return Failed("Danustica is not registered.");
            var candidate = MobileParty.AllLordParties.OrderBy(p => p.StringId)
                .FirstOrDefault(p => p.IsActive && !p.IsPlayerParty() && p.Army == null &&
                    p.MapEvent == null && p.CurrentSettlement == null && p.BesiegerCamp == null &&
                    p.LeaderHero?.Clan?.Kingdom != null && p.Ai != null &&
                    objects.TryGetId(p, out _) && objects.TryGetId(p.LeaderHero, out _) &&
                    objects.TryGetId(p.LeaderHero.Clan.Kingdom, out _) &&
                    snapshots.TryCreate(p, out var state) && snapshots.CanApply(p, state));
            if (candidate == null || !snapshots.TryCreate(candidate, out original))
                return Failed("No eligible registered AI lord party.");
            leader = candidate;
            rethink = leader.Ai.RethinkAtNextHourlyTick;
            needsUpdate = leader.Ai.DefaultBehaviorNeedsUpdate;
        }
        else if (args[0] == "restore")
        {
            if (leader == null) return Failed("No captured fixture.");
            if (leader.Army != null) return Failed("Destroy the temporary army before restoring its leader.");
            if (!snapshots.TryApply(leader, original, out _)) return Failed("Leader behavior restoration failed.");
            leader.Ai.RethinkAtNextHourlyTick = rethink;
            leader.Ai.DefaultBehaviorNeedsUpdate = needsUpdate;
            if (!snapshots.TryCreate(leader, out var actual) ||
                JsonConvert.SerializeObject(actual) != JsonConvert.SerializeObject(original))
                return Failed("Restored leader behavior differs from the captured state.");
            var restoredId = Id(leader);
            leader = null;
            return Result(new { restored = true, leaderPartyId = restoredId });
        }
        else if (args[0] != "state") return Failed("Expected prepare, state, or restore.");

        if (leader == null) return Failed("No captured fixture.");
        var objective = MobileParty.All.OrderBy(p => p.StringId)
            .FirstOrDefault(p => p != leader && p.IsActive && objects.TryGetId(p, out _));
        if (objective == null) return Failed("No distinct registered objective party.");
        return Result(new
        {
            leaderPartyId = Id(leader), leaderHeroId = Id(leader.LeaderHero),
            kingdomId = Id(leader.LeaderHero.Clan.Kingdom), objectivePartyId = Id(objective),
            armyId = leader.Army == null ? null : Id(leader.Army), original,
        });
    }

    private string Id(object value) => objects.TryGetId(value, out var id) ? id : null;
    private static CoopCommandResult Result(object value) => new CoopCommandResult(true, JsonConvert.SerializeObject(value));
    private static CoopCommandResult Failed(string reason) => new CoopCommandResult(false, reason, "command_failed");
}
#endif
