using GameInterface.Services.Heroes.Audit;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.ObjectManager.Extensions;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

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

        foreach (var hero in Campaign.Current.CampaignObjectManager.GetAllHeroes())
        {
            stringBuilder.Append(string.Format("ID: '{0}'\nName: '{1}'\n", hero.StringId, hero.Name));
        }

        return stringBuilder.ToString();
    }

    // coop.debug.hero.info
    [CommandLineArgumentFunction("info", "coop.debug.hero")]
    public static string Info(List<string> args)
    {
        if (args.Count == 1)
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

        HeroCreator.CreateBasicHero(template, out var newHero);

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

        hero.LastTimeStampForActivity = 23190475;

        return "Updated to " + hero.LastTimeStampForActivity;
    }

    [CommandLineArgumentFunction("getChange", "coop.debug.hero")]
    public static string GetTimeStamp(List<string> args)
    {
        Hero hero = Hero.FindFirst(x => x.LastTimeStampForActivity == 23190475);

        return hero.Name.Value;
    }
}
