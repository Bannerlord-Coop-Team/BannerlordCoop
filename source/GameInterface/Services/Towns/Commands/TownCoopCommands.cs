using Common.Commands;
using GameInterface.Utils.Commands;

namespace GameInterface.Services.Towns.Commands;

public interface IListTownsCoopCommand : ICoopCommand
{
}

public sealed class ListTownsCoopCommand : LegacyCoopCommand, IListTownsCoopCommand
{
    public ListTownsCoopCommand()
        : base(
            "coop.debug.town",
            "list_towns",
            "Lists towns for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            TownDebugCommand.ListTowns)
    {
    }
}

public interface IListItemsCoopCommand : ICoopCommand
{
}

public sealed class ListItemsCoopCommand : LegacyCoopCommand, IListItemsCoopCommand
{
    public ListItemsCoopCommand()
        : base(
            "coop.debug.town",
            "list_items",
            "Lists items for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            TownDebugCommand.ListItems)
    {
    }
}

public interface IInfoCoopCommand : ICoopCommand
{
}

public sealed class InfoCoopCommand : LegacyCoopCommand, IInfoCoopCommand
{
    public InfoCoopCommand()
        : base(
            "coop.debug.town",
            "info",
            "Shows the relevant state for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("townId", "The town id."),
            },
            TownDebugCommand.Info)
    {
    }
}

public interface IGarrisonBacklinkCoopCommand : ICoopCommand
{
}

public sealed class GarrisonBacklinkCoopCommand : LegacyCoopCommand, IGarrisonBacklinkCoopCommand
{
    public GarrisonBacklinkCoopCommand()
        : base(
            "coop.debug.town",
            "garrison_backlink",
            "Runs backlink for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("townId", "The town id."),
            },
            TownDebugCommand.GarrisonBacklink)
    {
    }
}

public interface IFocusGarrisonCoopCommand : ICoopCommand
{
}

public sealed class FocusGarrisonCoopCommand : LegacyCoopCommand, IFocusGarrisonCoopCommand
{
    public FocusGarrisonCoopCommand()
        : base(
            "coop.debug.town",
            "focus_garrison",
            "Focuses garrison for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("townId", "The town id."),
            },
            TownDebugCommand.FocusGarrison)
    {
    }
}

public interface IApplyGarrisonLifecycleCoopCommand : ICoopCommand
{
}

public sealed class ApplyGarrisonLifecycleCoopCommand : LegacyCoopCommand, IApplyGarrisonLifecycleCoopCommand
{
    public ApplyGarrisonLifecycleCoopCommand()
        : base(
            "coop.debug.town",
            "apply_garrison_lifecycle",
            "Applies garrison lifecycle for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("townId", "The town id."),
                new ExpectedArgs("operation", "The operation."),
            },
            TownDebugCommand.ApplyGarrisonLifecycle)
    {
    }
}

public interface IListBuildingsCoopCommand : ICoopCommand
{
}

public sealed class ListBuildingsCoopCommand : LegacyCoopCommand, IListBuildingsCoopCommand
{
    public ListBuildingsCoopCommand()
        : base(
            "coop.debug.town",
            "list_buildings",
            "Lists buildings for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("townId", "The town id."),
            },
            TownDebugCommand.ListBuildings)
    {
    }
}

public interface IListWorkshopsCoopCommand : ICoopCommand
{
}

public sealed class ListWorkshopsCoopCommand : LegacyCoopCommand, IListWorkshopsCoopCommand
{
    public ListWorkshopsCoopCommand()
        : base(
            "coop.debug.town",
            "list_workshops",
            "Lists workshops for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("townId", "The town id."),
            },
            TownDebugCommand.ListWorkshops)
    {
    }
}

public interface ISetFoodStocksCoopCommand : ICoopCommand
{
}

public sealed class SetFoodStocksCoopCommand : LegacyCoopCommand, ISetFoodStocksCoopCommand
{
    public SetFoodStocksCoopCommand()
        : base(
            "coop.debug.town",
            "set_food_stocks",
            "Sets food stocks for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("townId", "The town id."),
                new ExpectedArgs("foodStocks", "The food stocks."),
            },
            TownDebugCommand.SetFoodStocks)
    {
    }
}

public interface ISetTownGovernorCoopCommand : ICoopCommand
{
}

public sealed class SetTownGovernorCoopCommand : LegacyCoopCommand, ISetTownGovernorCoopCommand
{
    public SetTownGovernorCoopCommand()
        : base(
            "coop.debug.town",
            "set_governor",
            "Sets governor for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("townId", "The town id."),
                new ExpectedArgs("heroId", "The hero id."),
            },
            TownDebugCommand.SetTownGovernor)
    {
    }
}

public interface ISetTownLastCapturedByCoopCommand : ICoopCommand
{
}

public sealed class SetTownLastCapturedByCoopCommand : LegacyCoopCommand, ISetTownLastCapturedByCoopCommand
{
    public SetTownLastCapturedByCoopCommand()
        : base(
            "coop.debug.town",
            "set_last_captured_by",
            "Sets last captured by for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("townId", "The town id."),
                new ExpectedArgs("clanId", "The clan id."),
            },
            TownDebugCommand.SetTownLastCapturedBy)
    {
    }
}

public interface IAddToTownSoldItemsCoopCommand : ICoopCommand
{
}

public sealed class AddToTownSoldItemsCoopCommand : LegacyCoopCommand, IAddToTownSoldItemsCoopCommand
{
    public AddToTownSoldItemsCoopCommand()
        : base(
            "coop.debug.town",
            "add_item_to_sold_items",
            "Adds item to sold items for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("townId", "The town id."),
                new ExpectedArgs("itemId", "The item id."),
                new ExpectedArgs("numberOfItems", "The number of items."),
            },
            TownDebugCommand.AddToTownSoldItems)
    {
    }
}

public interface ISetTownProsperityCoopCommand : ICoopCommand
{
}

public sealed class SetTownProsperityCoopCommand : LegacyCoopCommand, ISetTownProsperityCoopCommand
{
    public SetTownProsperityCoopCommand()
        : base(
            "coop.debug.town",
            "set_prosperity",
            "Sets prosperity for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("townId", "The town id."),
                new ExpectedArgs("prosperity", "The prosperity."),
            },
            TownDebugCommand.SetTownProsperity)
    {
    }
}

public interface ISetTownLoyaltyCoopCommand : ICoopCommand
{
}

public sealed class SetTownLoyaltyCoopCommand : LegacyCoopCommand, ISetTownLoyaltyCoopCommand
{
    public SetTownLoyaltyCoopCommand()
        : base(
            "coop.debug.town",
            "set_loyalty",
            "Sets loyalty for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("townId", "The town id."),
                new ExpectedArgs("loyalty", "The loyalty."),
            },
            TownDebugCommand.SetTownLoyalty)
    {
    }
}

public interface ISetTownSecurityCoopCommand : ICoopCommand
{
}

public sealed class SetTownSecurityCoopCommand : LegacyCoopCommand, ISetTownSecurityCoopCommand
{
    public SetTownSecurityCoopCommand()
        : base(
            "coop.debug.town",
            "set_security",
            "Sets security for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("townId", "The town id."),
                new ExpectedArgs("security", "The security."),
            },
            TownDebugCommand.SetTownSecurity)
    {
    }
}

public interface ISetTownInRebelliousStateCoopCommand : ICoopCommand
{
}

public sealed class SetTownInRebelliousStateCoopCommand : LegacyCoopCommand, ISetTownInRebelliousStateCoopCommand
{
    public SetTownInRebelliousStateCoopCommand()
        : base(
            "coop.debug.town",
            "set_in_rebellious_state",
            "Sets in rebellious state for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("townId", "The town id."),
                new ExpectedArgs("inRebelliousState", "The in rebellious state."),
            },
            TownDebugCommand.SetTownInRebelliousState)
    {
    }
}

public interface IStartRebellionCoopCommand : ICoopCommand
{
}

public sealed class StartRebellionCoopCommand : LegacyCoopCommand, IStartRebellionCoopCommand
{
    public StartRebellionCoopCommand()
        : base(
            "coop.debug.town",
            "start_rebellion",
            "Starts rebellion for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("townId", "The town id."),
            },
            TownDebugCommand.StartRebellion)
    {
    }
}

public interface ISetTownGarrisonAutoRecruitmentIsEnabledCoopCommand : ICoopCommand
{
}

public sealed class SetTownGarrisonAutoRecruitmentIsEnabledCoopCommand : LegacyCoopCommand, ISetTownGarrisonAutoRecruitmentIsEnabledCoopCommand
{
    public SetTownGarrisonAutoRecruitmentIsEnabledCoopCommand()
        : base(
            "coop.debug.town",
            "set_garrison_auto_recruitment",
            "Sets garrison auto recruitment for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("townId", "The town id."),
                new ExpectedArgs("enabled", "The enabled."),
            },
            TownDebugCommand.SetTownGarrisonAutoRecruitmentIsEnabled)
    {
    }
}

public interface ISetTradeTaxAccumulatedCoopCommand : ICoopCommand
{
}

public sealed class SetTradeTaxAccumulatedCoopCommand : LegacyCoopCommand, ISetTradeTaxAccumulatedCoopCommand
{
    public SetTradeTaxAccumulatedCoopCommand()
        : base(
            "coop.debug.town",
            "set_trade_tax_acc",
            "Sets trade tax acc for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("townId", "The town id."),
                new ExpectedArgs("tradeTax", "The trade tax."),
            },
            TownDebugCommand.SetTradeTaxAccumulated)
    {
    }
}

public interface IChangeCurrentBuildingCoopCommand : ICoopCommand
{
}

public sealed class ChangeCurrentBuildingCoopCommand : LegacyCoopCommand, IChangeCurrentBuildingCoopCommand
{
    public ChangeCurrentBuildingCoopCommand()
        : base(
            "coop.debug.town",
            "change_current_building",
            "Changes current building for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("townId", "The town id."),
            },
            TownDebugCommand.ChangeCurrentBuilding)
    {
    }
}

public interface IChangeCurrentBuildingQueueCoopCommand : ICoopCommand
{
}

public sealed class ChangeCurrentBuildingQueueCoopCommand : LegacyCoopCommand, IChangeCurrentBuildingQueueCoopCommand
{
    public ChangeCurrentBuildingQueueCoopCommand()
        : base(
            "coop.debug.town",
            "change_current_building_queue",
            "Changes current building queue for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("townId", "The town id."),
            },
            TownDebugCommand.ChangeCurrentBuildingQueue)
    {
    }
}

public interface IViewManagementDataCoopCommand : ICoopCommand
{
}

public sealed class ViewManagementDataCoopCommand : LegacyCoopCommand, IViewManagementDataCoopCommand
{
    public ViewManagementDataCoopCommand()
        : base(
            "coop.debug.town",
            "management_data",
            "Runs data for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("townName", "The exact town name; quote values containing spaces."),
            },
            TownDebugCommand.ViewManagementData)
    {
    }
}
