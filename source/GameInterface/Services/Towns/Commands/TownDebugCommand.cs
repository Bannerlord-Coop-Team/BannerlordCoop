using Common.Commands;
using Autofac;
using Common;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Towns.Patches;
using Helpers;
using SandBox.View.Map;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
namespace GameInterface.Services.Villages.Commands;

public class TownDebugCommand
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    /// <summary>
    /// Attempts to get the ObjectManager
    /// </summary>
    /// <param name="objectManager">Resolved ObjectManager, will be null if unable to resolve</param>
    /// <returns>True if ObjectManager was resolved, otherwise False</returns>
    private static bool TryGetObjectManager(out IObjectManager objectManager)
    {
        objectManager = null;
        if (ContainerProvider.TryGetContainer(out var container) == false) return false;

        return container.TryResolve(out objectManager);
    }

    // coop.debug.town.list
    /// <summary>
    /// Lists all the towns
    /// </summary>
    /// <param name="args">actually none are being used..</param>
    /// <returns>strings of all the towns</returns>
    public sealed class ListTownsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.town";

        public string Name => "list_towns";

        public string Description => "Lists towns for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            StringBuilder stringBuilder = new StringBuilder();

            List<Settlement> settlements = Campaign.Current.CampaignObjectManager.Settlements
                .Where(settlement => settlement.IsTown).ToList();

            settlements.ForEach((settlement) =>
            {
                Town t = settlement.Town;
                stringBuilder.Append(string.Format("ID: '{0}'\nName: '{1}'\n", t.StringId, t.Name));
            });

            return Succeeded(stringBuilder.ToString());

        }
    }

    // coop.debug.town.list
    /// <summary>
    /// Lists all the items
    /// </summary>
    /// <param name="args">actually none are being used..</param>
    /// <returns>strings of all the items</returns>
    public sealed class ListItemsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.town";

        public string Name => "list_items";

        public string Description => "Lists items for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            StringBuilder stringBuilder = new StringBuilder();

            List<ItemCategory> items = Campaign.Current.ObjectManager.GetObjectTypeList<ItemCategory>().ToList();

            items.ForEach((item) =>
            {
                stringBuilder.Append(string.Format("ID: '{0}'\n", item.StringId));
            });

            return Succeeded(stringBuilder.ToString());

        }
    }

    /// <summary>
    /// Gets information on a specific town.
    /// </summary>
    /// <param name="args">town ID to lookup</param>
    /// <returns>Information regarding the town.</returns>
    public sealed class InfoCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.town";

        public string Name => "info";

        public string Description => "Shows the relevant state for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("townId", "The town id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (objectManager.TryGetObject(args[0], out Town town) == false)
            {
                return Failed($"ID: '{args[0]}' not found");
            }

            Fief fief = town.Settlement.SettlementComponent as Fief;

            StringBuilder sb = new();

            sb.AppendFormat("ID: '{0}'\n", args[0]);
            sb.AppendFormat("Name: '{0}'\n", town.Name);
            sb.AppendFormat("Governor: '{0}'\n", (town.Governor != null) ? town.Governor.Name : "null");
            sb.AppendFormat("LastCapturedBy: '{0}'\n", (town.LastCapturedBy != null) ? town.LastCapturedBy.Name : "null");
            sb.AppendFormat("Prosperity: '{0}'\n", town.Prosperity);
            sb.AppendFormat("Loyalty: '{0}'\n", town.Loyalty);
            sb.AppendFormat("Security: '{0}'\n", town.Security);
            sb.AppendFormat("InRebelliousState: '{0}'\n", town.InRebelliousState);
            sb.AppendFormat("GarrisonAutoRecruitmentIsEnabled: '{0}'\n", town.GarrisonAutoRecruitmentIsEnabled);
            sb.AppendFormat("Food stock '{0}' : \n", fief.FoodStocks);
            sb.AppendFormat("TradeTaxAccumulated: '{0}'\n", town.TradeTaxAccumulated);
            sb.AppendFormat("_tradeTax: '{0}'\n", town._tradeTax);
            sb.AppendFormat("Sold Items: \n");
            Town.SellLog[] logList = town._soldItems;
            if (logList != null)
            {
                foreach (Town.SellLog log in logList)
                {
                    sb.AppendFormat("SellLog: {0} {1}\n", log.Category.StringId, log.Number);
                }
            }
            return Succeeded(sb.ToString());

        }
    }

    public sealed class GarrisonBacklinkCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.town";

        public string Name => "garrison_backlink";

        public string Description => "Runs backlink for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("townId", "The town id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            if (!TryGetObjectManager(out var objectManager))
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (!objectManager.TryGetObject(args[0], out Town town))
            {
                return Failed($"ID: '{args[0]}' not found");
            }

            return Succeeded(FormatGarrisonBacklink(objectManager, town, args[0]));

        }
    }

    public sealed class FocusGarrisonCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.town";

        public string Name => "focus_garrison";

        public string Description => "Focuses garrison for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("townId", "The town id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            if (!TryGetObjectManager(out var objectManager))
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (!objectManager.TryGetObject(args[0], out Town town))
            {
                return Failed($"ID: '{args[0]}' not found");
            }

            var activeGarrisons = GetActiveGarrisons(town);
            if (activeGarrisons.Count != 1)
            {
                return Failed($"Expected exactly one active garrison for {town.Name}, found {activeGarrisons.Count}");
            }

            MapScreen mapScreen = MapScreen.Instance;
            if (mapScreen?.MapCameraView == null)
            {
                return Failed("Campaign map camera is unavailable");
            }

            MobileParty garrison = activeGarrisons[0];
            var targetPosition = garrison.MapEvent?.Position ?? garrison.Position;
            garrison.Party.SetAsCameraFollowParty();
            mapScreen.MapCameraView.FastMoveCameraToPosition(targetPosition, mapScreen.IsInMenu);
            mapScreen.MapCameraView.SetCameraMode(MapCameraView.CameraFollowMode.FollowParty);
            return Succeeded($"Following {garrison.StringId} at {town.Name} on the campaign map");

        }
    }

    public sealed class ApplyGarrisonLifecycleCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.town";

        public string Name => "apply_garrison_lifecycle";

        public string Description => "Applies garrison lifecycle for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("townId", "The town id."),
            new ExpectedArgs("operation", "The operation."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("This function can only be used by the server");
            }


            if (!TryGetObjectManager(out var objectManager))
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (!objectManager.TryGetObject(args[0], out Town town))
            {
                return Failed($"ID: '{args[0]}' not found");
            }

            var activeGarrisons = GetActiveGarrisons(town);
            if (activeGarrisons.Count != 1)
            {
                return Failed($"Expected exactly one active garrison for {town.Name}, found {activeGarrisons.Count}");
            }

            var garrison = (GarrisonPartyComponent)activeGarrisons[0].PartyComponent;
            switch (args[1].ToLowerInvariant())
            {
                case "finalize":
                    if (!ReferenceEquals(town.GarrisonPartyComponent, garrison))
                    {
                        return Failed($"Refusing to finalize: {town.Name} does not point at its active garrison");
                    }
                    garrison.OnFinalize();
                    break;
                case "initialize":
                    if (town.GarrisonPartyComponent != null &&
                        !ReferenceEquals(town.GarrisonPartyComponent, garrison))
                    {
                        return Failed($"Refusing to initialize: {town.Name} points at a different garrison");
                    }
                    garrison.OnInitialize();
                    break;
                default:
                    return Failed($"Unknown lifecycle action '{args[1]}'; expected initialize or finalize");
            }

            return Succeeded($"Applied {args[1].ToLowerInvariant()} to {activeGarrisons[0].StringId}; " +
                   FormatGarrisonBacklink(objectManager, town, args[0]));

        }
    }

    private static string FormatGarrisonBacklink(IObjectManager objectManager, Town town, string townId)
    {
        var activeGarrisons = GetActiveGarrisons(town);
        var backlink = town.GarrisonPartyComponent;
        string backlinkId = backlink == null
            ? "null"
            : objectManager.TryGetId(backlink, out string id) ? id : "unregistered";
        string activeParties = activeGarrisons.Count == 0
            ? "none"
            : string.Join(",", activeGarrisons.Select(party => party.StringId));
        bool backlinkMatchesActive = activeGarrisons.Count == 1 &&
                                     ReferenceEquals(activeGarrisons[0].PartyComponent, backlink);

        return $"{(ModInformation.IsServer ? "SERVER" : "CLIENT")} " +
               $"town={townId} settlement={town.Settlement.StringId} " +
               $"backlinkComponent={backlinkId} backlinkParty={backlink?.MobileParty?.StringId ?? "null"} " +
               $"activeGarrisonCount={activeGarrisons.Count} activeGarrisonParties={activeParties} " +
               $"backlinkMatchesActive={backlinkMatchesActive}";
    }

    private static List<MobileParty> GetActiveGarrisons(Town town)
    {
        return MobileParty.All
            .Where(party => party.IsActive &&
                            party.PartyComponent is GarrisonPartyComponent component &&
                            component.Settlement?.Town == town)
            .ToList();
    }

    // coop.debug.town.list_buildings <townId>
    /// <summary>
    /// Lists a town's Buildings + BuildingsInProgress (the synced collection-FIELD MBLists) with each
    /// building's level/progress, so server and client screenshots can be compared to confirm the building
    /// collection still replicates.
    /// </summary>
    public sealed class ListBuildingsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.town";

        public string Name => "list_buildings";

        public string Description => "Lists buildings for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("townId", "The town id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (TryGetObjectManager(out var objectManager) == false) return Failed("Unable to resolve ObjectManager");
            if (objectManager.TryGetObject(args[0], out Town town) == false) return Failed($"ID: '{args[0]}' not found");

            StringBuilder sb = new();
            sb.AppendFormat("Buildings for '{0}' ({1}):\n", town.Name, town.Buildings.Count);
            foreach (var building in town.Buildings)
                sb.AppendFormat("  {0} level={1} progress={2}\n", building.BuildingType?.StringId, building.CurrentLevel, building.BuildingProgress);
            sb.AppendFormat("BuildingsInProgress queue ({0}):\n", town.BuildingsInProgress.Count);
            foreach (var building in town.BuildingsInProgress)
                sb.AppendFormat("  {0} level={1}\n", building.BuildingType?.StringId, building.CurrentLevel);
            return Succeeded(sb.ToString());

        }
    }

    // coop.debug.town.list_workshops <townId>
    /// <summary>
    /// Lists a town's Workshops (the synced collection-PROPERTY array) with each workshop's type and owner,
    /// so server and client screenshots can be compared to confirm the workshop collection still replicates.
    /// </summary>
    public sealed class ListWorkshopsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.town";

        public string Name => "list_workshops";

        public string Description => "Lists workshops for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("townId", "The town id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (TryGetObjectManager(out var objectManager) == false) return Failed("Unable to resolve ObjectManager");
            if (objectManager.TryGetObject(args[0], out Town town) == false) return Failed($"ID: '{args[0]}' not found");

            StringBuilder sb = new();
            sb.AppendFormat("Workshops for '{0}' ({1}):\n", town.Name, town.Workshops.Length);
            foreach (var workshop in town.Workshops)
                sb.AppendFormat("  {0} owner={1}\n", workshop.WorkshopType?.StringId, workshop.Owner?.Name);
            return Succeeded(sb.ToString());

        }
    }

    // coop.debug.town.set_food_stocks
    /// <summary>
    /// Set the food stocks for a Town
    /// </summary>
    /// <param name="args">first arg : townId ; second arg : stock value</param>
    /// <returns></returns>
    public sealed class SetFoodStocksCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.town";

        public string Name => "set_food_stocks";

        public string Description => "Sets food stocks for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("townId", "The town id."),
            new ExpectedArgs("foodStocks", "The food stocks."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            string townId = args[0];
            string foodStocksString = args[1];

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }
            if (objectManager.TryGetObject(townId, out Town town) == false)
            {
                return Failed($"Town with ID: '{townId}' not found");
            }

            Fief fief = town.Settlement.SettlementComponent as Fief;

            if (float.TryParse(foodStocksString, out float foodStocks) == false)
            {
                return Failed($"Argument2: {foodStocksString} is not a float.");
            }

            fief.FoodStocks = foodStocks;

            return Succeeded($"Town food stocks has changed to: {fief.FoodStocks}");

        }
    }

    // coop.debug.town.set_governor town_comp_V1 lord_1_1
    /// <summary>
    /// Sets the Town governor of a specific Town.
    /// </summary>
    /// <param name="args">townID and the heroID to set</param>
    /// <returns>information if it changed</returns>
    public sealed class SetTownGovernorCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.town";

        public string Name => "set_governor";

        public string Description => "Sets governor for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("townId", "The town id."),
            new ExpectedArgs("heroId", "The hero id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            string townId = args[0];
            string heroId = args[1];

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (objectManager.TryGetObject(townId, out Town town) == false)
            {
                return Failed($"Town with ID: '{townId}' not found");
            }

            if (objectManager.TryGetObject(heroId, out Hero hero) == false)
            {
                return Failed($"Hero with ID: '{heroId}' not found");
            }

            town.Governor = hero;

            return Succeeded($"Town governor has changed to: {town.Governor?.Name} hero with ID: {town.Governor?.StringId}");

        }
    }

    // coop.debug.town.set_last_captured_by town_comp_V1 clan_sturgia_2
    /// <summary>
    /// Sets the Town LastCapturedBy property of a specific Town.
    /// </summary>
    /// <param name="args">townID and the clanID to set</param>
    /// <returns>information if it changed</returns>
    public sealed class SetTownLastCapturedByCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.town";

        public string Name => "set_last_captured_by";

        public string Description => "Sets last captured by for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("townId", "The town id."),
            new ExpectedArgs("clanId", "The clan id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            string townId = args[0];
            string clanId = args[1];

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (objectManager.TryGetObject(townId, out Town town) == false)
            {
                return Failed($"{nameof(Town)} with ID: '{townId}' not found");
            }

            if (objectManager.TryGetObject(clanId, out Clan clan) == false)
            {
                return Failed($"{nameof(Clan)} with ID: '{clanId}' not found");
            }

            town.LastCapturedBy = clan;

            return Succeeded($"{nameof(Town.LastCapturedBy)} has changed to: {town.LastCapturedBy.Name} clan with ID: {town.LastCapturedBy.StringId}");

        }
    }

    // coop.debug.town.add_item_to_sold_items town_comp_V1 noble_horse 100
    /// <summary>
    /// Adds a number of items to the Town sold items list of a specific Town.
    /// </summary>
    /// <param name="args">townID and the itemID to add and a number to add.</param>
    /// <returns>information if it changed</returns>
    public sealed class AddToTownSoldItemsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.town";

        public string Name => "add_item_to_sold_items";

        public string Description => "Adds item to sold items for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("townId", "The town id."),
            new ExpectedArgs("itemId", "The item id."),
            new ExpectedArgs("numberOfItems", "The number of items."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            string townId = args[0];
            string itemId = args[1];
            string count = args[2];

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (objectManager.TryGetObject(townId, out Town town) == false)
            {
                return Failed($"{nameof(Town)} with ID: '{townId}' not found");
            }

            if (objectManager.TryGetObject(itemId, out ItemCategory item) == false)
            {
                return Failed($"{nameof(ItemCategory)} with ID: '{itemId}' not found");
            }

            if (int.TryParse(count, out int numberOfItems) == false)
            {
                return Failed($"Argument3: {count} is not an integer.");
            }


            List<Town.SellLog> newSoldItems = new List<Town.SellLog>(town._soldItems);
            int idx = newSoldItems.FindIndex(log => log.Category == item);
            if (idx != -1)
            {
                newSoldItems[idx] = new Town.SellLog(item, newSoldItems[idx].Number + numberOfItems);
            }
            else
            {
                newSoldItems.Add(new Town.SellLog(item, numberOfItems));
            }
            town.SetSoldItems(newSoldItems);

            // Check if item was added
            if (town.SoldItems.Count(soldItem => soldItem.Category == item) <= 0)
            {
                return Failed($"Unable to find {item} in {nameof(Town.SoldItems)}");
            }

            var newItem = town.SoldItems.First(soldItem => soldItem.Category == item);

            return Succeeded($"Added {newItem.Number} number of {newItem.Category.StringId} to Town SoldItems");

        }
    }

    // coop.debug.town.set_prosperity town_comp_V1 100
    /// <summary>
    /// Sets the Town prosperity of a specific Town.
    /// </summary>
    /// <param name="args">townID and the prosperity to set</param>
    /// <returns>information if it changed</returns>
    public sealed class SetTownProsperityCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.town";

        public string Name => "set_prosperity";

        public string Description => "Sets prosperity for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("townId", "The town id."),
            new ExpectedArgs("prosperity", "The prosperity."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            string townId = args[0];
            string prosperityValue = args[1];

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (objectManager.TryGetObject(townId, out Town town) == false)
            {
                return Failed($"{nameof(Town)} with ID: '{townId}' not found");
            }

            if (int.TryParse(prosperityValue, out int prosperity) == false)
            {
                return Failed($"Argument2: {prosperityValue} is not an integer.");
            }

            town.Prosperity = prosperity;
            return Succeeded($"Town Prosperity has changed to: {town.Prosperity}.");

        }
    }

    // coop.debug.town.set_loyalty town_comp_V1 100
    /// <summary>
    /// Sets the Town loyalty of a specific Town.
    /// </summary>
    /// <param name="args">townID and the loyalty to set</param>
    /// <returns>information if it changed</returns>
    public sealed class SetTownLoyaltyCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.town";

        public string Name => "set_loyalty";

        public string Description => "Sets loyalty for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("townId", "The town id."),
            new ExpectedArgs("loyalty", "The loyalty."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            string townId = args[0];
            string loyaltyValue = args[1];

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (objectManager.TryGetObject(townId, out Town town) == false)
            {
                return Failed($"{nameof(Town)} with ID: '{townId}' not found");
            }

            if (float.TryParse(loyaltyValue, out float loyalty) == false)
            {
                return Failed($"Argument2: {loyaltyValue} is not a float.");
            }

            town.Loyalty = loyalty;
            return Succeeded($"Town Loyalty has changed to: {town.Loyalty}.");

        }
    }

    // coop.debug.town.set_security town_comp_V1 100
    /// <summary>
    /// Sets the Town security of a specific Town.
    /// </summary>
    /// <param name="args">townID and the security to set</param>
    /// <returns>information if it changed</returns>
    public sealed class SetTownSecurityCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.town";

        public string Name => "set_security";

        public string Description => "Sets security for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("townId", "The town id."),
            new ExpectedArgs("security", "The security."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            string townId = args[0];
            string securityValue = args[1];

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (objectManager.TryGetObject(townId, out Town town) == false)
            {
                return Failed($"{nameof(Town)} with ID: '{townId}' not found");
            }

            if (float.TryParse(securityValue, out float security) == false)
            {
                return Failed($"Argument2: {securityValue} is not a float.");
            }

            town.Security = security;
            return Succeeded($"Town Security has changed to: {town.Security}.");

        }
    }


    // coop.debug.town.set_in_rebellious_state town_comp_V1 true
    /// <summary>
    /// Sets the Town rebellious state of a specific Town.
    /// </summary>
    /// <param name="args">townID and the rebellious state to set</param>
    /// <returns>information if it changed</returns>
    public sealed class SetTownInRebelliousStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.town";

        public string Name => "set_in_rebellious_state";

        public string Description => "Sets in rebellious state for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("townId", "The town id."),
            new ExpectedArgs("inRebelliousState", "The in rebellious state."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            string townId = args[0];
            string rebellionStateValue = args[1];

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (objectManager.TryGetObject(townId, out Town town) == false)
            {
                return Failed($"{nameof(Town)} with ID: '{townId}' not found");
            }

            if (bool.TryParse(rebellionStateValue, out bool inRebelliousState) == false)
            {
                return Failed($"Argument2: {rebellionStateValue} is not a boolean.");
            }

            RebellionsCampaignBehaviorPatches.PublishTownInRebelliousStateChanged(town, inRebelliousState);
            return Succeeded($"Town InRebelliousState has changed to: {town.InRebelliousState}.");

        }
    }

    public sealed class StartRebellionCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.town";

        public string Name => "start_rebellion";

        public string Description => "Starts rebellion for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("townId", "The town id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Run coop.debug.town.start_rebellion on the server.");
            if (TryGetObjectManager(out var objectManager) == false) return Failed("Unable to resolve ObjectManager");
            if (objectManager.TryGetObject(args[0], out Town town) == false)
                return Failed($"{nameof(Town)} with ID: '{args[0]}' not found");
            if (town.OwnerClan.IsRebelClan) return Failed($"{town.Name} is already owned by a rebel clan.");
            if (town.Settlement.Party.MapEvent != null) return Failed($"{town.Name} is in a map event.");
            if (town.Settlement.Party.SiegeEvent != null) return Failed($"{town.Name} is under siege.");

            RebellionsCampaignBehavior behavior = Campaign.Current.GetCampaignBehavior<RebellionsCampaignBehavior>();
            if (behavior == null) return Failed("Unable to resolve RebellionsCampaignBehavior");

            behavior.StartRebellionEvent(town.Settlement);
            return Succeeded($"Started the vanilla rebellion in {town.Name}. New owner: {town.OwnerClan.Name} (rebel={town.OwnerClan.IsRebelClan}).");

        }
    }

    // coop.debug.town.set_garrison_auto_recruitment town_comp_V1 false
    /// <summary>
    /// Sets the Town GarrisonAutoRecruitmentIsEnabled property of a specific Town.
    /// </summary>
    /// <param name="args">townID and the GarrisonAutoRecruitmentIsEnabled property value to set</param>
    /// <returns>information if it changed</returns>
    public sealed class SetTownGarrisonAutoRecruitmentIsEnabledCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.town";

        public string Name => "set_garrison_auto_recruitment";

        public string Description => "Sets garrison auto recruitment for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("townId", "The town id."),
            new ExpectedArgs("enabled", "The enabled."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            string townId = args[0];
            string garrisonRecruitmentValue = args[1];

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (objectManager.TryGetObject(townId, out Town town) == false)
            {
                return Failed($"{nameof(Town)} with ID: '{townId}' not found");
            }

            if (bool.TryParse(garrisonRecruitmentValue, out bool garrisonAutoRecruitmentIsEnabled) == false)
            {
                return Failed($"Argument2: {garrisonRecruitmentValue} is not a boolean.");
            }

            UpdateClanSettlementAutoRecruitmentPatches.PublishTownGarrisonAutoRecruitmentIsEnabledChanged(town, garrisonAutoRecruitmentIsEnabled);
            return Succeeded($"Town GarrisonAutoRecruitmentIsEnabled has changed to: {town.GarrisonAutoRecruitmentIsEnabled}.");

        }
    }

    // coop.debug.town.set_trade_tax_acc town_comp_V1 100
    /// <summary>
    /// sets the tradetaxaccumulated value for a town.
    /// </summary>
    /// <param name="args">the town and tradetaxaccumulated value float</param>
    /// <returns>string output if success</returns>
    public sealed class SetTradeTaxAccumulatedCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.town";

        public string Name => "set_trade_tax_acc";

        public string Description => "Sets trade tax acc for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("townId", "The town id."),
            new ExpectedArgs("tradeTax", "The trade tax."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            string townId = args[0];
            string tradeTaxAccumulatedValue = args[1];

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (objectManager.TryGetObject(townId, out Town town) == false)
            {
                return Failed($"{nameof(Town)} with ID: '{townId}' not found");
            }

            if (int.TryParse(tradeTaxAccumulatedValue, out int tradeTaxAccumulated) == false)
            {
                return Failed($"Argument2: {tradeTaxAccumulatedValue} is not an integer.");
            }

            town.TradeTaxAccumulated = tradeTaxAccumulated;
            return Succeeded($"Town TradeTaxAccumulated has changed to: {town.TradeTaxAccumulated}.");

        }
    }

    // coop.debug.town.set_trade_tax_acc town_comp_V1 100
    /// <summary>
    /// sets the tradetaxaccumulated value for a town.
    /// </summary>
    /// <param name="args">the town and tradetaxaccumulated value float</param>
    /// <returns>string output if success</returns>
    public sealed class ChangeCurrentBuildingCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.town";

        public string Name => "change_current_building";

        public string Description => "Changes current building for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("townId", "The town id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string townId = args[0];
            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (objectManager.TryGetObject(townId, out Town town) == false)
            {
                return Failed($"{nameof(Town)} with ID: '{townId}' not found");
            }
            //BuildingHelper.ChangeCurrentBuilding(town.Buildings.Last().BuildingType, town);
            return Succeeded("success");

        }
    }

    // coop.debug.town.set_trade_tax_acc town_comp_V1 100
    /// <summary>
    /// sets the tradetaxaccumulated value for a town.
    /// </summary>
    /// <param name="args">the town and tradetaxaccumulated value float</param>
    /// <returns>string output if success</returns>
    public sealed class ChangeCurrentBuildingQueueCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.town";

        public string Name => "change_current_building_queue";

        public string Description => "Changes current building queue for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("townId", "The town id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string townId = args[0];
            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (objectManager.TryGetObject(townId, out Town town) == false)
            {
                return Failed($"{nameof(Town)} with ID: '{townId}' not found");
            }
            BuildingHelper.ChangeCurrentBuildingQueue(town.Buildings, town);
            return Succeeded("success");

        }
    }

    /// <summary>
    /// View town management data of a specified town
    /// </summary>
    public sealed class ViewManagementDataCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.town";

        public string Name => "management_data";

        public string Description => "Runs data for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("townName", "The exact town name; quote values containing spaces."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {

            StringBuilder stringBuilder = new StringBuilder();
            foreach (var town in Town.AllTowns)
            {
                if (town.Name.ToString() == strings[0])
                {
                    stringBuilder.AppendLine(town.Name.ToString() + ":");
                    stringBuilder.AppendLine("Reserves: " + town.BoostBuildingProcess.ToString());
                    stringBuilder.AppendLine("Governor Name: " + town.Governor?.Name?.ToString());
                    stringBuilder.AppendLine("Current default building: " + town.CurrentDefaultBuilding?.Name?.ToString());
                    stringBuilder.AppendLine("Current building queue:");
                    foreach (var building in town.BuildingsInProgress)
                    {
                        stringBuilder.AppendLine(building.Name.ToString());
                    }
                }
            }

            string result = stringBuilder.ToString();
            if (result.Length > 0)
            {
                return Succeeded(result);
            }
            return Failed("Town not found.");

        }
    }
}
