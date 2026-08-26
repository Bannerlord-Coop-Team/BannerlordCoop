using Common;
using Common.Logging;
using GameInterface.Configuration;
using GameInterface.Services.Heroes.Audit;
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
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Heroes.Commands;

public class HeroDebugCommand
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroDebugCommand>();

    // coop.debug.hero.list
    /// <summary>
    /// Lists heroes whose names start with the optional prefix
    /// </summary>
    /// <param name="args">Optional case-insensitive hero name prefix</param>
    /// <returns>Strings of the matching heroes</returns>
    [CommandLineArgumentFunction("list", "coop.debug.hero")]
    public static string ListHeroes(List<string> args)
    {
        StringBuilder stringBuilder = new StringBuilder();

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        string namePrefix = args == null ? string.Empty : string.Join(" ", args).Trim();
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

        return stringBuilder.Length == 0 && string.IsNullOrEmpty(namePrefix) == false
            ? $"No hero with a name starting with '{namePrefix}' was found."
            : stringBuilder.ToString();
    }

    internal static bool NameStartsWithPrefix(string heroName, string prefix)
    {
        return string.IsNullOrEmpty(prefix) ||
               heroName?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true;
    }

    [CommandLineArgumentFunction("home_settlement_snapshot", "coop.debug.hero")]
    public static string HomeSettlementSnapshot(List<string> args)
    {
        if (args.Count != 1 || !bool.TryParse(args[0], out bool resolveMissing))
            return "Usage: coop.debug.hero.home_settlement_snapshot <true|false>";

        if (Campaign.Current == null)
            return "Campaign is not loaded.";

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return $"Unable to get {nameof(IObjectManager)}";

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

        return $"role={role} resolveMissing={resolveMissing} heroCount={homeSettlements.Count} " +
               $"cacheMissesBeforeRead={cacheMissesBeforeRead} nullCount={nullCount} " +
               $"unregisteredSettlementCount={unregisteredSettlementCount}" + Environment.NewLine +
               $"LIVE_TEST_JSON={structuredState}";
    }

    [CommandLineArgumentFunction("id", "coop.debug.hero")]
    public static string FindIds(List<string> args)
    {
        if (args == null || args.Count == 0)
        {
            return "Usage: coop.debug.hero.id <hero name>";
        }

        var heroName = string.Join(" ", args).Trim();
        if (string.IsNullOrWhiteSpace(heroName)) return "Usage: coop.debug.hero.id <hero name>";

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        var campaign = Campaign.Current;
        if (campaign == null) return "Campaign is not loaded.";

        var heroes = campaign.CampaignObjectManager.GetAllHeroes()
            .Where(hero => string.Equals(hero.Name?.ToString(), heroName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (heroes.Count == 0) return $"No hero named '{heroName}' was found.";

        var result = new StringBuilder();
        foreach (var hero in heroes)
        {
            if (objectManager.TryGetId(hero, out var id))
            {
                result.AppendLine($"ID: '{id}', Name: '{hero.Name}', Game StringId: {hero.StringId}");
            }
            else
            {
                result.AppendLine($"Name: '{hero.Name}' was not registered with object manager");
            }
        }

        return result.ToString();
    }

    // coop.debug.hero.info
    [CommandLineArgumentFunction("info", "coop.debug.hero")]
    public static string Info(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.hero.info <heroId>";
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        if (objectManager.TryGetObject<Hero>(args[0], out var hero) == false)
        {
            return $"Unable to find hero with id: {args[0]}";
        }

        var fields = typeof(Hero).GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

        var stringBuilder = new StringBuilder();

        foreach (var field in fields)
        {
            stringBuilder.AppendLine($"{field.Name} = {field.GetValue(hero)}");
        }

        var results = stringBuilder.ToString();

        Logger.Debug("{Hero}", results);

        return results;
    }

    // coop.debug.hero.createHero lord_2_7
    [CommandLineArgumentFunction("createHero", "coop.debug.hero")]
    public static string CreateNewHero(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Create hero is only to be called on the server";
        }

        if (args.Count < 1 || args.Count > 2)
        {
            return "Usage: coop.debug.hero.createHero <CharacterObject.StringId> <optional age>";
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        var age = -1;
        if (args.Count == 2 && int.TryParse(args[1], out age) == false)
        {
            return $"{args[1].GetType().Name} was not of type int";
        }

        string characterObjectId = args[0];

        if (objectManager.TryGetObject<CharacterObject>(characterObjectId, out var template) == false)
        {
            return $"Unable to get {typeof(CharacterObject)} with id: {characterObjectId}";
        }

        HeroCreator.CreateBasicHero(characterObjectId, template, out var newHero);

        return $"Created new hero with string id: {newHero.StringId}";
    }

    // coop.debug.hero.audit
    [CommandLineArgumentFunction("audit", "coop.debug.hero")]
    public static string AuditHeroes(List<string> args)
    {
        if (ContainerProvider.TryResolve<HeroAuditor>(out var auditor) == false)
        {
            return $"Unable to get {nameof(HeroAuditor)}";
        }

        return auditor.Audit();
    }

    [CommandLineArgumentFunction("add_power", "coop.debug.hero")]
    public static string AddPower(List<string> args)
    {
        if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.add_power"))
        {
            return error;
        }

        if (args.Count != 2)
        {
            return "Usage: coop.debug.hero.add_power <heroId> <power>";
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        if (objectManager.TryGetObject<Hero>(args[0], out var hero) == false)
        {
            return $"Unable to find hero with id: {args[0]}";
        }

        if (int.TryParse(args[1], out int power) == false)
        {
            return $"{args[1]} is not a valid integer";
        }

        hero.AddPower(power);

        return $"Hero power changed to: {hero.Power}";
    }

    [CommandLineArgumentFunction("SetGold", "coop.debug.hero")]
    public static string SetGold(List<string> args)
    {
        if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.SetGold")) return error;

        if (args.Count < 2)
        {
            return "Usage: coop.debug.hero.SetGold <heroName> <gold>";
        }

        if (int.TryParse(args[args.Count - 1], out int gold) == false)
        {
            return $"{args[args.Count - 1]} is not a valid integer";
        }

        // Everything before the gold value is treated as the hero name (supports multi-word names)
        string heroName = string.Join(" ", args.Take(args.Count - 1));

        var heroes = Campaign.Current.CampaignObjectManager.GetAllHeroes()
            .Where(h => h.Name?.ToString() == heroName)
            .ToList();

        if (heroes.Count == 0)
        {
            return $"Unable to find hero with name: {heroName}";
        }

        foreach (var hero in heroes)
        {
            hero.Gold = gold;
        }

        return $"Set gold to {gold} for {heroes.Count} hero(es) named '{heroName}'";
    }

    [CommandLineArgumentFunction("gold_state", "coop.debug.hero")]
    public static string GoldState(List<string> args)
    {
        if (args.Count != 1) return "Usage: coop.debug.hero.gold_state <hero id>";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(args[0], out Hero hero))
            return $"Hero with id {args[0]} not found.";

        return $"HERO_GOLD_STATE hero={args[0]} gold={hero.Gold}";
    }

    [CommandLineArgumentFunction("set_gold_state", "coop.debug.hero")]
    public static string SetGoldState(List<string> args)
    {
        if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.set_gold_state"))
            return error;
        if (args.Count != 2 || !int.TryParse(args[1], out int gold) || gold < 0)
            return "Usage: coop.debug.hero.set_gold_state <hero id> <non-negative gold>";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(args[0], out Hero hero))
            return $"Hero with id {args[0]} not found.";

        int oldGold = hero.Gold;
        hero.Gold = gold;
        return $"HERO_GOLD_SET hero={args[0]} oldGold={oldGold} newGold={hero.Gold}";
    }

    // coop.debug.hero.set_hitpoints
    /// <summary>
    /// Sets the hitpoints of a hero
    /// </summary>
    /// <param name="args">heroId and hitPoints value to set </param>
    /// <returns>information if it changed</returns>
    [CommandLineArgumentFunction("set_hitpoints", "coop.debug.hero")]
    public static string SetHeroHitPoints(List<string> args)
    {
        if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.set_hitpoints"))
        {
            return error;
        }

        if (args.Count != 2)
        {
            return "Usage: coop.debug.hero.set_hitpoints <heroId> <hitPoints>";
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        if (objectManager.TryGetObject<Hero>(args[0], out var hero) == false)
        {
            return $"Unable to find hero with id: {args[0]}";
        }

        if (int.TryParse(args[1], out int hitPoints) == false)
        {
            return $"{args[1]} is not a valid integer";
        }

        hero.HitPoints = hitPoints;

        return $"Hero HitPoints changed to: {hero.HitPoints}";
    }
    // coop.debug.hero.set_banneritem
    /// <summary>
    /// Sets the banneritem of a hero
    /// </summary>
    /// <param name="args">heroId and BannerItem value to set </param>
    /// <returns>information if it changed</returns>
    [CommandLineArgumentFunction("set_banneritem", "coop.debug.hero")]
    public static string SetHeroBannerItem(List<string> args)
    {
        if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.set_banneritem"))
        {
            return error;
        }

        if (args.Count != 2)
        {
            return "Usage: coop.debug.hero.set_banneritem <heroId> <bannerItem>";
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }
        if (objectManager.TryGetObject<Hero>(args[0], out var hero) == false)
        {
            return $"Unable to find hero with id: {args[0]}";
        }
        var bannerItem = MBObjectManager.Instance.GetObjectTypeList<ItemObject>()
                .FirstOrDefault(i => i.StringId == args[1] && i.ItemType == ItemObject.ItemTypeEnum.Banner);
        if (bannerItem == null)
        {
            return $"Unable to find banneritem with StringId: {args[1]}";
        }

        hero.BannerItem = new EquipmentElement(bannerItem);

        return $"Hero BannerItem changed to: {hero.BannerItem.Item?.StringId}";
    }
    // coop.debug.hero.list_banneritems
    /// <summary>
    /// Lists all available banneritems
    /// </summary>
    /// <param name="args">none are used</param>
    /// <returns>returns all banneritems </returns>
    [CommandLineArgumentFunction("list_banneritems", "coop.debug.hero")]
    public static string ListBannerItems(List<string> args)
    {
        StringBuilder stringBuilder = new StringBuilder();

        foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>()
            .Where(i => i.ItemType == ItemObject.ItemTypeEnum.Banner))
        {
            stringBuilder.AppendLine($"Name: '{item.Name}', Game StringId: '{item.StringId}'");
        }
        return stringBuilder.ToString();
    }
    // coop.debug.hero.get_banneritem
    /// <summary>
    /// Gets bannerItem from hero
    /// </summary>
    /// <param name="args">HeroId</param>
    /// <returns>returns banneritem info from hero </returns>
    [CommandLineArgumentFunction("get_banneritem", "coop.debug.hero")]
    public static string GetHeroBannerItem(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.hero.get_banneritem <heroId>";
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        if (objectManager.TryGetObject<Hero>(args[0], out var hero) == false)
        {
            return $"Unable to find hero with id: {args[0]}";
        }

        return $"Hero BannerItem: {hero.BannerItem.Item?.StringId ?? "none"}";
    }
    // coop.debug.hero.list_issues
    /// <summary>
    /// Lists all available issues
    /// </summary>
    /// <param name="args">none are used</param>
    /// <returns>returns all issues available </returns>
    [CommandLineArgumentFunction("issues", "coop.debug.hero")]
    public static string ListIssues(List<string> args)
    {
        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }
        StringBuilder stringBuilder = new StringBuilder();

        foreach (var hero in Campaign.Current.CampaignObjectManager.GetAllHeroes()
            .Where(h => h.Issue != null))
        {
            stringBuilder.AppendLine($"Name: '{hero.StringId}', Game StringId: '{hero.Issue.StringId}'");
        }

        if (stringBuilder.Length == 0) return "No heroes with issues found";

        return stringBuilder.ToString();
    }
    // coop.debug.hero.set_issue
    /// <summary>
    /// Sets the issue of a hero
    /// </summary>
    /// <param name="args">heroId and issue value to set </param>
    /// <returns>information if it changed</returns>
    [CommandLineArgumentFunction("set_issue", "coop.debug.hero")]
    public static string SetHeroIssue(List<string> args)
    {
        if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.set_issue"))
        {
            return error;
        } 

        if (args.Count != 2)
        {
            return "Usage: coop.debug.hero.set_issue <heroId> <issueId>";
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }
        if (objectManager.TryGetObject<Hero>(args[0], out var hero) == false)
        {
            return $"Unable to find hero with id: {args[0]}";
        }
        var issue = Campaign.Current.CampaignObjectManager.GetAllHeroes()
            .Where(h => h.Issue != null)
            .Select(h => h.Issue)
            .FirstOrDefault(i => i.StringId == args[1]);
        if (issue == null)
        {
            return $"Unable to find Issue with StringId: {args[1]}";
        }
        // cant use hero.Issue = issue since Issue is a private setter
        hero.OnIssueCreatedForHero(issue);

        return $"Hero Issue changed to: {issue.StringId}";
    }
    // coop.debug.hero.get_issue
    /// <summary>
    /// Gets Issue from hero
    /// </summary>
    /// <param name="args">HeroId</param>
    /// <returns>returns Issue info from hero </returns>
    [CommandLineArgumentFunction("get_issue", "coop.debug.hero")]
    public static string GetHeroIssue(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.hero.get_issue <heroId>";
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        if (objectManager.TryGetObject<Hero>(args[0], out var hero) == false)
        {
            return $"Unable to find hero with id: {args[0]}";
        }

        return $"Hero Issue: {hero.Issue?.StringId ?? "none"}";
    }

    /// <summary>
    /// View available volunteers for a target hero
    /// </summary>
    [CommandLineArgumentFunction("volunteers", "coop.debug.hero")]
    public static string ViewVolunteersCommand(List<string> strings)
    {
        if (strings.Count == 0) return "Hero id required";

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var hero in Hero.AllAliveHeroes)
        {
            if (hero.StringId == strings[0])
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
            return result;
        }
        return "Hero not found.";
    }

    /// <summary>
    /// Runs the authoritative volunteer refresh for one settlement.
    /// </summary>
    [CommandLineArgumentFunction("refresh_volunteers", "coop.debug.hero")]
    public static string RefreshVolunteersCommand(List<string> args)
    {
        if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.hero.refresh_volunteers")) return error;
        if (args.Count > 1) return "Usage: coop.debug.hero.refresh_volunteers [settlementId]";

        string settlementId = args.Count == 0 ? "town_ES1" : args[0];
        var settlement = Settlement.All.FirstOrDefault(candidate => candidate.StringId == settlementId);
        if (settlement == null) return $"Settlement '{settlementId}' not found.";

        var behavior = Campaign.Current?.GetCampaignBehavior<RecruitmentCampaignBehavior>();
        if (behavior == null) return $"Unable to find {nameof(RecruitmentCampaignBehavior)}.";

        behavior.UpdateVolunteersOfNotablesInSettlement(settlement);
        return $"Refreshed volunteers for {settlement.Name} ({settlement.StringId}).";
    }

    // coop.debug.hero.set_relation
    [CommandLineArgumentFunction("set_relation", "coop.debug.hero")]
    public static string SetRelation(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Set relation is only to be called on the server";
        }

        if (args.Count != 3)
        {
            return "Usage: coop.debug.hero.set_relation <hero1Id> <hero2Id> <value>";
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        if (objectManager.TryGetObject<Hero>(args[0], out var hero1) == false)
        {
            return $"Unable to find hero with id: {args[0]}";
        }

        if (objectManager.TryGetObject<Hero>(args[1], out var hero2) == false)
        {
            return $"Unable to find hero with id: {args[1]}";
        }

        if (hero1 == hero2)
        {
            return "A hero cannot have a relation with itself";
        }

        if (int.TryParse(args[2], out int value) == false)
        {
            return $"{args[2]} is not a valid integer";
        }

        CharacterRelationManager.SetHeroRelation(hero1, hero2, value);

        return $"Set relation between '{hero1.Name}' and '{hero2.Name}' to {CharacterRelationManager.GetHeroRelation(hero1, hero2)}";
    }

    // coop.debug.hero.get_relation
    [CommandLineArgumentFunction("get_relation", "coop.debug.hero")]
    public static string GetRelation(List<string> args)
    {
        if (args.Count != 2)
        {
            return "Usage: coop.debug.hero.get_relation <hero1Id> <hero2Id>";
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        if (objectManager.TryGetObject<Hero>(args[0], out var hero1) == false)
        {
            return $"Unable to find hero with id: {args[0]}";
        }

        if (objectManager.TryGetObject<Hero>(args[1], out var hero2) == false)
        {
            return $"Unable to find hero with id: {args[1]}";
        }

        return $"Relation between '{hero1.Name}' and '{hero2.Name}': {CharacterRelationManager.GetHeroRelation(hero1, hero2)}";
    }

    [CommandLineArgumentFunction("get_effective_relation", "coop.debug.hero")]
    public static string GetEffectiveRelation(List<string> args)
    {
        if (args.Count != 2)
        {
            return "Usage: coop.debug.hero.get_effective_relation <hero1Id> <hero2Id>";
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        if (objectManager.TryGetObject<Hero>(args[0], out var hero1) == false)
        {
            return $"Unable to find hero with id: {args[0]}";
        }

        if (objectManager.TryGetObject<Hero>(args[1], out var hero2) == false)
        {
            return $"Unable to find hero with id: {args[1]}";
        }

        var campaign = Campaign.Current;
        if (campaign?.Models?.DiplomacyModel == null)
        {
            return "Campaign diplomacy model is not available";
        }

        campaign.Models.DiplomacyModel.GetHeroesForEffectiveRelation(
            hero1,
            hero2,
            out var effectiveHero1,
            out var effectiveHero2);
        if (effectiveHero1 == null || effectiveHero2 == null)
        {
            return "Unable to resolve effective relation heroes";
        }

        return $"Effective relation between '{effectiveHero1.Name}' and '{effectiveHero2.Name}': " +
            CharacterRelationManager.GetHeroRelation(effectiveHero1, effectiveHero2);
    }

    [CommandLineArgumentFunction("set_effective_relation", "coop.debug.hero")]
    public static string SetEffectiveRelation(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Set effective relation is only to be called on the server";
        }

        if (args.Count != 3)
        {
            return "Usage: coop.debug.hero.set_effective_relation <hero1Id> <hero2Id> <value>";
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        if (objectManager.TryGetObject<Hero>(args[0], out var hero1) == false)
        {
            return $"Unable to find hero with id: {args[0]}";
        }

        if (objectManager.TryGetObject<Hero>(args[1], out var hero2) == false)
        {
            return $"Unable to find hero with id: {args[1]}";
        }

        if (int.TryParse(args[2], out int value) == false)
        {
            return $"{args[2]} is not a valid integer";
        }

        var campaign = Campaign.Current;
        if (campaign?.Models?.DiplomacyModel == null)
        {
            return "Campaign diplomacy model is not available";
        }

        campaign.Models.DiplomacyModel.GetHeroesForEffectiveRelation(
            hero1,
            hero2,
            out var effectiveHero1,
            out var effectiveHero2);
        if (effectiveHero1 == null || effectiveHero2 == null)
        {
            return "Unable to resolve effective relation heroes";
        }

        if (effectiveHero1 == effectiveHero2)
        {
            return "A hero cannot have a relation with itself";
        }

        CharacterRelationManager.SetHeroRelation(effectiveHero1, effectiveHero2, value);

        return $"Set effective relation between '{effectiveHero1.Name}' and '{effectiveHero2.Name}' to " +
            CharacterRelationManager.GetHeroRelation(effectiveHero1, effectiveHero2);
    }
}
