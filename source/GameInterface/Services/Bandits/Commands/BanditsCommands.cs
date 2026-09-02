using Common;
using Common.Logging;
using GameInterface.CoopSessionData;
using Serilog;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Bandits.Commands;

internal class BanditsCommands
{
    private static readonly ILogger Logger = LogManager.GetLogger<BanditsCommands>();

    /// <summary>
    /// View interacted bandits for all players on server and for current player on client
    /// </summary>
    [CommandLineArgumentFunction("view_interacted_bandits", "coop.debug.bandits")]
    public static string ViewInteractedBanditsCommand(List<string> strings)
    {
        StringBuilder stringBuilder = new StringBuilder();
        if (ModInformation.IsServer)
        {
            if (!ContainerProvider.TryResolve<ICoopSessionProvider>(out var coopSessionProvider)) return "Unable to resolve CoopSessionProvider";

            foreach (var playerInteractedBandit in coopSessionProvider.CoopSession.InteractionsPlayerData.PlayerInteractedBandits)
            {
                if (playerInteractedBandit.Key == null || playerInteractedBandit.Value == null) continue;

                stringBuilder.AppendLine($"{playerInteractedBandit.Key}");
                foreach (var interactedBandit in playerInteractedBandit.Value)
                {
                    stringBuilder.AppendLine($"{interactedBandit.Key} ({interactedBandit.Value})");
                }
            }
        }
        else
        {
            stringBuilder.AppendLine($"{Hero.MainHero.Name}");
            foreach (var interactedBandit in Campaign.Current.GetCampaignBehavior<BanditInteractionsCampaignBehavior>()._interactedBandits)
            {
                stringBuilder.AppendLine($"{interactedBandit.Key.StringId} ({(int)interactedBandit.Value})");
            }
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Failed to retrieve interacted bandits";
    }
}
