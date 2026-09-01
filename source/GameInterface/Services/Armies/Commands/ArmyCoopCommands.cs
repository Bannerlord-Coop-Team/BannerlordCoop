using Common.Commands;
using GameInterface.Utils.Commands;

namespace GameInterface.Services.Armies.Commands;

public interface IArmyListCommand : ICoopCommand
{
}

public sealed class ArmyListCommand : LegacyCoopCommand, IArmyListCommand
{
    public ArmyListCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.army",
            "list",
            "Lists registered armies.",
            System.Array.Empty<IExpectedArgs>(),
            ArmyDebugCommand.ListArmy)
    {
    }
}

public interface IArmyCreateCommand : ICoopCommand
{
}

public sealed class ArmyCreateCommand : LegacyCoopCommand, IArmyCreateCommand
{
    public ArmyCreateCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.army",
            "create",
            "Creates an army on the server.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("kingdomId", "The registered kingdom id."),
                new ExpectedArgs("targetSettlementId", "The registered target settlement id."),
                new ExpectedArgs("heroLeaderId", "The registered leader hero id."),
                new ExpectedArgs("armyType", "The ArmyTypes name or value."),
            },
            ArmyDebugCommand.CreateArmy)
    {
    }
}

public interface IArmyDestroyCommand : ICoopCommand
{
}

public sealed class ArmyDestroyCommand : LegacyCoopCommand, IArmyDestroyCommand
{
    public ArmyDestroyCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.army",
            "destroy",
            "Destroys an army on the server.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("armyId", "The registered army id."),
                new ExpectedArgs("disbandReason", "The ArmyDispersionReason name or value."),
            },
            ArmyDebugCommand.DestroyArmy)
    {
    }
}

public interface IArmyMobilePartyListCommand : ICoopCommand
{
}

public sealed class ArmyMobilePartyListCommand : LegacyCoopCommand, IArmyMobilePartyListCommand
{
    public ArmyMobilePartyListCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.army",
            "mobile_party_list",
            "Lists parties in an army.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("armyId", "The registered army id."),
            },
            ArmyDebugCommand.GetMobilePartyList)
    {
    }
}

public interface IArmyMobilePartyAddCommand : ICoopCommand
{
}

public sealed class ArmyMobilePartyAddCommand : LegacyCoopCommand, IArmyMobilePartyAddCommand
{
    public ArmyMobilePartyAddCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.army",
            "mobile_party_add",
            "Adds a mobile party to an army.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("armyId", "The registered army id."),
                new ExpectedArgs("mobilePartyId", "The registered mobile party id."),
            },
            ArmyDebugCommand.AddMobileParty)
    {
    }
}

public interface IArmyMobilePartyRemoveCommand : ICoopCommand
{
}

public sealed class ArmyMobilePartyRemoveCommand : LegacyCoopCommand, IArmyMobilePartyRemoveCommand
{
    public ArmyMobilePartyRemoveCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.army",
            "mobile_party_remove",
            "Removes a mobile party from an army.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("armyId", "The registered army id."),
                new ExpectedArgs("mobilePartyId", "The registered mobile party id."),
            },
            ArmyDebugCommand.RemoveMobileParty)
    {
    }
}

public interface IArmyInfoCommand : ICoopCommand
{
}

public sealed class ArmyInfoCommand : LegacyCoopCommand, IArmyInfoCommand
{
    public ArmyInfoCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.army",
            "info",
            "Reports state for an army.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("armyId", "The registered army id."),
            },
            ArmyDebugCommand.Info)
    {
    }
}
