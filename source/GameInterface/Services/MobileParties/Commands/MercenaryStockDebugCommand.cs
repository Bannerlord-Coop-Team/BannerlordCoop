using Common.Commands;
using Common;
using Common.Network;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.Commands;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
namespace GameInterface.Services.MobileParties.Commands;

internal class MercenaryStockDebugCommand
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    private const string RefreshCommand = "coop.debug.town.refresh_mercenary_stocks";
    private const string RequestCommand = "coop.debug.town.request_mercenary_stock";
    private const string AllTowns = "ALL";

    private static readonly MethodInfo UpdateCurrentMercenaryTroopAndCount =
        AccessTools.Method(typeof(RecruitmentCampaignBehavior), "UpdateCurrentMercenaryTroopAndCount", new[] { typeof(Town), typeof(bool) });

    public sealed class RefreshMercenaryStocksCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.town";

        public string Name => "refresh_mercenary_stocks";

        public string Description => "Refreshes mercenary stocks for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("townName", "The exact town name; quote values containing spaces."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");

            string townName = args[0];
            var behavior = Campaign.Current?.GetCampaignBehavior<RecruitmentCampaignBehavior>();
            if (behavior == null)
            {
                return Failed($"Unable to find {nameof(RecruitmentCampaignBehavior)}.");
            }

            if (UpdateCurrentMercenaryTroopAndCount == null)
            {
                return Failed("Unable to find RecruitmentCampaignBehavior.UpdateCurrentMercenaryTroopAndCount.");
            }

            var allTowns = string.Equals(townName, AllTowns, StringComparison.OrdinalIgnoreCase);
            var towns = allTowns ? GetTowns().ToList() : new List<Town>();
            if (!allTowns)
            {
                if (!TryGetTownByName(townName, out var town, out string error)) return Failed(error);

                towns.Add(town);
            }

            var refreshed = 0;
            GameThread.RunSafe(() =>
            {
                foreach (var town in towns)
                {
                    UpdateCurrentMercenaryTroopAndCount.Invoke(behavior, new object[] { town, true });
                    RecruitmentCampaignBehaviorPatch.PublishMercenaryStock(behavior, town);
                    refreshed++;
                }
            }, blocking: true, context: RefreshCommand);

            if (allTowns) return Succeeded($"Refreshed mercenary stocks for {refreshed} towns.");

            return Succeeded($"Refreshed mercenary stock for {towns[0].Name}.");

        }
    }

    public sealed class RequestMercenaryStockCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.town";

        public string Name => "request_mercenary_stock";

        public string Description => "Requests mercenary stock for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("townName", "The exact town name; quote values containing spaces."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");

            string townName = args[0];
            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out string error)) return Failed(error);
            if (!TryGetTownByName(townName, out var town, out error)) return Failed(error);

            if (!objectManager.TryGetIdWithLogging(town, out var resolvedTownId))
            {
                return Failed($"Unable to resolve network id for {nameof(Town)} '{townName}'.");
            }

            if (!ContainerProvider.TryResolve<INetwork>(out var network))
            {
                return Failed($"Unable to get {nameof(INetwork)}.");
            }

            network.SendAll(new NetworkRequestMercenaryStockAudit(resolvedTownId));

            var serverStock = FormatServerStock(town, objectManager);
            return Succeeded($"Requested mercenary stock from all clients for {town.Name} ({resolvedTownId}). Server shows {serverStock}. Check the server log for client responses.");

        }
    }

    private static string FormatServerStock(Town town, IObjectManager objectManager)
    {
        if (!MercenaryStockHandler.TryGetMercenaryStock(town, out var troopType, out var number))
        {
            return "unknown stock";
        }

        if (!objectManager.TryGetIdWithLogging(troopType, out var troopTypeId)) return $"unresolved troop x{number}";

        return $"{troopTypeId} x{number}";
    }

    private static IEnumerable<Town> GetTowns()
    {
        return Campaign.Current.CampaignObjectManager.Settlements
            .Where(settlement => settlement.IsTown && settlement.Town != null)
            .Select(settlement => settlement.Town);
    }

    private static bool TryGetTownByName(string townName, out Town town, out string error)
    {
        town = null;
        error = null;

        if (string.IsNullOrWhiteSpace(townName))
        {
            error = "Town name cannot be empty.";
            return false;
        }

        var campaign = Campaign.Current;
        if (campaign == null)
        {
            error = "Unable to find current campaign.";
            return false;
        }

        var matchingSettlements = campaign.CampaignObjectManager.Settlements
            .Where(settlement => SettlementNameMatches(settlement, townName))
            .ToList();

        if (matchingSettlements.Count == 0)
        {
            error = $"No settlement found with name '{townName}'.";
            return false;
        }

        if (matchingSettlements.Count > 1)
        {
            error = $"Multiple settlements found with name '{townName}'. Use the exact town name.";
            return false;
        }

        var settlement = matchingSettlements[0];
        if (!settlement.IsTown)
        {
            error = $"{settlement.Name} is a {GetSettlementType(settlement)}, not a town. Mercenary stock only exists for towns.";
            return false;
        }

        if (settlement.Town == null)
        {
            error = $"{settlement.Name} is marked as a town but has no {nameof(Town)} component.";
            return false;
        }

        town = settlement.Town;
        return true;
    }

    private static bool SettlementNameMatches(Settlement settlement, string townName)
    {
        return string.Equals(settlement.Name?.ToString(), townName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(settlement.Town?.Name?.ToString(), townName, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetSettlementType(Settlement settlement)
    {
        if (settlement.IsCastle) return "castle";
        if (settlement.IsVillage) return "village";
        if (settlement.IsTown) return "town";

        return "settlement";
    }
}
