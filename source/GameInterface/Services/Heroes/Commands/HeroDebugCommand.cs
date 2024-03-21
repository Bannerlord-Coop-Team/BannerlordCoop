using GameInterface.Services.Heroes.Audit;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.ObjectManager.Extensions;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Heroes.Commands;

public class HeroDebugCommand
{

    // TODO: MOVE :)
    [CommandLineArgumentFunction("list", "coop.debug.itemobjects")]
    public static string ListItems(List<string> list)
    {
        StringBuilder stringBuilder = new StringBuilder();

        foreach (var item in Campaign.Current.AllItems)
        {
            stringBuilder.AppendFormat("ID: '{0}'\tName: '{1}'\n", item.StringId, item.Name);
        }
        return stringBuilder.ToString();
    }


    // coop.debug.hero.addspecialItem
    /// <summary>
    /// add SpecialItem
    /// </summary>
    /// <param name="args">ItemObjectId</param>
    /// <returns>if success</returns>
    [CommandLineArgumentFunction("add_special", "coop.debug.hero")]
    public static string AddSpecialItem(List<string> args)
    {
        StringBuilder stringBuilder = new StringBuilder();


        if (args.Count != 2)
        {
            return "Usage: coop.debug.hero.add_special <heroId> <item>";
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        if (objectManager.TryGetObject<Hero>(args[0], out var hero) == false)
        {
            return $"Unable to find hero with id: {args[0]}";
        }


        if (objectManager.TryGetObject<ItemObject>(args[1], out var obj) == false)
        {
            return $"Unable to find ItemObject with id: {args[1]}";
        }

        hero.SpecialItems.Add(obj);


        return "Successfully added itemobj";
    }


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



    // coop.debug.hero.add_children hero child
    [CommandLineArgumentFunction("add_children", "coop.debug.hero")]
    public static string AddChild(List<string> args)
    {
        if (args.Count != 2)
        {
            return "Usage: coop.debug.hero.add_children <heroId> <childid>";
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        if (objectManager.TryGetObject<Hero>(args[0], out var hero) == false)
        {
            return $"Unable to find hero with id: {args[0]}";
        }
        if (objectManager.TryGetObject<Hero>(args[1], out var childHero) == false)
        {
            return $"Unable to find hero with id: {args[1]}";
        }

        childHero.Father = hero;

        return "Successfully added children";
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
}
