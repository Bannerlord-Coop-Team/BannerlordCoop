#if DEBUG
using Common;
using Common.Commands;
using GameInterface.Utils.Commands;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Party.Commands;

/// <summary>
/// Stages prisoner stack xp so live tests can reach the vanilla xp cap on a prison roster.
/// </summary>
internal static class PrisonerXpFixtureCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    /// <summary>
    /// Sets one prison-roster stack's xp through SetElementXp, so vanilla clamps it and the change replicates.
    /// </summary>
    public sealed class SetPrisonerXpCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.mobile_party";

        public string Name => "set_prisoner_xp";

        public string Description => "Sets a prisoner stack's xp through the vanilla cap and reports the stored xp.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("party_id", "The registered MobileParty id.", true),
            new ExpectedArgs("character_id", "The prisoner CharacterObject id.", true),
            new ExpectedArgs("xp", "The xp to set before the vanilla cap.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");
            if (args.Count != 3 || !int.TryParse(args[2], out var xp) || xp < 0)
                return Failed("Xp must be a non-negative integer.");

            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out var error)) return Failed(error);
            if (!CommandHelpers.TryGetManagedObject<MobileParty>(objectManager, args[0], out var party, out error))
                return Failed(error);
            if (!CommandHelpers.TryGetManagedObject<CharacterObject>(objectManager, args[1], out var character, out error))
                return Failed(error);
            if (character.IsHero) return Failed("Hero prisoners always have zero xp.");

            var roster = party.PrisonRoster;
            int index = roster.FindIndexOfTroop(character);
            if (index < 0) return Failed($"{args[1]} is not in {args[0]}'s prison roster.");

            int oldXp = roster.GetElementXp(index);
            roster.SetElementXp(index, xp);
            return Succeeded($"PRISONER_XP_SET party={args[0]} character={args[1]} " +
                $"number={roster.GetElementNumber(index)} oldXp={oldXp} requestedXp={xp} " +
                $"newXp={roster.GetElementXp(index)}");
        }
    }
}
#endif
