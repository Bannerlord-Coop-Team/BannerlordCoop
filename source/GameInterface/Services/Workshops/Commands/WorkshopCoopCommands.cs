using Common.Commands;
using GameInterface.Utils.Commands;

namespace GameInterface.Services.Workshops.Commands;

public interface ISetWorkshopCustomNameCoopCommand : ICoopCommand
{
}

public sealed class SetWorkshopCustomNameCoopCommand : LegacyCoopCommand, ISetWorkshopCustomNameCoopCommand
{
    public SetWorkshopCustomNameCoopCommand()
        : base(
            "coop.debug.workshop",
            "set_workshop_custom_name",
            "Sets workshop custom name for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementName", "The exact settlement name; quote values containing spaces."),
                new ExpectedArgs("workshopType", "The workshop type."),
                new ExpectedArgs("newCustomName", "The exact new custom name; quote values containing spaces."),
            },
            WorkshopDebugCommand.SetWorkshopCustomName)
    {
    }
}

public interface ISetWorkshopOwnerCoopCommand : ICoopCommand
{
}

public sealed class SetWorkshopOwnerCoopCommand : LegacyCoopCommand, ISetWorkshopOwnerCoopCommand
{
    public SetWorkshopOwnerCoopCommand()
        : base(
            "coop.debug.workshop",
            "set_workshop_owner",
            "Sets workshop owner for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementName", "The exact settlement name; quote values containing spaces."),
                new ExpectedArgs("workshopType", "The workshop type."),
                new ExpectedArgs("newOwnerId", "The new owner id."),
            },
            WorkshopDebugCommand.SetWorkshopOwner)
    {
    }
}

public interface IOwnersInSettlementCommandCoopCommand : ICoopCommand
{
}

public sealed class OwnersInSettlementCommandCoopCommand : LegacyCoopCommand, IOwnersInSettlementCommandCoopCommand
{
    public OwnersInSettlementCommandCoopCommand()
        : base(
            "coop.debug.workshop",
            "owners_in_settlement",
            "Runs in settlement for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
            },
            WorkshopDebugCommand.OwnersInSettlementCommand)
    {
    }
}

public interface IHeroOwnedWorkshopsCommandCoopCommand : ICoopCommand
{
}

public sealed class HeroOwnedWorkshopsCommandCoopCommand : LegacyCoopCommand, IHeroOwnedWorkshopsCommandCoopCommand
{
    public HeroOwnedWorkshopsCommandCoopCommand()
        : base(
            "coop.debug.workshop",
            "hero_owned_workshops",
            "Runs owned workshops for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("heroId", "The hero id."),
            },
            WorkshopDebugCommand.HeroOwnedWorkshopsCommand)
    {
    }
}

public interface IViewWarehouseRostersCommandCoopCommand : ICoopCommand
{
}

public sealed class ViewWarehouseRostersCommandCoopCommand : LegacyCoopCommand, IViewWarehouseRostersCommandCoopCommand
{
    public ViewWarehouseRostersCommandCoopCommand()
        : base(
            "coop.debug.workshop",
            "view_warehouse_rosters",
            "Shows warehouse rosters for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            WorkshopDebugCommand.ViewWarehouseRostersCommand)
    {
    }
}

public interface IViewWorkshopInfoCommandCoopCommand : ICoopCommand
{
}

public sealed class ViewWorkshopInfoCommandCoopCommand : LegacyCoopCommand, IViewWorkshopInfoCommandCoopCommand
{
    public ViewWorkshopInfoCommandCoopCommand()
        : base(
            "coop.debug.workshop",
            "workshop_info",
            "Runs info for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
            },
            WorkshopDebugCommand.ViewWorkshopInfoCommand)
    {
    }
}
