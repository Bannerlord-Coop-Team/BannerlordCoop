using Common;
using GameInterface.Services.Heroes.Audit;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.ObjectManager.Extensions;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.Library.CommandLineFunctionality;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Heroes.Commands;

public class HeroDebugCommand
{
    // coop.debug.hero.list
    /// <summary>
    /// Lists all the heroes
    /// </summary>
    /// <param name="args">actually none are being used..</param>
    /// <returns>strings of all the heroes</returns>
    [CommandLineArgumentFunction("list", "coop.debug.hero")]
    public static string ListHeroes(List<string> args)
    {
        StringBuilder stringBuilder = new StringBuilder();

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        foreach (var hero in Campaign.Current.CampaignObjectManager.GetAllHeroes())
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

        return stringBuilder.ToString();
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

        return stringBuilder.ToString();
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

    [CommandLineArgumentFunction("change", "coop.debug.hero")]
    public static string ChangeTimeStamp(List<string> args)
    {
        Hero hero = Hero.MainHero;

        hero.AddPower(66);

        return "Updated to " + hero._power;
    }

    [CommandLineArgumentFunction("getChange", "coop.debug.hero")]
    public static string GetTimeStamp(List<string> args)
    {
        Hero hero = Hero.FindFirst(x => x._power == 66);

        return hero.Name.Value;
    }
    [CommandLineArgumentFunction("setGold", "coop.debug.hero")]
    public static string SetGold(List<string> args)
    {
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
        if (int.TryParse(args[1], out int gold) == false)
        {
            return $"{args[1]} is not a valid integer";
        }
        hero.Gold = gold;
        return $"Hero Gold changed to{hero.Gold}";
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
}
