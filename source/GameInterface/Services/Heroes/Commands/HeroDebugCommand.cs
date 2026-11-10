using Common.Commands;
using Common;
using Common.Logging;
using GameInterface.Configuration;
using GameInterface.Services.Heroes.Audit;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.ObjectManager.Extensions;
using GameInterface.Services.Players;
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
using TaleWorlds.CampaignSystem.Party;
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

        public CoopCommandSide Side => CoopCommandSide.Both;

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

        public CoopCommandSide Side => CoopCommandSide.Both;

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
        private readonly ICommandObjectResolver resolver;

        public HeroInfoCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.hero";

        public string Name => "info";

        public string Description => "Dumps fields for a registered hero.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "Hero name or id; quote names with spaces."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!resolver.TryResolveHero(args[0], out var hero, out var heroError))
            {
                return Failed(heroError);
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
        private readonly ICommandObjectResolver resolver;

        public HeroCreateCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.hero";

        public string Name => "create_hero";

        public string Description => "Creates a hero from a character template on the server.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("character_object_id", "Character template name or id; quote names with spaces."),
            new ExpectedArgs("age", "The optional hero age.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("Create hero is only to be called on the server");
            }

            var age = -1;
            if (args.Count == 2 && int.TryParse(args[1], out age) == false)
            {
                return Succeeded($"{args[1].GetType().Name} was not of type int");
            }

            if (!resolver.TryResolveCharacterObject(args[0], out var template, out var templateError))
            {
                return Failed(templateError);
            }

            HeroCreator.CreateBasicHero(template.StringId, template, out var newHero);

            return Succeeded($"Created new hero with string id: {newHero.StringId}");
        }
    }

    // coop.debug.hero.audit

    public sealed class HeroAuditCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "audit";

        public string Description => "Audits registered hero state.";

        public CoopCommandSide Side => CoopCommandSide.Both;

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
        private readonly ICommandObjectResolver resolver;

        public HeroAddPowerCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.hero";

        public string Name => "add_power";

        public string Description => "Adds power to a registered hero on the server.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "Hero name or id; quote names with spaces."),
            new ExpectedArgs("power", "The integer power amount."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.add_power"))
            {
                return Failed(error);
            }

            if (!resolver.TryResolveHero(args[0], out var hero, out var heroError))
            {
                return Failed(heroError);
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
        private readonly ICommandObjectResolver resolver;

        public HeroSetGoldCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.hero";

        public string Name => "set_gold";

        public string Description => "Sets gold for a hero by display name or registered id on the server.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_name", "Hero name or id; quote names with spaces."),
            new ExpectedArgs("gold", "The integer gold value."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.set_gold")) return Failed(error);

            if (int.TryParse(args[1], out int gold) == false)
            {
                return Failed($"{args[1]} is not a valid integer");
            }

            if (!resolver.TryResolveHero(args[0], out var hero, out var heroError))
            {
                return Failed(heroError);
            }

            hero.Gold = gold;

            return Succeeded($"Set gold to {gold} for hero '{hero.Name}'");
        }
    }

    public sealed class HeroGoldStateCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;
        private readonly IObjectManager objectManager;

        public HeroGoldStateCoopCommand(ICommandObjectResolver resolver, IObjectManager objectManager)
        {
            this.resolver = resolver;
            this.objectManager = objectManager;
        }

        public string Prefix => "coop.debug.hero";

        public string Name => "gold_state";

        public string Description => "Reports gold for a registered hero.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "Hero name or id; quote names with spaces."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!resolver.TryResolveHero(args[0], out var hero, out var heroError))
                return Failed(heroError);
            if (!objectManager.TryGetId(hero, out var heroId))
                return Failed($"Hero '{hero.Name}' is not registered with the object manager.");

            return Succeeded($"HERO_GOLD_STATE hero={heroId} gold={hero.Gold}");
        }
    }

    public sealed class HeroSetGoldStateCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;
        private readonly IObjectManager objectManager;

        public HeroSetGoldStateCoopCommand(ICommandObjectResolver resolver, IObjectManager objectManager)
        {
            this.resolver = resolver;
            this.objectManager = objectManager;
        }

        public string Prefix => "coop.debug.hero";

        public string Name => "set_gold_state";

        public string Description => "Sets non-negative gold for a registered hero on the server.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "Hero name or id; quote names with spaces."),
            new ExpectedArgs("gold", "The non-negative integer gold value."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.set_gold_state"))
                return Failed(error);
            if (!int.TryParse(args[1], out int gold) || gold < 0)
                return Failed($"Unable to parse non-negative gold amount: {args[1]}.");
            if (!resolver.TryResolveHero(args[0], out var hero, out var heroError))
                return Failed(heroError);
            if (!objectManager.TryGetId(hero, out var heroId))
                return Failed($"Hero '{hero.Name}' is not registered with the object manager.");

            int oldGold = hero.Gold;
            hero.Gold = gold;
            return Succeeded($"HERO_GOLD_SET hero={heroId} oldGold={oldGold} newGold={hero.Gold}");
        }
    }

    public sealed class HeroSetAgeCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;

        public HeroSetAgeCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.hero";

        public string Name => "set_age";

        public string Description => "Sets the age of a hero by display name or registered id on the server.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_name_or_id", "Hero name or id; quote names with spaces."),
            new ExpectedArgs("age", "The age in years."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.set_age")) return Failed(error);

            if (float.TryParse(args[1], out float age) == false)
            {
                return Failed($"{args[1]} is not a valid float");
            }

            if (!resolver.TryResolveHero(args[0], out var hero, out var heroError))
            {
                return Failed(heroError);
            }

            var ageInTicks = (long)(CampaignTime.TimeTicksPerYear * age);
            hero.SetBirthDay(new CampaignTime(CampaignTime.CurrentTicks - ageInTicks));

            return Succeeded($"Set age to {age} for hero '{hero.Name}'");
        }
    }

    public sealed class HeroKillPlayerCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;
        private readonly IObjectManager objectManager;

        public HeroKillPlayerCoopCommand(ICommandObjectResolver resolver, IObjectManager objectManager)
        {
            this.resolver = resolver;
            this.objectManager = objectManager;
        }

        public string Prefix => "coop.debug.hero";

        public string Name => "kill_player";

        public string Description => "Kills a living registered player hero on the server.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "Player hero name or id; quote names with spaces."),
            new ExpectedArgs("death_detail", "One of old_age, battle, or execution."),
            new ExpectedArgs("killer_hero_id", "Optional killer hero name or id; quote names with spaces.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.kill_player")) return Failed(error);
            return KillRegisteredHero(resolver, objectManager, args, requirePlayer: true);
        }
    }

    public sealed class HeroKillNpcCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;
        private readonly IObjectManager objectManager;

        public HeroKillNpcCoopCommand(ICommandObjectResolver resolver, IObjectManager objectManager)
        {
            this.resolver = resolver;
            this.objectManager = objectManager;
        }

        public string Prefix => "coop.debug.hero";
        public string Name => "kill_npc";
        public string Description => "Kills a living registered NPC through the native death action in a registered player's context.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "NPC hero name or id; quote names with spaces."),
            new ExpectedArgs("death_detail", "One of old_age, battle, or execution."),
            new ExpectedArgs("controller_id", "The acting player's registered controller id."),
            new ExpectedArgs("killer_hero_id", "Optional killer hero name or id; quote names with spaces.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.kill_npc")) return Failed(error);
            return KillRegisteredHero(resolver, objectManager, args, requirePlayer: false);
        }
    }

    // Resolves the target (and optional killer) hero by name or id, then applies the native death action.
    private static CoopCommandResult KillRegisteredHero(ICommandObjectResolver resolver, IObjectManager objectManager, ICoopCommandArgs args, bool requirePlayer)
    {
        if (!resolver.TryResolveHero(args[0], out var hero, out var heroError))
            return Failed(heroError);
        if (hero.IsPlayerHero() != requirePlayer || !hero.IsAlive)
            return Failed(requirePlayer ? "The hero must be a living registered player." : "The hero must be a living registered NPC.");

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

        var killerIndex = requirePlayer ? 2 : 3;
        Hero killer = null;
        if (args.Count > killerIndex && !resolver.TryResolveHero(args[killerIndex], out killer, out heroError))
            return Failed(heroError);
        if (detail == KillCharacterAction.KillCharacterActionDetail.Executed && killer == null)
            return Failed("Execution requires a killer hero id, use coop.debug.hero.list to find one.");

        Hero actor = null;
        MobileParty party = null;
        if (!requirePlayer && (!ContainerProvider.TryResolve<IPlayerManager>(out var players) ||
            !players.TryGetPlayer(args[2], out var player) ||
            !objectManager.TryGetObject(player.HeroId, out actor) ||
            !objectManager.TryGetObject(player.MobilePartyId, out party)))
            return Failed("The acting player's registered hero and party are required.");
        if (!requirePlayer && detail == KillCharacterAction.KillCharacterActionDetail.Executed &&
            killer.IsPlayerHero() && killer != actor)
            return Failed("A player execution killer must match the acting controller's hero.");

        using (requirePlayer ? null : new MainHeroSubstitutionScope(actor, party))
        {
            hero.AddDeathMark(killer, detail);
            KillCharacterAction.ApplyByDeathMarkForced(hero, true);
        }
        return Succeeded($"{(requirePlayer ? "Player" : "NPC")} {hero.Name} was killed with detail {detail}.");
    }

    public sealed class HeroIllDaysCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;

        public HeroIllDaysCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.hero";

        public string Name => "ill_days";

        public string Description => "Reports the local player's illness duration on clients or a hero's on the server.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_name", "Hero name or id required on the server; quote names with spaces.", false),
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
                return Failed("A hero name or id is required when running this command on the server.");
            }

            if (!ContainerProvider.TryResolve<IAgingCampaignBehaviorInterface>(out var agingBehaviorInterface))
                return Failed("Unable to resolve behavior interface.");

            if (!resolver.TryResolveHero(args[0], out var hero, out var heroError))
            {
                return Failed(heroError);
            }

            return Succeeded($"{hero.StringId}: {agingBehaviorInterface.GetPlayerIllDays(hero)}{Environment.NewLine}");
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
        private readonly ICommandObjectResolver resolver;

        public HeroSetHitpointsCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.hero";

        public string Name => "set_hitpoints";

        public string Description => "Sets hit points for a registered hero on the server.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "Hero name or id; quote names with spaces."),
            new ExpectedArgs("hit_points", "The integer hit-point value."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.set_hitpoints"))
            {
                return Failed(error);
            }

            if (!resolver.TryResolveHero(args[0], out var hero, out var heroError))
            {
                return Failed(heroError);
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
        private readonly ICommandObjectResolver resolver;

        public HeroSetBannerItemCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.hero";

        public string Name => "set_banneritem";

        public string Description => "Sets the banner item for a registered hero on the server.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "Hero name or id; quote names with spaces."),
            new ExpectedArgs("banner_item_id", "Banner item name or id; quote names with spaces."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.set_banneritem"))
            {
                return Failed(error);
            }

            if (!resolver.TryResolveHero(args[0], out var hero, out var heroError))
            {
                return Failed(heroError);
            }
            var bannerItems = MBObjectManager.Instance.GetObjectTypeList<ItemObject>()
                .Where(i => i.ItemType == ItemObject.ItemTypeEnum.Banner);
            if (!resolver.TryResolve(args[1], bannerItems, i => i.Name?.ToString(), out var bannerItem, out var itemError))
            {
                return Failed(itemError);
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

        public CoopCommandSide Side => CoopCommandSide.Both;

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
        private readonly ICommandObjectResolver resolver;

        public HeroGetBannerItemCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.hero";

        public string Name => "get_banneritem";

        public string Description => "Reports the banner item for a registered hero.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "Hero name or id; quote names with spaces."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!resolver.TryResolveHero(args[0], out var hero, out var heroError))
            {
                return Failed(heroError);
            }

            return Succeeded($"Hero BannerItem: {hero.BannerItem.Item?.StringId ?? "none"}");
        }
    }
    // coop.debug.hero.issues
    /// <summary>
    /// Lists heroes with active issues and the concrete type of each issue
    /// </summary>
    /// <param name="args">none are used</param>
    /// <returns>hero StringId, issue StringId and issue type of every active issue</returns>
    public sealed class HeroIssuesCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero";

        public string Name => "issues";

        public string Description => "Lists heroes with active issues and each issue's type.";

        public CoopCommandSide Side => CoopCommandSide.Both;

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
                stringBuilder.AppendLine(FormatHeroIssue(hero));
            }

            if (stringBuilder.Length == 0) return Failed("No heroes with issues found");

            return Succeeded(stringBuilder.ToString());
        }

        internal static string FormatHeroIssue(Hero hero)
        {
            return $"Hero StringId: '{hero.StringId}', Issue StringId: '{hero.Issue.StringId}', Issue Type: '{hero.Issue.GetType().FullName}'";
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
        private readonly ICommandObjectResolver resolver;

        public HeroSetIssueCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.hero";

        public string Name => "set_issue";

        public string Description => "Sets an issue for a registered hero on the server.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "Hero name or id; quote names with spaces."),
            new ExpectedArgs("issue_id", "The issue StringId."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.set_issue"))
            {
                return Failed(error);
            }

            if (!resolver.TryResolveHero(args[0], out var hero, out var heroError))
            {
                return Failed(heroError);
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
        private readonly ICommandObjectResolver resolver;

        public HeroGetIssueCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.hero";

        public string Name => "get_issue";

        public string Description => "Reports the issue for a registered hero.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "Hero name or id; quote names with spaces."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!resolver.TryResolveHero(args[0], out var hero, out var heroError))
            {
                return Failed(heroError);
            }

            return Succeeded($"Hero Issue: {hero.Issue?.StringId ?? "none"}");
        }
    }

    /// <summary>
    /// View available volunteers for a target hero
    /// </summary>
    public sealed class HeroVolunteersCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;

        public HeroVolunteersCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.hero";

        public string Name => "volunteers";

        public string Description => "Lists volunteers for a hero.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "Hero name or id; quote names with spaces."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!resolver.TryResolveHero(args[0], out var hero, out var heroError))
            {
                return Failed(heroError);
            }

            StringBuilder stringBuilder = new StringBuilder();
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

            return Succeeded(stringBuilder.ToString());
        }
    }

    /// <summary>
    /// Runs the authoritative volunteer refresh for one settlement.
    /// </summary>
    public sealed class HeroRefreshVolunteersCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;

        public HeroRefreshVolunteersCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.hero";

        public string Name => "refresh_volunteers";

        public string Description => "Refreshes volunteers for a settlement on the server.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlement_id", "Optional settlement name or id; defaults to town_ES1. Quote names with spaces.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.refresh_volunteers")) return Failed(error);

            string settlementQuery = args.Count == 0 ? "town_ES1" : args[0];
            if (!resolver.TryResolveSettlement(settlementQuery, out var settlement, out var settlementError))
                return Failed(settlementError);

            var behavior = Campaign.Current?.GetCampaignBehavior<RecruitmentCampaignBehavior>();
            if (behavior == null) return Failed($"Unable to find {nameof(RecruitmentCampaignBehavior)}.");

            behavior.UpdateVolunteersOfNotablesInSettlement(settlement);
            return Succeeded($"Refreshed volunteers for {settlement.Name} ({settlement.StringId}).");
        }
    }

    // coop.debug.hero.set_relation

    public sealed class HeroSetRelationCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;

        public HeroSetRelationCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.hero";

        public string Name => "set_relation";

        public string Description => "Sets the base relation between two registered heroes.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero1_id", "First hero name or id; quote names with spaces."),
            new ExpectedArgs("hero2_id", "Second hero name or id; quote names with spaces."),
            new ExpectedArgs("value", "The integer relation value."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("Set relation is only to be called on the server");
            }

            if (!resolver.TryResolveHero(args[0], out var hero1, out var heroError))
            {
                return Failed(heroError);
            }

            if (!resolver.TryResolveHero(args[1], out var hero2, out heroError))
            {
                return Failed(heroError);
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
        private readonly ICommandObjectResolver resolver;

        public HeroGetRelationCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.hero";

        public string Name => "get_relation";

        public string Description => "Reports the base relation between two registered heroes.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero1_id", "First hero name or id; quote names with spaces."),
            new ExpectedArgs("hero2_id", "Second hero name or id; quote names with spaces."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!resolver.TryResolveHero(args[0], out var hero1, out var heroError))
            {
                return Failed(heroError);
            }

            if (!resolver.TryResolveHero(args[1], out var hero2, out heroError))
            {
                return Failed(heroError);
            }

            return Succeeded($"Relation between '{hero1.Name}' and '{hero2.Name}': {CharacterRelationManager.GetHeroRelation(hero1, hero2)}");
        }
    }

    public sealed class HeroGetEffectiveRelationCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;

        public HeroGetEffectiveRelationCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.hero";

        public string Name => "get_effective_relation";

        public string Description => "Reports the effective relation between two registered heroes.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero1_id", "First hero name or id; quote names with spaces."),
            new ExpectedArgs("hero2_id", "Second hero name or id; quote names with spaces."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!resolver.TryResolveHero(args[0], out var hero1, out var heroError))
            {
                return Failed(heroError);
            }

            if (!resolver.TryResolveHero(args[1], out var hero2, out heroError))
            {
                return Failed(heroError);
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
        private readonly ICommandObjectResolver resolver;

        public HeroSetEffectiveRelationCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.hero";

        public string Name => "set_effective_relation";

        public string Description => "Sets effective relation between two registered heroes.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero1_id", "First hero name or id; quote names with spaces."),
            new ExpectedArgs("hero2_id", "Second hero name or id; quote names with spaces."),
            new ExpectedArgs("value", "The integer effective relation value."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("Set effective relation is only to be called on the server");
            }

            if (!resolver.TryResolveHero(args[0], out var hero1, out var heroError))
            {
                return Failed(heroError);
            }

            if (!resolver.TryResolveHero(args[1], out var hero2, out heroError))
            {
                return Failed(heroError);
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
