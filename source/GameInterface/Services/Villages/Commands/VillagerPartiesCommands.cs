using Common;
using Common.Logging;
using GameInterface.CoopSessionData;
using Serilog;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Villages.Commands;

internal class VillagerPartiesCommands
{
    private static readonly ILogger Logger = LogManager.GetLogger<VillagerPartiesCommands>();

    /// <summary>
    /// View interacted villagers for all players on server and for current player on client
    /// </summary>
    [CommandLineArgumentFunction("view_interacted_villagers", "coop.debug.villagers")]
    public static string ViewInteractedVillagersCommand(List<string> strings)
    {
        StringBuilder stringBuilder = new StringBuilder();
        if (ModInformation.IsServer)
        {
            if (!ContainerProvider.TryResolve<ICoopSessionProvider>(out var coopSessionProvider)) return "Unable to resolve CoopSessionProvider";

            foreach (var playerInteractedVillager in coopSessionProvider.CoopSession.InteractionsPlayerData.PlayerInteractedVillagers)
            {
                if (playerInteractedVillager.Key == null || playerInteractedVillager.Value == null) continue;

                stringBuilder.AppendLine($"{playerInteractedVillager.Key}");
                foreach (var interactedVillager in playerInteractedVillager.Value)
                {
                    stringBuilder.AppendLine($"{interactedVillager.Key} ({interactedVillager.Value})");
                }
            }
        }
        else
        {
            stringBuilder.AppendLine($"{Hero.MainHero.Name}");
            foreach (var interactedVillager in Campaign.Current.GetCampaignBehavior<VillagerCampaignBehavior>()._interactedVillagers)
            {
                stringBuilder.AppendLine($"{interactedVillager.Key.StringId} ({(int)interactedVillager.Value})");
            }
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Failed to retrieve interacted villagers";
    }

    [CommandLineArgumentFunction("view_looted_villagers", "coop.debug.villagers")]
    public static string ViewLootedVillagers(List<string> strings)
    {
        StringBuilder stringBuilder = new StringBuilder();
        foreach (var lootedVillager in Campaign.Current.GetCampaignBehavior<VillagerCampaignBehavior>()._lootedVillagers)
        {
            stringBuilder.AppendLine($"{lootedVillager.Key.StringId} ({lootedVillager.Value.NumTicks})");
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Failed to retrieve looted villagers";
    }
}
