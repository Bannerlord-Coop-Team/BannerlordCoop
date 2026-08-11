using Common;
using GameInterface.CoopSessionData;
using SandBox.CampaignBehaviors;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Arenas.Commands;

internal class ArenaMasterCommands
{
    /// <summary>
    /// View arena master interactions data for all players on server and for current player on client
    /// </summary>
    [CommandLineArgumentFunction("list_interactions", "coop.debug.arenas")]
    public static string ViewArenaMasterInteractionsCommand(List<string> strings)
    {
        StringBuilder stringBuilder = new StringBuilder();
        if (ModInformation.IsServer)
        {
            if (!ContainerProvider.TryResolve<ICoopSessionProvider>(out var coopSessionProvider)) return "Unable to resolve CoopSessionProvider";

            foreach (var playerMetArenaMasters in coopSessionProvider.CoopSession.InteractionsPlayerData.PlayerMetArenaMasters)
            {
                if (playerMetArenaMasters.Key == null || playerMetArenaMasters.Value == null) continue;

                if (!coopSessionProvider.CoopSession.InteractionsPlayerData.PlayerKnowTournaments.ContainsKey(playerMetArenaMasters.Key)) continue;

                stringBuilder.AppendLine($"{playerMetArenaMasters.Key} knows of tournaments: {coopSessionProvider.CoopSession.InteractionsPlayerData.PlayerKnowTournaments[playerMetArenaMasters.Key]}");
                stringBuilder.AppendLine($"They have met the arena masters at the following settlements:");
                foreach (var metArenaMasterSettlementId in playerMetArenaMasters.Value)
                {
                    stringBuilder.AppendLine($"    {metArenaMasterSettlementId}");
                }
            }
        }
        else
        {
            var arenaMasterBehavior = Campaign.Current.GetCampaignBehavior<ArenaMasterCampaignBehavior>();

            stringBuilder.AppendLine($"{Hero.MainHero.Name} knows of tournaments: {arenaMasterBehavior._knowTournaments}");
            stringBuilder.AppendLine($"They have met the arena masters at the following settlements:");
            foreach (var metArenaMasterSettlement in arenaMasterBehavior._arenaMasterHasMetInSettlements)
            {
                stringBuilder.AppendLine($"    {metArenaMasterSettlement.StringId}");
            }
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Failed to retrieve player arena master interactions data.";
    }
}
