#if DEBUG
using Common.Commands;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Heroes.Patches;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem.Settlements;
using GameInterface.Services.Players;
using System.Globalization;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Crime;

public class CrimeDebugCommands
{
    public sealed class Read : ICoopCommand
    {
        public string Prefix => "coop.debug.crime";
        public string Name => "read";
        public string Description => "Read each registered player's crime rating and faction hostility.";
        public CoopCommandSide Side => CoopCommandSide.Both;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[] { new ExpectedArgs("faction_id", "The faction registry id.") };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objects)
                || !ContainerProvider.TryResolve<IPlayerManager>(out var players)
                || !objects.TryGetObjectWithLogging<IFaction>(args[0], out var faction))
                return new CoopCommandResult(false, "Faction or services unavailable.", "command_failed");
            objects.TryGetId(faction, out var factionId);
            var rows = players.Players.Select(player =>
            {
                player.CrimeRatings.TryGetValue(factionId, out var rating);
                objects.TryGetObject<Hero>(player.HeroId, out var hero);
                return $"hero={player.HeroId};faction={args[0]};rating={rating.ToString(CultureInfo.InvariantCulture)};war={hero?.MapFaction?.IsAtWarWith(faction)}";
            });
            return new CoopCommandResult(true, string.Join("\n", rows));
        }
    }

    public sealed class Preflight : ICoopCommand
    {
        public string Prefix => "coop.debug.crime";
        public string Name => "preflight";
        public string Description => "Read concrete caravan, faction and daily-model inputs for crime verification.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objects)
                || !ContainerProvider.TryResolve<IPlayerManager>(out var players)
                || Settlement.Find("town_ES1")?.MapFaction == null)
                return new CoopCommandResult(false, "Campaign services or Danustica unavailable.", "command_failed");
            var faction = Settlement.Find("town_ES1").MapFaction;
            if (!objects.TryGetIdWithLogging(faction, out var factionId))
                return new CoopCommandResult(false, "Danustica faction unavailable.", "command_failed");
            var caravans = MobileParty.All.Where(party => party.IsCaravan && party.MapFaction == faction)
                .Select(party => objects.TryGetId(party, out var id) ? id : null).Where(id => id != null).ToArray();
            var rows = players.Players.Select(player =>
            {
                if (!objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)) return null;
                using (new MainHeroSubstitutionScope(hero, hero.PartyBelongedTo))
                    return new { hero = player.HeroId, controller = player.ControllerId,
                        daily = Campaign.Current.Models.CrimeModel.GetDailyCrimeRatingChange(faction).ResultNumber,
                        war = hero.MapFaction.IsAtWarWith(faction), sameFaction = hero.MapFaction == faction };
            }).Where(row => row != null).ToArray();
            return new CoopCommandResult(true, JsonConvert.SerializeObject(new { faction = factionId, caravans, players = rows,
                threshold = Campaign.Current.Models.CrimeModel.DeclareWarCrimeRatingThreshold,
                alleyDaily = Campaign.Current.Models.AlleyModel.GetDailyCrimeRatingOfAlley }));
        }
    }

    public sealed class Apply : ICoopCommand
    {
        public string Prefix => "coop.debug.crime";
        public string Name => "apply";
        public string Description => "Apply the production crime action for a registered player.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The player hero registry id."),
            new ExpectedArgs("faction_id", "The faction registry id."),
            new ExpectedArgs("delta", "The crime rating change.")
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objects)
                || !ContainerProvider.TryResolve<ICrimeRatingService>(out var ratings)
                || !objects.TryGetObjectWithLogging<Hero>(args[0], out var hero)
                || !objects.TryGetObjectWithLogging<IFaction>(args[1], out var faction)
                || !PlayerManager.TryGetControlledObjectInfo(hero, out _)
                || !float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var delta)
                || float.IsNaN(delta) || float.IsInfinity(delta))
                return new CoopCommandResult(false, "Invalid player, faction, or finite delta.", "command_failed");
            ratings.Apply(hero, faction, delta, false);
            return new CoopCommandResult(true, "Crime action applied.");
        }
    }

    public sealed class Coerce : ICoopCommand
    {
        public string Prefix => "coop.debug.crime";
        public string Name => "coerce";
        public string Description => "Exercise the production caravan minor coercion action.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The player hero registry id."),
            new ExpectedArgs("caravan_id", "The caravan registry id.")
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objects)
                || !objects.TryGetObjectWithLogging<Hero>(args[0], out var hero)
                || !objects.TryGetObjectWithLogging<MobileParty>(args[1], out var caravan)
                || !PlayerManager.TryGetControlledObjectInfo(hero, out _)
                || hero.PartyBelongedTo == null || !caravan.IsCaravan)
                return new CoopCommandResult(false, "Registered player party and caravan required.", "command_failed");
            BeHostileAction.ApplyMinorCoercionHostileAction(hero.PartyBelongedTo.Party, caravan.Party);
            return new CoopCommandResult(true, "Caravan coercion action applied.");
        }
    }

    public sealed class DailyTick : ICoopCommand
    {
        public string Prefix => "coop.debug.crime";
        public string Name => "daily_tick";
        public string Description => "Apply one authoritative daily crime tick, including owned alleys.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ContainerProvider.TryResolve<ICrimeRatingService>(out var ratings))
                return new CoopCommandResult(false, "Crime service unavailable.", "command_failed");
            ratings.DailyTick();
            return new CoopCommandResult(true, "Daily crime tick applied.");
        }
    }
}
#endif
