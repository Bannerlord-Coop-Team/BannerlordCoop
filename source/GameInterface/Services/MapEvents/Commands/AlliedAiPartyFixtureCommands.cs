#if DEBUG
using Common;
using Common.Commands;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Villages.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Commands;

/// <summary>
/// Joins AI parties to a player's running battle on the player's side, the late-ally path from #3462. The join
/// reaches the battle host only inside the AI join window, the same as a nearby AI reinforcement.
/// </summary>
internal class AlliedAiPartyFixtureCommands
{
    // Each party with the battle it was added to, so restore leaves a later battle of that party alone.
    private static readonly List<(MobileParty Party, MapEvent MapEvent)> addedParties = new();

    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    // coop.debug.map_event.add_allied_ai_party PlayerOne
    /// <summary>Adds the given AI party, or the nearest free allied lord party, to the player's battle side.</summary>
    public sealed class AddAlliedAiPartyCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "add_allied_ai_party";

        public string Description => "Joins an AI party to a player's running battle on the player's side.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The controller id of a player in a running battle."),
            new ExpectedArgs("party_id", "An AI mobile party id. Defaults to the nearest free lord party allied to the player.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");

            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
                return Failed("Unable to resolve ObjectManager");
            if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
                return Failed("Unable to resolve PlayerManager");
            if (!playerManager.TryGetPlayer(args[0], out var player))
                return Failed($"No registered player has controller id {args[0]}.");
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var playerParty))
                return Failed($"Unable to resolve player party {player.MobilePartyId}.");

            var mapEvent = playerParty.MapEvent;
            var playerSide = playerParty.Party.MapEventSide;
            if (mapEvent == null || playerSide == null || mapEvent.IsFinalized)
                return Failed($"Player {args[0]} is not in a running battle.");
            if (!InteractionPatches.IsWithinAiJoinWindow(mapEvent))
                return Failed("The battle's AI join window is closed, so the battle host would not receive the party.");

            MobileParty alliedParty;
            if (args.Count == 2)
            {
                if (!objectManager.TryGetObjectWithLogging(args[1], out alliedParty))
                    return Failed($"Unable to resolve party {args[1]}.");
                if (!alliedParty.IsActive || alliedParty.IsPlayerParty() || alliedParty.MapEvent != null)
                    return Failed($"Party {args[1]} must be an active AI party outside any battle.");
            }
            else if (!TryFindAlliedParty(playerParty, playerSide, out alliedParty))
            {
                return Failed("No free AI lord party allied to the player is available.");
            }

            alliedParty.Party.MapEventSide = playerSide;
            addedParties.Add((alliedParty, mapEvent));

            objectManager.TryGetId(mapEvent, out string mapEventId);
            return Succeeded($"Allied AI party added: controller={args[0]}, party={alliedParty.StringId}, " +
                $"healthy={alliedParty.MemberRoster.TotalHealthyCount}, side={playerSide.MissionSide}, " +
                $"mapEvent={mapEventId}, sideParties={playerSide.Parties.Count}.");
        }
    }

    // coop.debug.map_event.restore_allied_ai_parties
    /// <summary>Takes the fixture's parties back out of the battles they were added to and forgets them.</summary>
    public sealed class RestoreAlliedAiPartiesCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "restore_allied_ai_parties";

        public string Description => "Takes the parties add_allied_ai_party joined back out of their battle if it is still running.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");

            int removed = 0;
            foreach (var added in addedParties)
            {
                var party = added.Party;
                if (!party.IsActive || party.MapEvent != added.MapEvent || added.MapEvent.IsFinalized) continue;

                party.Party.MapEventSide = null;
                removed++;
            }

            int total = addedParties.Count;
            addedParties.Clear();
            return Succeeded($"Restored {total} allied AI fixture party/parties, removed {removed} from their unfinished fixture battles.");
        }
    }

    private static bool TryFindAlliedParty(MobileParty playerParty, MapEventSide playerSide, out MobileParty alliedParty)
    {
        var enemyFaction = playerSide.OtherSide?.MapFaction;
        var playerPosition = playerParty.Position.ToVec2();
        alliedParty = MobileParty.All
            .Where(p => p.IsActive && p.IsLordParty && !p.IsPlayerParty() && p.MapEvent == null && p.Army == null &&
                        p.CurrentSettlement == null && p.MemberRoster.TotalHealthyCount > 0 && p.MapFaction != null &&
                        !VillageHostileFactionStanceHelper.HasWarStance(playerParty.MapFaction, p.MapFaction) &&
                        VillageHostileFactionStanceHelper.HasWarStance(enemyFaction, p.MapFaction))
            .OrderBy(p => p.Position.ToVec2().DistanceSquared(playerPosition))
            .FirstOrDefault();
        return alliedParty != null;
    }
}
#endif
