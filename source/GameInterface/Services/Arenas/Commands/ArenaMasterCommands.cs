using Common.Commands;
using Common;
using GameInterface.CoopSessionData;
using SandBox.CampaignBehaviors;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
namespace GameInterface.Services.Arenas.Commands;

internal class ArenaMasterCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    /// <summary>
    /// View arena master interactions data for all players on server and for current player on client
    /// </summary>
    public sealed class ViewArenaMasterInteractionsCommandCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.arenas";

        public string Name => "list_interactions";

        public string Description => "Lists interactions for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            StringBuilder stringBuilder = new StringBuilder();
            if (ModInformation.IsServer)
            {
                if (!ContainerProvider.TryResolve<ICoopSessionProvider>(out var coopSessionProvider)) return Failed("Unable to resolve CoopSessionProvider");

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
                return Succeeded(result);
            }
            return Failed("Failed to retrieve player arena master interactions data.");

        }
    }
}
