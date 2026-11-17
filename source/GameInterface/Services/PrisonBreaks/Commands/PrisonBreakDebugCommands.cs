using Common.Commands;
using Common.Messaging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.PrisonBreaks.Interfaces;
using GameInterface.Services.PrisonBreaks.Messages;
using GameInterface.Utils.Commands;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PrisonBreaks.Commands;

/// <summary>
/// Console commands that stage and inspect prison breaks for live tests.
/// </summary>
internal class PrisonBreakDebugCommands
{
    // Builds a successful command result.
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    // Builds a failed command result.
    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    // coop.debug.town.imprison_hero town_ES1 lord_4_2
    /// <summary>
    /// Imprisons an AI hero in a town or castle prison so a client can break them out.
    /// </summary>
    public sealed class ImprisonHeroCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;
        private readonly IPrisonBreakCampaignBehaviorInterface behaviorInterface;

        // Takes the resolver for the settlement and hero lookups and the interface that decides who is held in a prison.
        public ImprisonHeroCoopCommand(ICommandObjectResolver resolver, IPrisonBreakCampaignBehaviorInterface behaviorInterface)
        {
            this.resolver = resolver;
            this.behaviorInterface = behaviorInterface;
        }

        public string Prefix => "coop.debug.town";

        public string Name => "imprison_hero";

        public string Description => "Puts an AI hero in a town or castle prison.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlement", "Town or castle name or id; quote names with spaces."),
            new ExpectedArgs("hero", "Hero name or id; quote names with spaces."),
        };

        // Runs the vanilla capture with patches live so the imprisonment replicates to clients.
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!resolver.TryResolveTown(args[0], out var town, out var error)) return Failed(error);
            if (!resolver.TryResolveHero(args[1], out var hero, out error)) return Failed(error);

            // Prison breaks never target player heroes, and a second capture would duplicate the prisoner.
            if (hero.IsPlayerHero()) return Failed($"{hero.Name} ({hero.StringId}) is a player hero.");
            if (hero.IsPrisoner) return Failed($"{hero.Name} ({hero.StringId}) is already a prisoner.");

            var settlement = town.Settlement;
            TakePrisonerAction.Apply(settlement.Party, hero);

            if (!behaviorInterface.IsHeldIn(hero, settlement))
                return Failed($"{hero.Name} ({hero.StringId}) did not end up in the {settlement.Name} prison.");

            return Succeeded($"{hero.Name} ({hero.StringId}) is now a prisoner in {settlement.Name} ({settlement.StringId}).");
        }
    }

    // coop.debug.town.set_prison_break_cooldown town_ES1 0
    /// <summary>
    /// Sets or clears a settlement's prison break cooldown on the server and every client.
    /// </summary>
    public sealed class SetPrisonBreakCooldownCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;
        private readonly IMessageBroker messageBroker;

        // Takes the resolver for the settlement lookup and the broker the cooldown handler listens on.
        public SetPrisonBreakCooldownCoopCommand(ICommandObjectResolver resolver, IMessageBroker messageBroker)
        {
            this.resolver = resolver;
            this.messageBroker = messageBroker;
        }

        public string Prefix => "coop.debug.town";

        public string Name => "set_prison_break_cooldown";

        public string Description => "Sets a settlement's prison break cooldown in days, 0 clears it.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlement", "Town or castle name or id; quote names with spaces."),
            new ExpectedArgs("days", "Days until a prison break can start again, 0 clears the cooldown."),
        };

        // Publishes the cooldown so PrisonBreakHandler applies and broadcasts it.
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!resolver.TryResolveTown(args[0], out var town, out var error)) return Failed(error);
            if (!float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var days) || days < 0f)
                return Failed($"'{args[1]}' is not a number of days of 0 or more.");

            if (days == 0f)
            {
                messageBroker.Publish(this, new PrisonBreakCooldownSet(town.Settlement, CampaignTime.Zero));
                return Succeeded($"{town.Settlement.Name} prison break cooldown cleared.");
            }

            messageBroker.Publish(this, new PrisonBreakCooldownSet(town.Settlement, CampaignTime.DaysFromNow(days)));
            return Succeeded($"{town.Settlement.Name} prison break cooldown set to {days} days.");
        }
    }

    // coop.debug.town.prison_break_state town_ES1
    /// <summary>
    /// Prints a settlement's prison heroes, prison break cooldown and running break on this machine.
    /// </summary>
    public sealed class PrisonBreakStateCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;
        private readonly IPrisonBreakCampaignBehaviorInterface behaviorInterface;

        // Takes the resolver for the settlement lookup and the interface holding the prison break state.
        public PrisonBreakStateCoopCommand(ICommandObjectResolver resolver, IPrisonBreakCampaignBehaviorInterface behaviorInterface)
        {
            this.resolver = resolver;
            this.behaviorInterface = behaviorInterface;
        }

        public string Prefix => "coop.debug.town";

        public string Name => "prison_break_state";

        public string Description => "Shows a settlement's prison heroes and prison break state.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlement", "Town or castle name or id; quote names with spaces."),
        };

        // Lists the settlement and garrison prison heroes, the rosters vanilla offers in the prisoner selection.
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!resolver.TryResolveTown(args[0], out var town, out var error)) return Failed(error);

            var settlement = town.Settlement;
            var prisoners = town.GetPrisonerHeroes()
                .Select(character => $"{character.HeroObject.Name} ({character.HeroObject.StringId})");

            var output = new StringBuilder();
            output.AppendLine($"{settlement.Name} ({settlement.StringId})");
            output.AppendLine($"Prison heroes: {string.Join(", ", prisoners.DefaultIfEmpty("none"))}");
            output.AppendLine($"On cooldown: {behaviorInterface.IsOnCooldown(settlement)}");
            output.AppendLine($"Prison break running: {behaviorInterface.IsPrisonBreakActive(settlement)}");

            return Succeeded(output.ToString());
        }
    }
}
