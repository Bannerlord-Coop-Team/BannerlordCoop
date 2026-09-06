using Common.Commands;
using Common;
using Common.Logging;
using GameInterface.CoopSessionData;
using Serilog;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
namespace GameInterface.Services.Villages.Commands;

internal class VillagerPartiesCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    private static readonly ILogger Logger = LogManager.GetLogger<VillagerPartiesCommands>();

    /// <summary>
    /// View interacted villagers for all players on server and for current player on client
    /// </summary>
    public sealed class ViewInteractedVillagersCommandCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.villagers";

        public string Name => "view_interacted_villagers";

        public string Description => "Shows interacted villagers for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            StringBuilder stringBuilder = new StringBuilder();
            if (ModInformation.IsServer)
            {
                if (!ContainerProvider.TryResolve<ICoopSessionProvider>(out var coopSessionProvider)) return Failed("Unable to resolve CoopSessionProvider");

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
                return Succeeded(result);
            }
            return Failed("Failed to retrieve interacted villagers");

        }
    }

    public sealed class ViewLootedVillagersCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.villagers";

        public string Name => "view_looted_villagers";

        public string Description => "Shows looted villagers for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var lootedVillager in Campaign.Current.GetCampaignBehavior<VillagerCampaignBehavior>()._lootedVillagers)
            {
                stringBuilder.AppendLine($"{lootedVillager.Key.StringId} ({lootedVillager.Value.NumTicks})");
            }

            string result = stringBuilder.ToString();
            if (result.Length > 0)
            {
                return Succeeded(result);
            }
            return Failed("Failed to retrieve looted villagers");

        }
    }
}
