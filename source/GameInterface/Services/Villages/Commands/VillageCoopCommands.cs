using Common.Commands;
using GameInterface.Utils.Commands;

namespace GameInterface.Services.Villages.Commands;

public interface IListVillagesCoopCommand : ICoopCommand
{
}

public sealed class ListVillagesCoopCommand : LegacyCoopCommand, IListVillagesCoopCommand
{
    public ListVillagesCoopCommand()
        : base(
            "coop.debug.village",
            "list",
            "Lists the relevant state for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            VillageDebugCommand.ListVillages)
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
            "coop.debug.village",
            "info",
            "Shows the relevant state for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("villageId", "The village id."),
            },
            VillageDebugCommand.Info)
    {
    }
}

public interface ISetVillageStateCoopCommand : ICoopCommand
{
}

public sealed class SetVillageStateCoopCommand : LegacyCoopCommand, ISetVillageStateCoopCommand
{
    public SetVillageStateCoopCommand()
        : base(
            "coop.debug.village",
            "set_state",
            "Sets state for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("villageId", "The village id."),
                new ExpectedArgs("state", "The state."),
            },
            VillageDebugCommand.SetVillageState)
    {
    }
}

public interface ISetVillageHearthCoopCommand : ICoopCommand
{
}

public sealed class SetVillageHearthCoopCommand : LegacyCoopCommand, ISetVillageHearthCoopCommand
{
    public SetVillageHearthCoopCommand()
        : base(
            "coop.debug.village",
            "set_hearth",
            "Sets hearth for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("villageId", "The village id."),
                new ExpectedArgs("hearth", "The hearth."),
            },
            VillageDebugCommand.SetVillageHearth)
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
            "coop.debug.village",
            "set_trade_tax_acc",
            "Sets trade tax acc for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("villageId", "The village id."),
                new ExpectedArgs("tradeTax", "The trade tax."),
            },
            VillageDebugCommand.SetTradeTaxAccumulated)
    {
    }
}

public interface ISetLastDemandTimeSatisifiedCoopCommand : ICoopCommand
{
}

public sealed class SetLastDemandTimeSatisifiedCoopCommand : LegacyCoopCommand, ISetLastDemandTimeSatisifiedCoopCommand
{
    public SetLastDemandTimeSatisifiedCoopCommand()
        : base(
            "coop.debug.village",
            "set_demand_time",
            "Sets demand time for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("villageId", "The village id."),
                new ExpectedArgs("demandTime", "The demand time."),
            },
            VillageDebugCommand.SetLastDemandTimeSatisified)
    {
    }
}
