using Common;
using Common.Commands;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Interfaces;
using GameInterface.Services.Rafts.Interfaces;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Rafts.Commands;

/// <summary>Debug commands for testing a player's raft state and cached fleet.</summary>
public class RaftDebugCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    // coop.debug.naval.strand_on_raft Player
    /// <summary>Strands a player's party on a raft the same way as being released from captivity at sea.</summary>
    public sealed class StrandOnRaftCoopCommand : ICoopCommand
    {
        private readonly IPlayerRaftFleetInterface raftFleetInterface;
        private readonly IObjectManager objectManager;
        private readonly IPlayerPartyLookup playerPartyLookup;

        public StrandOnRaftCoopCommand(
            IPlayerRaftFleetInterface raftFleetInterface,
            IObjectManager objectManager,
            IPlayerPartyLookup playerPartyLookup)
        {
            this.raftFleetInterface = raftFleetInterface;
            this.objectManager = objectManager;
            this.playerPartyLookup = playerPartyLookup;
        }

        public string Prefix => "coop.debug.naval";

        public string Name => "strand_on_raft";

        public string Description => "Puts a player's party at sea on a raft and caches its fleet until it washes ashore.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The player hero whose party is stranded.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");

            if (!ModInformation.IsNavalDlcActive)
                return Failed("NavalDLC is not active on the server.");

            if (args.Count != 1)
                return Failed("Expected a hero id.");

            if (!objectManager.TryGetObject<Hero>(args[0], out var hero))
                return Failed($"No hero has id {args[0]}.");

            if (!playerPartyLookup.TryGetPlayerParty(hero, out var party))
                return Failed($"{args[0]} is not a registered player hero.");

            if (!party.IsCurrentlyAtSea)
                return Failed($"{party.StringId} is not at sea.");

            if (party.IsInRaftState)
                return Failed($"{party.StringId} is already on a raft.");

            var shipCount = party.Ships.Count;
            raftFleetInterface.StrandAfterRelease(party);

            if (!party.IsInRaftState)
                return Failed($"{party.StringId} was not put on a raft, check the server log.");

            return Succeeded($"Stranded {party.StringId} on a raft and cached {shipCount} ship(s), they return when it washes ashore.");
        }
    }
}
