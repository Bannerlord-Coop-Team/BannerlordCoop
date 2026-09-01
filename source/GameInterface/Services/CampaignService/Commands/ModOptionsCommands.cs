using GameInterface.Configuration;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace GameInterface.Services.CampaignService.Commands;

internal class ModOptionsCommands
{
    public static string ListOptionsCommand(List<string> strings)
    {
        StringBuilder stringBuilder = new();

        var modOptions = ModConfigProvider.ModOptions;

        foreach (PropertyInfo property in typeof(ModOptions).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            string name = property.Name;
            string value = property.GetValue(modOptions)?.ToString() ?? "null";

            stringBuilder.AppendLine($"{name}: {value}");
        }

        return stringBuilder.ToString();
    }
}
