using Autofac;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Localization;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Workshops.Commands
{
    public class WorkshopDebugCommand
    {
        private static bool TryGetObjectManager(out IObjectManager objectManager)
        {
            objectManager = null;
            if (ContainerProvider.TryGetContainer(out var container) == false) return false;

            return container.TryResolve(out objectManager);
        }

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

        [CommandLineArgumentFunction("set_workshop_custom_name", "coop.debug.workshop")]
        public static string SetWorkshopCustomName(List<string> args)
        {
            if (args.Count != 3)
            {
                return "Usage: coop.debug.workshop.set_custom_name <settlementName> <workshopType> <newCustomName>";
            }

            string settlementName = args[0];
            string workshopType = args[1];
            string newCustomName = args[2];

            Settlement settlement = Settlement.Find(settlementName);
            if (settlement == null)
            {
                return $"Settlement with name: '{settlementName}' not found";
            }

            WorkshopType type = WorkshopType.Find(workshopType); // Ensure this method or logic to find WorkshopType exists
            if (type == null)
            {
                return $"Workshop type: '{workshopType}' not found";
            }

            Workshop workshop = GetWorkshopBySettlementAndType(settlement, type);
            if (workshop == null)
            {
                return $"Workshop of type '{workshopType}' not found in settlement '{settlementName}'";
            }

            // Change the custom name
            workshop.SetCustomName(new TextObject(value: newCustomName));

            return $"Workshop custom name has been changed to: {workshop._customName}";
        }

        [CommandLineArgumentFunction("set_workshop_owner", "coop.debug.workshop")]
        public static string SetWorkshopOwner(List<string> args)
        {
            // Expect three arguments: settlement name, workshop type, and new owner (hero ID or name)
            if (args.Count != 3)
            {
                return "Usage: coop.debug.workshop.set_owner <settlementName> <workshopType> <newOwnerId>";
            }

            string settlementName = args[0];
            string workshopType = args[1];
            string newOwnerId = args[2];

            // Find the settlement
            Settlement settlement = Settlement.Find(settlementName);
            if (settlement == null)
            {
                return $"Settlement with name: '{settlementName}' not found";
            }

            // Find the workshop type (ensure the workshop type is valid)
            WorkshopType type = WorkshopType.Find(workshopType); // Ensure this method exists in your codebase or mod
            if (type == null)
            {
                return $"Workshop type: '{workshopType}' not found";
            }

            // Find the workshop by settlement and type
            Workshop workshop = GetWorkshopBySettlementAndType(settlement, type);
            if (workshop == null)
            {
                return $"Workshop of type '{workshopType}' not found in settlement '{settlementName}'";
            }

            // Find the new owner (Hero) by ID or name
            Hero newOwner = Hero.FindFirst(h => h.StringId == newOwnerId || h.Name.ToString() == newOwnerId);
            if (newOwner == null)
            {
                return $"Hero with ID or Name: '{newOwnerId}' not found";
            }

            // Use the existing Workshop method to change the owner
            workshop.ChangeOwnerOfWorkshop(newOwner, workshop.WorkshopType, 1000);

            return $"Workshop owner has been changed to: {newOwner.Name} with the type {workshop.WorkshopType} and with a capital of {workshop.Capital}";
        }
    }
}