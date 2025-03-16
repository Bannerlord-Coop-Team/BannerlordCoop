using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.CharacterObjects.Commands;
internal class CharacterObjectCommands
{
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
