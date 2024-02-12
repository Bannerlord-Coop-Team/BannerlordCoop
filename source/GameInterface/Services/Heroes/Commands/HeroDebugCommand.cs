using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using Helpers;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Heroes.Commands
{
    public class HeroDebugCommand
    {
        // coop.debug.clan.list
        /// <summary>
        /// Lists all the heroes
        /// </summary>
        /// <param name="args">actually none are being used..</param>
        /// <returns>strings of all the heroes</returns>
        [CommandLineArgumentFunction("list", "coop.debug.hero")]
        public static string ListHeroes(List<string> args)
        {
            StringBuilder stringBuilder = new StringBuilder();

            Campaign.Current.CampaignObjectManager.AliveHeroes.ForEach((hero) =>
            {
                stringBuilder.Append(string.Format("ID: '{0}'\nName: '{1}'\n", hero.StringId, hero.Name));
            });

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

        // TODO move
        // coop.debug.characterObjects.list
        [CommandLineArgumentFunction("list", "coop.debug.characterObjects")]
        public static string ListCharacterObjects(List<string> args)
        {
            var characters = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>();

            var stringBuilder = new StringBuilder();
            foreach (var character in characters)
            {
                stringBuilder.AppendLine(character.StringId);
            }

            return stringBuilder.ToString();
        }


        // coop.debug.hero.createHero lord_2_7
        [CommandLineArgumentFunction("createHero", "coop.debug.hero")]
        public static string CreateNewHero(List<string> args)
        {
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
    }
}
