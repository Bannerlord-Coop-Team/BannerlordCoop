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
        private readonly ICommandObjectResolver resolver;

        public RefreshMercenaryStocksCoopCommand(ICommandObjectResolver resolver) => this.resolver = resolver;

        public string Prefix => "coop.debug.town";

        public string Name => "refresh_mercenary_stocks";

        public string Description => "Refreshes mercenary stocks for co-op debugging.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("townName", "Town name or id, or ALL for every town; quote names with spaces."),
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
                if (!TryGetMercenaryTown(resolver, townName, out var town, out string error)) return Failed(error);

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
        private readonly ICommandObjectResolver resolver;

        public RequestMercenaryStockCoopCommand(ICommandObjectResolver resolver) => this.resolver = resolver;

        public string Prefix => "coop.debug.town";

        public string Name => "request_mercenary_stock";

        public string Description => "Requests mercenary stock for co-op debugging.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("townName", "Town name or id; quote names with spaces."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");

            string townName = args[0];
            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out string error)) return Failed(error);
            if (!TryGetMercenaryTown(resolver, townName, out var town, out error)) return Failed(error);

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

    // Resolves a town by name or id and rejects castles, which have no mercenary stock.
    private static bool TryGetMercenaryTown(ICommandObjectResolver resolver, string townQuery, out Town town, out string error)
    {
        if (!resolver.TryResolveTown(townQuery, out town, out error)) return false;

        var settlement = town.Settlement;
        if (settlement.IsTown) return true;

        error = $"{settlement.Name} is a {GetSettlementType(settlement)}, not a town. Mercenary stock only exists for towns.";
        town = null;
        return false;
    }

    private static string GetSettlementType(Settlement settlement)
    {
        if (settlement.IsCastle) return "castle";
        if (settlement.IsVillage) return "village";
        if (settlement.IsTown) return "town";

        return "settlement";
    }
}
