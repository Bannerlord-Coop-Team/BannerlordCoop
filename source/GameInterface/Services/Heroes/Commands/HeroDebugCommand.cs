using Common.Commands;
using Common;
using Common.Logging;
using GameInterface.Configuration;
using GameInterface.Services.Heroes.Audit;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.ObjectManager.Extensions;
using GameInterface.Utils.Commands;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Heroes.Commands;

public class HeroDebugCommand
{
    private static CoopCommandResult Succeeded(string output) =>

        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>

        new CoopCommandResult(false, output, "command_failed");

    private static readonly ILogger Logger = LogManager.GetLogger<HeroDebugCommand>();

    // coop.debug.hero.list
    /// <summary>
    /// Lists heroes whose names start with the optional prefix
    /// </summary>
    /// <param name="args">Optional case-insensitive hero name prefix</param>
    /// <returns>Strings of the matching heroes</returns>

    public sealed class HeroListCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "list";

        public string Description => "Lists registered heroes, optionally filtered by display-name prefix.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("name_prefix", "The optional display-name prefix. Quote multi-word values.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            StringBuilder stringBuilder = new StringBuilder();

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }

            string namePrefix = args.Count == 0 ? string.Empty : args[0];
            foreach (var hero in Campaign.Current.CampaignObjectManager.GetAllHeroes()
                         .Where(hero => NameStartsWithPrefix(hero.Name?.ToString(), namePrefix)))
            {
                if (objectManager.TryGetId(hero, out var id))
                {
                    stringBuilder.AppendLine($"ID: '{id}', Name: '{hero.Name}', Game ID: {hero.Id}, Game StringId {hero.StringId}");
                }
                else
                {
                    stringBuilder.AppendLine($"Name: '{hero.Name}' was not registered with object manager");
                }
            }

            if (stringBuilder.Length == 0 && string.IsNullOrEmpty(namePrefix) == false)
                return Failed($"No hero with a name starting with '{namePrefix}' was found.");

            return Succeeded(stringBuilder.ToString());
        }
    }

    internal static bool NameStartsWithPrefix(string heroName, string prefix)
    {
        return string.IsNullOrEmpty(prefix) ||
               heroName?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true;
    }

    public sealed class HeroHomeSettlementSnapshotCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "home_settlement_snapshot";

        public string Description => "Reports registered hero home-settlement state.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("resolve_missing", "Whether missing home settlements should be resolved."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!bool.TryParse(args[0], out bool resolveMissing))
                return Failed($"Unable to parse {args[0]} as a boolean.");

            if (Campaign.Current == null)
                return Failed("Campaign is not loaded.");

            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
                return Failed($"Unable to get {nameof(IObjectManager)}");

            var homeSettlements = new SortedDictionary<string, string>(StringComparer.Ordinal);
            int cacheMissesBeforeRead = 0;
            int nullCount = 0;
            int unregisteredSettlementCount = 0;

            foreach (var hero in Campaign.Current.CampaignObjectManager.GetAllHeroes())
            {
                if (!objectManager.TryGetId(hero, out string heroId)) continue;

                if (hero._homeSettlement == null) cacheMissesBeforeRead++;
                var homeSettlement = resolveMissing ? hero.HomeSettlement : hero._homeSettlement;
                if (homeSettlement == null)
                {
                    homeSettlements.Add(heroId, null);
                    nullCount++;
                    continue;
                }

                if (objectManager.TryGetId(homeSettlement, out string settlementId))
                {
                    homeSettlements.Add(heroId, settlementId);
                }
                else
                {
                    homeSettlements.Add(heroId, $"unregistered:{homeSettlement.StringId}");
                    unregisteredSettlementCount++;
                }
            }

            string role = ModInformation.IsServer ? "server" : "client";
            string structuredState = JsonConvert.SerializeObject(new
            {
                role,
                resolveMissing,
                heroCount = homeSettlements.Count,
                cacheMissesBeforeRead,
                nullCount,
                unregisteredSettlementCount,
                homeSettlements,
            });

            return Succeeded($"role={role} resolveMissing={resolveMissing} heroCount={homeSettlements.Count} " +
                   $"cacheMissesBeforeRead={cacheMissesBeforeRead} nullCount={nullCount} " +
                   $"unregisteredSettlementCount={unregisteredSettlementCount}" + Environment.NewLine +
                   $"LIVE_TEST_JSON={structuredState}");
        }
    }

    // coop.debug.hero.info

    public sealed class HeroInfoCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "info";

        public string Description => "Dumps fields for a registered hero.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered hero id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }

            if (objectManager.TryGetObject<Hero>(args[0], out var hero) == false)
            {
                return Failed($"Unable to find hero with id: {args[0]}");
            }

            var fields = typeof(Hero).GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            var stringBuilder = new StringBuilder();

            foreach (var field in fields)
            {
                stringBuilder.AppendLine($"{field.Name} = {field.GetValue(hero)}");
            }

            var results = stringBuilder.ToString();

            Logger.Debug("{Hero}", results);

            return Succeeded(results);
        }
    }

    // coop.debug.hero.create_hero lord_2_7

    public sealed class HeroCreateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "create_hero";

        public string Description => "Creates a hero from a character template on the server.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("character_object_id", "The character template StringId."),
            new ExpectedArgs("age", "The optional hero age.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("Create hero is only to be called on the server");
            }

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }

            var age = -1;
            if (args.Count == 2 && int.TryParse(args[1], out age) == false)
            {
                return Succeeded($"{args[1].GetType().Name} was not of type int");
            }

            string characterObjectId = args[0];

            if (objectManager.TryGetObject<CharacterObject>(characterObjectId, out var template) == false)
            {
                return Failed($"Unable to get {typeof(CharacterObject)} with id: {characterObjectId}");
            }

            HeroCreator.CreateBasicHero(characterObjectId, template, out var newHero);

            return Succeeded($"Created new hero with string id: {newHero.StringId}");
        }
    }

    // coop.debug.hero.audit

    public sealed class HeroAuditCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "audit";

        public string Description => "Audits registered hero state.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ContainerProvider.TryResolve<HeroAuditor>(out var auditor) == false)
        {
                return Failed($"Unable to get {nameof(HeroAuditor)}");
            }

            return Succeeded(auditor.Audit());
        }
    }

    public sealed class HeroAddPowerCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "add_power";

        public string Description => "Adds power to a registered hero on the server.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered hero id."),
            new ExpectedArgs("power", "The integer power amount."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.add_power"))
            {
                return Failed(error);
            }

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }

            if (objectManager.TryGetObject<Hero>(args[0], out var hero) == false)
            {
                return Failed($"Unable to find hero with id: {args[0]}");
            }

            if (int.TryParse(args[1], out int power) == false)
            {
                return Failed($"{args[1]} is not a valid integer");
            }

            hero.AddPower(power);

            return Succeeded($"Hero power changed to: {hero.Power}");
        }
    }

    public sealed class HeroSetGoldCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "set_gold";

        public string Description => "Sets gold for every hero with an exact display name on the server.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_name", "The exact hero display name. Quote multi-word values."),
            new ExpectedArgs("gold", "The integer gold value."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.set_gold")) return Failed(error);

            if (int.TryParse(args[1], out int gold) == false)
            {
                return Failed($"{args[1]} is not a valid integer");
            }

            string heroName = args[0];

            var heroes = Campaign.Current.CampaignObjectManager.GetAllHeroes()
                .Where(h => h.Name?.ToString() == heroName)
                .ToList();

            if (heroes.Count == 0)
            {
                return Failed($"Unable to find hero with name: {heroName}");
            }

            foreach (var hero in heroes)
            {
                hero.Gold = gold;
            }

            return Succeeded($"Set gold to {gold} for {heroes.Count} hero(es) named '{heroName}'");
        }
    }

    public sealed class HeroGoldStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "gold_state";

        public string Description => "Reports gold for a registered hero.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered hero id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
                return Failed("Unable to resolve ObjectManager.");
            if (!objectManager.TryGetObject(args[0], out Hero hero))
                return Failed($"Hero with id {args[0]} not found.");

            return Succeeded($"HERO_GOLD_STATE hero={args[0]} gold={hero.Gold}");
        }
    }

    public sealed class HeroSetGoldStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "set_gold_state";

        public string Description => "Sets non-negative gold for a registered hero on the server.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered hero id."),
            new ExpectedArgs("gold", "The non-negative integer gold value."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.set_gold_state"))
                return Failed(error);
            if (!int.TryParse(args[1], out int gold) || gold < 0)
                return Failed($"Unable to parse non-negative gold amount: {args[1]}.");
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
                return Failed("Unable to resolve ObjectManager.");
            if (!objectManager.TryGetObject(args[0], out Hero hero))
                return Failed($"Hero with id {args[0]} not found.");

            int oldGold = hero.Gold;
            hero.Gold = gold;
            return Succeeded($"HERO_GOLD_SET hero={args[0]} oldGold={oldGold} newGold={hero.Gold}");
        }
    }

    public sealed class HeroSetAgeCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "set_age";

        public string Description => "Sets the age of heroes matching a display name or registered id on the server.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_name_or_id", "The exact hero display name or registered id. Quote multi-word values."),
            new ExpectedArgs("age", "The age in years."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.set_age")) return Failed(error);

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }

            if (float.TryParse(args[1], out float age) == false)
            {
                return Failed($"{args[1]} is not a valid float");
            }

            string heroNameOrId = args[0];
            var heroes = Campaign.Current.CampaignObjectManager.GetAllHeroes()
                .Where(h => h.Name?.ToString() == heroNameOrId)
                .ToList();

            if (heroes.Count == 0)
            {
                if (objectManager.TryGetObject<Hero>(heroNameOrId, out var hero))
                {
                    heroes.Add(hero);
                }
                else
                {
                    return Failed($"Unable to find hero with id or name: {heroNameOrId}");
                }
            }

            foreach (var hero in heroes)
            {
                var ageInTicks = (long)(CampaignTime.TimeTicksPerYear * age);
                hero.SetBirthDay(new CampaignTime(CampaignTime.CurrentTicks - ageInTicks));
            }

            return Succeeded($"Set age to {age} for {heroes.Count} hero(es) matching '{heroNameOrId}'");
        }
    }

    public sealed class HeroKillPlayerCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "kill_player";

        public string Description => "Kills a living registered player hero on the server.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered player hero id."),
            new ExpectedArgs("death_detail", "One of old_age, battle, or execution."),
            new ExpectedArgs("killer_hero_id", "The optional registered killer hero id.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.kill_player")) return Failed(error);

            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
                return Failed("Unable to resolve ObjectManager.");
            if (!objectManager.TryGetObject(args[0], out Hero hero))
                return Failed($"Hero with id {args[0]} not found.");
            if (!hero.IsPlayerHero() || !hero.IsAlive)
                return Failed("The hero must be a living registered player.");

            KillCharacterAction.KillCharacterActionDetail detail;
            switch (args[1].ToLowerInvariant())
            {
                case "old_age":
                    detail = KillCharacterAction.KillCharacterActionDetail.DiedOfOldAge;
                    break;
                case "battle":
                    detail = KillCharacterAction.KillCharacterActionDetail.DiedInBattle;
                    break;
                case "execution":
                    detail = KillCharacterAction.KillCharacterActionDetail.Executed;
                    break;
                default:
                    return Failed($"Unknown death detail: {args[1]}. Expected old_age, battle, or execution.");
            }

            Hero killer = null;
            if (args.Count == 3 && !objectManager.TryGetObject(args[2], out killer))
                return Failed($"Hero with id {args[2]} not found.");
            if (detail == KillCharacterAction.KillCharacterActionDetail.Executed && killer == null)
                return Failed("Execution requires a killer hero id, use coop.debug.hero.list to find one.");

            hero.AddDeathMark(killer, detail);
            KillCharacterAction.ApplyByDeathMarkForced(hero, true);
            return Succeeded($"Player {hero.Name} was killed with detail {detail}.");
        }
    }

    public sealed class HeroIllDaysCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "ill_days";

        public string Description => "Reports the local player's illness duration on clients or matching heroes on the server.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_name", "The exact hero display name required on the server. Quote multi-word values.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                if (Campaign.Current.MainHeroIllDays == -1)
                {
                    return Succeeded($"{Hero.MainHero.Name} is not ill.");
                }

                return Succeeded($"{Hero.MainHero.Name} has been ill for {Campaign.Current.MainHeroIllDays} day(s).");
            }

            if (args.Count == 0)
            {
                return Failed("A hero name is required when running this command on the server.");
            }

            if (!ContainerProvider.TryResolve<IAgingCampaignBehaviorInterface>(out var agingBehaviorInterface))
                return Failed("Unable to resolve behavior interface.");

            string heroName = args[0];

            var heroes = Campaign.Current.CampaignObjectManager.GetAllHeroes()
                .Where(h => h.Name?.ToString() == heroName)
                .ToList();

            if (heroes.Count == 0)
            {
                return Failed($"Unable to find hero with name: {heroName}");
            }

            StringBuilder stringBuilder = new StringBuilder();
            foreach (var hero in heroes)
            {
                stringBuilder.AppendLine($"{hero.StringId}: {agingBehaviorInterface.GetPlayerIllDays(hero)}");
            }

            return Succeeded(stringBuilder.ToString());
        }
    }

    // coop.debug.hero.set_hitpoints
    /// <summary>
    /// Sets the hitpoints of a hero
    /// </summary>
    /// <param name="args">heroId and hitPoints value to set </param>
    /// <returns>information if it changed</returns>

    public sealed class HeroSetHitpointsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "set_hitpoints";

        public string Description => "Sets hit points for a registered hero on the server.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered hero id."),
            new ExpectedArgs("hit_points", "The integer hit-point value."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.set_hitpoints"))
            {
                return Failed(error);
            }

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }

            if (objectManager.TryGetObject<Hero>(args[0], out var hero) == false)
            {
                return Failed($"Unable to find hero with id: {args[0]}");
            }

            if (int.TryParse(args[1], out int hitPoints) == false)
            {
                return Failed($"{args[1]} is not a valid integer");
            }

            hero.HitPoints = hitPoints;

            return Succeeded($"Hero HitPoints changed to: {hero.HitPoints}");
        }
    }
    // coop.debug.hero.set_banneritem
    /// <summary>
    /// Sets the banneritem of a hero
    /// </summary>
    /// <param name="args">heroId and BannerItem value to set </param>
    /// <returns>information if it changed</returns>

    public sealed class HeroSetBannerItemCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "set_banneritem";

        public string Description => "Sets the banner item for a registered hero on the server.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered hero id."),
            new ExpectedArgs("banner_item_id", "The banner item StringId."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.set_banneritem"))
            {
                return Failed(error);
            }

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }
            if (objectManager.TryGetObject<Hero>(args[0], out var hero) == false)
            {
                return Failed($"Unable to find hero with id: {args[0]}");
            }
            var bannerItem = MBObjectManager.Instance.GetObjectTypeList<ItemObject>()
                    .FirstOrDefault(i => i.StringId == args[1] && i.ItemType == ItemObject.ItemTypeEnum.Banner);
            if (bannerItem == null)
            {
                return Failed($"Unable to find banneritem with StringId: {args[1]}");
            }

            hero.BannerItem = new EquipmentElement(bannerItem);

            return Succeeded($"Hero BannerItem changed to: {hero.BannerItem.Item?.StringId}");
        }
    }
    // coop.debug.hero.list_banneritems
    /// <summary>
    /// Lists all available banneritems
    /// </summary>
    /// <param name="args">none are used</param>
    /// <returns>returns all banneritems </returns>

    public sealed class HeroListBannerItemsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "list_banneritems";

        public string Description => "Lists available banner items.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            StringBuilder stringBuilder = new StringBuilder();

            foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>()
                .Where(i => i.ItemType == ItemObject.ItemTypeEnum.Banner))
            {
                stringBuilder.AppendLine($"Name: '{item.Name}', Game StringId: '{item.StringId}'");
            }
            return Succeeded(stringBuilder.ToString());
        }
    }
    // coop.debug.hero.get_banneritem
    /// <summary>
    /// Gets bannerItem from hero
    /// </summary>
    /// <param name="args">HeroId</param>
    /// <returns>returns banneritem info from hero </returns>

    public sealed class HeroGetBannerItemCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "get_banneritem";

        public string Description => "Reports the banner item for a registered hero.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered hero id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }

            if (objectManager.TryGetObject<Hero>(args[0], out var hero) == false)
            {
                return Failed($"Unable to find hero with id: {args[0]}");
            }

            return Succeeded($"Hero BannerItem: {hero.BannerItem.Item?.StringId ?? "none"}");
        }
    }
    // coop.debug.hero.list_issues
    /// <summary>
    /// Lists all available issues
    /// </summary>
    /// <param name="args">none are used</param>
    /// <returns>returns all issues available </returns>

    public sealed class HeroIssuesCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "issues";

        public string Description => "Lists heroes with active issues.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }
            StringBuilder stringBuilder = new StringBuilder();

            foreach (var hero in Campaign.Current.CampaignObjectManager.GetAllHeroes()
                .Where(h => h.Issue != null))
            {
                stringBuilder.AppendLine($"Name: '{hero.StringId}', Game StringId: '{hero.Issue.StringId}'");
            }

            if (stringBuilder.Length == 0) return Failed("No heroes with issues found");

            return Succeeded(stringBuilder.ToString());
        }
    }
    // coop.debug.hero.set_issue
    /// <summary>
    /// Sets the issue of a hero
    /// </summary>
    /// <param name="args">heroId and issue value to set </param>
    /// <returns>information if it changed</returns>

    public sealed class HeroSetIssueCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "set_issue";

        public string Description => "Sets an issue for a registered hero on the server.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered hero id."),
            new ExpectedArgs("issue_id", "The issue StringId."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.set_issue"))
            {
                return Failed(error);
            }

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }
            if (objectManager.TryGetObject<Hero>(args[0], out var hero) == false)
            {
                return Failed($"Unable to find hero with id: {args[0]}");
            }
            var issue = Campaign.Current.CampaignObjectManager.GetAllHeroes()
                .Where(h => h.Issue != null)
                .Select(h => h.Issue)
                .FirstOrDefault(i => i.StringId == args[1]);
            if (issue == null)
            {
                return Failed($"Unable to find Issue with StringId: {args[1]}");
            }
            // cant use hero.Issue = issue since Issue is a private setter
            hero.OnIssueCreatedForHero(issue);

            return Succeeded($"Hero Issue changed to: {issue.StringId}");
        }
    }
    // coop.debug.hero.get_issue
    /// <summary>
    /// Gets Issue from hero
    /// </summary>
    /// <param name="args">HeroId</param>
    /// <returns>returns Issue info from hero </returns>

    public sealed class HeroGetIssueCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "get_issue";

        public string Description => "Reports the issue for a registered hero.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered hero id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }

            if (objectManager.TryGetObject<Hero>(args[0], out var hero) == false)
            {
                return Failed($"Unable to find hero with id: {args[0]}");
            }

            return Succeeded($"Hero Issue: {hero.Issue?.StringId ?? "none"}");
        }
    }

    /// <summary>
    /// View available volunteers for a target hero
    /// </summary>

    public sealed class HeroVolunteersCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "volunteers";

        public string Description => "Lists volunteers for a hero.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The hero StringId."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero.StringId == args[0])
                {
                    stringBuilder.AppendLine(hero.Name.ToString());
                    foreach (var volunteer in hero.VolunteerTypes)
                    {
                        if (volunteer == null)
                        {
                            stringBuilder.AppendLine("[EMPTY SLOT]");
                            continue;
                        }
                        stringBuilder.AppendLine(volunteer.Name.ToString());
                    }
                }
            }

            string result = stringBuilder.ToString();
            if (result.Length > 0)
            {
                return Succeeded(result);
            }
            return Failed("Hero not found.");
        }
    }

    /// <summary>
    /// Runs the authoritative volunteer refresh for one settlement.
    /// </summary>

    public sealed class HeroRefreshVolunteersCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "refresh_volunteers";

        public string Description => "Refreshes volunteers for a settlement on the server.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlement_id", "The optional settlement StringId; defaults to town_ES1.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.refresh_volunteers")) return Failed(error);

            string settlementId = args.Count == 0 ? "town_ES1" : args[0];
            var settlement = Settlement.All.FirstOrDefault(candidate => candidate.StringId == settlementId);
            if (settlement == null) return Failed($"Settlement '{settlementId}' not found.");

            var behavior = Campaign.Current?.GetCampaignBehavior<RecruitmentCampaignBehavior>();
            if (behavior == null) return Failed($"Unable to find {nameof(RecruitmentCampaignBehavior)}.");

            behavior.UpdateVolunteersOfNotablesInSettlement(settlement);
            return Succeeded($"Refreshed volunteers for {settlement.Name} ({settlement.StringId}).");
        }
    }

    // coop.debug.hero.set_relation

    public sealed class HeroSetRelationCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "set_relation";

        public string Description => "Sets the base relation between two registered heroes.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero1_id", "The first registered hero id."),
            new ExpectedArgs("hero2_id", "The second registered hero id."),
            new ExpectedArgs("value", "The integer relation value."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("Set relation is only to be called on the server");
            }

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }

            if (objectManager.TryGetObject<Hero>(args[0], out var hero1) == false)
            {
                return Failed($"Unable to find hero with id: {args[0]}");
            }

            if (objectManager.TryGetObject<Hero>(args[1], out var hero2) == false)
            {
                return Failed($"Unable to find hero with id: {args[1]}");
            }

            if (hero1 == hero2)
            {
                return Failed("A hero cannot have a relation with itself");
            }

            if (int.TryParse(args[2], out int value) == false)
            {
                return Failed($"{args[2]} is not a valid integer");
            }

            CharacterRelationManager.SetHeroRelation(hero1, hero2, value);

            return Succeeded($"Set relation between '{hero1.Name}' and '{hero2.Name}' to {CharacterRelationManager.GetHeroRelation(hero1, hero2)}");
        }
    }

    // coop.debug.hero.get_relation

    public sealed class HeroGetRelationCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "get_relation";

        public string Description => "Reports the base relation between two registered heroes.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero1_id", "The first registered hero id."),
            new ExpectedArgs("hero2_id", "The second registered hero id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }

            if (objectManager.TryGetObject<Hero>(args[0], out var hero1) == false)
            {
                return Failed($"Unable to find hero with id: {args[0]}");
            }

            if (objectManager.TryGetObject<Hero>(args[1], out var hero2) == false)
            {
                return Failed($"Unable to find hero with id: {args[1]}");
            }

            return Succeeded($"Relation between '{hero1.Name}' and '{hero2.Name}': {CharacterRelationManager.GetHeroRelation(hero1, hero2)}");
        }
    }

    public sealed class HeroGetEffectiveRelationCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "get_effective_relation";

        public string Description => "Reports the effective relation between two registered heroes.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero1_id", "The first registered hero id."),
            new ExpectedArgs("hero2_id", "The second registered hero id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }

            if (objectManager.TryGetObject<Hero>(args[0], out var hero1) == false)
            {
                return Failed($"Unable to find hero with id: {args[0]}");
            }

            if (objectManager.TryGetObject<Hero>(args[1], out var hero2) == false)
            {
                return Failed($"Unable to find hero with id: {args[1]}");
            }

            var campaign = Campaign.Current;
            if (campaign?.Models?.DiplomacyModel == null)
            {
                return Failed("Campaign diplomacy model is not available");
            }

            campaign.Models.DiplomacyModel.GetHeroesForEffectiveRelation(
                hero1,
                hero2,
                out var effectiveHero1,
                out var effectiveHero2);
            if (effectiveHero1 == null || effectiveHero2 == null)
            {
                return Failed("Unable to resolve effective relation heroes");
            }

            return Succeeded($"Effective relation between '{effectiveHero1.Name}' and '{effectiveHero2.Name}': " +
                CharacterRelationManager.GetHeroRelation(effectiveHero1, effectiveHero2));
        }
    }

    public sealed class HeroSetEffectiveRelationCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "set_effective_relation";

        public string Description => "Sets effective relation between two registered heroes.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero1_id", "The first registered hero id."),
            new ExpectedArgs("hero2_id", "The second registered hero id."),
            new ExpectedArgs("value", "The integer effective relation value."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("Set effective relation is only to be called on the server");
            }

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed($"Unable to get {nameof(IObjectManager)}");
            }

            if (objectManager.TryGetObject<Hero>(args[0], out var hero1) == false)
            {
                return Failed($"Unable to find hero with id: {args[0]}");
            }

            if (objectManager.TryGetObject<Hero>(args[1], out var hero2) == false)
            {
                return Failed($"Unable to find hero with id: {args[1]}");
            }

            if (int.TryParse(args[2], out int value) == false)
            {
                return Failed($"{args[2]} is not a valid integer");
            }

            var campaign = Campaign.Current;
            if (campaign?.Models?.DiplomacyModel == null)
            {
                return Failed("Campaign diplomacy model is not available");
            }

            campaign.Models.DiplomacyModel.GetHeroesForEffectiveRelation(
                hero1,
                hero2,
                out var effectiveHero1,
                out var effectiveHero2);
            if (effectiveHero1 == null || effectiveHero2 == null)
            {
                return Failed("Unable to resolve effective relation heroes");
            }

            if (effectiveHero1 == effectiveHero2)
            {
                return Failed("A hero cannot have a relation with itself");
            }

            CharacterRelationManager.SetHeroRelation(effectiveHero1, effectiveHero2, value);

            return Succeeded($"Set effective relation between '{effectiveHero1.Name}' and '{effectiveHero2.Name}' to " +
                CharacterRelationManager.GetHeroRelation(effectiveHero1, effectiveHero2));
        }
    }
}
