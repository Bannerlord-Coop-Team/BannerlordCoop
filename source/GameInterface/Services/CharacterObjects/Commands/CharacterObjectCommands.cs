using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.CharacterObjects.Commands;
internal class CharacterObjectCommands
{
    // coop.debug.characterObjects.info <charId>
    /// <summary>
    /// Reflection-dumps every field of a CharacterObject (walking up to its BasicCharacterObject base, where
    /// the synced _characterTraits / _occupation / _persona fields live) so a server screenshot and a client
    /// screenshot can be compared field-for-field to confirm CharacterObject syncs still replicate.
    /// </summary>
    [CommandLineArgumentFunction("info", "coop.debug.characterObjects")]
    public static string Info(List<string> args)
    {
        if (args.Count != 1) return "Usage: coop.debug.characterObjects.info <charId>";
        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return "Unable to resolve object manager";
        if (objectManager.TryGetObject<CharacterObject>(args[0], out var character) == false) return $"Unable to find character with id: {args[0]}";

        var stringBuilder = new StringBuilder();
        for (Type type = typeof(CharacterObject); type != null && type != typeof(object); type = type.BaseType)
        {
            foreach (var field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                stringBuilder.AppendLine($"{type.Name}.{field.Name} = {field.GetValue(character)}");
            }
        }
        return stringBuilder.ToString();
    }

    // coop.debug.characterObjects.list
    [CommandLineArgumentFunction("list", "coop.debug.characterObjects")]
    public static string ListCharacterObjects(List<string> args)
    {
        var characters = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>();

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return "Unable to resolve object manager";
        }

        var stringBuilder = new StringBuilder();
        foreach (var character in characters)
        {
            if (objectManager.TryGetId(character, out var id) == false)
            {
                stringBuilder.Append($"Unable to get id for {character.StringId}");
                continue;
            }

            stringBuilder.AppendLine(id);
        }

        return stringBuilder.ToString();
    }
}
