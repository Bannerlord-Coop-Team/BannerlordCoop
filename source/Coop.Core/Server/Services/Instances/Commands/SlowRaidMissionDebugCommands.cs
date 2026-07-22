#if DEBUG
using Common;
using GameInterface;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Missions;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace Coop.Core.Server.Services.Instances.Commands;

public static class SlowRaidMissionDebugCommands
{
    private static readonly Dictionary<string, RaidFixture> activeFixtures = new();

    [CommandLineArgumentFunction("opposing_raid_setup", "coop.debug.mapevent")]
    public static string Setup(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count < 2 || args.Count > 3)
            return "Usage: coop.debug.mapevent.opposing_raid_setup <controllerId> <settlementId> [raiderPartyId]";

        if (!TryGetPlayerState(args[0], args[1], out var objectManager, out var playerParty,
                out var settlement, out var error))
            return error;

        if (playerParty.MapEvent != null || playerParty.CurrentSettlement != null || settlement.Party?.MapEvent != null)
            return "Fixture requires the player to be free on the map and the village to have no map event.";

        if (settlement.Village.VillageState != Village.VillageStates.Normal)
            return $"Fixture requires a Normal village; current state is {settlement.Village.VillageState}.";

        if (!TryGetRaider(args.Count == 3 ? args[2] : null, objectManager, settlement,
                out var raider, out error))
            return error;

        var defenderRoster = settlement.Party.MemberRoster;
        var defenderSnapshot = defenderRoster.GetTroopRoster().ToArray();

        // An unopposed village is required until the player joins the raid as its defender.
        defenderRoster.Clear();

        MapEvent mapEvent = null;
        try
        {
            raider.Position = settlement.GatePosition;
            raider.SetMoveModeHold();
            StartBattleAction.ApplyStartRaid(raider, settlement);

            mapEvent = settlement.Party.MapEvent;
            var detachedDefenders = mapEvent == null
                ? 0
                : RaidDebugFixture.DetachNonSettlementDefenders(mapEvent);
            if (mapEvent == null || !ReferenceEquals(raider.MapEvent, mapEvent) || !mapEvent.IsActiveSlowVillageRaid())
            {
                mapEvent?.FinalizeEvent();
                RestoreDefenders(settlement, defenderSnapshot);
                return "Unable to create an active slow raid with the selected AI lord.";
            }

            if (!objectManager.TryGetIdWithLogging(mapEvent, out var mapEventId) ||
                !objectManager.TryGetIdWithLogging(raider, out var raiderId))
            {
                mapEvent.FinalizeEvent();
                RestoreDefenders(settlement, defenderSnapshot);
                return "Created raid objects were not registered.";
            }

            activeFixtures[mapEventId] = new RaidFixture(settlement, defenderSnapshot);

            playerParty.Position = settlement.GatePosition + new Vec2(1.5f, 0f);
            playerParty.SetMoveModeHold();

            return $"created=true; controller={args[0]}; settlement={args[1]}; mapEvent={mapEventId}; " +
                   $"raider={raiderId}; raiderStringId={raider.StringId}; activeSlow=true; instanceOccupied=false; " +
                   $"hitPoints={Format(settlement.SettlementHitPoints)}; defendersStaged={defenderSnapshot.Sum(element => element.Number)}; " +
                   $"insideDefendersDetached={detachedDefenders}; " +
                   $"next=On Player 1 attack {raider.StringId} at {settlement.StringId} and open the deployment screen.";
        }
        catch
        {
            mapEvent?.FinalizeEvent();
            RestoreDefenders(settlement, defenderSnapshot);
            throw;
        }
    }

    [CommandLineArgumentFunction("opposing_raid_state", "coop.debug.mapevent")]
    public static string State(List<string> args)
    {
        if (args.Count != 2)
            return "Usage: coop.debug.mapevent.opposing_raid_state <controllerId> <settlementId>";

        if (!TryGetPlayerState(args[0], args[1], out var objectManager, out var playerParty,
                out var settlement, out var error))
            return error;

        var mapEvent = settlement.Party?.MapEvent;
        var mapEventId = mapEvent != null && objectManager.TryGetId(mapEvent, out var id) ? id : null;
        var raider = mapEvent?.AttackerSide?.LeaderParty?.MobileParty;
        var raiderId = raider != null && objectManager.TryGetId(raider, out var resolvedRaiderId)
            ? resolvedRaiderId
            : "<none>";
        var instanceOccupied = mapEventId != null &&
                               ContainerProvider.TryResolve<IMissionMembershipRegistry>(out var membershipRegistry) &&
                               membershipRegistry.IsInstanceOccupied(mapEventId);

        return $"controller={args[0]}; settlement={args[1]}; mapEvent={mapEventId ?? "<none>"}; " +
               $"raider={raiderId}; activeSlow={mapEvent?.IsActiveSlowVillageRaid() == true}; " +
               $"instanceOccupied={instanceOccupied}; hitPoints={Format(settlement.SettlementHitPoints)}; " +
               $"villageState={settlement.Village.VillageState}; playerAttached={ReferenceEquals(playerParty.MapEvent, mapEvent) && mapEvent != null}; " +
               $"raiderAttached={ReferenceEquals(raider?.MapEvent, mapEvent) && mapEvent != null}";
    }

    [CommandLineArgumentFunction("opposing_raid_cleanup", "coop.debug.mapevent")]
    public static string Cleanup(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.opposing_raid_cleanup <mapEventId>";

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "Unable to resolve opposing raid fixture services.";

        activeFixtures.TryGetValue(args[0], out var fixture);
        objectManager.TryGetObject(args[0], out MapEvent mapEvent);

        if (mapEvent == null && fixture == null)
            return $"Unable to resolve map event {args[0]}.";

        if (ContainerProvider.TryResolve<IMissionMembershipRegistry>(out var membershipRegistry) &&
            membershipRegistry.IsInstanceOccupied(args[0]))
            return "Exit the battle mission before cleaning up the raid.";

        try
        {
            mapEvent?.FinalizeEvent();
        }
        finally
        {
            if (fixture != null)
            {
                RestoreDefenders(fixture.Settlement, fixture.DefenderRoster);
                activeFixtures.Remove(args[0]);
            }
        }

        return $"cleaned=true; mapEvent={args[0]}; defendersRestored={fixture != null}";
    }

    [CommandLineArgumentFunction("opposing_raid_camera", "coop.debug.mapevent")]
    public static string FollowVillage(List<string> args)
    {
        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.opposing_raid_camera <settlementId>";

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !objectManager.TryGetObjectWithLogging<Settlement>(args[0], out var settlement))
            return $"Unable to resolve settlement {args[0]}.";

        settlement.Party.SetAsCameraFollowParty();
        return $"Camera following village {args[0]}.";
    }

    private static bool TryGetRaider(
        string requestedPartyId,
        IObjectManager objectManager,
        Settlement settlement,
        out MobileParty raider,
        out string error)
    {
        if (requestedPartyId != null)
        {
            if (!objectManager.TryGetObjectWithLogging(requestedPartyId, out raider))
            {
                error = $"Unable to resolve raider party {requestedPartyId}.";
                return false;
            }

            if (!IsEligibleRaider(raider, settlement))
            {
                error = $"Party {requestedPartyId} is not a free hostile AI lord party with healthy troops.";
                return false;
            }

            error = null;
            return true;
        }

        raider = MobileParty.AllLordParties
            .Where(party => IsEligibleRaider(party, settlement))
            .OrderByDescending(party => party.Party.CalculateCurrentStrength())
            .FirstOrDefault();

        error = raider == null
            ? $"No hostile AI lord party is available to raid {settlement.StringId}; pass a raiderPartyId explicitly."
            : null;
        return raider != null;
    }

    private static bool IsEligibleRaider(MobileParty party, Settlement settlement)
    {
        return party != null && party.IsActive && !party.IsPlayerParty() && party.LeaderHero != null &&
               party.Party.NumberOfHealthyMembers > 0 && party.MapEvent == null && party.CurrentSettlement == null &&
               party.BesiegerCamp == null && party.Army == null &&
               party.MapFaction?.IsAtWarWith(settlement.MapFaction) == true;
    }

    private static bool TryGetPlayerState(
        string controllerId,
        string settlementId,
        out IObjectManager objectManager,
        out MobileParty playerParty,
        out Settlement settlement,
        out string error)
    {
        objectManager = null;
        playerParty = null;
        settlement = null;

        if (!ContainerProvider.TryResolve(out objectManager) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
        {
            error = "Unable to resolve opposing raid fixture services.";
            return false;
        }

        if (!playerManager.TryGetPlayer(controllerId, out var player) || !playerManager.IsConnected(player))
        {
            error = $"No connected player has controller id {controllerId}.";
            return false;
        }

        if (!objectManager.TryGetObjectWithLogging(player.MobilePartyId, out playerParty))
        {
            error = $"Unable to resolve player party {player.MobilePartyId}.";
            return false;
        }

        if (!objectManager.TryGetObjectWithLogging(settlementId, out settlement) ||
            settlement.Village == null || settlement.Party == null)
        {
            error = $"Unable to resolve village settlement {settlementId}.";
            return false;
        }

        error = null;
        return true;
    }

    private static string Format(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static void RestoreDefenders(Settlement settlement, IReadOnlyList<TroopRosterElement> defenderRoster)
    {
        var roster = settlement.Party.MemberRoster;
        roster.Clear();
        foreach (var element in defenderRoster)
            roster.Add(element);
    }

    private sealed class RaidFixture
    {
        public Settlement Settlement { get; }
        public IReadOnlyList<TroopRosterElement> DefenderRoster { get; }

        public RaidFixture(Settlement settlement, IReadOnlyList<TroopRosterElement> defenderRoster)
        {
            Settlement = settlement;
            DefenderRoster = defenderRoster;
        }
    }
}
#endif
