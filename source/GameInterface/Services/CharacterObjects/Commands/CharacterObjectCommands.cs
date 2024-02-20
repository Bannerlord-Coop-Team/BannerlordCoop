using System;
using System.Collections.Generic;
using System.Text;
using static TaleWorlds.Library.CommandLineFunctionality;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.CharacterObjects.Commands;
internal class CharacterObjectCommands
{
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
}
