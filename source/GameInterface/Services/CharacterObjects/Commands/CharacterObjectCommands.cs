using Common.Commands;
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
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    // coop.debug.character_objects.info <charId>
    /// <summary>
    /// Reflection-dumps every field of a CharacterObject (walking up to its BasicCharacterObject base, where
    /// the synced _characterTraits / _occupation / _persona fields live) so a server screenshot and a client
    /// screenshot can be compared field-for-field to confirm CharacterObject syncs still replicate.
    /// </summary>
    public sealed class CharacterObjectsInfoCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.character_objects";

        public string Name => "info";

        public string Description => "Reports info.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("character_id", "The registered character id.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return Failed("Unable to resolve object manager");
            if (objectManager.TryGetObject<CharacterObject>(args[0], out var character) == false) return Failed($"Unable to find character with id: {args[0]}");

            var stringBuilder = new StringBuilder();
            for (Type type = typeof(CharacterObject); type != null && type != typeof(object); type = type.BaseType)
            {
                foreach (var field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    stringBuilder.AppendLine($"{type.Name}.{field.Name} = {field.GetValue(character)}");
                }
            }
            return Succeeded(stringBuilder.ToString());
        }
    }

    // coop.debug.character_objects.list
    public sealed class CharacterObjectsListCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.character_objects";

        public string Name => "list";

        public string Description => "Reports list.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var characters = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>();

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                return Failed("Unable to resolve object manager");
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

            return Succeeded(stringBuilder.ToString());
        }
    }
}
