using Common.Commands;
using Common;
using GameInterface.CoopSessionData;
using GameInterface.Services.Actions.Patches;
using GameInterface.Utils.Commands;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;
namespace GameInterface.Services.Workshops.Commands
{
    public class WorkshopDebugCommand
    {
        private static CoopCommandResult Succeeded(string output) =>
            new CoopCommandResult(true, output);

        private static CoopCommandResult Failed(string output) =>
            new CoopCommandResult(false, output, "command_failed");

        public static Workshop GetWorkshopBySettlementAndType(Settlement settlement, WorkshopType workshopType)
        {
            foreach (var workshop in settlement.Town.Workshops)
            {
                if (workshop.WorkshopType == workshopType)
                {
                    return workshop;
                }
            }
            return null;
        }

        // Resolves a workshop type by its id first, then by a unique display name.
        internal static bool TryResolveWorkshopType(ICommandObjectResolver resolver, string query, out WorkshopType type, out string error)
        {
            error = null;
            type = string.IsNullOrWhiteSpace(query) ? null : WorkshopType.Find(query);
            if (type != null)
                return true;

            return resolver.TryResolve(query, WorkshopType.All, w => w.Name?.ToString(), out type, out error);
        }

        internal static void ApplyOwnerChange(Workshop workshop, Hero newOwner)
        {
            ChangeOwnerOfWorkshopActionPatches.ApplyInternalOverride(
                workshop,
                newOwner,
                workshop.WorkshopType,
                1000,
                0);
        }

        public sealed class SetWorkshopCustomNameCoopCommand : ICoopCommand
        {
            private readonly ICommandObjectResolver resolver;

            public SetWorkshopCustomNameCoopCommand(ICommandObjectResolver resolver)
            {
                this.resolver = resolver;
            }

            public string Prefix => "coop.debug.workshop";

            public string Name => "set_workshop_custom_name";

            public string Description => "Sets workshop custom name for co-op debugging.";

            public CoopCommandSide Side => CoopCommandSide.Both;

            public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "Settlement name or id; quote names with spaces."),
                new ExpectedArgs("workshopType", "Workshop type name or id; quote names with spaces."),
                new ExpectedArgs("newCustomName", "The exact new custom name; quote values containing spaces."),
            };

            public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
            {

                string settlementId = args[0];
                string workshopType = args[1];
                string newCustomName = args[2];

                if (!resolver.TryResolveSettlement(settlementId, out Settlement settlement, out string error))
                    return Failed(error);

                if (!TryResolveWorkshopType(resolver, workshopType, out WorkshopType type, out error))
                    return Failed(error);

                Workshop workshop = GetWorkshopBySettlementAndType(settlement, type);
                if (workshop == null)
                {
                    return Failed($"Workshop of type '{workshopType}' not found in settlement '{settlementId}'");
                }

                // Change the custom name
                workshop.SetCustomName(new TextObject(value: newCustomName));

                return Succeeded($"Workshop custom name has been changed to: {workshop._customName}");

            }
        }

        public sealed class SetWorkshopOwnerCoopCommand : ICoopCommand
        {
            private readonly ICommandObjectResolver resolver;

            public SetWorkshopOwnerCoopCommand(ICommandObjectResolver resolver)
            {
                this.resolver = resolver;
            }

            public string Prefix => "coop.debug.workshop";

            public string Name => "set_workshop_owner";

            public string Description => "Sets workshop owner for co-op debugging.";

            public CoopCommandSide Side => CoopCommandSide.Both;

            public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "Settlement name or id; quote names with spaces."),
                new ExpectedArgs("workshopType", "Workshop type name or id; quote names with spaces."),
                new ExpectedArgs("newOwnerId", "New owner hero name or id; quote names with spaces."),
            };

            public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
            {
                // Expect three arguments: settlement (id or name), workshop type (id or name), and new owner (hero ID or name)

                string settlementId = args[0];
                string workshopType = args[1];
                string newOwnerId = args[2];

                // Find the settlement
                if (!resolver.TryResolveSettlement(settlementId, out Settlement settlement, out string error))
                    return Failed(error);

                // Find the workshop type (ensure the workshop type is valid)
                if (!TryResolveWorkshopType(resolver, workshopType, out WorkshopType type, out error))
                    return Failed(error);

                // Find the workshop by settlement and type
                Workshop workshop = GetWorkshopBySettlementAndType(settlement, type);
                if (workshop == null)
                {
                    return Failed($"Workshop of type '{workshopType}' not found in settlement '{settlementId}'");
                }

                // Find the new owner (Hero) by ID or name
                if (!resolver.TryResolveHero(newOwnerId, out Hero newOwner, out error))
                    return Failed(error);

                // Use the same action path as normal workshop purchases so ownership-dependent
                // campaign behavior and client warehouse data are updated too.
                ApplyOwnerChange(workshop, newOwner);

                return Succeeded($"Workshop owner has been changed to: {newOwner.Name} with the type {workshop.WorkshopType} and with a capital of {workshop.Capital}");

            }
        }

        /// <summary>
        /// View workshop owners in a settlement
        /// </summary>
        public sealed class OwnersInSettlementCommandCoopCommand : ICoopCommand
        {
            private readonly ICommandObjectResolver resolver;

            public OwnersInSettlementCommandCoopCommand(ICommandObjectResolver resolver)
            {
                this.resolver = resolver;
            }

            public string Prefix => "coop.debug.workshop";

            public string Name => "owners_in_settlement";

            public string Description => "Runs in settlement for co-op debugging.";

            public CoopCommandSide Side => CoopCommandSide.Both;

            public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "Settlement name or id; quote names with spaces."),
            };

            public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
            {

                StringBuilder stringBuilder = new StringBuilder();
                if (!resolver.TryResolveSettlement(strings[0], out Settlement settlement, out string error))
                    return Failed(error);

                stringBuilder.AppendLine($"{settlement.Name}");
                foreach (var workshop in settlement.Town.Workshops)
                {
                    stringBuilder.AppendLine($"{workshop.Name}: {workshop.Owner.Name} ({workshop.Owner.StringId})");
                }

                string result = stringBuilder.ToString();
                if (result.Length > 0)
                {
                    return Succeeded(result);
                }
                return Failed("No workshop owners were found");

            }
        }

        /// <summary>
        /// View workshops a hero owns
        /// </summary>
        public sealed class HeroOwnedWorkshopsCommandCoopCommand : ICoopCommand
        {
            private readonly ICommandObjectResolver resolver;

            public HeroOwnedWorkshopsCommandCoopCommand(ICommandObjectResolver resolver)
            {
                this.resolver = resolver;
            }

            public string Prefix => "coop.debug.workshop";

            public string Name => "hero_owned_workshops";

            public string Description => "Runs owned workshops for co-op debugging.";

            public CoopCommandSide Side => CoopCommandSide.Both;

            public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
            {
                new ExpectedArgs("heroId", "Hero name or id; quote names with spaces."),
            };

            public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
            {

                StringBuilder stringBuilder = new StringBuilder();
                if (!resolver.TryResolveHero(strings[0], out Hero hero, out string error))
                    return Failed(error);

                stringBuilder.AppendLine($"{hero.Name}");
                foreach (var workshop in hero.OwnedWorkshops)
                {
                    stringBuilder.AppendLine($"{workshop.Name} ({workshop.Settlement.Name})");
                }

                string result = stringBuilder.ToString();
                if (result.Length > 0)
                {
                    return Succeeded(result);
                }
                return Failed("No owned workshops were found");

            }
        }

        /// <summary>
        /// View warehouse rosters for all players on server and for current player on client
        /// </summary>
        public sealed class ViewWarehouseRostersCommandCoopCommand : ICoopCommand
        {
            public string Prefix => "coop.debug.workshop";

            public string Name => "view_warehouse_rosters";

            public string Description => "Shows warehouse rosters for co-op debugging.";

            public CoopCommandSide Side => CoopCommandSide.Both;

            public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

            public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
            {
                StringBuilder stringBuilder = new StringBuilder();
                if (ModInformation.IsServer)
                {
                    if (!ContainerProvider.TryResolve<ICoopSessionProvider>(out var coopSessionProvider)) return Failed("Unable to resolve CoopSessionProvider");

                    foreach (var playerWarehouseData in coopSessionProvider.CoopSession.WorkshopPlayerData.PlayerWarehouseRosterPerSettlement)
                    {
                        if (playerWarehouseData.Key == null || playerWarehouseData.Value == null) continue;

                        stringBuilder.AppendLine($"{playerWarehouseData.Key}");
                        foreach (var warehouseData in playerWarehouseData.Value)
                        {
                            if (warehouseData.Key == null || warehouseData.Value == null) continue;

                            stringBuilder.AppendLine($"{warehouseData.Key}");
                            foreach (var item in warehouseData.Value)
                            {
                                if (item.ItemObjectData.ItemObjectId == null) continue;
                                stringBuilder.AppendLine($"{item.ItemObjectData.ItemObjectId} ({item.Amount})");
                            }
                        }
                    }
                }
                else
                {
                    stringBuilder.AppendLine($"{Hero.MainHero.Name}");
                    foreach (var warehouseRoster in Campaign.Current.GetCampaignBehavior<WorkshopsCampaignBehavior>()._warehouseRosterPerSettlement)
                    {
                        if (warehouseRoster.Key == null || warehouseRoster.Value == null) continue;

                        stringBuilder.AppendLine($"{warehouseRoster.Key.Name}");
                        foreach (var item in warehouseRoster.Value)
                        {
                            stringBuilder.AppendLine($"{item.EquipmentElement.Item.Name} ({item.Amount})");
                        }
                    }
                }

                string result = stringBuilder.ToString();
                if (result.Length > 0)
                {
                    return Succeeded(result);
                }
                return Failed("Failed to retrieve warehouse rosters");

            }
        }

        /// <summary>
        /// View information about workshops in a settlement
        /// </summary>
        public sealed class ViewWorkshopInfoCommandCoopCommand : ICoopCommand
        {
            private readonly ICommandObjectResolver resolver;

            public ViewWorkshopInfoCommandCoopCommand(ICommandObjectResolver resolver)
            {
                this.resolver = resolver;
            }

            public string Prefix => "coop.debug.workshop";

            public string Name => "workshop_info";

            public string Description => "Runs info for co-op debugging.";

            public CoopCommandSide Side => CoopCommandSide.Both;

            public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "Settlement name or id; quote names with spaces."),
            };

            public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
            {

                StringBuilder stringBuilder = new StringBuilder();
                if (!resolver.TryResolveSettlement(strings[0], out Settlement settlement, out string error))
                    return Failed(error);

                stringBuilder.AppendLine($"{settlement.Name}");
                foreach (var workshop in settlement.Town.Workshops)
                {
                    stringBuilder.AppendLine($"Name: {workshop.Name}");
                    stringBuilder.AppendLine($"Owner: {workshop.Owner.StringId} ({workshop.Owner.Name})");
                    stringBuilder.AppendLine($"Type: {workshop.WorkshopType}");

                    var workshopData = Campaign.Current.GetCampaignBehavior<WorkshopsCampaignBehavior>().GetDataOfWorkshop(workshop);
                    if (workshopData == null)
                    {
                        stringBuilder.AppendLine($"{workshop.Name} had no workshop data.");
                        continue;
                    }
                    stringBuilder.AppendLine($"WorkshopData:");
                    stringBuilder.AppendLine($"IsGettingInputsFromWarehouse: {workshopData.IsGettingInputsFromWarehouse}");
                    stringBuilder.AppendLine($"ProductionProgressForWarehouse: {workshopData.ProductionProgressForWarehouse}");
                    stringBuilder.AppendLine($"ProductionProgressForTown: {workshopData.ProductionProgressForTown}");
                    stringBuilder.AppendLine($"StockProductionInWarehouseRatio: {workshopData.StockProductionInWarehouseRatio}");
                }

                string result = stringBuilder.ToString();
                if (result.Length > 0)
                {
                    return Succeeded(result);
                }
                return Failed("No workshop owners were found");

            }
        }
    }
}
